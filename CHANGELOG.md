# Cambios

## 1.6.0 — 23/11/2026 (etapa: gráficos, como la vista previa aprobada)
- **Paneles con aspecto de macOS** (Centro de control, Widgets, Clima, Wi-Fi, Bluetooth, Sonido): vidrio más transparente (se ve más el fondo difuminado), esquinas más redondeadas (18), **sombra grande y suave** debajo y un **brillo fino arriba** en el borde.
- **Animación con resorte**: los paneles, el buscador y el Launchpad aparecen con un rebote chiquito, todos con la misma curva.
- **Buscador**: misma sombra y brillo; la sombra va en una capa aparte y ya no se recalcula con cada letra.
- **Menús** (barra, Dock, clic derecho): esquinas más redondeadas, sombra más grande y suave y brillo arriba.
- Tarjetas del Centro de control con esquinas de 14 y textos un poco más livianos.

## 1.5.0 — 23/11/2026 (etapa: rendimiento)
- **Nítido en monitores con escalas distintas** (por ejemplo notebook al 125 % y monitor al 100 %): McBar ahora usa la escala de CADA monitor (antes tomaba la del principal para todos y en el otro se veía borroso o corrido). Dock, barra, isla, Launchpad, buscador, paneles, Alt+Tab, Win+Tab, vista previa, Quick Look, cartel de volumen y el ícono que se arrastra se ubican en píxeles reales del monitor donde están. Si una ventana pasa a un monitor con otra escala, se vuelve a acomodar sola.
- **Abrir el Launchpad y los paneles sin tironcitos**: McBar ya no vacía la memoria a la fuerza al cerrarlos (Windows tenía que volver a cargarla del disco la próxima vez).
- **Launchpad más fluido**: los nombres de las apps tienen una sombra dibujada una sola vez (antes, 81 sombras recalculadas en cada cuadro), y mientras se abre, se cierra o se pasa de página el contenido se dibuja como una sola imagen.
- **Íconos en memoria**: el Launchpad reutiliza los íconos ya cargados (la segunda vez abre al instante) y la memoria de íconos guarda hasta 400 y saca el más viejo (antes se vaciaba entera al pasar de 120).
- **Isla más suave**: mientras se agranda o se achica, su forma queda fija en vez de recalcularse 60 veces por segundo.
- **Medición** en Preferencias → Diagnóstico: cuadros por segundo del Dock en la última animación (y % con tironcitos) y cuánto tarda en aparecer el Launchpad.

## 1.4.1 — 23/11/2026 (arreglos)
- **Quitar del Dock arrastrando a cualquier lado**: apenas el ícono sale del Dock (hacia arriba o a los costados de la barra), lo sigue al mouse por toda la pantalla con el cartel "Quitar del Dock" / "Cerrar app", y al soltarlo hace la nubecita ahí mismo. Antes, unos centímetros arriba del Dock el ícono desaparecía.
- **Arrastrar del Launchpad al Dock oculto**: el Dock ahora sube entero al pasar algo arrastrando por encima (antes quedaba a mitad de camino) y se vuelve a esconder al terminar.
- **Apps del Launchpad al escritorio**: al soltar una app en el escritorio (o en cualquier carpeta) se crea su acceso directo, también para las apps de la Tienda.
- **Estilos que no cambiaban**: si una parte de McBar fallaba al aplicar los ajustes, las demás (Dock, isla, barra) no se enteraban; ahora cada una se actualiza por separado y el error queda en `mcbar.log`. Además, el estilo de la isla (Negro, Grafito, Vidrio, Color) ahora manda siempre; antes el tema Vidrio lo pisaba. Al elegir el tema Vidrio, la isla pasa a Vidrio una vez.

