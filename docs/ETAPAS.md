# Etapas de McBar

## Etapa 1 — versión 0.1 (17/11/2026) ✅ escrita, pendiente de primera compilación y prueba
- Dock con ampliación, rebote, indicadores, etiquetas, reordenar/quitar arrastrando, soltar archivos, Descargas, Papelera, menús contextuales, ocultación automática.
- Barra de menú con menú del sistema, menús de la app activa (por atajos), estado (bandeja, batería, red, volumen), búsqueda, Centro de control y reloj.
- Centro de control, Widgets (calendario, clima, batería, sistema, notas), Launchpad con búsqueda y páginas.
- Preferencias con aplicación en vivo, tema claro/oscuro/automático, iniciar con Windows.
- Ocultar/restaurar barra de Windows de forma segura (`--restaurar`).
- Pruebas xUnit de la lógica, workflow de GitHub, documentación.

## Etapa 2 — versión 0.2 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Pedidos  después de probar la 0.1:
- Espacio de la barra de Windows (franja negra) → área de trabajo propia por monitor.
- Varios monitores → barra y Dock por monitor.
- "Al minimizar la barra deja de funcionar" → detección propia de pantalla completa.
- Launchpad con página vacía → carga en hilo con bucle de mensajes + plan B; aspecto tipo macOS.
- Transparencia en todo → desenfoque real en Dock, Centro de control, Widgets (los menús siguen semitransparentes).
- Animación rara al pasar el mouse (el fondo se movía) → fondo quieto por defecto, opción de estirar.
- Menú contextual que achicaba el ícono y quedaba pegado → arreglado (PopupGuard).
- Animaciones más fluidas → transformaciones en vez de recalcular el diseño, ventana del Dock más chica.
- Animaciones al minimizar/restaurar → hacia el ícono del Dock (HSHELL_GETMINRECT) + activar la opción de Windows.
- Arrastrar afuera para borrar → etiqueta + nubecita.
- Sacar Papelera/Descargas y poner lo que quiera → lado derecho configurable.
- Clima con íconos dinámicos en la barra.
- Dock "flotando" → estilos Clásico / Flotante / Ancho completo.
- Systray propio → TrayHost (Shell_TrayWnd propio + reenvío al Explorador).

## Etapa 3 — versión 0.3 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
- Isla dinámica como opción en Preferencias, con todo lo de la barra adentro.
- Eventos elegidos: música y videos, volumen y brillo, batería y carga, notificaciones.
- Notificaciones: primero intenta el lector oficial de Windows; si Windows no lo permite (pasa con apps no instaladas como paquete), lee el cartelito de la notificación con la accesibilidad de Windows. En Preferencias se ve cuál está usando.

## Versión 0.3.1 (22/11/2026) — arreglos de rendimiento y errores
Reportado por Pedido: app lenta, sin animación al soltar para quitar, barra superior que se oculta, Launchpad con páginas vacías, barra de Windows que aparece sola. Ver CHANGELOG.

## Versión 0.3.2 (22/11/2026)
Isla: cajón del Centro de control, apariencia configurable, reloj grande; sin íconos de bandeja de Windows; revisión de varios monitores; esquinas del desenfoque del Dock; barra de Windows al vaciar la papelera. Ver CHANGELOG.

## Versión 0.3.3 (22/11/2026)
Ventanas rápidas (vidrio de DWM + región) para el Dock y la isla; arreglo de las ventanas "locas" con la ocultación automática. Ver CHANGELOG.

## Etapa 4 — versión 0.4.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Pedido de Pedido: fondo del Launchpad más transparente, carpetas o grupos de íconos, y un centro de notificaciones propio (adaptado a la píldora y a la barra) en lugar del panel lateral de Windows.
- Centro de notificaciones propio (`Core/NotificationCenter.cs`, `Panels/NotificationListView.cs`): cajón desde la píldora o panel del reloj.
- Atajos Win+N / Win+A propios (`Native/KeyboardHook.cs`).
- Launchpad: transparencia 0–100, desenfoque opcional y carpetas (`Core/LaunchpadLayout.cs`).
- Isla: cada ícono (Wi-Fi, sonido, batería) abre su propio cajón (pedido del 22/11 15:09).
- Dock: arrastrar afuera una app abierta sin fijar la cierra (pedido del 22/11 15:14).
- Arreglos del video del 22/11 15:06: íconos del Dock cortados, cajón de la isla cortado, panel de widgets con hueco, buscador del Launchpad tapado por la isla, aviso de compilación.

## Versión 0.4.1 (22/11/2026)
Rendimiento, ventanas que se sacudían (área de trabajo), aspecto nuevo del Dock (fondo que crece + vidrio propio), Papelera que se perdía, widget del clima propio, isla Vidrio sin cuadrado oscuro, Launchpad menos transparente, Preferencias en pestañas. Ver CHANGELOG.

## Versión 0.4.2 (22/11/2026)
Ocultar apps del Launchpad, modo "Gota" para la barra superior, Centro de control con filas enteras como botones. Ver CHANGELOG.

## Versión 0.4.3 (22/11/2026)
Launchpad: selección múltiple, carpetas ocultables y movibles, orden propio, desinstalar, arrastrar al Dock/escritorio, efecto al pasar el mouse. Barra: menús solo con app activa, sin Gota, píldora oculta en pantalla completa. Dock: estilos Barra y Solo íconos, resaltado, desenfoque sin estirar, paquetes de íconos. Tema Vidrio. Ver CHANGELOG.

## Versión 0.4.4 (22/11/2026)
Isla en un solo renglón (logo a la derecha) cuando no hay app activa.

## Etapa 5 — versión 0.5.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Pedido de Pedido: "un instalador copado, lindo" e integrarse mejor a Windows tapando todo lo posible. Plan acordado:
1. **0.5.0 — Instalador y arranque** (hecho): instalador propio, Aplicaciones instaladas, desinstalador, actualizar, arranque sin parpadeo.
2. **0.6.0 — Inicio, buscador y avisos**: tecla Windows → Launchpad, buscador tipo Spotlight (apps, archivos, calculadora), carteles de volumen/brillo en la isla.
3. **0.7.0 — Wi-Fi, Bluetooth y sonido propios**.
4. **0.8.0 — Ventanas estilo Mac**: vista previa en el Dock, Alt+Tab propio, Win+Tab tipo Mission Control.
Pendiente aparte: **actualización automática** — necesita que el workflow publique un *Release* en GitHub (hoy deja el .exe como *Artifact*, que pide iniciar sesión para bajarlo). El workflow solo se puede cambiar subiéndolo a mano.
No se puede tapar desde una app: inicio de sesión, avisos de UAC, la app Configuración y las ventanas del Explorador de archivos. Reemplazar el shell (explorer.exe) se descartó: rompe la Tienda, las notificaciones y otras partes.

