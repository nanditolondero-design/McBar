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

## Íconos (`Core/IconLoader.cs`)
- Hilo de fondo STA con su propio Dispatcher (bucle de mensajes), que necesitan los objetos COM del Shell. `IShellItemImageFactory` sirve para .exe, .lnk, carpetas, archivos y `shell:AppsFolder\<id>`.
- Caché chico para el Dock (tope 120); el Launchpad usa su propio caché que se descarta al cerrar.

## Barra de Windows (`Native/Taskbar.cs`)
- Guarda el estado original, pone "ocultar automáticamente" (para liberar el espacio) y esconde las ventanas de la barra.
- Si Windows la vuelve a mostrar (Inicio, reinicio del Explorador), se vuelve a esconder.
- Botón de bandeja: la muestra un momento con Win+B (experimental).

## Datos guardados
- `settings.json` (ajustes y apps fijadas), `notas.txt` (widget de notas), `mcbar.log` (errores, se recorta a 512 KB).
