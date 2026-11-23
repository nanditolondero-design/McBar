# Arquitectura de McBar

## Arranque (`App.xaml.cs`)
1. Carga ajustes (`%AppData%\McBar\settings.json`). Con `--restaurar` devuelve la barra de Windows y sale.
2. Instancia única (mutex). Manejo de errores: los menores se registran y la app sigue; ante uno fatal se restaura la barra de Windows.
3. Primera vez: fija Launchpad, Explorador, Edge/Chrome (si están), Calculadora y Configuración.
4. Tema (`ThemeManager`), seguimiento de ventanas (`WindowTracker`), ocultar barra de Windows (`Taskbar`), barra de menú y Dock.

## Seguimiento de ventanas (`Core/WindowTracker.cs`)
- Hooks `SetWinEventHook` (foco, mostrar/ocultar/destruir, cambio de título, "cloaked"). Varios avisos seguidos se agrupan (220 ms) en un solo recuento con `EnumWindows`.
- Filtra igual que Alt+Tab (visible, sin dueño, no herramienta, no "cloaked", con título).
- Cada ventana tiene **claves**: `exe:<ruta>` y, si existe, `aumid:<AppUserModelID>` (de la ventana o, en apps de la Store, del proceso hijo de ApplicationFrameHost).
- Un ícono fijado tiene sus propias claves (`Core/AppIdentity.cs`); si alguna coincide, esa ventana es de ese ícono.

## Monitores y área de trabajo (`Native/Monitors.cs`)
- `Monitors.All()` lista los monitores; `App.BuildMonitors()` crea una barra y un Dock por monitor (o solo en el principal).
- Ya **no se usa la API de AppBar**: `WorkArea` fija el área de trabajo de cada monitor con `SPI_SETWORKAREA` (monitor menos barra arriba y Dock abajo). Si el Explorador la cambia, se corrige (`WM_SETTINGCHANGE`, con límite para no pelear sin fin). Al salir se devuelve todo el monitor.
- Pantalla completa: `Core/FullscreenWatcher.cs` recorre las ventanas en orden (de arriba hacia abajo) y, en cada monitor, mira la primera ventana "de verdad"; si cubre el monitor y no está maximizada, se esconden la barra y el Dock de ese monitor. Corre al cambiar el foco y desde el vigilante.
- `Taskbar.EnforceBurst()`: vigilancia cada 100 ms por unos segundos después de acciones que hacen asomar la barra de Windows.
- Vigilante (`App.StartWatchdog`, cada 0,7 s, solo consultas baratas): esconde la barra de Windows si asomó, mantiene la bandeja propia primera, revisa pantalla completa, verifica el área de trabajo y vuelve a poner arriba barra/isla/Dock.

## Bandeja propia (`Native/TrayHost.cs`, `Core/TrayData.cs`)
- Ventana invisible de clase `Shell_TrayWnd` en un **hilo propio** (no frena la interfaz), puesta primera: `Shell_NotifyIcon` de las apps nos llega a nosotros (WM_COPYDATA tipo 1).
- Al arrancar se manda `TaskbarCreated` para que todas las apps reenvíen sus íconos.
- Todo se reenvía también al Explorador (y los mensajes de AppBar de otros programas, tipo 0, se le pasan tal cual).
- Clics: se le manda a la app su `uCallbackMessage` con el formato de su versión (v4: NIN_SELECT / WM_CONTEXTMENU).
- `Taskbar.ExplorerTray()` saltea nuestra propia ventana al buscar la barra real.

## Animación de minimizar (`Native/ShellHook.cs`)
- Ventana registrada con `RegisterShellHookWindow`; ante `HSHELL_GETMINRECT` se responde con el rectángulo del ícono de la app en el Dock.
- Activa la opción de Windows "animar ventanas al minimizar y maximizar" y guarda el valor original para restaurarlo.

## Ventanas rápidas (0.3.3)
- `WindowOps.MakeGlass`: ventana común (AllowsTransparency=false) con el vidrio de DWM extendido a todo el cliente y `CompositionTarget.BackgroundColor` transparente → la dibuja la tarjeta de video.
- `WindowOps.SetShape`: región de la ventana (rectángulo redondeado + rectángulos extra). Define dónde se dibuja y dónde recibe clics.
- Dock: forma = barra + zona de íconos agrandados/etiqueta + franja de ocultación automática (se recalcula solo cuando cambia). Durante la "nubecita" la forma es toda la ventana.
- Isla: forma = la píldora (se recalcula en cada cuadro de su animación de tamaño). En estilo Vidrio el desenfoque va en la propia isla.
- `AppSettings.FastWindows = false` vuelve a las ventanas "en capas" (modo compatible).
- Área de trabajo: `WorkArea.Verify` con freno anti-pelea (3 correcciones en 30 s → pausa de 3 minutos).

