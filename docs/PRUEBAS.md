# Pruebas de McBar

## Automáticas (se corren solas en GitHub antes de compilar)
`tests/McBar.Tests`:
- `MagnifierTests` — ampliación (simetría, separadores, ancla bajo el mouse, centrado), acercamiento suave y rebote.
- `IdentityAndCatalogTests` — claves de apps (.exe, Store, carpetas conocidas, accesos directos) y filtro del Launchpad.
- `SettingsAndWeatherTests` — guardar/leer/corregir ajustes, códigos de clima, lectura de la respuesta de Open-Meteo.
- `Stage3Tests` — avisos de batería de la isla, modo de barra y posición estimada de la música.
- `Stage2Tests` — área de trabajo, detección de pantalla completa, lectura de mensajes de bandeja, tipo de ícono del clima, lado derecho del Dock.

Para correrlas a mano en Windows: `dotnet test tests/McBar.Tests/McBar.Tests.csproj`

## Manuales (para QA)
Marcar en Windows 10 y en Windows 11, con escala 100 % y 125/150 %.

### Arranque y salida
- [ ] Al abrir McBar.exe desaparece la barra de Windows y aparecen Dock y barra de menú.
- [ ] Una ventana maximizada no queda tapada ni por la barra de arriba ni por el Dock.
- [ ] Menú de McBar → Salir: vuelve la barra de Windows como estaba (incluido si antes estaba en "ocultar automáticamente").
- [ ] Cerrar McBar desde el Administrador de tareas y ejecutar `McBar.exe --restaurar`: vuelve la barra.
- [ ] Reiniciar el Explorador (Administrador de tareas → Explorador de Windows → Reiniciar): McBar sigue funcionando y la barra de Windows se vuelve a ocultar.
- [ ] Abrir McBar dos veces: no se abre una segunda copia.

### Dock
- [ ] Pasar el mouse: los íconos se agrandan suavemente; el ícono bajo el cursor no "se escapa".
- [ ] Aparece el nombre arriba del ícono.
- [ ] Abrir una app fijada: rebota y aparece el puntito al abrirse.
- [ ] Una app no fijada aparece a la derecha (antes del separador) al abrirse y desaparece al cerrarla.
- [ ] Clic en una app abierta la trae al frente (y la restaura si estaba minimizada).
- [ ] Clic derecho → lista de ventanas, Mantener en el Dock, Mostrar en el Explorador, Nueva ventana, Ocultar, Salir.
- [ ] Arrastrar un ícono fijado de lugar: se reordena y queda así al reiniciar McBar.
- [ ] Arrastrar un ícono hacia arriba y soltar: se quita (si la app estaba abierta queda como no fijada).
- [ ] Soltar un .exe / acceso directo / carpeta sobre el Dock: queda fijado.
- [ ] Soltar archivos sobre la Papelera: van a la papelera. Clic derecho → Vaciar (pide confirmación).
- [ ] Descargas: muestra los últimos archivos; clic abre el archivo.
- [ ] Ocultación automática: el Dock baja al sacar el mouse y sube al tocar el borde inferior.
- [ ] Con un video en pantalla completa, el Dock y la barra se esconden y vuelven al salir.
- [ ] Hacer clic en el escritorio NO esconde el Dock.

### Barra de menú
- [ ] El nombre en negrita cambia con la app activa.
- [ ] Edición → Copiar / Pegar funciona en el Bloc de notas y en el navegador.
- [ ] Ventana → Minimizar / Zoom / Mover a la izquierda funcionan sobre la app activa.
- [ ] Reloj con día y hora correctos; cambia de minuto a tiempo.
- [ ] Batería (en notebooks): porcentaje, rayito al cargar, rojo con 20 % o menos.
- [ ] Volumen: el deslizador cambia el volumen de Windows; ícono de silencio en 0.
- [ ] Botón ⌃: muestra la barra de Windows para usar la bandeja y se vuelve a esconder al terminar.

### Paneles
- [ ] Centro de control: modo oscuro cambia Windows y McBar (si el tema es "Automática"); brillo en notebooks; sonido.
- [ ] Widgets: calendario (con meses anteriores/siguientes), clima de la ciudad elegida, sistema actualizándose, notas que se guardan.
- [ ] Launchpad: aparecen las apps (de escritorio y de la Store), búsqueda, páginas con rueda y puntitos, Esc cierra, clic derecho → Agregar al Dock.
- [ ] Los paneles se cierran al hacer clic afuera, y tocar de nuevo el botón los cierra.
- [ ] Preferencias: cada cambio se aplica al instante.

