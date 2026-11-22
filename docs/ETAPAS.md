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