## Dock (`Dock/`)
- Ventana transparente abajo de cada monitor, del ancho necesario (o del monitor en estilo "ancho completo").
- `DockBackdrop`: ventanita aparte, COMÚN (no en capas, porque Windows ignora el recorte redondeado en ventanas en capas), con fondo transparente vía `DwmExtendFrameIntoClientArea`, desenfoque acrílico y región redondeada; solo se mueve cuando cambia la barra.
- Cada ícono tiene tamaño fijo (el máximo) y se escala/mueve con transformaciones: sin recalcular diseño en cada cuadro.
- Los píxeles transparentes dejan pasar los clics (ventana "layered"); los íconos tienen un fondo casi invisible para que sí reciban clics.
- **Motor de animación**: `CompositionTarget.Rendering` se engancha solo cuando algo cambia. Cada cuadro:
  1. tamaños deseados según el mouse (`Magnifier.TargetSizes`, con posiciones sin ampliar),
  2. acercamiento suave (`Magnifier.Approach`),
  3. disposición anclada al mouse (`Magnifier.Layout`),
  4. rebote (`Magnifier.Bounce`), etiqueta y ocultación automática.
  Cuando todo quedó quieto se desengancha.
- Agregar/quitar elementos = crecer desde 0 / achicarse hasta 0 (misma animación). Quitar arrastrando = "nubecita" (escala + desvanecido).
- Lado izquierdo: `Pinned` (apps y Launchpad). Lado derecho: `Stacks` (Descargas, Papelera, carpetas, archivos). Al cambiar, los Docks de otros monitores se rehacen (`SettingsStore.PinsChanged`).
- `Core/PopupGuard.cs`: cierra menús y ventanitas al hacer clic en otra app (el Dock no toma el foco y WPF a veces no se enteraba).

## Barra de menú (`MenuBar/`)
- Arriba de cada monitor (28 px a 100 %), fondo acrílico (`SetWindowCompositionAttribute`).
- Íconos de la bandeja propia (en color o blanco y negro con máscara de opacidad) y clima (`Panels/WeatherIcon.cs`, dibujado; se actualiza cada 30 min).
- No se activa al hacer clic: la app activa sigue teniendo el foco y los menús le mandan atajos con `SendInput`.
- Escucha `TaskbarCreated` para recuperarse si el Explorador se reinicia.
- Reloj: temporizador alineado al cambio de minuto. Red, batería y volumen: al cambiar (eventos del sistema) y cada minuto.

## Isla dinámica (`MenuBar/IslandWindow.cs`, `Core/ActivityHub.cs`)
- `MenuBarContent` (UserControl) tiene todos los botones y menús de la barra; la barra lo usa en una fila y la isla en dos filas.
- `IslandWindow`: ventana transparente arriba al centro de cada monitor con una píldora (`Border`) que cambia de tamaño con animación (BackEase). Capas: reposo, expandida, actividad y reproductor (fundido entre capas).
- Siempre negra: mezcla `Themes/Dark.xaml` en sus recursos, así sus menús también son oscuros.
- `ActivityHub` (solo se enciende en modo isla):
  - Volumen: `VolumeWatcher` implementa `IAudioEndpointVolumeCallback` (Core Audio avisa; los cambios hechos por McBar llevan una marca y se ignoran).
  - Brillo: evento WMI `WmiMonitorBrightnessEvent` (solo pantallas que lo permiten).
  - Batería: `SystemEvents.PowerModeChanged` → `BatteryActivity()` decide qué mostrar.
  - Música/video: `MediaWatcher` con `GlobalSystemMediaTransportControlsSessionManager` (WinRT).
  - Notificaciones: `NotificationWatcher` (lector oficial o lectura del cartelito por UI Automation, avisado por `WindowTracker` al mostrarse/descubrirse la ventana).
