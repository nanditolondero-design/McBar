# McBar

**Dock y barra de menú estilo macOS para Windows 10 y 11.**
Creado por Andrés Londero.

McBar reemplaza la barra de tareas de Windows por un Dock abajo y una barra de menú arriba, con el aspecto y las animaciones de macOS, consumiendo lo mínimo posible.

## Qué incluye (versión 0.6.0)

**Dock**
- Ampliación de íconos al pasar el mouse (el ícono bajo el cursor queda fijo y los vecinos se corren, como en macOS).
- Rebote al abrir una app, puntito debajo de las apps abiertas, etiqueta con el nombre.
- Apps fijadas + apps abiertas sin fijar, separador, **Descargas** (pila con los últimos archivos) y **Papelera** (soltar archivos para borrarlos, vaciar con confirmación).
- Arrastrar íconos para reordenar; arrastrarlos hacia arriba y soltar para quitarlos.
- Soltar un .exe, un acceso directo o una carpeta en el Dock para fijarlo.
- Clic derecho: ventanas abiertas, Mantener en el Dock, Mostrar en el Explorador, Nueva ventana, Ocultar, Salir.
- Clic con la rueda: abre otra ventana de la app.
- Ocultación automática opcional.
- **Estilos**: Clásico, Flotante (separado del borde, con sombra), Ancho completo, Barra (ancha y flotante, como la de Windows 11) o Solo íconos (sin fondo).
- **Paquetes de íconos**: una carpeta con .png/.ico de otras barras o sistemas.
- Fondo con **desenfoque real**; el fondo puede quedar quieto (por defecto) o estirarse como en macOS.
- Lado derecho configurable: sacá o agregá Papelera, Descargas, carpetas y archivos (arrastrándolos o con clic derecho → Agregar al Dock).
- Arrastrar un ícono hacia arriba muestra "Quitar del Dock" y al soltarlo desaparece con una nubecita. Si es una app abierta sin fijar, muestra "Cerrar app" y al soltarla se cierra.
- Las ventanas se achican **hacia su ícono del Dock** al minimizar y vuelven desde ahí.
- **Varios monitores**: barra de menú y Dock en cada uno (se puede limitar al principal).

**Barra de menú**
- Menú de McBar (Acerca de este equipo, Configuración, Forzar salida, Suspender, Reiniciar, Apagar, Bloquear, Cerrar sesión).
- Nombre de la app activa con menús Archivo, Edición, Ver, Ventana y Ayuda (mandan los atajos estándar de Windows a esa app).
- A la derecha: **bandeja propia** (los íconos de WhatsApp, antivirus, etc., con sus menús de clic derecho), **clima** con ícono dinámico, batería, red, volumen, búsqueda, Centro de control y reloj.
- Los íconos de la bandeja se pueden mostrar en color o en blanco y negro, y ocultar los que no quieras (quedan en el menú ⌃).
- Fondo desenfocado (acrílico).

**Isla dinámica** (opcional, en Preferencias → Barra superior → Tipo): en lugar de la barra de menú, una píldora negra arriba al centro, como en el iPhone.
- En reposo muestra la hora (y la tapa del disco con un ecualizador si suena algo).
- Al pasar el mouse se agranda con todo lo de la barra: menús, bandeja, clima, batería, red, volumen, búsqueda, Centro de control y reloj.
- Se agranda sola un momento con: volumen y brillo, enchufar/desenchufar y batería baja, tema nuevo y notificaciones.
- Tocándola mientras suena algo: reproductor con tapa, título, artista, progreso y controles (Spotify, YouTube, etc.).

**Centro de control**: Wi-Fi, Bluetooth, modo avión, modo oscuro (cambia Windows de verdad), concentración, brillo (notebooks) y sonido.

**Notificaciones propias** (0.4): McBar junta las notificaciones de Windows en su propio centro, agrupadas por app. Con la isla se abre como cajón desde la píldora; con la barra, tocando el reloj (arriba de los widgets). Win+N y Win+A abren las de McBar en vez de los paneles de Windows. Opcional: ocultar el cartelito de Windows.

