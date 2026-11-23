# Etapas de McBar

## Etapa 1 — versión 0.1 (17/11/2026) ✅ escrita, pendiente de primera compilación y prueba
- Dock con ampliación, rebote, indicadores, etiquetas, reordenar/quitar arrastrando, soltar archivos, Descargas, Papelera, menús contextuales, ocultación automática.
- Barra de menú con menú del sistema, menús de la app activa (por atajos), estado (bandeja, batería, red, volumen), búsqueda, Centro de control y reloj.
- Centro de control, Widgets (calendario, clima, batería, sistema, notas), Launchpad con búsqueda y páginas.
- Preferencias con aplicación en vivo, tema claro/oscuro/automático, iniciar con Windows.
- Ocultar/restaurar barra de Windows de forma segura (`--restaurar`).
- Pruebas xUnit de la lógica, workflow de GitHub, documentación.

## Etapa 2 — versión 0.2 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Pedidos de Andrés después de probar la 0.1:
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
- Isla dinámica como opción en Preferencias (elegido por Andrés), con todo lo de la barra adentro.
- Eventos elegidos: música y videos, volumen y brillo, batería y carga, notificaciones.
- Notificaciones: primero intenta el lector oficial de Windows; si Windows no lo permite (pasa con apps no instaladas como paquete), lee el cartelito de la notificación con la accesibilidad de Windows. En Preferencias se ve cuál está usando.

## Versión 0.3.1 (22/11/2026) — arreglos de rendimiento y errores
Reportado por Andrés: app lenta, sin animación al soltar para quitar, barra superior que se oculta, Launchpad con páginas vacías, barra de Windows que aparece sola. Ver CHANGELOG.

## Versión 0.3.2 (22/11/2026)
Isla: cajón del Centro de control, apariencia configurable, reloj grande; sin íconos de bandeja de Windows; revisión de varios monitores; esquinas del desenfoque del Dock; barra de Windows al vaciar la papelera. Ver CHANGELOG.

## Versión 0.3.3 (22/11/2026)
Ventanas rápidas (vidrio de DWM + región) para el Dock y la isla; arreglo de las ventanas "locas" con la ocultación automática. Ver CHANGELOG.

## Etapa 4 — versión 0.4.0 (22/11/2026) ✅ escrita, pendiente de compilación y prueba
Pedido de Andrés: fondo del Launchpad más transparente, carpetas o grupos de íconos, y un centro de notificaciones propio (adaptado a la píldora y a la barra) en lugar del panel lateral de Windows.
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
Pedido de Andrés: "un instalador copado, lindo" e integrarse mejor a Windows tapando todo lo posible. Plan acordado:
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
1. **0.9.0 — Estabilidad** (hecho): informe y recuperación ante errores, Windows lo reabre, consumo en el registro y en Preferencias. La actualización automática se descartó: Andrés no la quiere.
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
Andrés reportó que McBar se traba (el Dock deja de aparecer y la píldora queda quieta) y que no apareció el instalador. El registro no tenía errores (una trabada no deja error), así que se agregó `Core/HangWatch`: anota qué tarea de la interfaz no termina y reinicia McBar a los 20 s. El instalador no salía porque esa ruta estaba guardada como "probar sin instalar"; ahora solo se saltea para la misma versión (`PortableVersion`). Pendiente: con el registro de la próxima trabada, arreglar la causa.
También: la isla reservaba una franja de lado a lado arriba (Andrés: "la píldora ocupa todo el lugar, las ventanas no se estiran del todo"). `IslandReserveSpace` pasa a false por defecto y `Migrate` (CurrentLook 2) lo apaga una vez a los que ya lo tenían.

Vista previa de minimizadas (Andrés: "no cargan siempre o tardan mucho"): `DwmQueryThumbnailSourceSize` de una ventana minimizada da el tamaño del botón minimizado, así que se usa `Win32.NormalSize` (GetWindowPlacement); ícono de la app detrás de la miniatura; reintentos de `Sync`; 400 ms y cambio inmediato entre íconos (`App.DockPreviewOpen`).
Píldora sobre menús (Andrés: "la píldora tapa el menú", "los botones de minutos quedan cortados"): `PopupGuard` sube cada menú/popup a lo más alto al abrirse; `KeepBarsOnTop` no actúa con un menú abierto; `App.KeepBelowTopBar(Window)` para Preferencias; `TimerView` en dos renglones.

## 1.3.2 (22/11/2026) — íconos con forma de Mac
Pedido de Andrés: "¿no se puede hacer que solo le dé forma de ícono de Mac?". `Core/IconShaper` (en el hilo del Shell, con RenderTargetBitmap): superelipse n=5 al 90 % del cuadro; si el ícono llena su cuadro (`IsFullBleed`: cubre ≥85 %, bordes y esquinas opacos) se recorta, si no va al 66 % sobre una placa con degradé claro del color dominante (`Dominant`, `Mix`). `IconLoader.Request(..., appShape: true)` desde el Dock (apps), Launchpad y Alt+Tab/vista previa; clave de caché con "|mac". Ajuste `IconShape` ("none"/"mac"). Pruebas en `Stage15Tests`.