## Etapa 6 — versión 0.6.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Tecla Windows → Launchpad, buscador tipo Spotlight (apps, archivos, calculadora, Configuración, web), teclas de volumen con cartel propio. Sigue: 0.7.0 Wi-Fi, Bluetooth y sonido propios.

## Etapa 7 — versión 0.7.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Wi-Fi (WinRT Windows.Devices.WiFi), Bluetooth (bthprops), salida/entrada de audio (Core Audio + IPolicyConfig) y radios (Windows.Devices.Radios), en cajones de la isla y paneles. Sigue: 0.8.0 ventanas estilo Mac.
Límites: vincular un dispositivo Bluetooth nuevo sigue siendo de Windows; conectar/desconectar desde McBar funciona solo con dispositivos de audio (Windows no tiene una función pública para los demás).

## Etapa 8 — versión 0.8.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Vista previa de ventanas en el Dock, Alt+Tab estilo Cmd+Tab y Win+Tab tipo Mission Control (miniaturas DWM). Con esto se completa el plan de integración acordado (0.5–0.8).

## Etapa 9 — versión 0.9.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Plan acordado después de la 0.8.0 ("hacé todo en etapas"):
1. **0.9.0 — Estabilidad** (hecho): informe y recuperación ante errores, Windows lo reabre, consumo en el registro y en Preferencias. La actualización automática se descartó: el usuario no la quiere.
2. **1.0.0 — Más cosas de Mac**: esquinas activas, ventanas en mosaico, escritorios en Win+Tab, Quick Look.
3. **1.1.0 — Widgets**: en el escritorio, calendario Google/Outlook, recordatorios.
4. **1.2.0 — Isla más viva**: temporizador/cronómetro, micrófono/cámara en uso, descargas. ✅
5. **1.3.0 — Personalización**: perfiles, modo juego, exportar/importar ajustes, fondos que cambian con la hora. ✅

## Etapa 10 — versión 1.0.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Esquinas activas, mosaico (menú Ventana + Ctrl+Alt), escritorios en Win+Tab y Quick Look. Sigue: 1.1.0 widgets.
Límites: Windows no deja mover ventanas de otras apps entre escritorios, así que eso no está; Quick Look funciona en las ventanas del Explorador (no en el escritorio); el arrastre de ventanas a los bordes lo sigue haciendo Windows (Snap).

## Etapa 11 — versión 1.1.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Widgets en el escritorio, calendario iCal (Google/Outlook) con avisos y recordatorios en lenguaje natural. Sigue: 1.2.0 isla más viva.

Límites: el calendario es de solo lectura (para crear eventos, en Google/Outlook); los widgets de escritorio se esconden con Win+D como el resto de las ventanas.

## Limitaciones conocidas (posibles próximas etapas)
- **Isla y notificaciones**: con el "plan B" no siempre se distingue bien qué texto es la app, el título y el mensaje; el cartelito de Windows se sigue viendo abajo a la derecha.
- **Isla y monitores con distinta escala**: igual que el resto de McBar, usa la escala del sistema.
- **Bandeja**: alguna app vieja que no vuelva a mandar su ícono cuando McBar arranca aparece recién al reiniciarla. Los globitos de notificación siguen siendo los de Windows.
- **Escalas distintas por monitor**: McBar usa la escala del sistema; en un monitor con otra escala se puede ver un poco borroso.
- **Desenfoque en menús contextuales**: no (Windows no lo permite en ventanas emergentes de WPF sin riesgo); son semitransparentes.
- **Menús reales de cada app**: Windows no los expone de forma general; se usan atajos estándar. A futuro: menús específicos para apps conocidas (Office, navegadores, Explorador).
- **Animación "genio" al minimizar**: no se puede aplicar a ventanas de otras apps sin efectos de bajo nivel; se descartó para mantener bajo consumo.
- **Notificaciones**: McBar las junta en su centro propio, pero no puede responderlas ni usar sus botones (Windows no da acceso); tocar una abre la app. Ocultar el cartelito de Windows es experimental.
- **Launchpad**: mover un ícono a otra página todavía no se puede arrastrando (hay que moverlo dentro de la página). Los paquetes de íconos en .icns o .svg no se leen (convertirlos a .png).
- Spotlight (búsqueda con atajo de teclado), "Mission Control" propio, pilas de otras carpetas, widgets de música (reproducción actual).

## Etapa 12 — versión 1.2.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Temporizador y cronómetro en vivo (isla, barra, panel y buscador), puntitos de micrófono/cámara en uso y progreso de descargas. Pruebas en `Stage12Tests`. Sigue: 1.3.0 personalización (perfiles, modo juego, exportar/importar ajustes, fondos según la hora).

## Etapa 13 — versión 1.3.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Perfiles (Trabajo/Juegos…) con modo juego opcional, modo juego (Centro de control, menú de McBar, buscador, Preferencias), exportar/importar ajustes y fondo de pantalla según la hora. Pruebas en `Stage13Tests`. Con esto se termina el plan de etapas acordado después de la 0.8.0.
Arreglos: `Typography` (está en `System.Windows.Documents`) en el temporizador y la isla; en Preferencias → Widgets → Calendarios había comillas rectas dentro de un texto que rompían la compilación de la 1.1.0; ahora son «».
Límites: el modo juego no se guarda (al reiniciar McBar arranca apagado, salvo que el perfil en uso lo tenga); el fondo cambia en todos los monitores a la vez (Windows usa una sola imagen).

## 1.3.1 (22/11/2026) — arreglos después de probar la 1.3.0
reportó que McBar se traba (el Dock deja de aparecer y la píldora queda quieta) y que no apareció el instalador. El registro no tenía errores (una trabada no deja error), así que se agregó `Core/HangWatch`: anota qué tarea de la interfaz no termina y reinicia McBar a los 20 s. El instalador no salía porque esa ruta estaba guardada como "probar sin instalar"; ahora solo se saltea para la misma versión (`PortableVersion`). Pendiente: con el registro de la próxima trabada, arreglar la causa.
También: la isla reservaba una franja de lado a lado arriba (pedido: "la píldora ocupa todo el lugar, las ventanas no se estiran del todo"). `IslandReserveSpace` pasa a false por defecto y `Migrate` (CurrentLook 2) lo apaga una vez a los que ya lo tenían.

