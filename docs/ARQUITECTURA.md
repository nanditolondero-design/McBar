# Arquitectura de McBar

## Arranque (`App.xaml.cs`)
1. Carga ajustes (`%AppData%\McBar\settings.json`). Con `--restaurar` devuelve la barra de Windows y sale.
2. Instancia única (mutex). Manejo de errores: los menores se registran y la app sigue; ante uno fatal se restaura la barra de Windows.
3. Primera vez: fija Launchpad, Explorador, Edge/Chrome (si están), Calculadora y Configuración.
4. Tema (`ThemeManager`), seguimiento de ventanas (`WindowTracker`), ocultar barra de Windows (`Taskbar`), barra de menú y Dock.

## Seguimiento de ventanas (`Core/WindowTracker.cs`)
- Hooks `SetWinEventHook` (foco, mostrar/ocultar/destruir, cambio de título, "cloaked"). Varios avisos seguidos se agrupan (220 ms) en un solo recuento con `EnumWindows`.
- Filtra igual que Alt+Tab (visible, sin dueño, no herramienta, no "cloaked", con título).
- Cada ventana tiene **claves**: `exe:<ruta>` y, si existe, `aumid:<AppUserModelID>` (de la ventana o, en apps de la Store, del proceso hijo de ApplicationFrameHost).
- Un ícono fijado tiene sus propias claves (`Core/AppIdentity.cs`); si alguna coincide, esa ventana es de ese ícono.

## Dock (`Dock/`)
- Ventana transparente a lo ancho, abajo, registrada como AppBar (reserva el alto de la barra, no el de los íconos ampliados).
- Los píxeles transparentes dejan pasar los clics (ventana "layered"); los íconos tienen un fondo casi invisible para que sí reciban clics.
- **Motor de animación**: `CompositionTarget.Rendering` se engancha solo cuando algo cambia. Cada cuadro:
  1. tamaños deseados según el mouse (`Magnifier.TargetSizes`, con posiciones sin ampliar),
  2. acercamiento suave (`Magnifier.Approach`),
  3. disposición anclada al mouse (`Magnifier.Layout`),
  4. rebote (`Magnifier.Bounce`), etiqueta y ocultación automática.
  Cuando todo quedó quieto se desengancha.
- Agregar/quitar elementos = crecer desde 0 / achicarse hasta 0 (misma animación).

## Barra de menú (`MenuBar/`)
- AppBar arriba (28 px a 100 %), fondo acrílico (`SetWindowCompositionAttribute`).
- No se activa al hacer clic: la app activa sigue teniendo el foco y los menús le mandan atajos con `SendInput`.
- Escucha `TaskbarCreated` para recuperarse si el Explorador se reinicia.
- Reloj: temporizador alineado al cambio de minuto. Red, batería y volumen: al cambiar (eventos del sistema) y cada minuto.

## Paneles (`Panels/`)
- `PanelWindow`: base con tarjeta, sombra, animación y cierre al perder el foco.
- `ControlCenterWindow`, `WidgetsWindow`, `LaunchpadWindow`, `SettingsWindow`, `McDialog`.
- Se crean al abrir y se cierran del todo; luego `App.ScheduleTrim()` libera memoria.

## Íconos (`Core/IconLoader.cs`)
- Hilo de fondo STA con cola. `IShellItemImageFactory` sirve para .exe, .lnk, carpetas, archivos y `shell:AppsFolder\<id>`.
- Caché chico para el Dock (tope 120); el Launchpad usa su propio caché que se descarta al cerrar.

## Barra de Windows (`Native/Taskbar.cs`)
- Guarda el estado original, pone "ocultar automáticamente" (para liberar el espacio) y esconde las ventanas de la barra.
- Si Windows la vuelve a mostrar (Inicio, reinicio del Explorador), se vuelve a esconder.
- Botón de bandeja: la muestra un momento con Win+B (experimental).

## Datos guardados
- `settings.json` (ajustes y apps fijadas), `notas.txt` (widget de notas), `mcbar.log` (errores, se recorta a 512 KB).