### Etapa 2
- [ ] Ya no queda la franja negra abajo: una ventana maximizada llega hasta el Dock (o hasta abajo de todo con ocultación automática).
- [ ] Con dos monitores: barra y Dock en los dos; maximizar en cada uno respeta su barra y su Dock.
- [ ] Minimizar una app (con el botón o con clic en el Dock): la ventana vuela hacia su ícono y el Dock/barra siguen funcionando.
- [ ] Restaurar desde el Dock: la ventana sale desde el ícono.
- [ ] Video en pantalla completa en un monitor: se esconden barra y Dock solo en ese monitor.
- [ ] Clic derecho en un ícono: el ícono NO se achica; clic en otra app: el menú se cierra siempre.
- [ ] Pasar el mouse por el Dock: el fondo queda quieto (con la opción "El fondo se estira" apagada) y la animación es fluida.
- [ ] Estilos Clásico / Flotante / Ancho completo (clic derecho en el fondo del Dock → Estilo).
- [ ] Arrastrar un ícono hacia arriba: aparece "Quitar del Dock"; al soltar, nubecita y desaparece.
- [ ] Sacar Papelera y Descargas; volver a ponerlas con clic derecho en el fondo → Agregar al Dock.
- [ ] Soltar una carpeta en el Dock: aparece a la derecha; clic muestra su contenido.
- [ ] Clima en la barra: ícono acorde al clima (y luna de noche); en Widgets el ícono se anima.
- [ ] Launchpad: aparecen las apps (primero dice "Cargando apps…"), fondo desenfocado, nombres con sombra, páginas fluidas.
- [ ] Centro de control y Widgets con desenfoque detrás.
- [ ] Bandeja: aparecen los íconos de WhatsApp, antivirus, Steam, etc.; clic izquierdo abre la app; clic derecho muestra su menú; doble clic funciona.
- [ ] Preferencias → Bandeja: apagar un ícono lo manda al menú ⌃; "blanco y negro" cambia los íconos.
- [ ] Salir de McBar: la barra de Windows vuelve con todos sus íconos de bandeja.

### Etapa 3 — Isla dinámica (Preferencias → Barra superior → Tipo: Isla dinámica)
- [ ] Desaparece la barra de menú y aparece la isla negra arriba al centro con la hora.
- [ ] Las ventanas maximizadas empiezan debajo de la isla (con "Dejar lugar arriba" activado).
- [ ] Pasar el mouse: se agranda con animación y muestra menús, bandeja, clima, batería, red, volumen, búsqueda, Centro de control y reloj; todo funciona igual que en la barra.
- [ ] Abrir un menú desde la isla: la isla no se cierra hasta cerrar el menú.
- [ ] Sacar el mouse: vuelve a la píldora chica.
- [ ] Subir/bajar el volumen con el teclado o el mezclador de Windows: la isla muestra la barrita de volumen.
- [ ] Notebook: cambiar el brillo muestra la barrita amarilla; enchufar/desenchufar muestra "Cargando"/"Usando la batería".
- [ ] Reproducir algo en Spotify o YouTube: aparece la tapa y el ecualizador en reposo; al cambiar de tema se muestra el tema nuevo.
- [ ] Tocar la isla mientras suena algo: reproductor con progreso; anterior/pausa/siguiente funcionan.
- [ ] Llega un mensaje de WhatsApp/mail: se muestra en la isla. (Anotar qué dice Preferencias en "Notificaciones:".)
- [ ] Volver a "Barra de menú": vuelve la barra normal.

### Versión 0.3.1
- [ ] La app responde rápido (Dock, barra y menús) aun con WhatsApp, antivirus, Steam, etc. abiertos.
- [ ] `%AppData%\McBar\mcbar.log` no tiene errores "Index was out of range".
- [ ] Soltar un ícono arrastrado hacia arriba: nubecita de humo.
- [ ] Con una ventana maximizada (Chrome, Explorador) la barra superior sigue visible.
- [ ] La barra de Windows no aparece sola (si asoma, desaparece en menos de un segundo).
- [ ] Launchpad: se ven las apps en la primera página y en las siguientes (rueda del mouse o puntitos).