- Si se reserva espacio, el área de trabajo de arriba es el alto de la isla en reposo.
- Cajón: `ControlCenterContent` (el mismo contenido del Centro de control) se agrega debajo de la isla expandida (estado `Drawer`).
- Apariencia: `IslandStyle` (black/graphite/glass/accent), `IslandSize`, `IslandShowDate`, `IslandGlow`. El estilo Vidrio usa un `DockBackdrop` que sigue a la píldora.
- Avisos: con varios monitores solo se muestran en la isla del monitor de la ventana con foco (`App.ActiveMonitorDevice`).
- `ShellHook` (ventana oculta) recibe `TaskbarCreated` y `WM_SETTINGCHANGE` para todo McBar.

## Paneles (`Panels/`)
- `PanelWindow`: base con tarjeta, sombra, animación y cierre al perder el foco.
- `ControlCenterWindow`, `WidgetsWindow`, `LaunchpadWindow`, `SettingsWindow`, `McDialog`.
- Se crean al abrir y se cierran del todo; luego `App.ScheduleTrim()` libera memoria.

## Notificaciones propias (0.4.0)
- `Core/NotificationWatcher`: fuente (plan A: lector oficial `UserNotificationListener`; plan B: lee el cartelito de Windows con UI Automation cuando `WindowTracker` avisa que apareció).
- `Core/NotificationCenter`: lista en memoria (máx. 60), sin leer, agrupado por app (`Grouped`) y tiempo relativo (`Ago`), con pruebas. Avisa `Changed`/`Added`; `ActivityHub` usa `Added` para la isla.
- `Panels/NotificationListView`: la lista (se usa en el cajón de la isla y arriba del panel de widgets).
- `Native/KeyboardHook`: gancho de teclado de bajo nivel en su hilo; Win+N → `App.ToggleWidgets`, Win+A → `App.ToggleControlCenter`; manda la tecla vacía 0xE8 para que no se abra Inicio.

## Cajones de la isla
`IslandWindow.ToggleDrawer(DrawerKind)`: Centro de control, Notificaciones, Wi-Fi, Sonido, Batería, Clima, Bluetooth y Temporizador (1.2.0). `BuildDrawer` arma el contenido; se mide dos veces (al pedirlo y ya cargado) para que no quede cortado.

## Launchpad y carpetas (0.4.0)
- `Core/LaunchpadLayout`: lógica pura (crear, agregar, sacar, renombrar, deshacer, limpiar, ordenar, buscar) sobre `AppSettings.LaunchpadFolders` (lista de `LaunchpadFolder` con los Target de las apps).
- `LaunchpadWindow`: arrastrar con `DragDrop` (dato = Target de la app); capa `_folderLayer` para la carpeta abierta. Fondo = `Mc.LaunchpadBg` y tinte del desenfoque escalados por `LaunchpadTransparency` (50 = como antes).

## Pedidos 1.6.1
- Ícono propio: `AppSettings.CustomIcons` (destino o "mcbar:launchpad" → imagen copiada en `iconos-propios`); `DockWindow.LoadIcon` lo mira primero (`IconLoader.FromImageFile`).
- Isla en pantalla completa: `SetFullscreen(true)` esconde la píldora y muestra `PeekHandle`; `PeekIn` (mouse en la manijita) la muestra; `PeekOut` (700 ms después de salir el mouse) la vuelve a esconder.
- Tema Vidrio: `ThemeManager.ApplyGlassTransparency` cambia la opacidad de `GlassBackgrounds` según `GlassTransparency` (50 = original). `App.OnSettingsChanged` rearma el tema cuando cambia.

## Aspecto de los paneles (1.6.0)
`Panels/PanelLook`: `Radius` (18), `Spring` (BackEase 0.32), `Highlight()` (borde con brillo arriba, encima del contenido) y `SpringIn()` (escala 0.94 → 1 y baja 14, 460 ms). `PanelShadow.Attach(window)`: ventana transparente detrás del panel con un rectángulo redondeado con `DropShadowEffect` en `BitmapCache`, recortado (`CombinedGeometry.Exclude`) para que no oscurezca el vidrio; sigue al panel (posición, tamaño, opacidad) y se cierra con él. `PanelWindow` los usa a todos.