## 1.4.0 — 23/11/2026 (íconos de macOSicons)
- **McBar baja solo los íconos estilo Mac de tus apps** desde macOSicons.com (Preferencias → Dock → "Íconos de macOSicons.com"). Lee las apps instaladas (primero las del Dock, después las del Launchpad), busca cada una y baja su ícono a `%AppData%\McBar\iconos-macos`, que pasa a usarse como paquete de íconos. McBar no trae ningún ícono adentro: cada uno los baja con su propia cuenta.
- Necesita la **clave gratis** de una cuenta de macOSicons.com (se guarda cifrada con tu usuario de Windows; no va en los perfiles ni en las exportaciones).
- Cuida el **cupo** del plan gratis (50 búsquedas por mes): cuenta las que usa y frena; el mes siguiente sigue con las que falten.
- Solo usa un ícono si el nombre coincide (para no ponerle a una app el ícono de otra). Las apps sin ícono en macOSicons siguen con el suyo y se vuelven a probar en 30 días. Desinstaladores, "léame" y ayudas no se buscan.
- "Bajar solos los íconos de las apps nuevas": 2 minutos después de abrir McBar y cada 6 horas busca las apps que todavía no tienen.
- **Créditos**: cada ícono es de su autor; McBar guarda quién lo hizo en `creditos.txt` (botón "Ver créditos"), como piden los términos de macOSicons.

## 1.3.2 — 22/11/2026
- **Íconos con forma de Mac** (Preferencias → Dock → Forma de los íconos → "Como en Mac"): todas las apps del Dock, el Launchpad y Alt+Tab toman la forma de los íconos de macOS (cuadrado de esquinas suaves). Si el ícono ya llena su cuadro (apps de la Tienda con fondo), se le recortan las esquinas; si es un ícono suelto (casi todos los de Windows), va al centro de una placa de un color muy claro sacado del mismo ícono. Funciona también con los paquetes de íconos. Viene en "Original".
- Paquetes de íconos: se corrigió el comentario que decía que Papirus trae PNG (trae SVG, que todavía no se lee).

## 1.3.1 — 22/11/2026 (arreglos)
- **Si McBar se traba** (Dock e isla quietos): a los 5 segundos anota en `mcbar.log` qué estaba haciendo («La interfaz no responde hace…»), y a los 20 segundos guarda un informe en `%AppData%\McBar\errores` y **se reinicia solo**. Se puede apagar en Preferencias → Diagnóstico.
- **Instalador**: si antes se eligió "Probar sin instalar" con un `McBar.exe` en una carpeta y después se baja una versión NUEVA al mismo lugar, ahora vuelve a aparecer el instalador (antes se salteaba siempre).
- **Isla**: ya no deja una franja vacía de lado a lado arriba. La píldora flota sobre las ventanas (como en el iPhone) y las ventanas maximizadas usan toda la pantalla. Se aplica una sola vez al actualizar; quien prefiera la franja la vuelve a prender en Preferencias → Isla dinámica → "Dejar lugar arriba para la isla". De paso, McBar ya no "pelea" con el Explorador por el área de trabajo en ese caso.
- **Vista previa del Dock**: las ventanas minimizadas ya no salen finitas o vacías (se usa su tamaño normal); mientras Windows entrega la miniatura (o si no la da, como con ventanas de otro escritorio) se ve el ícono de la app en vez de un cuadro vacío; se vuelve a pedir la imagen a los 0,12 y 0,45 s. Aparece más rápido (0,4 s en vez de 0,55 s) y, con una vista previa abierta, al pasar a otro ícono cambia al instante.
- **La píldora ya no tapa los menús**: los menús que se abren desde la isla o la barra quedan por encima, y mientras hay un menú abierto McBar no vuelve a subir la isla. **Preferencias** se abre debajo de la isla/barra (y se achica si la pantalla es chica), así la parte de arriba no queda tapada.
- **Panel del temporizador** (también el del modo juego): los atajos de minutos van en un renglón y "minutos a mano + Iniciar" en otro; antes no entraban y quedaban cortados.
- README nuevo para GitHub con ilustraciones (`docs/img`).
- Compilación: `App.RemoveDesktopWidget` pasa a interno (CS0051).

