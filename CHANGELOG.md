# Cambios

## 0.3.0 — 22/11/2026
- Isla dinámica estilo iPhone como alternativa a la barra de menú (Preferencias → Barra superior → Tipo).
  - Reposo con la hora; expandida con menús, bandeja y estado; actividades de volumen, brillo, batería, música y notificaciones; reproductor con controles.
- El contenido de la barra de menú pasó a un componente compartido (MenuBarContent) que usan la barra y la isla.
- Los avisos del Explorador (TaskbarCreated, cambios de área de trabajo) ahora los recibe una ventana oculta, así funcionan con barra o con isla.
- El proyecto apunta a Windows 10 (net8.0-windows10.0.19041.0) para usar los controles multimedia y las notificaciones de Windows.

## 0.2.0 — 22/11/2026
Arreglos:
- Se libera el espacio que Windows guardaba para su barra de tareas (franja negra abajo): McBar fija el área de trabajo de cada monitor.
- Las barras ya no dejan de funcionar al minimizar: se reemplazó el aviso de "pantalla completa" de Windows (daba falsos positivos) por una detección propia, por monitor.
- El menú del clic derecho ya no achica el ícono ni queda pegado.
- Launchpad: carga más confiable de la lista de apps (y plan B con el menú Inicio).

Nuevo:
- Varios monitores (barra y Dock en cada uno, configurable).
- Animaciones más fluidas (todo con transformaciones de la tarjeta de video).
- Desenfoque real detrás del Dock, el Centro de control y los Widgets; más transparencia en general.
- Fondo del Dock quieto o que se estira (opción).
- Estilos del Dock: Clásico, Flotante, Ancho completo.
- Animación de minimizar/restaurar hacia el ícono del Dock.
- Quitar arrastrando con etiqueta "Quitar del Dock" y nubecita.
- Papelera y Descargas se pueden sacar; se pueden agregar carpetas y archivos al lado derecho.
- Clima en la barra de menú con íconos dibujados que cambian con el clima y de día/noche (animados en el panel de Widgets).
- Bandeja del sistema propia: los íconos de las apps en la barra de McBar.

## 0.1.0 — 17/11/2026
Primera versión de McBar (creado por Andrés Londero):
- Dock estilo macOS con ampliación, rebote, indicadores, Descargas, Papelera, arrastrar y soltar, ocultación automática.
- Barra de menú superior con menús, estado del sistema y reloj.
- Centro de control, Widgets, Launchpad y Preferencias.
- Oculta y restaura la barra de tareas de Windows de forma segura.
- Diseñado para bajo consumo (por eventos, sin bucles en reposo).
