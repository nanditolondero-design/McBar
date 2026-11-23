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
`IslandWindow.ToggleDrawer(DrawerKind)`: Centro de control, Notificaciones, Wi-Fi, Sonido, Batería. `BuildDrawer` arma el contenido; se mide dos veces (al pedirlo y ya cargado) para que no quede cortado.

## Launchpad y carpetas (0.4.0)
- `Core/LaunchpadLayout`: lógica pura (crear, agregar, sacar, renombrar, deshacer, limpiar, ordenar, buscar) sobre `AppSettings.LaunchpadFolders` (lista de `LaunchpadFolder` con los Target de las apps).
- `LaunchpadWindow`: arrastrar con `DragDrop` (dato = Target de la app); capa `_folderLayer` para la carpeta abierta. Fondo = `Mc.LaunchpadBg` y tinte del desenfoque escalados por `LaunchpadTransparency` (50 = como antes).

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