### Versión 0.3.2
- [ ] Isla: no aparece el ícono de volumen de Windows (solo el de McBar) y nada se traba al tocar los íconos.
- [ ] Isla: el reloj se ve grande; Preferencias → Isla dinámica → Tamaño/Estilo/Día/Borde cambian la píldora al instante.
- [ ] Isla: tocar Centro de control, Wi-Fi, volumen o batería despliega el cajón con los atajos; sacar el mouse lo pliega.
- [ ] Estilo Vidrio: la píldora se ve translúcida con desenfoque y bordes redondeados prolijos.
- [ ] Dock: el desenfoque de atrás tiene las esquinas redondeadas (sin esquinas cuadradas).
- [ ] Dos monitores: video en pantalla completa en el monitor 2 mientras se trabaja en el 1 → el monitor 2 sigue sin barras.
- [ ] Dos monitores: subir el volumen → el aviso sale solo en la isla del monitor donde está la ventana activa.
- [ ] Dos monitores: "Cada Dock muestra solo las apps abiertas en su monitor" → mover una ventana de monitor la pasa de Dock en ~2 s.
- [ ] Desenchufar y volver a enchufar un monitor: barras y Docks se rehacen bien.
- [ ] Vaciar la papelera desde el Dock: la barra de Windows no aparece (o apenas un instante).

### Versión 0.3.3
- [ ] Pasar el mouse por el Dock y abrir/cerrar la isla: animaciones fluidas.
- [ ] El Dock y la isla NO se ven con fondo negro (si pasa: anotar Windows/placa de video y apagar "Animaciones rápidas").
- [ ] Se puede hacer clic en las ventanas que están al lado y arriba del Dock y alrededor de la isla.
- [ ] Activar "Ocultar y mostrar el Dock automáticamente": las ventanas maximizadas se acomodan una sola vez y quedan quietas.
- [ ] Revisar `mcbar.log`: si aparece "El Explorador sigue cambiando el área de trabajo" o "Windows no aceptó poner su barra en ocultar automáticamente", avisar.

### Versión 0.4.0
Dock (arreglo del video):
- [ ] Pasar el mouse de punta a punta por el Dock: los íconos agrandados se ven enteros (arriba y a los costados), igual que la Papelera y el Launchpad en las puntas.
- [ ] La etiqueta con el nombre se ve completa.
- [ ] Al sacar el mouse, vuelve a poder hacerse clic en las ventanas de arriba del Dock.
- [ ] Abrir el Bloc de notas (sin fijar), arrastrar su ícono hacia arriba: dice "Cerrar app"; al soltar, nubecita y se cierra. Con texto sin guardar: pregunta y el ícono vuelve.
- [ ] Una app fijada arrastrada afuera solo se quita del Dock (no se cierra).
Isla:
- [ ] Clic en Wi-Fi, en volumen y en batería: cada uno abre su propio cajón (no el Centro de control).
- [ ] Botón del Centro de control: el cajón se ve completo (sonido, brillo, música) y con las esquinas de abajo redondeadas.
Notificaciones:
- [ ] Mandarse un mensaje de WhatsApp: aparece en la isla y queda en el cajón de notificaciones (clic en la hora) agrupado por app.
- [ ] Con barra de menú: el reloj muestra el puntito azul y el panel tiene las notificaciones arriba de los widgets, sin hueco vacío abajo.
- [ ] Win+N abre las notificaciones de McBar y Win+A el Centro de control; al soltar Windows no se abre el menú Inicio.
- [ ] Con "Ocultar los avisos de Windows": el cartelito de Windows desaparece enseguida y la notificación igual queda en McBar.
- [ ] En Preferencias → Notificaciones se ve qué método usa (lector oficial o accesibilidad).
Launchpad:
- [ ] Preferencias → Launchpad → Transparencia en 100: se ve el escritorio casi limpio; en 0: bien oscuro. Desenfoque apagado: sin desenfoque.
- [ ] El buscador no queda tapado por la isla.
- [ ] Arrastrar Word sobre Excel: se crea "Carpeta" con las dos. Arrastrar PowerPoint sobre la carpeta: se agrega.
- [ ] Clic en la carpeta: se abre; cambiar el nombre y apretar Enter; cerrar y volver a abrir el Launchpad: sigue igual.
- [ ] Arrastrar una app fuera del recuadro de la carpeta: sale. Al vaciarla, la carpeta desaparece.
- [ ] Clic derecho en una app: Agregar a carpeta / Nueva carpeta / Sacar de la carpeta. En una carpeta: Renombrar / Deshacer carpeta.
- [ ] Buscar una app que está dentro de una carpeta: aparece.