## Escala por monitor y rendimiento (1.5.0)
- El proceso es `PerMonitorV2` (app.manifest). `MonitorInfo.Scale` = escala de ese monitor. Regla: lo que está atado a un monitor usa `m.Scale` (nunca `App.Scale`, que es la del sistema). Ventanas de McBar que se ubican en un monitor: `WindowOps.PlaceOnMonitor(w, m, leftDip, topDip[, ancho, alto])` con distancias desde la esquina del monitor en unidades de su escala; la ubica en píxeles con SetWindowPos. Dentro de una ventana ya mostrada, `WindowOps.DpiScale(visual)`.
- `Core/Perf`: el Dock anota cada cuadro (`DockFrame`) y al terminar la animación `DockAnimationEnded` resume (pura: `Summarize`). El Launchpad anota cuánto tarda en `ContentRendered`.

## Arrastrar fuera del Dock (1.4.1)
`DockWindow.UpdateGhost()` (desde `Stage_MouseMove` mientras se arrastra): si `IsPulledOut` (= `OutsideDock`: más de 50 arriba o 40 a un costado de la barra) crea un `Dock/DragGhost` con el ícono y lo mueve a `GetCursorPos`; el ícono dentro del Dock queda con opacidad 0. Al soltar: `Poof()` (nubecita en el fantasma) y `_suppressPuff` evita la del Dock; si no se quitó, `Cancel()`.

## Íconos de macOSicons (1.4.0)
- `Core/MacIcons.RunAsync(manual)`: arma la lista (apps fijadas + `AppCatalog`), saca las que no hace falta (`ShouldSkip`, `NeedsLookup` con el índice), y por cada una hace POST a la API (cuenta el cupo con `CountOne`; 401/403 = clave mala, 429 = sin cupo), elige el resultado con `Pick` (mismo nombre normalizado o que empiece igual, el más bajado) y baja `lowResPngUrl` como `<nombre de la app>.png`. Así `IconPack` lo encuentra por nombre. Guarda `indice.json` y `creditos.txt`. Evento `IconsChanged` → `App.UseMacIconsFolder` (pone la carpeta como `IconPackPath` y recarga los íconos).
- Clave: `AppSettings.MacIconsKey` cifrada con `Native/Dpapi` (CryptProtectData); está en `Profiles.MachineFields` y se borra en `Export`/`Capture`.
- Puras con pruebas: `Pick`, `ShouldSkip`, `Remaining`, `SafeFileName`, `NeedsLookup`, `ParseHits`.

## Íconos con forma de Mac (1.3.2)
`Core/IconShaper.Shape(src, px)` corre en el hilo STA del Shell dentro de `IconLoader.Request` cuando `appShape` y `IconShape == "mac"`. Puras: `SquirclePoints`, `IsFullBleed`, `Dominant`, `Mix`. Al cambiar el ajuste, `App.OnSettingsChanged` vacía el caché y rehace los íconos del Dock (igual que con el paquete de íconos).

## Vigilante de trabadas (1.3.1)
`Core/HangWatch`: hilo aparte que cada segundo manda un "ping" (`BeginInvoke`) al hilo de la interfaz. `Dispatcher.Hooks.OperationStarted/Completed` guardan la tarea en curso (nombre interno de `DispatcherOperation.Name` por reflexión). 5 s sin respuesta → log con la tarea; 20 s (`RestartIfHung`) → `Recovery.SaveReport` + `McBar.exe --reinicio` + `Kill()`. `--reinicio` espera hasta 15 s el mutex (abandonado = tomado) y no muestra el instalador. `Decide` es pura.

