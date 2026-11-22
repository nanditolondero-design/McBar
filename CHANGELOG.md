# Cambios

## 0.4.0 — 22/11/2026
Pedidos de Andrés: fondo del Launchpad más transparente, carpetas, y dejar de usar el panel lateral de notificaciones de Windows. Más los errores vistos en su video del 22/11 (15:06).

**Nuevo**
- **Centro de notificaciones propio**: las notificaciones de Windows se guardan en McBar (hasta 60, agrupadas por app, con "hace 5 min"). Con la isla se abre como cajón desde la píldora (clic en la hora, o en la píldora si hay sin leer: aparece un globito con la cantidad). Con la barra de menú, el reloj muestra un puntito azul y abre el panel de la derecha con las notificaciones arriba de los widgets.
- **Win+N y Win+A** abren el centro de notificaciones y el Centro de control de McBar en vez de los paneles de Windows (Preferencias → Notificaciones; se puede apagar).
- **Ocultar los avisos de Windows** (experimental): el cartelito de Windows se esconde apenas McBar lo leyó.
- **Launchpad**: transparencia del fondo regulable (0–100) y desenfoque que se puede apagar (Preferencias → Launchpad).
- **Carpetas en el Launchpad**: arrastrar una app sobre otra crea una carpeta; sobre una carpeta, la agrega. Clic abre la carpeta con el nombre editable; arrastrar una app fuera del recuadro la saca. Clic derecho: Agregar a carpeta, Nueva carpeta, Sacar de la carpeta, Renombrar, Deshacer carpeta. La búsqueda encuentra también las apps de adentro de las carpetas. Se guardan en settings.json.
- **Arrastrar para cerrar**: una app abierta que no está fijada en el Dock se puede arrastrar hacia arriba; aparece "Cerrar app" y al soltarla se cierra con la nubecita (se les pide cerrar a sus ventanas, como "Salir"). Si la app pregunta algo (por ejemplo, guardar cambios) y no se cierra, su ícono vuelve a aparecer.
- **Cada ícono de la isla abre lo suyo**: Wi-Fi, sonido y batería despliegan su propio cajón (antes los tres abrían el Centro de control).

**Arreglos (video del 22/11)**
- **Dock: íconos agrandados cortados** arriba y a los costados. La forma de la ventana se recalculaba cuadro a cuadro y Windows la aplicaba antes de dibujar el contenido nuevo. Ahora, mientras el mouse está encima, la zona es amplia y fija; vuelve a ser solo la barra cuando todo queda quieto. Además hay más lugar arriba para la etiqueta con el nombre.
- **Cajón de la isla cortado y con esquinas cuadradas**: la ventana de la isla medía 300 de alto y el Centro de control no entraba. Ahora mide lo necesario y el cajón se vuelve a medir cuando termina de armarse.
- **Panel de widgets con un hueco vacío abajo**: ahora mide lo que su contenido.
- **Launchpad**: el buscador quedaba debajo de la isla; ahora está más abajo.
- Avisos de compilación en MediaWatcher.cs (llamada sin await) resueltos.

## 0.3.3 — 22/11/2026
- **Animaciones lentas**: el Dock y la isla ahora son ventanas comunes, aceleradas por la tarjeta de video, con fondo transparente (vidrio de DWM) y forma recortada. Antes eran ventanas "en capas", que Windows copia enteras en cada cuadro de animación. Fuera de la forma (barra, íconos, píldora) los clics pasan a las ventanas de atrás. Si en alguna PC se ven con fondo negro: Preferencias → "Animaciones rápidas" apagado vuelve al modo anterior.
- **Ventanas "locas" con la ocultación automática**: McBar y el Explorador se peleaban por el área de trabajo y cada corrección reacomodaba todas las ventanas maximizadas. Ahora se espera 1,5 s antes de revisar, se revisa cada ~10 s y, si el Explorador la cambia 3 veces en 30 s, McBar deja de corregirla por 3 minutos (queda anotado en el registro).
- Se anota en el registro si Windows no acepta poner su barra en "ocultar automáticamente".

## 0.3.2 — 22/11/2026
Pedidos de Andrés después de probar la isla (video del 22/11):
- **Se trababa todo**: los íconos de bandeja que pone el propio Windows (volumen, red) ya no se muestran ni se tocan. Tocarlos abría paneles de Windows anclados a su barra escondida, que quedaban invisibles y se quedaban con el mouse.
- **Volumen repetido**: era el ícono de volumen de Windows en la bandeja; queda solo el de McBar.
- **Reloj de la píldora más grande** (y tamaño configurable).
- **Cajón**: en modo isla, el Centro de control (Wi-Fi, Bluetooth, modo oscuro, brillo, sonido) se despliega desde la píldora como un cajón. También lo abren los botones de Wi-Fi, volumen y batería de la isla.
- **Apariencia de la isla** (Preferencias → Isla dinámica): estilo Negro, Grafito, Vidrio (con desenfoque) o Color; tamaño Chica/Normal/Grande; día junto a la hora; borde brillante.
- **Esquinas del Dock**: el desenfoque detrás de la barra se veía con esquinas cuadradas. Ahora es una ventana común con recorte redondeado. Nueva opción para apagar el desenfoque.
- **Varios monitores** (revisión completa):
  - La pantalla completa se detecta en todos los monitores (antes solo en el de la ventana con foco: un video en el monitor 2 volvía a tener barras si trabajabas en el 1).
  - Los avisos de la isla salen solo en el monitor donde estás trabajando.
  - Los paneles usan los datos actuales del monitor.
  - Al enchufar/desenchufar un monitor se vuelve a revisar a los 2 segundos.
  - Nueva opción: cada Dock muestra solo las apps abiertas en su monitor.
- **Barra de Windows al vaciar la papelera**: después de vaciar la papelera, borrar archivos o reiniciarse el Explorador, McBar la vigila cada 100 ms unos segundos para que casi no se vea. También cuando aparece sola.
- El control de brillo (WMI) se inicia en segundo plano (en PCs de escritorio podía demorar el arranque).

## 0.3.1 — 22/11/2026
Arreglos después de probar la 0.2 en la PC de Andrés:
- **Lentitud**: el bucle de animación del Dock tenía un error que se repetía (registro: "Index was out of range" en DockWindow.Step). Ahora trabaja sobre una copia fija de la lista en cada cuadro y, si algo falla, se detiene en vez de repetir el error.
- **Lentitud**: la bandeja propia ahora corre en su propio hilo; antes cada mensaje de cada app (y su reenvío al Explorador) frenaba la interfaz.
- **Lentitud**: la detección de pantalla completa ya no escucha cada movimiento de la ventana activa (despertaba a McBar con cada movimiento del mouse); ahora se revisa al cambiar el foco y una vez por segundo.
- **Animación al quitar del Dock**: ahora se ve una nubecita de humo donde se suelta el ícono.
- **Barra superior que se escondía**: una ventana maximizada ya no cuenta como "pantalla completa". Además la barra, la isla y el Dock se vuelven a poner arriba de todo cada pocos segundos.
- **Barra de Windows que aparecía sola**: un vigilante liviano (cada 0,7 s) la vuelve a esconder y mantiene la bandeja propia primera.
- **Launchpad con páginas vacías**: se dibuja solo la página visible con tarjetas simples (sin efectos), con animación al pasar de página.

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
