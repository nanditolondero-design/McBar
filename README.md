# McBar

**Dock y barra de menú estilo macOS para Windows 10 y 11.**
Creado por Andrés Londero.

McBar reemplaza la barra de tareas de Windows por un Dock abajo y una barra de menú arriba, con el aspecto y las animaciones de macOS, consumiendo lo mínimo posible.

## Qué incluye (versión 0.1)

**Dock**
- Ampliación de íconos al pasar el mouse (el ícono bajo el cursor queda fijo y los vecinos se corren, como en macOS).
- Rebote al abrir una app, puntito debajo de las apps abiertas, etiqueta con el nombre.
- Apps fijadas + apps abiertas sin fijar, separador, **Descargas** (pila con los últimos archivos) y **Papelera** (soltar archivos para borrarlos, vaciar con confirmación).
- Arrastrar íconos para reordenar; arrastrarlos hacia arriba y soltar para quitarlos.
- Soltar un .exe, un acceso directo o una carpeta en el Dock para fijarlo.
- Clic derecho: ventanas abiertas, Mantener en el Dock, Mostrar en el Explorador, Nueva ventana, Ocultar, Salir.
- Clic con la rueda: abre otra ventana de la app.
- Ocultación automática opcional.

**Barra de menú**
- Menú de McBar (Acerca de este equipo, Configuración, Forzar salida, Suspender, Reiniciar, Apagar, Bloquear, Cerrar sesión).
- Nombre de la app activa con menús Archivo, Edición, Ver, Ventana y Ayuda (mandan los atajos estándar de Windows a esa app).
- A la derecha: bandeja de Windows, batería, red, volumen (con deslizador), búsqueda, Centro de control y reloj.
- Fondo desenfocado (acrílico).

**Centro de control**: Wi-Fi, Bluetooth, modo avión, modo oscuro (cambia Windows de verdad), concentración, brillo (notebooks) y sonido.

**Widgets** (tocando el reloj): calendario, clima (Open-Meteo, sin clave), batería, procesador y memoria, notas.

**Launchpad**: todas las apps instaladas (de escritorio y de la Store) en páginas, con búsqueda, fondo desenfocado y animación. Clic derecho → Agregar al Dock.

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

## Usar

- Ejecutar `McBar.exe`. La barra de Windows se oculta y aparecen el Dock y la barra de menú.
- Para salir: menú de McBar (arriba a la izquierda) → **Salir de McBar**. La barra de Windows vuelve.
- Si algo sale mal y la barra de Windows queda oculta: ejecutar `McBar.exe --restaurar`.
- Los ajustes están en `%AppData%\McBar\settings.json`; el registro de errores en `%AppData%\McBar\mcbar.log`.

## Documentación

- [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md) — cómo está hecho.
- [docs/ETAPAS.md](docs/ETAPAS.md) — qué se hizo y qué sigue.
- [docs/PRUEBAS.md](docs/PRUEBAS.md) — lista de pruebas manuales para QA.
- [CLAUDE.md](CLAUDE.md) — guía para seguir desarrollando.