Vista previa de minimizadas (pedido: "no cargan siempre o tardan mucho"): `DwmQueryThumbnailSourceSize` de una ventana minimizada da el tamaño del botón minimizado, así que se usa `Win32.NormalSize` (GetWindowPlacement); ícono de la app detrás de la miniatura; reintentos de `Sync`; 400 ms y cambio inmediato entre íconos (`App.DockPreviewOpen`).
Píldora sobre menús (Pedido: "la píldora tapa el menú", "los botones de minutos quedan cortados"): `PopupGuard` sube cada menú/popup a lo más alto al abrirse; `KeepBarsOnTop` no actúa con un menú abierto; `App.KeepBelowTopBar(Window)` para Preferencias; `TimerView` en dos renglones.

## 1.3.2 (22/11/2026) — íconos con forma de Mac
Pedido de Pedido: "¿no se puede hacer que solo le dé forma de ícono de Mac?". `Core/IconShaper` (en el hilo del Shell, con RenderTargetBitmap): superelipse n=5 al 90 % del cuadro; si el ícono llena su cuadro (`IsFullBleed`: cubre ≥85 %, bordes y esquinas opacos) se recorta, si no va al 66 % sobre una placa con degradé claro del color dominante (`Dominant`, `Mix`). `IconLoader.Request(..., appShape: true)` desde el Dock (apps), Launchpad y Alt+Tab/vista previa; clave de caché con "|mac". Ajuste `IconShape` ("none"/"mac"). Pruebas en `Stage15Tests`.