## 1.4.0 (23/11/2026) — íconos de macOSicons para las apps instaladas
Pedido de Andrés: "que no vengan los íconos sino que la app lea las apps instaladas y baje los íconos sola para cada app". API de macOSicons (POST https://api.macosicons.com/api/v1/search con `x-api-key`, respuesta `hits[]` con `appName`, `lowResPngUrl`, `credit`, `creditUrl`, `downloads`). Plan gratis: 50 búsquedas por mes; los términos piden conservar el crédito del autor y permiten uso personal. `Core/MacIcons` (índice `indice.json`, `creditos.txt`), `Native/Dpapi` (clave cifrada), `App.StartMacIcons` (automático: a los 2 min y cada 6 h) y `MacIconsBox` en Preferencias → Dock. Pruebas en `Stage16Tests`.
Ideas para después: elegir otro ícono para una app (clic derecho en el Launchpad), leer .svg/.icns/.zip en los paquetes.

## 1.4.1 (23/11/2026) — arreglos pedidos por Andrés (sus mensajes se perdieron y los resumió)
- "El estilo de la barra, píldora y Dock no cambiaba": `SettingsStore.NotifyChanged` avisa a cada suscriptor con su propio try/catch (si uno fallaba, los siguientes no se enteraban); el tema Vidrio ya no fuerza el estilo de la isla.
- "La animación de borrado se queda corta": `Dock/DragGhost` (ventana transparente que no recibe clics) sigue al mouse por toda la pantalla; `DockWindow.OutsideDock` (puro) también cuenta los costados.
- "Arrastrando del Launchpad al Dock oculto queda a mitad de camino": `DragEnter`/`DragOver` muestran el Dock (`_hidden = false`) y `DragLeave`/`Drop` lo vuelven a esconder.
- "No puedo poner íconos en el escritorio desde el Launchpad": se arrastra un .lnk real (`ShellCom.CreateLinkTo` con `SHParseDisplayName` + `IShellLinkW.SetIDList`) en `%TEMP%\McBar\arrastre`.
- Pregunta: barras de título estilo macOS en todas las ventanas → ver respuesta en el chat (no se hizo: requiere meterse en otros programas).

## 1.5.0 (23/11/2026) — rendimiento
Andrés tiene 2 monitores con escalas distintas, así que la escala por monitor fue lo primero. Manifest `PerMonitorV2`; `MonitorInfo.Scale` (GetDpiForMonitor); Dock, barra e isla usan `_monitor.Scale` y se reacomodan en `DpiChanged`; los paneles se ubican con `WindowOps.PlaceOnMonitor` (píxeles reales). Además: sin `TrimWorkingSet`; Launchpad sin `DropShadowEffect` (sombra = copia del texto) y con `BitmapCache` durante las animaciones; caché de íconos de 400 con salida del más viejo; forma fija de la isla durante la animación; `Core/Perf` en Diagnóstico. Pruebas en `Stage18Tests`.
Sigue: 1.6.0 gráficos (animaciones tipo resorte, vidrio nativo de Windows 11, sombras y bordes de panel, tipografía) — primero se le muestra a Andrés cómo quedaría.

## 1.6.0 (23/11/2026) — gráficos
Andrés aprobó la vista previa ("Me gusta la vista previa de la 1.6.0, hacela así"). `Panels/PanelLook` (curva `Spring`, `Highlight`, `SpringIn`) y `PanelShadow` (ventana aparte, que no recibe clics, recortada para que la sombra solo se vea afuera del panel, debajo de él en el orden de ventanas). Mc.GlassBg más transparente, radio 18 en paneles y 14 en tarjetas, menús con sombra en capa aparte y brillo, buscador con sombra separada, Launchpad con resorte.

## 1.6.1 (23/11/2026) — pedidos de Andrés
Ícono propio (`AppSettings.CustomIcons`, `DockWindow.ChooseIcon/ResetIcon/IconMenu`, `IconLoader.FromImageFile`), fijar arrastrando (`ReorderWhileDragging` deja mover apps abiertas sin fijar entre las fijas y `PinHere`), `LaunchpadLayout.LeavesGrid` (zonas más grandes), `KeepBarsOnTop` no sube el Dock con el Launchpad abierto, `MenuBar/PeekHandle` (manijita en pantalla completa, `IslandWindow.PeekIn/PeekOut`), globito → `App.OpenWidgetsPanel(monitor, centered: true)`, `GlassTransparency` (`ThemeManager.ScaleAlpha` sobre los fondos del tema Vidrio), sin `BackdropBlur` (Sanitize lo apaga). Pruebas en `Stage19Tests`.
Interpretación: "en el Launchpad el Dock queda siempre a la vista" se tomó como problema (el Dock aparecía encima del Launchpad); si quería lo contrario (Dock visible como en macOS), se cambia en `KeepBarsOnTop`.
