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

### Consumo
- [ ] En reposo (sin tocar nada) McBar usa ~0 % de procesador.
- [ ] La memoria vuelve a bajar unos segundos después de cerrar el Launchpad o los paneles.