## 1.4.0 (23/11/2026) — íconos de macOSicons para las apps instaladas
Pedido de Pedido: "que no vengan los íconos sino que la app lea las apps instaladas y baje los íconos sola para cada app". API de macOSicons (POST https://api.macosicons.com/api/v1/search con `x-api-key`, respuesta `hits[]` con `appName`, `lowResPngUrl`, `credit`, `creditUrl`, `downloads`). Plan gratis: 50 búsquedas por mes; los términos piden conservar el crédito del autor y permiten uso personal. `Core/MacIcons` (índice `indice.json`, `creditos.txt`), `Native/Dpapi` (clave cifrada), `App.StartMacIcons` (automático: a los 2 min y cada 6 h) y `MacIconsBox` en Preferencias → Dock. Pruebas en `Stage16Tests`.
Ideas para después: elegir otro ícono para una app (clic derecho en el Launchpad), leer .svg/.icns/.zip en los paquetes.

## 1.4.1 (23/11/2026) — arreglos pedidos por (sus mensajes se perdieron y los resumió)
- "El estilo de la barra, píldora y Dock no cambiaba": `SettingsStore.NotifyChanged` avisa a cada suscriptor con su propio try/catch (si uno fallaba, los siguientes no se enteraban); el tema Vidrio ya no fuerza el estilo de la isla.
- "La animación de borrado se queda corta": `Dock/DragGhost` (ventana transparente que no recibe clics) sigue al mouse por toda la pantalla; `DockWindow.OutsideDock` (puro) también cuenta los costados.
- "Arrastrando del Launchpad al Dock oculto queda a mitad de camino": `DragEnter`/`DragOver` muestran el Dock (`_hidden = false`) y `DragLeave`/`Drop` lo vuelven a esconder.
- "No puedo poner íconos en el escritorio desde el Launchpad": se arrastra un .lnk real (`ShellCom.CreateLinkTo` con `SHParseDisplayName` + `IShellLinkW.SetIDList`) en `%TEMP%\McBar\arrastre`.
- Pregunta: barras de título estilo macOS en todas las ventanas → ver respuesta en el chat (no se hizo: requiere meterse en otros programas).

## 1.5.0 (23/11/2026) — rendimiento
tiene 2 monitores con escalas distintas, así que la escala por monitor fue lo primero. Manifest `PerMonitorV2`; `MonitorInfo.Scale` (GetDpiForMonitor); Dock, barra e isla usan `_monitor.Scale` y se reacomodan en `DpiChanged`; los paneles se ubican con `WindowOps.PlaceOnMonitor` (píxeles reales). Además: sin `TrimWorkingSet`; Launchpad sin `DropShadowEffect` (sombra = copia del texto) y con `BitmapCache` durante las animaciones; caché de íconos de 400 con salida del más viejo; forma fija de la isla durante la animación; `Core/Perf` en Diagnóstico. Pruebas en `Stage18Tests`.
Sigue: 1.6.0 gráficos (animaciones tipo resorte, vidrio nativo de Windows 11, sombras y bordes de panel, tipografía) — primero se le muestra a cómo quedaría.

## 1.6.0 (23/11/2026) — gráficos
aprobó la vista previa ("Me gusta la vista previa de la 1.6.0, hacela así"). `Panels/PanelLook` (curva `Spring`, `Highlight`, `SpringIn`) y `PanelShadow` (ventana aparte, que no recibe clics, recortada para que la sombra solo se vea afuera del panel, debajo de él en el orden de ventanas). Mc.GlassBg más transparente, radio 18 en paneles y 14 en tarjetas, menús con sombra en capa aparte y brillo, buscador con sombra separada, Launchpad con resorte.

## 1.6.1 (23/11/2026) — pedidos 
Ícono propio (`AppSettings.CustomIcons`, `DockWindow.ChooseIcon/ResetIcon/IconMenu`, `IconLoader.FromImageFile`), fijar arrastrando (`ReorderWhileDragging` deja mover apps abiertas sin fijar entre las fijas y `PinHere`), `LaunchpadLayout.LeavesGrid` (zonas más grandes), `KeepBarsOnTop` no sube el Dock con el Launchpad abierto, `MenuBar/PeekHandle` (manijita en pantalla completa, `IslandWindow.PeekIn/PeekOut`), globito → `App.OpenWidgetsPanel(monitor, centered: true)`, `GlassTransparency` (`ThemeManager.ScaleAlpha` sobre los fondos del tema Vidrio), sin `BackdropBlur` (Sanitize lo apaga). Pruebas en `Stage19Tests`.
Interpretación: "en el Launchpad el Dock queda siempre a la vista" se tomó como problema; era al revés (ver 1.6.2).

## 1.6.2 (23/11/2026)
Pedido: "el Dock y la píldora deben mostrarse siempre en el Launchpad" y "cuando minimizo una ventana me sigue mostrando las opciones de la ventana". `DockWindow.ShowOverLaunchpad` (sube el Dock y no se oculta solo), `App.LaunchpadBars` (Docks + `ApplyFullscreen` sin esconder la isla del monitor del Launchpad + `KeepBarsOnTop`), `LaunchpadWindow(monitor, dockHeight)` (fila extra del alto del Dock), `App.CloseLaunchpad` al tocar el Dock. `WindowTracker.Minimized` (EVENT_SYSTEM_MINIMIZESTART) → `MenuBarContent.UpdateAppName` a los 120 y 450 ms.

## 1.6.3 (23/11/2026)
Pedido: "sigue pasando lo mismo, tengo que hacer foco en el escritorio". `MenuBarContent`: temporizador `_appCheck` (400 ms, solo mientras hay menús de app a la vista) que llama a `UpdateAppName`; `HasActiveApp` mira `GetForegroundWindow` (ninguna, minimizada, escritorio o barra de Windows = sin app; las de McBar no cuentan).

## 1.7.0 (23/11/2026)
Pedido: "¿podrías hacer wallpapers en 4K para la función del fondo según la hora, de cero, basándote en los de la última versión de macOS?" y, por el inicio de sesión de Windows, pidió que McBar cambie también la pantalla de bloqueo y traiga los fondos con un botón. Fondos generados por código (cintas con brillo sobre degradé, 3840×2160, JPEG 95) en `src/McBar/Assets/Fondos`, embebidos en el .exe (`EmbeddedResource`, nombre lógico `McBar.Fondos.<archivo>`). `DynamicWallpaper`: `BundledNames`, `BundledPaths`/`UsesBundled` (puros), `ExtractBundled`, `UseBundled`, `SetLockScreenAsync` (WinRT `LockScreen.SetImageFileAsync`). Ajuste `WallpaperLockScreen` (apagado). Cambiar la disposición del inicio de sesión de Windows NO se hace (requiere tocar archivos del sistema).

## 1.8.0 (23/11/2026) — Estilo Mac
Pedido: "¿qué más podemos hacer para que Windows se vea como macOS?" → propuse 12 ideas y pidió "hacé todo pero que sean opciones, nada fijo". Todo va en Preferencias → Estilo Mac y viene apagado.
- `Core/MacLook` (coordina; `Bundled` copia recursos del .exe; `RegBackup` guarda y devuelve valores del registro).
- `Core/MacSystemLook`: `AutoAppearance` (Personalize, WM_SETTINGCHANGE "ImmersiveColorSet"), `MacCursors` (Control Panel\Cursors + SPI_SETCURSORS), `MacSounds` (AppEvents\Schemes + PlaySound).
- `Core/MacInput`: `MouseHook` (WH_MOUSE_LL en hilo propio), `ShakeDetector` (puro), `ShakeOverlay`, `WallpaperClick` (WindowFromPoint + UI Automation para no actuar sobre íconos → Win+D).
- `Core/Screenshots` + `Native/ScreenCapture` (BitBlt / PrintWindow) + `Panels/CaptureViews` (zona, barrita, miniatura).
- `Core/MacKeys` (Alt como Cmd; `KeyboardHook` suelta Alt, manda Ctrl+tecla, vuelve a apretar Alt y la tecla vacía 0xE8).
- `Core/AppMenus` (GetMenu/GetMenuItemInfo + WM_INITMENUPOPUP + WM_COMMAND) → `MenuBarContent.RealMenusPanel`.
- `Core/StageManager` + `Panels/StageStrip`; `WinUi.WallpaperBrush` para los escritorios de Win+Tab; `DockWindow.ShowStackFan` + `Core/StackFan`.
- Recursos propios generados por código: `tools/estilo-mac/gen_cursores.py`, `gen_sonidos.py` → `Assets/Cursores`, `Assets/Sonidos` (embebidos).

## 1.8.1 (23/11/2026)
Pedido: "lo de la ventana minimizada sigue pasando, no hace foco en el escritorio", "la tecla de Windows abre el menú Inicio y el Launchpad", "¿cómo elimino escritorios virtuales?", "pilas del Dock no sé cómo funcionan", "hay palabras que se cortan en las opciones", "poder agregar carpetas al Dock y que se abran en abanico o con otras formas" y "los botones semáforos sacalos, son feos y no van bien".
- `KeyboardHook`: la tecla Windows se traga entera (`_winSwallowed`); con otra tecla se inyecta Windows+tecla (`_winInjected`) y al soltar, tecla vacía + Windows arriba. Win+L → `Launcher.Lock`. `WindowOps.Key`: más teclas extendidas.
- `MenuBarContent.IsAppWindow` (la ventana o su dueña tiene que estar en `Tracker.Windows`); `App.FocusDesktopIfNoApp` al minimizar (Progman).
- `VirtualDesktops.Remove` + × / menú "Cerrar escritorio" en Win+Tab.
- `AppSettings.StackStyles` + `StackFan.Effective` + `DockWindow.StackStyleMenu` ("Mostrar como"); botón "Agregar una carpeta al Dock…" en Estilo Mac.
- `McSwitch` y `Segment`: textos con `TextWrapping`.
- Pedido: "el dibujo que le hace a los íconos para que parezcan de Mac debería pintarlos de un color distinto para cada ícono". `IconShaper.PlateColors` (tono del color principal, saturación 0,45–0,85, luz 0,84 → 0,66; gris si el ícono es gris) + `ToHsl`/`FromHsl`; `Dominant` da más peso a lo colorido (sat²).
- Se sacaron los botones de colores (`Core/TrafficLights`, `Panels/TrafficLightsWindow`, ajustes `TrafficLights*`).

## 1.8.2 (23/11/2026)
Pedido: "¿puede usar icns para íconos la app?" → "agregalo". `Core/Icns` (lee las entradas del contenedor, elige el PNG más chico que alcance el tamaño o el más grande; `Entries`/`BestPng` puros, pruebas en Stage22Tests). Lo usan `IconLoader.FromImageFile` (ícono propio) e `IconPack` (`.icns` en `Extensions` y en `Load`). El selector de "Cambiar ícono…" acepta *.icns.

## 1.8.3 (23/11/2026)
Pedido: "el menú de config (Preferencias) debería ser como el glass del iPhone", "el efecto glass es nulo", "el menú de configuración de McBar no aparece más" y "poder agregar carpetas de apps en el Dock como en Android".
- `Mc.SettingsBg` (Dark/Light opaco, Glass translúcido y en `ThemeManager.GlassBackgrounds`) + `WindowOps.SetGlassBackdrop` (DWMWA_SYSTEMBACKDROP_TYPE = acrílico en Win11 22H2+, `EnableBlur` en Win10). `App.OpenSettings` restaura si está minimizada y avisa si falla.
- `Core/DockFolders` (Drop, NewWith, TakeOut, Dissolve; puros, Stage23Tests) + `AppSettings.DockFolders`; en `Pinned` va `mcbar:appfolder:<id>`. Dock: `DockItemKind.AppFolder`, `UpdateMerge` (quieta 450 ms en el centro de otra app = carpeta), `LoadFolderIcon` (2×2), `ShowAppFolder`, `AddToFolderMenu`, `McDialog.Prompt` para el nombre.
- Decisión: se arman arrastrando (como Android) y además desde el clic derecho.

## 1.8.4 (23/11/2026)
Pedido: "cuando elijo un icns para el ícono del Launchpad no cambia en el Dock". `DockWindow.ChooseIcon` prueba la imagen con `IconLoader.FromImageFile` antes de guardarla y avisa si no se puede. `Icns.UnRle` + `Icns.DecodeOld` (RLE + máscara, ARGB) cuando el archivo no trae PNG; `Icns.Describe` para el registro. Pruebas en Stage22Tests.

## 1.8.5 (23/11/2026)
Se probaron dos .icns de Launchpad que "la app no lee" (ambos: PNG en ic07…ic14, uno con bloque eXIf), "faltan palabras" en Preferencias y "el slider del glass debe hacer lo mismo con la píldora". `Icns.DecodePng` (copia propia, IgnoreColorProfile, PngBitmapDecoder de respaldo, otros tamaños). `IslandWindow.ApplyAppearance`: vidrio con `ThemeManager.ScaleAlpha(…, GlassTransparency)` y `_backdrop` (desenfoque) siempre con estilo Vidrio, también en la ventana rápida. `SettingsWindow.SliderSetting(…, format)` y letra 13 en SliderSetting/Segment.

## 1.8.6 (23/11/2026)
Revisión: con la 1.8.5 la píldora de vidrio tenía un rectángulo claro alrededor (el `_backdrop` con desenfoque de Windows no respeta la región redondeada en Windows 11). `Core/WallpaperGlass`: fondo de pantalla achicado a 96 px (borroso al estirarlo), recorte "Rellenar"/"Expandir" (`VisiblePart`, pura), `DrawingBrush` con Viewport = posición de la píldora en el monitor + tinte según la transparencia. `IslandWindow` ya no muestra `_backdrop`; actualiza el pincel al cambiar de tamaño y cada minuto.

## 1.8.7 (23/11/2026)
Pedido: "¿no hay forma de que sea real la transparencia?" → opciones: (1) transparencia real sin desenfoque, (2) desenfoque real con la composición de Windows 11 (experimental). Eligió la 1. `IslandWindow.GlassTint(transparencia)`: degradé con alfa de `ScaleAlpha` (mínimo 0x30/0x38). `WallpaperGlass` queda sin uso. Pendiente si lo pide: opción 2 (HostBackdropBrush + recorte redondeado).

## 1.8.8 (23/11/2026)
Pedido: "agregar la opción de instalar si estoy probando la app". `App.IsTrying` / `App.InstallNow` (InstallerWindow con `fromRunning`: "Ahora no" / "Seguir probando"; al instalar abre la copia instalada con `--reinicio` y esta se cierra con `SafeCleanup(keepTaskbarHidden: true)`). "Instalar McBar…" en `MenuBarContent.BuildSystemMenu` y en el pie de Preferencias.

## 1.8.9 (23/11/2026)
Pedido: "que el Dock se oculte igual que la píldora". `AppSettings.DockHideOnMaximized` (true); `DockWindow.AutoHide` deja de ser estático y suma `_maximizedHere`; `DockWindow.SetMaximized` (desde `App.ApplyFullscreen` con `FullscreenWatcher.MaximizedMonitors`) y, si cambió, `App.UpdateWorkAreas` para soltar o volver a reservar el lugar del Dock.

## 1.9.0 (23/11/2026)
Revisión: el panel de widgets con un rectángulo claro detrás y texto que no se lee (el `EnableBlur` de `PanelWindow` en ventana en capas no respeta la región redondeada en Windows 11). `PanelWindow` sin `EnableBlur`; fondo `Mc.PanelCardBg` (Dark/Light casi opaco, Glass vidrio oscuro en `GlassBackgrounds`) y `ThemeManager.MinAlpha` (mínimo 0x5C para ese fondo).

## 1.9.1 (23/11/2026)
Pedido: "cuando arranca Windows primero se ve la barra de Windows y después carga la app" y "una opción para poner un botón de apagado/reinicio en el Dock como la Papelera". `Taskbar.Hide()` al principio de OnStartup; `Application.SessionEnding` además de `SystemEvents.SessionEnding`; `AppSettings.FastStartup` + `SystemInfo.SetFastStartup` (Explorer\Serialize StartupDelayInMSec/WaitForIdleState = 0, con `RegBackup`, se devuelve al desinstalar). Botón: `AppIdentity.Power` como pila, `DockItemKind.Power`, `DockWindow.PowerIcon` (dibujado) y `ShowPowerMenu`. En su captura de 15:40 "Abrir McBar al iniciar Windows" estaba apagado.

## 1.9.2 (23/11/2026) — Edición Microsoft Store
Publicación gratis en la Store: paquete MSIX (Microsoft lo firma). `-p:McBarStore=true` define `STORE`; `Core/Edition` (`IsStore`, `StartupTaskId`, `StoreAlias`). Sin instalador propio en esa edición; `SystemInfo.SetStartWithWindows` usa `StartupTask`; `HangWatch` se reabre por el alias. `packaging/store/AppxManifest.xml` (identidad del Partner Center, `runFullTrust`, `unvirtualizedResources` con las claves de HKCU que usa McBar, tarea de inicio, alias) y `packaging/store/Assets`. El workflow arma `McBar-Store.msix` con makepri + makeappx.

## 1.9.3 (23/11/2026)
Revisión con capturas: Win+Tab se veía negro y el Dock a veces no se escondía con una ventana maximizada. `MissionControlWindow`: fondo = `WinUi.WallpaperBrush()` (320 px estirado, queda difuminado) + capa oscura, sin `EnableBlur`. `FullscreenWatcher.Check`: "maximizada" se decide con la primera ventana GRANDE (o maximizada) de cada monitor (`decidedMax`), aparte de la pantalla completa.

## 1.9.4 (23/11/2026)
Con el Organizador visual prendido, la franja izquierda (ventana de 200 DIP de ancho y toda la altura, con fondo casi invisible #01000000) tapaba los íconos del escritorio de esa columna. `StageStrip`: fondo `null` (transparente de verdad: en las ventanas con transparencia, los píxeles con alfa 0 dejan pasar el mouse).

## 1.9.5 (23/11/2026)
El fondo de Win+Tab se veía pixelado (copia de 320 px estirada). `WinUi.WallpaperBrushLarge` (1280 px, en caché aparte) + `BlurEffect` radio 36 con `BitmapCache`, agrandado 80 DIP por lado y recortado, para que no se vea el borde del difuminado.

## 1.9.6 (23/11/2026)
Algunas apps (el Explorador) no se reacomodan cuando cambia el área de trabajo al esconderse o volver el Dock por una ventana maximizada. `Core/MaximizedFit`: `App.ApplyFullscreen` guarda las áreas de antes; a los 300 ms y a los 1000 ms se recorren las ventanas maximizadas y las que siguen pegadas al área vieja se estiran o achican (`Fit`, pura) con `SetWindowPos`. `Screenshots.CopyToClipboard`: cada captura va al portapapeles (imagen + PNG), con reintentos; "Copiar" de la miniatura usa lo mismo.

## 1.9.7 (23/11/2026)
`DockWindow`: `_dropTrash` se marca en `Window_DragOver` cuando un archivo está sobre la Papelera y `Step` muestra la etiqueta `TrashDropLabel` ("Mover a la Papelera"); se limpia en `DragLeave` y `Drop`. El efecto sigue siendo `Link` (con `Move` el Explorador podría borrar el original por su cuenta). `CloseWhenAway(popup, contenido, ocupado)`: cada 150 ms mira el mouse (`GetCursorPos`) contra el contenido de la carpeta o pila y la barra del Dock (con 24 px de margen, `IsNear`, pura); lejos por `AutoCloseDelayMs` (900 ms) → se cierra. No cierra con un menú contextual abierto dentro de la carpeta. Ajuste `DockPopupsAutoClose` (prendido de fábrica).

## 1.9.8 (24/11/2026)
Con la barra de menú, ventanas e íconos del escritorio quedaban debajo de la barra: `WorkArea.Verify` corregía el área SIN avisar (para no sacudir ventanas) y al arrancar el Explorador devuelve el monitor entero. `WorkArea.Refresh` (a los 3 y 10 s del arranque y al reiniciarse el Explorador): pone el área avisando a todos y llama a `MaximizedFit.Schedule` con el monitor entero como área vieja. `Verify` avisa cuando lo que se perdió es el borde de arriba, y siempre programa `MaximizedFit`. `WorkArea.Set` devuelve si cambió; `UpdateWorkAreas` programa `MaximizedFit` con las áreas que cambiaron (antes solo lo hacía `ApplyFullscreen`). `MaximizedFit.FitTop` (pura): ventanas comunes con barra de título debajo del borde de arriba se recortan (o se corren si quedan de menos de 200 px).

## 1.9.9 (24/11/2026)
La 1.9.8 no alcanzó: con la barra de menú, nada se acomodaba. `Native/TopAppBar`: `SHAppBarMessage` ABM_NEW / ABM_QUERYPOS / ABM_SETPOS con ABE_TOP y el alto de la barra; ABM_REMOVE al esconderla, al pasar a píldora y al cerrar (`Teardown`). `MenuBarWindow.ApplyGeometry` reserva antes de ubicar la ventana; el gancho de mensajes vuelve a ubicarla con ABN_POSCHANGED; `OnShellRestarted` (desde `App.OnExplorerRestarted`) vuelve a registrarla porque el Explorador nuevo no conoce las barras viejas. `WorkArea` sigue manejando el Dock (abajo) como antes; arriba pide lo mismo que reserva Windows, así no se pelean.

## 1.9.10 (24/11/2026)
`LaunchpadWindow.Fit(ancho, alto, dock)` (pura): con menos de 860 DIP de alto las filas de arriba/abajo pasan de 124/90 a 84/50; prueba grillas 7×5, 6×5, 5×5, 7×4… y elige la primera con íconos de al menos 52 px (máximo 80) con lugar para el nombre. `Cols`, `Rows`, `_iconSize`, `_labelMax` y `_labelFont` son de cada ventana; las carpetas escalan con el ícono. Búsqueda con plantilla propia (`SearchTemplate`): fondo negro 25 %, borde blanco 31 %, esquinas de 9.

## 1.9.11 (24/11/2026)
`DockWindow.TrashUnder(p, arrastrado)`: la Papelera si el punto está sobre ella (4 px a los costados, 24 arriba). Al arrastrar un ícono del Dock (`Stage_MouseMove`) se marca `_dragTrash`, se cancela armar carpeta y no se reordena; `Step` muestra "Quitar del Dock" / "Cerrar app" sobre la Papelera; al soltar (`Stage_MouseLeftButtonUp`) se quita con nubecita (`RemoveFromDock`) o se cierra la app (`CloseByDrag`). Los archivos (`Window_DragOver` / `Window_Drop`) usan el mismo margen.

## 1.9.12 (24/11/2026)
`Core/AutoSize`: `DockIcon(altoDip)` = 4,8 % del alto (38 a 64; 1080 → 52), `Magnified(ícono)` = ×1,85 (hasta 128), `DockAuto(ajustes)`. `AppSettings.DockSizeAuto` (`bool?`, true de fábrica; null en ajustes viejos → automático solo si estaba en 52/96; `Sanitize` lo fija). `DockWindow.BaseSize`/`MaxSize` usan el automático con el alto de SU monitor. Preferencias: interruptor nuevo; los deslizadores muestran el tamaño automático y al moverlos lo pasan a manual. `PanelWindow.ShowBelowMenuBar`: `MaxHeight` hasta el área de trabajo. `App.DefaultPins` (primera vez): Launchpad + Explorador + `Installer.TaskbarPins()` (ordenados por fecha de creación del acceso, que es cuando se fijaron); si no hay, las de ejemplo.

## 1.9.13 (24/11/2026)
`DockWindow.RefreshTrashIfChanged` (lo llama el vigilante de App cada ~2 s): compara `SystemInfo.RecycleBinHasItems()` con `_trashFull` y recarga el ícono de la Papelera si cambió; `LoadIcon(Trash)` sincroniza `_trashFull`. Ícono de apps empaquetadas: en `LoadIcon` (App), si la ventana existe, `IconSource` no es `shell:AppsFolder\…` y apunta a un .exe en `\WindowsApps\`, se usa `IconLoader.FromWindow(hwnd)` (con forma Mac si corresponde) en vez del ícono genérico del .exe.

## 1.9.32 (25/11/2026)
Bandeja: importar los íconos que ya están en la bandeja REAL del Explorador (apps que no se re-registran en TaskbarCreated: NVIDIA, SecurityHealthSystray, etc.). `Native/TrayImport.Read()` busca las ToolbarWindow32 de `Shell_TrayWnd>TrayNotifyWnd>SysPager` y de `NotifyIconOverflowWindow`, y por cada botón lee la TBBUTTON (dwData@16, layout x64) y la TRAYDATA apuntada (hwnd@0, uID@8, uCallback@12, hIcon@24) con `VirtualAllocEx`+`SendMessage(TB_GETBUTTON)`+`ReadProcessMemory` sobre el proceso del Explorador (misma integridad). `TrayHost.ImportExisting()` (hilo de la bandeja) fusiona por hwnd+uid, descarta los de explorer.exe, toma el ícono del handle (o de la ventana como respaldo), Id en formato `h:hwnd:uid` para no duplicar; `Handle` quita duplicados por hwnd+uid al llegar un NIM_ADD real. Se llama al arrancar y cada ~10 s desde el vigilante. Nuevos P/Invokes en Win32: `VirtualAllocEx/VirtualFreeEx/ReadProcessMemory/SendMessage` + constantes.

## 1.9.31 (25/11/2026)
`TrayHelper`: el reenvío ya no escribe en la tubería desde el hilo de la interfaz (eso colgaba McBar al apretar varias veces si el ayudante no leía). `Forward` solo encola en un `ConcurrentQueue` y hace `AutoResetEvent.Set()`; el hilo del servidor, ya conectado, drena la cola y escribe (si se cortó, `WriteLine` tira y reconecta). Al conectar descarta los clics viejos encolados. Cola tope 256. El ayudante (`RunServer`), antes de `PostMessage`, llama `AllowSetForegroundWindow(ASFW_ANY)` + el del proceso destino para que la app pueda traer su ventana/menú al frente.

## 1.9.30 (24/11/2026)
Dos arreglos. (1) Pantalla completa: `Taskbar.ReassertAutohide()` vuelve a poner `ABS_AUTOHIDE` si el Explorador se lo sacó a su barra (entonces Windows reservaba el alto y las maximizadas quedaban con hueco abajo, y McBar las reacomodaba a mano → salto). `Monitors.Verify()` lo llama antes de tocar el área de trabajo; si corrigió, sale (Windows devuelve la pantalla completa sin pelear ni sacudir ventanas). (2) `TrayHelper`: el servidor de la tubería guarda `_server` y `Stop()` lo hace `Dispose()` para destrabar `WaitForConnection` (antes `Stop` solo despertaba al hilo ya conectado; el que esperaba conexión quedaba colgado reteniendo la única instancia → "todas las instancias en uso" en loop). `ServerLoop` usa `PipeOptions.Asynchronous`, loguea el error una sola vez y reintenta cada 3 s; `StartServer` con `lock`.

## 1.9.29 (24/11/2026)
Ayudante del systray por **tarea programada** (permiso una sola vez) + **Instalación** en Preferencias. `Core/TrayHelper` reescrito: `StartServer` deja un `NamedPipeServerStream` (Out) esperando en un hilo, reusando la tubería ante reconexiones (un `ManualResetEvent` la rearma cuando un `Forward` falla); `Enable`/`Disable` llaman `RunElevated("--installtask"/"--uninstalltask")` (Verb=runas); `InstallTask` (elevado) corre `schtasks /Create /TN "McBar Tray Helper" /TR "\"<exe>\" --trayhelper" /SC ONLOGON /RL HIGHEST /F` y lanza el ayudante ya mismo; `UninstallTask` hace `schtasks /Delete`. `App.OnStartup`: ramas `--installtask`/`--uninstalltask` (hacen su trabajo y `Shutdown`). `StartTray`: si `TrayAdminHelper`, `TrayHelper.StartServer()` desde el arranque (la tarea abre el ayudante al iniciar sesión y se conecta). `TrayHost.Send`: ya no llama `EnsureStarted` (eliminado), solo `Forward`. Toggle de Bandeja ahora llama `TrayHelper.Enable()/Disable()`. `Installer.EnsureStartMenuShortcut` crea el acceso del menú Inicio apuntando al exe en ejecución; `App.UninstallNow` (espejo de `--uninstall`, saca la tarea con `Disable` si estaba). Nueva sección `InstallBox()` en `SettingsWindow` (oculta en Store).

## 1.9.28 (24/11/2026)
`Core/TrayHelper`: McBar (normal, integridad media) crea una tubería con nombre (`NamedPipeServerStream`, Out) y lanza una copia de sí mismo elevada con `Verb="runas"` y args `--trayhelper <pid>`. Esa copia (integridad alta) corre `RunServer`: conecta como cliente (In), lee líneas "hwnd msg wParam lParam" (hex) y hace `PostMessage` (puede, por ser admin). `App.OnStartup`: rama `--trayhelper` (ShutdownMode explícito, sin ventanas, vigila el PID padre y sale si muere). `TrayHost.Send`: si `PostMessage` falla con error 5 y `TrayAdminHelper` está prendido, `TrayHelper.EnsureStarted` + `Forward`. Ajuste `TrayAdminHelper` (apagado) + toggle en Preferencias → Bandeja. `SafeCleanup` llama `TrayHelper.Stop` (mata la copia). La tubería la crea el proceso medio para que el alto pueda abrirla (alto accede a medio).

## 1.9.27 (24/11/2026)
`WindowOps.TypeUnicode` (SendInput con `KEYEVENTF_UNICODE`, maneja pares suplentes de los emojis). `Panels/EmojiViewer`: ventana no-activa (no roba foco), categorías con botones de salto, al tocar un emoji `TypeUnicode` lo escribe en la app enfocada; atajo Win+Mayús+E (vk 0x45, gate `EmojiShortcut`), menú y toggle en Preferencias. `Panels/ScreenSaverWindow`: ventana a pantalla completa (escritorio virtual), reloj que deriva cada ~20 s, se cierra con tecla/clic/movimiento; acción "screensaver" en `HotCorners` y ítem en el menú. (Choques de nombres evitados: `System.Windows.Controls.Dock.Top`, etc.)

## 1.9.26 (24/11/2026)
`Core/Speaker`: `Toggle` (si habla, corta; si no, lee la selección). `SpeakSelectionAsync` espera 160 ms (soltar las teclas), copia con `WindowOps.SendChord(VK_CONTROL, 'C')`, lee el portapapeles, lo restaura 400 ms después. `Speak` usa `Windows.Media.SpeechSynthesis.SpeechSynthesizer` → stream → `MediaPlayer` (WinRT, sin librerías nuevas; sin importar `System.Windows.Media` para no chocar con el `MediaPlayer` de WPF). Atajo Win+Mayús+L (vk 0x4C, gate `SpeakSelection`), ítem en el menú del sistema y toggle en Preferencias → Teclado.

## 1.9.25 (24/11/2026)
`Panels/MarkupWindow`: lienzo a resolución real (Canvas con ImageBrush) dentro de un `Viewbox` (coordenadas del mouse = píxeles reales, guardado sin perder calidad). Herramientas pen/marker (Polyline, marker semitransparente ×5), rect (Rectangle), arrow (Line + punta en un sub-Canvas), text (TextBox). Colores, Deshacer (lista `_added`), `Flatten` (RenderTargetBitmap) para Copiar/Guardar (sobrescribe el .png). `CaptureThumb`: tocar la miniatura abre `MarkupWindow.Open`; ítem "Marcar" en su menú.

## 1.9.24 (24/11/2026)
`Core/LiveText`: reusa `CaptureOverlay` (nuevo parámetro `onRegion`) para elegir zona; `ToSoftwareBitmap` (CopyPixels BGRA → `SoftwareBitmap.CreateCopyFromBuffer`), `OcrEngine.TryCreateFromUserProfileLanguages().RecognizeAsync`, junta las líneas, copia al portapapeles y avisa con `NotificationCenter`. Atajo Win+Mayús+T en `KeyboardHook` (gate `LiveText`, vk 0x54), ítem en el menú del sistema y toggle en Preferencias → Teclado. `CaptureOverlay` entrega la zona a `onRegion` tanto en arrastre como en modo ventana.

## 1.9.23 (24/11/2026)
`Core/NightShift`: `Start` (timer de 1 min + `SettingsStore.Changed`), `ShouldWarm(enabled, allDay, now)` pura (noche = hora ≥20 o <7), `BuildRamp(intensidad)` pura (256×3; rojo full, verde ×(1−0,28·i), azul ×(1−0,58·i)), `Apply`/`Reset`/`Stop` con `SetDeviceGammaRamp` sobre `GetDC(0)`. `Win32.SetDeviceGammaRamp` nuevo. Ajustes `NightShift`/`NightShiftAllDay`/`NightShiftStrength`. `App.OnStartup` llama `Start`; `SafeCleanup` llama `Stop` (devuelve la rampa). UI en General (3 controles). Tests en Stage26.

## 1.9.22 (24/11/2026)
`SystemInfo.OpensAtLogin`/`SetOpenAtLogin(appName, command, on)`: entrada propia en HKCU\…\Run con nombre `McBarLogin_<alfanum>`; el comando es la ruta del .exe entre comillas, o `explorer.exe "shell:AppsFolder\…"` para apps de la Store. En `ShowContextMenu` (App), dentro de "Opciones" y solo para apps fijadas con Target, ítem chequeable "Abrir al iniciar sesión".

## 1.9.21 (24/11/2026)
`SettingsWindow.Build`: se reordenaron los bloques de secciones (cada uno delimitado por su `Header(...)`) sin tocar su contenido ni los textos. Orden nuevo: General, Dock, Barra superior, Isla dinámica, Launchpad, Notificaciones, Teclado, Esquinas activas, Bandeja del sistema, Widgets, Estilo Mac, Personalización, Diagnóstico. `Tabbed()` sigue armando una pestaña por Header. El bloque de widgets de escritorio/calendario (sin Header) sigue dentro de Widgets.

## 1.9.20 (24/11/2026)
`PostMessage` con `SetLastError`; `TrayHost.Send` registra cuando el envío falla (`Marshal.GetLastWin32Error`), con nota especial para error 5 (acceso denegado = ventana de mayor integridad; McBar normal no puede mandarle mensajes a un proceso elevado — límite UIPI). Confirma si el problema de los íconos que no abren es la elevación. Fix previsto: opción de reiniciar McBar como administrador.

## 1.9.19 (24/11/2026)
El log de la 1.9.15/1.9.18 confirmó que `TrayHost.Send` reenvía bien (LeftUp/RightUp con el protocolo correcto por versión); el problema era el bloqueo de foco. `RelaxForegroundLock` (una vez, al primer clic): guarda `SPI_GETFOREGROUNDLOCKTIMEOUT` y lo pone en 0 con `SPI_SETFOREGROUNDLOCKTIMEOUT`; `RestoreForegroundLock` en `Dispose`. Se saca el `AttachThreadInput` de la 1.9.17 (se desenganchaba antes de que el programa procesara el mensaje asíncrono). Sigue el log. Pendiente aparte: íconos que no re-anuncian con TaskbarCreated (NVIDIA) → habría que leer los de la bandeja vieja.

## 1.9.18 (24/11/2026)
`Native/ShellLink`: creación de .lnk con IShellLink + IPersistFile (reemplaza WScript.Shell, que fallaba callado en single-file). `Installer.CreateShortcut` lo usa y devuelve bool; `Install` avisa en el log si el del menú Inicio no se creó. Menú del sistema (`MenuBarContent.BuildSystemMenu`): "Abrir registro (diagnóstico)" abre `mcbar.log` (o la carpeta de McBar).

## 1.9.17 (24/11/2026)
`TrayHost.Send`: antes de reenviar, `AttachThreadInput(hilo McBar, hilo del dueño del ícono, true)` + `AllowSetForegroundWindow(ASFW_ANY)` para saltear el bloqueo de foco (la barra no toma foco, así que el programa no podía hacer `SetForegroundWindow` para su menú); se desengancha al final. Sigue el log de la 1.9.15.

## 1.9.16 (24/11/2026)
`StageManager.Close(Stage)`: `WindowOps.Close` (WM_CLOSE) a cada ventana de la pila, la saca de `_recent` y refresca la franja. `StageStrip`: crucita (Border redondo con "×") por miniatura, oculta y sin recibir clics; aparece al `MouseEnter` (opacity + IsHitTestVisible) y se va al `MouseLeave`; su clic llama a `Close`, el del resto de la miniatura sigue siendo `Bring`.

## 1.9.15 (24/11/2026)
Revertida la secuencia de clics de la 1.9.14 en `TrayHost.Send` (la interfaz ya manda abajo/arriba por separado, así que acá va un mensaje por llamada). Se agrega `Log.Write` en `Send` con click, hwnd, uid, versión, mensaje de callback, IsWindowsSystem y los mensajes reenviados, para diagnosticar los íconos que no responden. Pendiente de confirmar con el log si el problema es el bloqueo de foco al mostrar el menú.

## 1.9.14 (24/11/2026)
Systray (`TrayHost.Send`): antes de reenviar, `SetForegroundWindow(icon.HWnd)` (varios programas no muestran el menú si su ventana no está al frente) y se manda el par abajo+arriba (antes solo "arriba", que algunos ignoran). Resorte al arrastrar: `StageStrip` con `AllowDrop` y un `DispatcherTimer` de 500 ms por miniatura → `StageManager.Bring` (no acepta el drop, `Effects=None`); `DockWindow.Window_DragOver` detecta el ícono de app bajo el puntero (`HitItem`) y a los 600 ms activa su ventana (`_springTimer`, `_springHwnd`), restaurándola si estaba minimizada. Ninguno de los dos toma el archivo: solo trae la ventana al frente para que el usuario lo suelte adentro.