## Personalización (1.3.0)
- `Core/Profiles`: `SettingsProfile {Name, GameMode, Data}` en `AppSettings.Profiles` + `ActiveProfile`. `Capture` (JSON sin perfiles), `SaveAs`, `Switch` (actualiza la foto del actual y copia la del nuevo con `CopyInto`, por reflexión, sobre el MISMO objeto `SettingsStore.Current`), `MachineFields` (nunca se copian) y `ProfileFields`. `Use` = Switch + `NotifyChanged` + `NotifyPinsChanged` + modo juego + evento `ProfileChanged` (App rehace tema y widgets del escritorio; Preferencias se vuelve a armar). `Export`/`ReadExport`/`ImportFrom` (archivo `{App:"McBar", Version, Exported, Settings}` o un settings.json suelto). Todo puro salvo `Use`/`ImportFrom`/`ExportTo`.
- `Core/GameMode`: `On`, `Set`, `Toggle`, `Changed` (no se guarda). Lo miran `KeyboardHook` (Win sola), `HotCorners.Tick`, `ActivityHub` (notificaciones en la isla), `DockWindow.AutoHide` (+ `OnGameMode`), `LiveStatus.Summary` ("Juego") y `App` (`UpdateWorkAreas`).
- Carpetas de apps del Dock (1.8.3): `Core/DockFolders` (lógica pura) + `AppSettings.DockFolders`; el Dock las muestra como `DockItemKind.AppFolder` (destino `mcbar:appfolder:<id>` en `Pinned`). Cualquier cambio pasa por `DockWindow.ApplyFolderChange` (guarda el orden, aplica, rearma y avisa a los otros Docks).
- Preferencias de vidrio (1.8.3): `Mc.SettingsBg` + `WindowOps.SetGlassBackdrop`.
- Íconos .icns (1.8.2): `Core/Icns` saca el PNG de adentro; lo usan `IconLoader.FromImageFile` e `IconPack.Load`.
- Estilo Mac (1.8.0): `Core/MacLook.Apply()` (desde `App.OnSettingsChanged` y al arrancar) prende/apaga `AutoAppearance`, `MacCursors`, `MacSounds`, `MouseHook` y `StageManager`. Cursores y sonidos: copia del registro en `%AppData%\McBar\cursores-anteriores.json` / `sonidos-anteriores.json` (`RegBackup`), se devuelven al apagar y en `Installer.Uninstall`. Recursos embebidos `McBar.Cursores.*` / `McBar.Sonidos.*` → `%AppData%\McBar\cursores|sonidos`. Gancho de mouse solo si `NeedsMouseHook`. Capturas y Alt como Cmd van por `KeyboardHook` (cuentan en `NeedsKeyboardHook`). `App.KeepBarsOnTop` no sube las barras mientras `Screenshots.Busy`.
- `Core/DynamicWallpaper`: `Slots` (6/12/18/21), `SlotFor` y `PathFor` puros; timer de 1 min; `SystemParametersInfo(SPI_SETDESKWALLPAPER)` en otro hilo, solo si cambió la imagen. `Apply()` desde `App.OnSettingsChanged`. 1.7.0: fondos propios embebidos (`McBar.Fondos.*`) que `ExtractBundled` copia a `%AppData%\McBar\fondos`; con `WallpaperLockScreen` también cambia la pantalla de bloqueo (`Windows.System.UserProfile.LockScreen`, asincrónico, errores al registro).
- Preferencias → pestaña "Personalización": `ProfilesBox`, `GameModeBox`, `WallpaperBox`, `BackupBox`. Centro de control: `GameModeToggle`. Menú de McBar: submenú Perfil + Modo juego. Buscador: "modo juego" y nombres de perfiles.

## Isla más viva (1.2.0)
- `Core/LiveStatus`: estado del temporizador (`TimerEnd`/`TimerPaused`) y del cronómetro; `DispatcherTimer` de 1 s (`Tick`): fin del temporizador → `NotificationCenter.Add` + `MessageBeep`; descargas (tamaño y velocidad suavizada); cada 2 s micrófono/cámara desde `HKCU\...\CapabilityAccessManager\ConsentStore\{microphone,webcam}` (y `NonPackaged`): en uso = `LastUsedTimeStop == 0`. `FileSystemWatcher` sobre Descargas (`FinalName` reconoce .crdownload/.part/.partial/.download/.opdownload; el renombrado al nombre final = terminó). Evento `Changed`. Puras con pruebas: `Format`, `FinalName`, `AppName`, `PrivacyText`, `ParseTimerCommand`, `SizeText`.
- `Panels/TimerView` (panel/cajón; `ParseMinutes` puro). `App.ShowConnectivity("timer")` → `DrawerKind.Timer` en la isla o `ViewWindow` debajo de la barra.
- `IslandWindow`: `_liveChip` en la columna izquierda (al lado de la carátula), `_micDot`/`_camDot` antes del globito; `CompactNowWidth()` suma el ancho del chip (+84) y de los puntitos (+18). `MenuBarContent`: `LiveButton` y `PrivacyDots` (XAML) + `UpdateLive`.
- Ajustes: `LiveActivities`, `LiveDownloads`, `PrivacyDots`, `TimerPresets` (Sanitize: 1..1440, sin repetir, hasta 6).