### Versión 0.4.1
- [ ] Usar la PC un rato con ventanas maximizadas: no se sacuden ni "intentan acomodarse". En `mcbar.log` puede aparecer una vez "deja de corregirla hasta que cambie algo".
- [ ] Dock: pasar el mouse de punta a punta: el fondo crece suave con los íconos y nada se corta; esquinas redondeadas lisas, sin bordes dentados ni marcos.
- [ ] Dock: la lupa se siente fluida (comparar con la 0.4.0).
- [ ] Arrastrar la Papelera hacia arriba y soltarla enseguida: NO se quita. Mantenerla afuera medio segundo y soltar: se quita. Preferencias → Dock → "Volver a poner la Papelera y Descargas": vuelven.
- [ ] Tocar el clima (barra o isla): widget con ahora, 24 horas (rueda del mouse para moverlas) y 7 días.
- [ ] Isla con estilo Vidrio: solo la píldora translúcida, sin cuadrado oscuro alrededor.
- [ ] Launchpad: abrir/cerrar fluido; Transparencia en 0 = casi opaco, en 100 = casi transparente.
- [ ] Preferencias: pestañas a la izquierda; al cerrar y volver a abrir, queda en la última.

### Versión 0.4.2
- [ ] Launchpad: clic derecho en una app → "Ocultar del Launchpad": desaparece y abajo a la derecha aparece "Mostrar apps ocultas (1)". Tocándolo se ve atenuada; clic derecho → "Dejar de ocultar".
- [ ] Preferencias → Barra superior → Tipo: "Gota": la píldora queda pegada al borde de arriba con curvas suaves a los costados; se agranda con el mouse, los avisos y los cajones.
- [ ] Gota con estilo Vidrio y con Borde brillante: se ve bien, sin cuadrados.
- [ ] Centro de control: tocar el texto "Bluetooth" (no el círculo) abre la configuración; tocar "Modo oscuro" cambia el tema.

### Versión 0.4.3
Launchpad:
- [ ] Ctrl+clic en 3 apps: se marcan con ✓ y abajo aparece la barra; "Ocultar" las esconde a las 3. "Mostrar lo oculto" las muestra atenuadas.
- [ ] Buscar una app oculta: aparece atenuada.
- [ ] Clic derecho en una carpeta → "Ocultar carpeta".
- [ ] Arrastrar una carpeta al borde de una app: rayita blanca y se mueve ahí. Cerrar y abrir el Launchpad: el orden sigue.
- [ ] Arrastrar una app al centro de otra: carpeta nueva (como antes).
- [ ] Pasar el mouse: el ícono crece suave.
- [ ] Clic derecho → Desinstalar… en una app de la Tienda (abre su página en Configuración) y en una de escritorio (abre su desinstalador).
- [ ] Arrastrar una app hacia abajo: el Launchpad se esconde; soltarla en el Dock la fija; soltar otra en el escritorio crea un acceso directo.
Barra:
- [ ] Minimizar todo (o clic en el escritorio): desaparecen los menús de la app; al elegir una ventana vuelven.
- [ ] Video de YouTube en pantalla completa (Chrome maximizado): la píldora y el Dock se ocultan; al salir vuelven.
- [ ] Quien tenía "Gota": ahora es isla.
Dock:
- [ ] Estilo "Barra": barra ancha flotante, sin lupa, rayita debajo de las apps abiertas, resaltado al pasar el mouse.
- [ ] Estilo "Solo íconos": sin fondo.
- [ ] Con desenfoque: el fondo no se estira y no se ven esquinas cuadradas.
- [ ] Paquete de íconos: elegir una carpeta con chrome.png → el ícono de Chrome cambia en el Dock y el Launchpad; "Usar los de Windows" vuelve atrás.
Tema:
- [ ] Apariencia → Vidrio: paneles, menús, Dock, barra e isla transparentes con brillo en el borde.
- [ ] Preferencias → Launchpad: "Transparencia del fondo" se lee completo.

### Versión 0.4.4
- [ ] Isla: minimizar todo y pasar el mouse por la isla: un solo renglón con el logo de McBar a la derecha. Elegir una ventana: vuelven las dos filas con el nombre de la app y sus menús.

### Consumo
- [ ] En reposo (sin tocar nada) McBar usa ~0 % de procesador.
- [ ] La memoria vuelve a bajar unos segundos después de cerrar el Launchpad o los paneles.
