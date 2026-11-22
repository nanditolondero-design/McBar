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

## Limitaciones conocidas (posibles próximas etapas)
- **Bandeja**: alguna app vieja que no vuelva a mandar su ícono cuando McBar arranca aparece recién al reiniciarla. Los globitos de notificación siguen siendo los de Windows.
- **Escalas distintas por monitor**: McBar usa la escala del sistema; en un monitor con otra escala se puede ver un poco borroso.
- **Desenfoque en menús contextuales**: no (Windows no lo permite en ventanas emergentes de WPF sin riesgo); son semitransparentes.
- **Menús reales de cada app**: Windows no los expone de forma general; se usan atajos estándar. A futuro: menús específicos para apps conocidas (Office, navegadores, Explorador).
- **Animación "genio" al minimizar**: no se puede aplicar a ventanas de otras apps sin efectos de bajo nivel; se descartó para mantener bajo consumo.
- **Notificaciones**: siguen siendo las de Windows.
- Spotlight (búsqueda con atajo de teclado), "Mission Control" propio, pilas de otras carpetas, widgets de música (reproducción actual).