## Widgets, calendario y recordatorios (1.1.0)
- `Core/IcsCalendar`: baja los .ics (`CalendarUrls`) cada 15 min; lector propio `Parse` (puro, con pruebas: días enteros, TZID/Z, RRULE simple, EXDATE, CANCELLED, renglones partidos). `DueForReminder` para el aviso de 10 min.
- `Core/Reminders`: lista en `recordatorios.json`, `ParseQuick` (lenguaje natural, puro) y `When`.
- `Panels/WidgetViews`: `WidgetFactory.Build(tipo)` y `DesktopWidgetWindow` (fuerza HWND_BOTTOM en WM_WINDOWPOSCHANGING). `App.StartDesktopWidgets/AddDesktopWidget/RemoveDesktopWidget`, `App.StartReminders` (timer de 30 s → `NotificationCenter.Add`).

## Más cosas de Mac (1.0.0)
- `Core/HotCorners`: timer de 100 ms (solo si hay alguna esquina en uso), `CornerAt` puro, `Monitors.FromPoint`.
- `Core/Tiling` (`Compute` puro) + `Native/WindowTiler` (compensa DWMWA_EXTENDED_FRAME_BOUNDS, guarda el tamaño anterior). Atajos en `KeyboardHook` (`TileFor` puro; exige Alt IZQUIERDO para no chocar con AltGr).
- `Core/VirtualDesktops`: lista del registro (`ParseIds` puro), cambiar/crear con Ctrl+Win+flechas / Ctrl+Win+D. Franja en `MissionControlWindow`.
- Quick Look: `KeyboardHook` se come el Espacio solo si el foco está en la lista de archivos (`DirectUIHWND`) de un `CabinetWClass`; `Core/QuickLookData` (selección con Shell.Application, PDF con Windows.Data.Pdf, `KindOf`/`Size` puros); `Panels/QuickLookWindow`.

## Estabilidad (0.9.0)
`Core/Recovery`: `RegisterApplicationRestart` al arrancar (salvo 3 errores en 10 min, `TooManyRecentCrashes`), `SaveReport` en `UnhandledException` (antes de `SafeCleanup`), `RecentReport` para avisar al reabrir. `Core/Diagnostics`: memoria, % CPU (`CpuPercent` puro), identificadores, GDI/USER; `App.StartHealth` lo anota cada 10 min.

## Ventanas estilo Mac (0.8.0)
- `Native/DwmThumb`: DwmRegisterThumbnail / DwmUpdateThumbnailProperties (`Fit` puro). `Native/DwmThumb.Show` usa `Win32.NormalSize` si la ventana está minimizada (1.3.1). `Panels/ThumbHost` ubica las miniaturas sobre recuadros WPF (en píxeles) en cada `LayoutUpdated`.
- `Core/WindowSwitch`: `AppsByRecency`, `Step`, `GridLayout` (puros, con pruebas).
- `Panels/WindowViews`: `SwitcherWindow` (Alt+Tab), `PreviewWindow` (Dock, timer de 550 ms en `DockWindow`), `MissionControlWindow` (Win+Tab). `App.SwitcherStep/Commit/Cancel`, `App.ToggleMissionControl`, `App.ShowDockPreview/HideDockPreview`.
- `KeyboardHook`: Alt+Tab se "come" el Tab mientras Alt está apretado (y manda 0xE8 para que soltar Alt no active el menú); al soltar Alt confirma.

## Wi-Fi, Bluetooth y sonido (0.7.0)
- `Core/WifiService` (WinRT: WiFiAdapter, Radio) — `Arrange` puro.
- `Native/BluetoothDevices` (bthprops: listar, `KindOf` puro, `SetAudioConnected` con BluetoothSetServiceState).
- `Native/AudioDevices` (IMMDeviceEnumerator para listar, IPolicyConfig para el predeterminado).
- `Panels/ConnectivityViews`: `WifiView`, `BluetoothView`, `SoundView` y `ViewWindow`. `App.ShowConnectivity(kind)` los abre como cajón de la isla (`DrawerKind.Wifi/Bluetooth/Sound`) o como panel.

## Teclado, buscador y volumen (0.6.0)
- `Native/KeyboardHook`: un solo gancho (en su hilo) para Win sola → Launchpad, Win+S → `App.ToggleSpotlight`, Win+N/Win+A y teclas de volumen (se "comen" y van a `Core/VolumeKeys`). Se instala si `AppSettings.NeedsKeyboardHook`.
- `Panels/SpotlightWindow`: resultados por secciones; `Core/Calc` (analizador propio, puro), `Core/FileIndex` (índice propio de carpetas del usuario, `Rank` puro), páginas de Configuración fijas.
- `Core/VolumeKeys` → `ActivityHub.Show` (isla) o `Panels/OsdWindow` (cartel estilo macOS).