## 1.3.0 — 22/11/2026 (etapa 13: personalización)
- **Perfiles** (Preferencias → Personalización): un perfil guarda TODOS los ajustes (Dock y apps fijadas, barra o isla, tema, widgets, esquinas…). Atajos "+ Trabajo" y "+ Juegos (con modo juego)", o cualquier nombre con "Guardar los ajustes actuales como perfil". Se cambia desde Preferencias ("Usar"), el menú de McBar → Perfil o el buscador (escribir el nombre del perfil). Al pasar a otro perfil, lo que cambiaste en el actual queda guardado en él. Cada perfil puede prender el **modo juego**.
- **Modo juego**: la tecla Windows sola no abre el Launchpad ni el Inicio, las esquinas activas no hacen nada, las notificaciones no aparecen en la isla (quedan en el centro) y el Dock se oculta solo y deja toda la pantalla. Se prende desde el Centro de control, el menú de McBar, el buscador ("modo juego") o Preferencias. Mientras está prendido, la isla y la barra muestran "Juego"; tocándolo se apaga.
- **Exportar / importar ajustes** (Preferencias → Personalización → Copia de seguridad): un .json con todos los ajustes y los perfiles. Al importar no se pisan los datos propios de la PC (estado de la barra de Windows, arranque con Windows).
- **Fondo de pantalla según la hora**: una imagen para la mañana (6), la tarde (12), el atardecer (18) y la noche (21). Si un momento no tiene imagen, sigue la anterior. Se revisa una vez por minuto y solo cambia cuando hace falta.
- Arreglos de compilación (venían de la 1.1.0): comillas rectas dentro de un texto de Preferencias → Calendarios y la secuencia `"\;"` en el lector de iCal (C# no la acepta; ahora `"\\;"`).

## 1.2.0 — 22/11/2026 (etapa 12: isla más viva)
- **Temporizador y cronómetro** (como las actividades en vivo del iPhone): mientras corren, la isla muestra la cuenta (se ensancha un poco) y la barra de menú un chip al lado del reloj. Tocándolos se abre el panel "Temporizador": atajos de 1, 5, 10 y 25 min, minutos a mano ("10", "1:30", "1,5"), Pausar / Seguir / Cancelar; cronómetro con décimas, Iniciar / Pausar / Reiniciar. Al terminar: notificación "¡Terminó el temporizador!" y tres sonidos de aviso. El temporizador sigue aunque se cierre el panel.
- Desde el **buscador** (Win+S): "temporizador 10", "timer 5 min", "temporizador 1:30", "temporizador 1 hora" o "cronómetro" + Enter.
- **Micrófono y cámara en uso**: puntito naranja (micrófono) o verde (cámara) en la isla y en la barra, con el nombre de la app al pasar el mouse. Se lee del registro de privacidad de Windows (el mismo de su ícono), cada 2 segundos. En el panel "Temporizador" aparece la lista y un clic lleva a la privacidad de Windows.
- **Descargas en vivo**: mientras Chrome, Edge, Firefox u Opera bajan algo a la carpeta Descargas, la isla y la barra muestran cuánto va (y "+1" si hay más de una); el panel muestra nombre, tamaño y velocidad. Al terminar: notificación "Descarga completa".
- Preferencias → Isla dinámica: tres interruptores nuevos (actividades en vivo, progreso de descargas, micrófono/cámara). Vienen prendidos.
- Todo se refresca una vez por segundo solo mientras hay algo en vivo; si no, no gasta.

## 1.1.0 — 22/11/2026 (etapa 11: widgets, calendario y recordatorios)
- **Widgets en el escritorio** (como macOS Sonoma): Reloj, Calendario, Clima, Recordatorios, Notas, Batería y Sistema. Se agregan en Preferencias → Widgets ("+ Reloj", etc.), quedan detrás de las ventanas, se mueven arrastrándolos (la posición se guarda) y se quitan con clic derecho.
- **Calendario de Google / Outlook** sin iniciar sesión: se pega el enlace iCal (Google: "Dirección secreta en formato iCal"; Outlook: enlace ICS de "Publicar calendario"). McBar lo baja cada 15 minutos y muestra los eventos: puntitos en los días del mes, "Próximos eventos" en el widget y en el panel del reloj, y **aviso 10 minutos antes** (notificación e isla). Entiende eventos de todo el día, zonas horarias y repeticiones (diaria, semanal con días, mensual, anual).
- **Recordatorios** en lenguaje natural: "comprar pan mañana 18:00", "llamar a Juan en 20 min", "turno 25/11 9:30", "pagar la luz el miércoles", "sacar la basura a las 23". A la hora avisa con una notificación (y en la isla). Se marcan como hechos tocando el círculo; clic derecho → Borrar. Están en el panel del reloj y como widget de escritorio. Se guardan en `%AppData%\McBar\recordatorios.json`.

## 1.0.0 — 22/11/2026 (etapa 10: más cosas de Mac)
- **Esquinas activas** (Preferencias → Esquinas activas): a cada esquina de la pantalla se le puede asignar Launchpad, Todas las ventanas, Escritorio, Buscador, Notificaciones, Centro de control o Bloquear pantalla. Se dispara dejando el mouse un instante; no actúa con pantalla completa ni arrastrando algo. Vienen todas apagadas.
- **Ventanas en mosaico**, como "Mover y cambiar tamaño" de macOS Sequoia:
  - Menú **Ventana → Mover y cambiar tamaño**: mitades, cuartos, tercios, dos tercios, Llenar, Centrar y "Volver al tamaño anterior".
  - Atajos: **Ctrl+Alt+flechas** (mitades), Ctrl+Alt+U/I/J/K (cuartos), Ctrl+Alt+Enter (llenar), Ctrl+Alt+C (centrar), Ctrl+Alt+Retroceso (como estaba). Con AltGr no se activan (para no molestar al escribir @ y otros signos).
  - Deja un margen chico entre ventanas y compensa el borde invisible de Windows para que queden justas.
- **Escritorios en Win+Tab**: arriba aparece una franja con los escritorios virtuales de Windows (el actual resaltado) para pasar de uno a otro con un clic, y "+" para crear uno nuevo.
- **Quick Look**: con un archivo elegido en el Explorador, la **barra espaciadora** muestra una vista previa grande: fotos, videos y audio (se reproducen), PDF (primera página), texto y código, carpetas (cuántos elementos tienen) y el resto con su miniatura de Windows. Espacio o Esc cierran, Enter o "Abrir" lo abren.
- Preferencias → Teclado: interruptores para Quick Look y los atajos de mosaico.
- El menú Ventana: "Vista de tareas" pasa a ser "Todas las ventanas" (la de McBar).

## 0.9.0 — 22/11/2026 (etapa 9: estabilidad)
- **Si McBar se cierra por un error**: guarda un informe en `%AppData%\McBar\errores`, devuelve la barra de Windows y el espacio de las ventanas a la normalidad, y **Windows lo vuelve a abrir solo** (el mismo mecanismo que usa Office). Si se cae 3 veces en 10 minutos no se reabre más, para no quedar en un círculo.
- Al volver a abrirse, avisa en las notificaciones que hubo un error y dónde quedó el informe.
- Los errores de tareas en segundo plano ya no pueden tirar la app: se anotan en el registro.
- **Consumo**: cada 10 minutos se anota en `mcbar.log` cuánta memoria, procesador, identificadores y objetos gráficos usa McBar (para encontrar pérdidas de memoria). En vivo en Preferencias → Diagnóstico, junto con la versión y un botón para abrir los informes de errores.
- La actualización automática se descartó (pedido de Andrés).

## 0.8.0 — 22/11/2026 (etapa 8: ventanas estilo Mac)
- **Vista previa en el Dock**: dejando el mouse medio segundo sobre una app abierta aparecen miniaturas EN VIVO de sus ventanas (hasta 5) con su título; clic para ir a esa ventana, ✕ para cerrarla. Se puede apagar en Preferencias → Dock.
- **Alt+Tab estilo Cmd+Tab**: una fila con los íconos de las apps abiertas (la usada más recientemente primero, agrupando sus ventanas) y el nombre de la elegida. Tab / Mayús+Tab o flechas para moverse, soltar Alt para ir, Esc para cancelar.
- **Win+Tab: todas las ventanas** (como Mission Control): fondo desenfocado y las ventanas de la pantalla en una grilla con miniaturas en vivo, ícono y título. Clic o Enter para ir, flechas/Tab para elegir, Esc o clic en el fondo para salir.
- Preferencias → Teclado: interruptores para Alt+Tab y Win+Tab propios.
- Las miniaturas las dibuja Windows (DWM), igual que su propia barra: no consumen memoria de McBar. Las ventanas minimizadas no aparecen en Win+Tab (Windows no da su imagen).

## 0.7.0 — 22/11/2026 (etapa 7: Wi-Fi, Bluetooth y sonido propios)
Se abren tocando los íconos de la barra o de la isla, o las filas del Centro de control. Con la isla aparecen como cajón; con la barra, como panel. Ya no hace falta abrir Configuración de Windows.
- **Wi-Fi**: interruptor para prender/apagar, red conectada arriba, redes cercanas con barras de señal y candado; clic para conectarse (si hace falta, pide la contraseña ahí mismo) o para desconectarse. Si Windows no deja ver las redes (Windows 11 24H2 pide permiso de Ubicación), ofrece abrir ese permiso.
- **Bluetooth**: interruptor para prender/apagar y los dispositivos vinculados con su estado e ícono (auriculares, mouse, teclado, teléfono). Los de audio se conectan y desconectan con un clic. Los demás muestran su estado (se conectan solos al usarlos). "Agregar un dispositivo…" abre la vinculación de Windows.
- **Sonido**: volumen, **salida** (parlantes, auriculares, HDMI…) y **entrada** (micrófonos) con un clic; el elegido queda como predeterminado para todo. La isla se vuelve a enganchar al dispositivo nuevo.

## 0.6.0 — 22/11/2026 (etapa 6: Inicio, buscador y avisos)
- **Tecla Windows sola → Launchpad** en vez del menú Inicio. Los atajos con Windows (Win+E, Win+D, Win+L…) siguen funcionando: al apretar Windows McBar manda una tecla vacía, así Inicio no se abre al soltar.
- **Buscador tipo Spotlight** (Win+S o la lupa de la barra/isla): ventana de vidrio centrada con:
  - **Apps** (las del Launchpad).
  - **Calculadora**: "12*3+4", "15% de 300", "raiz(81)", "2^10", con coma o punto decimal; Enter copia el resultado.
  - **Configuración**: Wi-Fi, Bluetooth, Sonido, Pantalla, Fondo, Windows Update, etc., y las Preferencias de McBar.
  - **Archivos**: índice propio de Escritorio, Documentos, Descargas, Imágenes, Música, Videos y OneDrive (no usa el indexador de Windows). Ctrl+Enter muestra el archivo en su carpeta.
  - **Buscar en la web**.
  - Flechas para elegir, Enter para abrir, Esc para cerrar.
- **Teclas de volumen con cartel propio**: McBar cambia el volumen (de a 2, como Windows) y lo muestra en la isla, o en modo barra con un cartel de vidrio estilo macOS (ícono + 16 rayitas) abajo al centro. El cartel de Windows ya no aparece.
- **Preferencias → Teclado**: cada atajo (tecla Windows, Win+S, volumen) se puede apagar.
- Límite: el cartel de **brillo** de Windows no se puede reemplazar (esas teclas las maneja el equipo directamente); la isla igual muestra el brillo.

## 0.5.0 — 22/11/2026 (etapa 5: instalador y arranque)
- **Instalador propio estilo Mac**: McBar.exe es su propio instalador. Abierto desde fuera de la carpeta instalada muestra una ventana de vidrio con semáforo, el ícono flotando, opciones (iniciar con Windows, accesos directos en Inicio y escritorio, traer las apps fijadas de la barra de Windows, tema Vidrio), barra de progreso animada y pantalla de "¡Listo!".
  - Sin permisos de administrador: se instala en `%LocalAppData%\Programs\McBar`.
  - Aparece en **Aplicaciones instaladas** (nombre, versión, editor "Andrés Londero", ícono y tamaño).
  - **Actualizar**: abrir un McBar.exe más nuevo ofrece "Actualizar" y mantiene ajustes, apps fijadas y carpetas. Si la versión es igual o más vieja, abre la instalada.
  - **Desinstalar** desde Aplicaciones instaladas: pide confirmación, devuelve la barra de Windows tal como estaba, quita inicio automático, accesos directos y registro, y borra la carpeta. Los ajustes se conservan.
  - **Probar sin instalar** (o `--portable`): esa copia no vuelve a preguntar.
  - Las apps fijadas de la barra de Windows se copian a `%AppData%\McBar\Accesos` para que el Dock no las pierda si después se desfijan.
- **Arranque sin parpadeo**: al apagar o cerrar sesión, si McBar arranca con Windows, la barra de Windows queda oculta (ocultar automático), así al volver a entrar no aparece antes de que McBar abra.

## 0.4.4 — 22/11/2026
- **Isla sin app activa en un solo renglón** (pedido de Andrés): cuando no hay ventana elegida, la primera fila quedaba solo con el logo de McBar. Ahora el logo pasa a la derecha, después de la bandeja, el clima, la red, el volumen y el reloj, y todo queda en una sola fila (la isla se achica). Al elegir una app vuelven las dos filas con sus menús.

## 0.4.3 — 22/11/2026
Pedidos de Andrés:

**Launchpad**
- **Seleccionar varias**: Ctrl o Mayús + clic (o clic derecho → Seleccionar). Abajo aparece una barra con Ocultar, Mostrar, Nueva carpeta y Cancelar; el clic derecho sobre una seleccionada ofrece lo mismo y "Agregar a carpeta". Esc cancela.
- **Carpetas ocultables y movibles**: clic derecho → "Ocultar carpeta". Arrastrar una carpeta o una app al BORDE izquierdo/derecho de otra la mueve ahí (aparece una rayita blanca); al CENTRO de otra app arma o amplía una carpeta. El orden se guarda (`LaunchpadOrder`). Dentro de una carpeta abierta también se pueden reordenar.
- **El buscador muestra también lo oculto** (atenuado).
- **Efecto al pasar el mouse**: el ícono crece suave y aparece un brillo; los nombres tienen una sombrita para leerse sobre cualquier fondo. Carpetas con aspecto de vidrio.
- **Desinstalar…** (clic derecho), como en el menú Inicio: apps de la Tienda → su página en Configuración; apps de escritorio → su desinstalador registrado; si no se encuentra → Configuración → Aplicaciones instaladas.
- **Arrastrar al Dock o al escritorio, como los cajones de Android**: al llevar una app hacia abajo o a los bordes, el Launchpad se esconde; soltándola en el Dock queda fijada y en el escritorio Windows crea un acceso directo.

**Barra superior**
- **Menús solo con una app activa** (como GNOME): con el escritorio, todo minimizado o sin ventanas, desaparecen el nombre de la app y Archivo/Edición/Ver/Ventana/Ayuda; vuelven al elegir una app abierta.
- **Se quitó el modo Gota** (quien lo tenía pasa a la isla).
- **La píldora se oculta con videos y apps en pantalla completa**: la detección ahora reconoce los videos de Chrome/Edge (que siguen marcados como "maximizados") y no se confunde con ventanitas "siempre visibles" encima del video.

**Dock**
- **Estilo "Barra"**: ancha y flotante, parecida a la de Windows 11 (sin lupa, indicador en forma de rayita, resaltado al pasar el mouse).
- **Estilo "Solo íconos"**: sin fondo, solo los íconos con sus indicadores.
- **Resaltado al pasar el mouse** cuando no hay lupa.
- **Desenfoque**: con el desenfoque de Windows el fondo ya no se estira (la ventana del desenfoque no puede seguir cuadro a cuadro: se veía mal y volvían las esquinas) y el desenfoque va 1 px adentro del borde, tapando los escalones.
- **Paquetes de íconos**: Preferencias → Dock → "Paquete de íconos": una carpeta con .png/.ico (de RocketDock/ObjectDock, temas de Linux como Papirus, packs de macOS convertidos o hechos a mano). Se busca por nombre ("google-chrome.png", "Chrome.ico"…); lo que no esté usa el ícono de Windows.

**Tema Vidrio** (Preferencias → General → Apariencia → Vidrio): estilo vidrio del iPhone en todo — paneles, menús, Dock, barra, Launchpad y la isla — con superficies muy transparentes y un brillo blanco en el borde.

**Arreglos**
- "Transparencia del fondo" (y otros nombres largos) ya no se cortan en Preferencias: la columna del nombre se adapta al texto.

## 0.4.2 — 22/11/2026
Pedidos de Andrés:
- **Ocultar apps del Launchpad**: clic derecho → "Ocultar del Launchpad". Abajo a la derecha aparece "Mostrar apps ocultas (N)": las muestra atenuadas y desde ahí se pueden "Dejar de ocultar". Se guardan en settings.json (`LaunchpadHidden`).
- **Nuevo modo de barra superior "Gota"** (Preferencias → Barra superior → Tipo): como la isla, pero pegada al borde de arriba, con curvas que la unen al borde como una gota, y del mismo tamaño dinámico (crece con los menús, los avisos y los cajones).
- **Centro de control con botones enteros**: Wi-Fi, Bluetooth, Modo avión, Modo oscuro, Concentración, Configuración y Preferencias se activan tocando toda la fila (ícono y texto), no solo el circulito. También en el cajón de la isla y la gota.
- Error de compilación de la 0.4.1 en Preferencias (tipo del contenido de las pestañas) corregido.

## 0.4.1 — 22/11/2026
Pedidos de Andrés después de probar la 0.4.0 (registro de su PC revisado):
- **Ventanas que "querían acomodarse y no podían"**: el registro mostraba que McBar y el Explorador se peleaban por el área de trabajo cada pocos minutos. Ahora las correcciones de McBar son silenciosas (no avisan a todas las ventanas, así nada se sacude ni el Explorador reacciona) y, si la pelea sigue, McBar deja de corregir hasta que cambie algo (monitores, Preferencias o reinicio del Explorador).
- **Rendimiento**:
  - El Dock flotante tenía una sombra con efecto (DropShadowEffect + caché) que se recalculaba en cada cuadro de la lupa: ahora es una sombra dibujada, sin costo.
  - El Dock ya no cambia su forma en cada cuadro mientras el mouse está encima.
  - El Launchpad usa ventana rápida (antes era una ventana "en capas" de pantalla completa: Windows copiaba toda la pantalla en cada cuadro).
  - La ventana de la isla vuelve a medir 300 de alto y solo crece mientras un cajón lo necesita.
- **Dock** (elegido por Andrés):
  - El fondo crece con la lupa como en macOS: los íconos ya no se salen.
  - "Vidrio dibujado por McBar" en lugar del desenfoque de Windows: esquinas suaves, sin bordes dentados ni cortes de la sombra. El desenfoque sigue como opción en Preferencias → Dock.
  - La forma de la ventana ya no recorta las esquinas (Windows no las suaviza): las dibuja WPF.
- **Papelera que desaparecía**: para quitar o cerrar un ícono arrastrándolo hay que mantenerlo afuera un momento (se ve el cartel), así no se pierde por un tirón sin querer. Si la Papelera y Descargas no estaban, vuelven solas al actualizar; también hay un botón en Preferencias → Dock para volver a ponerlas.
- **Clima propio** (elegido por Andrés): tocar el clima de la barra o de la isla abre un widget con el tiempo de ahora, sensación térmica, humedad, viento, las próximas 24 horas y los próximos 7 días (con barrita de mínima/máxima y probabilidad de lluvia). Datos de Open-Meteo, nada de Windows.
- **Isla con estilo Vidrio**: dibujaba un cuadrado oscuro enorme (el desenfoque de Windows no respeta la forma de la ventana). Ahora es vidrio dibujado por McBar.
- **Launchpad muy transparente**: la escala de transparencia ahora va de casi opaco (0) a casi nada (100), pareja; con desenfoque el velo va a la mitad.
- **Preferencias en pestañas**: secciones a la izquierda (Dock, Barra superior, Isla dinámica, Notificaciones, Launchpad, Bandeja, General, Widgets) y su contenido a la derecha; recuerda la última abierta.

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
- Error de compilación CS0051 (App.ToggleIslandDrawer era público y usaba un tipo interno): ahora es `internal`.

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