**Teclado y buscador** (0.6): la tecla Windows abre el Launchpad, Win+S abre un buscador tipo Spotlight (apps, archivos, cuentas, Configuración y web) y las teclas de volumen muestran el cartel de McBar.

**Clima** (tocando el clima de la barra o la isla): ahora, sensación, humedad, viento, 24 horas y 7 días.

**Widgets** (tocando el reloj): calendario, clima (Open-Meteo, sin clave), batería, procesador y memoria, notas.

**Launchpad** (como el de macOS): todas las apps instaladas (de escritorio y de la Store) en páginas, con búsqueda, fondo desenfocado (transparencia regulable) y animación. **Carpetas**: arrastrá una app sobre otra; arrastrando al borde se reordena. Ctrl+clic para elegir varias y ocultarlas juntas; las ocultas se ven al buscar. Clic derecho → Agregar al Dock, carpetas, Ocultar, Desinstalar. Arrastrando una app hacia abajo se puede soltar en el Dock o en el escritorio.

**Preferencias**: tamaño y ampliación del Dock, ocultación automática, tema claro/oscuro/automático, ocultar o no la barra de Windows, iniciar con Windows, ciudad del clima y qué widgets mostrar.

## Bajo consumo

- Nada se repite en un bucle en reposo: las ventanas abiertas se detectan por avisos de Windows (WinEvents), el reloj se actualiza una vez por minuto.
- Las animaciones solo corren mientras algo se mueve.
- Paneles, Launchpad y Preferencias se crean al abrirse y se destruyen al cerrarse; después se devuelve la memoria al sistema.
- Recolector de memoria simple (sin hilos extra) y compilación ReadyToRun (arranca más rápido).

## Compilar (GitHub)

1. La primera vez, crear en el repo el archivo `.github/workflows/build.yml` (está dentro del zip) subiéndolo a mano.
2. Subir **mc.zip** a la rama `main`.
3. El workflow descomprime el zip, sube los archivos, borra mc.zip, corre las pruebas y compila.
4. Descargar **McBar-win-x64** desde la pestaña *Actions* → la ejecución → *Artifacts*. Adentro está `McBar.exe` (no necesita instalar .NET).

> Si en el futuro cambia el workflow, hay que subirlo a mano: GitHub no deja que el propio workflow se modifique a sí mismo.

## Instalar (0.5.0)

- Abrir `McBar.exe` (por ejemplo desde Descargas): aparece el **instalador** estilo Mac. Opciones: iniciar con Windows, accesos directos en el menú Inicio y el escritorio, traer las apps fijadas de la barra de Windows y usar el tema Vidrio.
- Se instala para el usuario, **sin permisos de administrador**, en `%LocalAppData%\Programs\McBar`, y aparece en **Configuración → Aplicaciones instaladas**.
- Para **actualizar**: abrir el `McBar.exe` nuevo; si es más nuevo que el instalado, ofrece "Actualizar" y conserva todos los ajustes.
- Para **desinstalar**: Configuración → Aplicaciones instaladas → McBar → Desinstalar (o `McBar.exe --uninstall`). Devuelve la barra de Windows y borra McBar; los ajustes quedan en `%AppData%\McBar`.
- **Probar sin instalar**: botón del instalador (o `McBar.exe --portable`). Esa copia no vuelve a preguntar.

## Usar

- Abrir McBar. La barra de Windows se oculta y aparecen el Dock y la barra de menú.
- Para salir: menú de McBar (arriba a la izquierda) → **Salir de McBar**. La barra de Windows vuelve.
- Si algo sale mal y la barra de Windows queda oculta: ejecutar `McBar.exe --restaurar` (devuelve la barra, el espacio de las ventanas y la animación de Windows como estaban).
- Los ajustes están en `%AppData%\McBar\settings.json`; el registro de errores en `%AppData%\McBar\mcbar.log`.

## Documentación

- [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md) — cómo está hecho.
- [docs/ETAPAS.md](docs/ETAPAS.md) — qué se hizo y qué sigue.
- [docs/PRUEBAS.md](docs/PRUEBAS.md) — lista de pruebas manuales para QA.
- [CLAUDE.md](CLAUDE.md) — guía para seguir desarrollando.