## Instalador (0.5.0)
`App.OnStartup` decide antes de crear el mutex: `--uninstall` → confirmación + `Installer.Uninstall()`; si el .exe no está en `Installer.InstallDir` (y no es `--portable` ni `PortablePath`), `Installer.Decide` → `InstallerWindow` (instalar/actualizar) o abrir la instalada. `Core/Installer.cs` tiene todo lo que toca disco y registro (HKCU\...\Uninstall\McBar, Run, accesos directos con WScript.Shell). `SafeCleanup(keepTaskbarHidden)` al cerrar sesión no devuelve la barra si McBar arranca con Windows.

## Launchpad 0.4.3
- Claves: `LaunchpadEntry.Key` = Target de la app o `folder:<id>`. `LaunchpadHidden` guarda claves de apps y de carpetas; `LaunchpadOrder` el orden (`ApplyOrder`, `Move`); `WithoutHidden` filtra. Todo puro y con pruebas.
- Arrastre: `DataObject` con `McBar.LaunchpadKey` (mover/carpetas), `McBar.Pin` ("nombre\ntarget", lo entiende el Dock) y "Shell IDList Array" (de `ShellCom.ShellDataObject`, el escritorio crea el acceso directo). `QueryContinueDrag` esconde el Launchpad si el puntero va abajo o a los bordes.
- `Core/Uninstaller`: Tienda → `ms-settings:appsfeatures-app?<familia>`; escritorio → `UninstallString` del registro (`BestMatch`, `SplitCommand`).

## Paquetes de íconos (0.4.3)
`Core/IconPack`: índice de la carpeta (recursivo, el archivo más grande por nombre), nombres posibles (`Candidates`) y carga (`Load`). `IconLoader.Request(..., displayName)` lo consulta primero.

## Tema Vidrio (0.4.3)
`Themes/Glass.xaml` se agrega AL FINAL de los diccionarios sobre `Dark.xaml` (`ThemeManager.Apply`). La isla usa estilo vidrio con este tema.

## Gota (0.4.2, quitada en 0.4.3)
`TopBarMode = "drop"` usa la misma `IslandWindow` que la isla (`AppSettings.IsIslandLike`). Diferencias: separación 0 con el borde (`TopGapFor`), solo esquinas de abajo redondeadas (`Corners`) y dos "hombros" (`MakeShoulder`: cuadradito menos un cuarto de círculo) a los costados de la píldora. La forma de la ventana es un rectángulo que incluye los hombros.

## Clima propio (0.4.1)
`Core/Weather.cs` pide a Open-Meteo el tiempo actual (con sensación, humedad, viento), 24 horas y 7 días. `Panels/WeatherView` lo dibuja; se usa en el cajón `DrawerKind.Weather` de la isla y en `WeatherWindow` (barra de menú, `App.ToggleWeather`).

## Preferencias (0.4.1)
`SettingsWindow.Build()` arma las secciones en orden (cada una empieza con `Header`); `Tabbed()` las reparte en pestañas a la izquierda. Los elementos con `Tag = "tail"` (botones de abajo y firma) quedan siempre visibles.

## Íconos (`Core/IconLoader.cs`)
- Hilo de fondo STA con su propio Dispatcher (bucle de mensajes), que necesitan los objetos COM del Shell. `IShellItemImageFactory` sirve para .exe, .lnk, carpetas, archivos y `shell:AppsFolder\<id>`.
- Caché chico para el Dock (tope 120); el Launchpad usa su propio caché que se descarta al cerrar.

## Barra de Windows (`Native/Taskbar.cs`)
- Guarda el estado original, pone "ocultar automáticamente" (para liberar el espacio) y esconde las ventanas de la barra.
- Si Windows la vuelve a mostrar (Inicio, reinicio del Explorador), se vuelve a esconder.
- Botón de bandeja: la muestra un momento con Win+B (experimental).

## Datos guardados
- `settings.json` (ajustes y apps fijadas), `notas.txt` (widget de notas), `mcbar.log` (errores, se recorta a 512 KB).
