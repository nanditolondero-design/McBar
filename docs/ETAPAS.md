# Etapas de McBar

## Etapa 1 — versión 0.1 (17/11/2026) ✅ escrita, pendiente de primera compilación y prueba
- Dock con ampliación, rebote, indicadores, etiquetas, reordenar/quitar arrastrando, soltar archivos, Descargas, Papelera, menús contextuales, ocultación automática.
- Barra de menú con menú del sistema, menús de la app activa (por atajos), estado (bandeja, batería, red, volumen), búsqueda, Centro de control y reloj.
- Centro de control, Widgets (calendario, clima, batería, sistema, notas), Launchpad con búsqueda y páginas.
- Preferencias con aplicación en vivo, tema claro/oscuro/automático, iniciar con Windows.
- Ocultar/restaurar barra de Windows de forma segura (`--restaurar`).
- Pruebas xUnit de la lógica, workflow de GitHub, documentación.

## Limitaciones conocidas (posibles próximas etapas)
- **Íconos de la bandeja de Windows** (WhatsApp, antivirus, etc.): por ahora se ven con el botón ⌃ de la barra, que muestra un momento la barra de Windows. A futuro: mostrarlos dentro de la barra de McBar.
- **Menús reales de cada app**: Windows no los expone de forma general; se usan atajos estándar. A futuro: menús específicos para apps conocidas (Office, navegadores, Explorador).
- **Animación "genio" al minimizar**: no se puede aplicar a ventanas de otras apps sin efectos de bajo nivel; se descartó para mantener bajo consumo.
- **Varios monitores**: Dock y barra solo en el monitor principal.
- **Notificaciones**: siguen siendo las de Windows.
- Spotlight (búsqueda con atajo de teclado), "Mission Control" propio, pilas de otras carpetas, widgets de música (reproducción actual).
