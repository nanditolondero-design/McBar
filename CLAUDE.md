# CLAUDE.md — guía para trabajar en McBar

McBar: Dock + barra de menú estilo macOS para Windows 10/11. Autor: Andrés Londero (firma: "Creado por Andrés Londero").

## Reglas del proyecto
- Todo en castellano (rioplatense): textos de la interfaz, comentarios y documentación.
- **No cambiar la interfaz (aspecto, colores, textos de pantalla) sin consultarlo antes con Andrés.**
- Al terminar cada etapa, dejar todo documentado dentro del zip: este archivo, `docs/`, `README.md`, `CHANGELOG.md`, pruebas y comentarios.
- Entrega: **mc.zip** con el contenido del repo en la raíz (sin carpeta contenedora). El workflow lo descomprime, lo sube y compila.
- Prioridad: **bajo consumo**. No agregar temporizadores que corran en reposo ni sondeos; usar avisos de Windows y crear ventanas a demanda.
- Siempre restaurar la barra de tareas de Windows al salir o ante un error.

## Tecnología
- C# / .NET 8 / WPF (`net8.0-windows10.0.19041.0`), x64, publicación de un solo archivo autocontenido + ReadyToRun.
- Única dependencia NuGet: `System.Management` (brillo por WMI).
- Pruebas: xUnit en `tests/McBar.Tests` (solo lógica sin interfaz).
- No hay compilador en el entorno de Claude: el código se compila en GitHub Actions (`windows-latest`). Escribir con cuidado y revisar el log de Actions si falla.

## Estructura
```
src/McBar/
  App.xaml(.cs)          arranque, coordinación, limpieza al salir
  Themes/                Dark.xaml, Light.xaml (colores) y Controls.xaml (estilos)
  Native/                Win32 (P/Invoke), AppBar, COM del Shell, audio, barra de Windows, operaciones de ventanas
  Core/                  ajustes, seguimiento de ventanas, identidad de apps, catálogo, íconos, clima, ampliación (matemática), sistema
  Dock/                  DockWindow + DockItem (vista y modelo) + DockBackdrop (desenfoque)
  MenuBar/               MenuBarContent (contenido compartido), MenuBarWindow (barra), IslandWindow (isla dinámica)
  Panels/                PanelWindow (base), Centro de control, Widgets, Launchpad, Preferencias, diálogos
tests/McBar.Tests/       pruebas xUnit
docs/                    arquitectura, etapas, pruebas manuales
.github/workflows/       build.yml
```

## Convenciones
- Colores siempre por `DynamicResource` / `SetResourceReference` con claves `Mc.*` (cambian en vivo con el tema).
- Los fondos de paneles son bastante opacos a propósito (se tiene que leer bien siempre).
- Paneles nuevos: heredar de `PanelWindow`; abrir/cerrar desde `App.ToggleX()` con el control de "recién cerrado".
- Lógica testeable en `Core/` sin depender de WPF; agregar su prueba en `tests/`.
- Ajustes nuevos: propiedad en `AppSettings`, corregir rango en `SettingsStore.Sanitize`, control en `SettingsWindow`, reaccionar a `SettingsStore.Changed`.

## Puntos delicados
- Dock e isla son ventanas "rápidas" (vidrio de DWM + región). Si se agrega algo que se dibuja fuera de la barra/píldora, incluirlo en la forma (`UpdateShape`) o no se va a ver ni recibir clics.
- Nunca corregir el área de trabajo en un bucle: cada corrección reacomoda todas las ventanas maximizadas.
- Íconos de bandeja del propio Windows (`TrayIcon.IsWindowsSystem`): nunca mostrarlos ni mandarles clics.
- Varios monitores: todo lo que se muestra va atado a un `MonitorInfo`; pedir datos frescos con `Monitors.Get(handle)` antes de usar el área de trabajo.
- Barra e isla comparten `MenuBarContent`: cualquier botón/menú nuevo va ahí (sirve para las dos). La isla es siempre oscura.
- `ActivityHub` solo corre en modo isla; toda fuente nueva de eventos tiene que ser por eventos (nada de sondeos) y avisar en el hilo de la interfaz.
- TFM `net8.0-windows10.0.19041.0` (APIs de WinRT): no importar `Windows.Foundation` ni `Windows.UI` en archivos de WPF (choques de nombres como `Size`/`Color`).
- `WorkArea` (no AppBar): el área de trabajo de cada monitor la fija McBar. Siempre llamar `App.UpdateWorkAreas()` si cambia algo que ocupa espacio, y `WorkArea.Reset()` al salir.
- `TrayHost`: nuestra ventana se llama `Shell_TrayWnd`. Todo lo que no sea bandeja (mensajes de AppBar) se reenvía al Explorador; `Taskbar.ExplorerTray()` saltea la nuestra. Ignorar `TaskbarCreated` propio (`IsOwnBroadcastRecent`).
- Menús y popups del Dock/barra: siempre `PopupGuard.Attach(...)`.
- El bucle de animación del Dock trabaja sobre una copia de `_items` por cuadro y está protegido con try/catch: nunca dejar que un error se repita en cada cuadro.
- No enganchar eventos de Windows que se disparen con cada movimiento del mouse (LOCATIONCHANGE global, etc.): usar el vigilante.
- Cosas de pantalla por monitor: recibir `MonitorInfo`; las medidas en píxeles se convierten con `App.Scale` (escala del sistema).
- `Taskbar`: guarda el estado original en settings (`TaskbarModified`, `OriginalTaskbarState`) antes de tocar nada. `--restaurar` lo devuelve.
- `AppBar`: reserva espacio arriba/abajo; el aviso de pantalla completa del escritorio se ignora (falso positivo de Windows).
- `WindowTracker`: el delegado del hook debe quedar referenciado en un campo (si no, el GC lo borra y la app se cae).
- Ventanas del Dock y la barra: `WS_EX_NOACTIVATE`, así la app activa no pierde el foco y los menús Archivo/Edición le mandan los atajos a ella.
- Widgets nuevos: agregarlos a `WidgetFactory.Kinds` y `Build`; los timers se prenden en `Loaded` y se apagan en `Unloaded` (helper `Every`).
- Atajos con Ctrl+Alt: usar Alt IZQUIERDO (`VK_LMENU`) y rechazar `VK_RMENU`: AltGr es Ctrl+Alt derecho y se usa para escribir @, #, etc.
- Errores: `UnhandledException` guarda el informe (`Recovery.SaveReport`) y llama a `SafeCleanup`; Windows reabre McBar. No agregar actualización automática: Andrés no la quiere.
- Miniaturas DWM: solo en ventanas NO en capas (`AllowsTransparency=false` + `MakeGlass`) y siempre liberarlas al cerrar (`ThumbHost.Dispose`).
- WinRT (Wi-Fi, radios, medios) va solo en archivos de `Core/` sin `using System.Windows…`: los nombres chocan. Las vistas WPF usan clases simples (`WifiNet`, `BtDevice`, `AudioDevice`).
- Teclado: el gancho tiene que contestar al instante; todo lo que haga algo va por `Ui(...)` al hilo de la interfaz. Nunca "comerse" la tecla Windows (quedaría trabada): para que no abra Inicio se manda la tecla vacía 0xE8.
- Instalador: no tocar `Installer.InstallDir` ni la clave `Uninstall\McBar` sin migrar; `--uninstall` lo llama Windows. Lo que corre antes del mutex (instalar/desinstalar) no puede depender de ventanas de McBar ya creadas: solo `ThemeManager.Apply()` y diálogos.
- `MenuBarContent.ApplyRows`: en la isla, sin app activa, el logo se mueve a `RightGroup` y queda una sola fila (`IsTwoRowsNow`, evento `RowsChanged` → la isla se vuelve a medir).
- La gota se quitó en la 0.4.3 (Sanitize la pasa a isla). `IsIslandLike` queda por si vuelve otro modo con la ventana de la isla.
- Colores nuevos: agregarlos a Dark.xaml y Light.xaml, y si cambian con el vidrio, también a Glass.xaml (que solo pisa claves).
- Dock: nada de `Effect` (DropShadowEffect, Blur) ni `BitmapCache` en elementos que cambian de tamaño cada cuadro; la sombra son dos `Border` (`BarShadow1/2`). La forma de la ventana es rectangular con margen: las esquinas las suaviza WPF.
- Nunca desenfoque de Windows (accent) en una ventana rápida que sea más grande que lo que se ve: Windows lo aplica a toda la ventana, no a la forma.
- `WorkArea.Verify` corrige SIN avisar (`notify: false`); solo los pedidos explícitos (`WorkArea.Set`) avisan a las ventanas.
- Ajustes que cambian de valor por defecto para usuarios existentes: hacerlo una sola vez en `SettingsStore.Migrate` subiendo `CurrentLook`.
- Dock: mientras el mouse está encima, la forma (`UpdateShape`) es una zona amplia y FIJA; no volver a recalcularla al tamaño justo en cada cuadro (Windows aplica la región antes de dibujar y los íconos se ven cortados).
- Isla: la ventana mide 680 de alto para que entren los cajones; la forma limita los clics a la píldora. Cajón nuevo → `IslandWindow.BuildDrawer` + `DrawerKind`.
- `KeyboardHook` (Win+N / Win+A) corre en su propio hilo: el gancho tiene que contestar rápido, todo lo demás se manda al hilo de la interfaz.
- Carpetas del Launchpad: toda la lógica en `Core/LaunchpadLayout.cs` (con pruebas); la ventana solo dibuja y guarda con `SettingsStore.Save()`.
- La ampliación usa posiciones "sin ampliar" para calcular tamaños (`Core/Magnifier.cs`); no cambiar a posiciones reales porque tiembla.
- Actividades en vivo (1.2.0): todo el estado está en `Core/LiveStatus` (temporizador, cronómetro, descargas, micrófono/cámara) y avisa con `LiveStatus.Changed` (cada segundo solo si hay algo en vivo). La isla y la barra solo dibujan (`UpdateLive`). Si se agrega otra actividad, sumarla en `LiveStatus.Summary()` y `HasLive`; si cambia el ancho de la isla en reposo, tocar `CompactNowWidth()`.
- Micrófono/cámara: se LEE el registro de privacidad de Windows (ConsentStore); McBar nunca abre ni usa el micrófono ni la cámara.
- Perfiles / importar (1.3.0): NUNCA reemplazar `SettingsStore.Current` por otro objeto (Preferencias, Docks y barras lo tienen agarrado): copiar con `Profiles.CopyInto`. Un ajuste nuevo propio de la PC (que no deba viajar en perfiles ni exportaciones) va en `Profiles.MachineFields`.
- Modo juego (1.3.0): todo lo que "moleste" (atajos, avisos, esquinas, cosas que aparecen solas) tiene que mirar `GameMode.On`.
- Textos en C#: no usar comillas rectas dentro de un string; usar «» (en la 1.1.0 rompieron la compilación).
- Strings de C#: las barras invertidas van dobles (`"\;"`, `"\\n"` literal). Un `"\;"` no compila (CS1009).
- Trabadas (1.3.1): `Core/HangWatch` anota en `mcbar.log` «La interfaz no responde… Ocupada en: X». Ante un reporte de "se traba", pedir/leer ese log primero. Nunca hacer trabajo largo (disco, red, registro grande) en el hilo de la interfaz.
- "Probar sin instalar" se recuerda por ruta Y versión (`PortablePath` + `PortableVersion`).
- Isla/barra y menús (1.3.1): todo menú o popup nuevo va con `PopupGuard.Attach` (además de cerrarlo con clics afuera, lo sube por encima de la isla). Ventanas propias grandes (como Preferencias): `App.KeepBelowTopBar(this)` en `Loaded`.
- Íconos de apps nuevos (1.3.2): pedirlos con `IconLoader.Request(..., appShape: true)` para que respeten "Forma de los íconos → Como en Mac". Carpetas, archivos, Papelera y Descargas NO llevan forma.
- macOSicons (1.4.0): NUNCA incluir íconos bajados en el zip ni en el repo (son de sus autores; uso personal). Siempre conservar el crédito (`creditos.txt`). Respetar el cupo (`MacIconsMonthlyLimit`, gratis 50/mes). La clave se guarda solo cifrada (`Native/Dpapi`) y nunca se exporta.
- Suscriptores de `SettingsStore.Changed` (1.4.1): cada uno se llama con su try/catch; igual, no tirar excepciones ahí. Lo que se dibuja fuera de la ventana del Dock (como el ícono arrastrado) va en una ventana propia (`Dock/DragGhost`), no en la del Dock.
- Escala por monitor (1.5.0): McBar es PerMonitorV2. NUNCA usar `App.Scale` para algo atado a un monitor: usar `monitor.Scale`; ubicar ventanas con `WindowOps.PlaceOnMonitor`. Nada de `DropShadowEffect` en listas grandes ni en cosas que se animan (usar copia del texto o `BitmapCache` durante la animación). No volver a vaciar la memoria con TrimWorkingSet.
- Aspecto (1.6.0): paneles nuevos → heredar de `PanelWindow` (ya trae vidrio, sombra `PanelShadow`, brillo y entrada con resorte). Animaciones de entrada: `PanelLook.SpringIn` / `PanelLook.Spring`, para que todo rebote igual.
- 1.6.1: "Desenfoque detrás del Dock" (`BackdropBlur`) se quitó a pedido de Andrés: Sanitize lo deja siempre en false; no volver a ofrecerlo. Con el Launchpad abierto, Dock e isla van ENCIMA de él (1.6.2, como en macOS).
- Fondos (1.7.0): los 4 fondos "Ondas" de `Assets/Fondos` son ORIGINALES de McBar (generados por código, inspirados en el estilo de macOS sin copiarlo) y van embebidos en el .exe; mantener `BundledNames` en el mismo orden que `DynamicWallpaper.Slots`. El inicio de sesión de Windows solo se toca a través de la imagen de la pantalla de bloqueo (API oficial); nunca modificar archivos del sistema (LogonUI).
- Estilo Mac (1.8.0): TODA opción nueva de este tipo va apagada de fábrica (Andrés: "que sean opciones, nada fijo") y en la pestaña Estilo Mac. Lo que cambia ajustes de Windows (registro) tiene que guardar antes los valores (`RegBackup.Save`) y devolverlos al apagar (`RegBackup.Restore`) y al desinstalar (`MacLook.RestoreAll`). Cursores y sonidos son PROPIOS (generados en `tools/estilo-mac`), nunca copiar los de Apple. Ganchos nuevos: en hilo propio y sin trabajo largo adentro (ver `MouseHook`). Todo lo que aparece solo tiene que respetar `GameMode.On`.
- 1.8.1: los "botones de colores" (semáforos) sobre ventanas ajenas se sacaron a pedido de Andrés ("son feos y no van bien"): no volver a ofrecerlos. Tecla Windows: se traga entera (ver `KeyboardHook`, `_winSwallowed`/`_winInjected`); no volver al truco de la tecla vacía sola.
- .icns (1.8.2): cualquier lugar nuevo que cargue una imagen elegida por el usuario tiene que pasar por `IconLoader.FromImageFile` (ya entiende .icns).
- Borrar archivos: el zip NO borra archivos del repo (el workflow solo descomprime encima). Si se saca un .cs, dejarlo en el zip VACÍO (solo comentario), como `Core/TrafficLights.cs` y `Panels/TrafficLightsWindow.cs` (1.8.2), o pedirle a Andrés que lo borre del repo.
- Carpetas del Dock (1.8.3): toda la lógica en `Core/DockFolders` (pura, con pruebas); en el Dock, todo cambio por `ApplyFolderChange`. Si se agrega otro tipo de elemento fijo al Dock, sumarlo en `PinnedEnd`, `SavePins` y `CanDrag` (filtran por tipo).
- Vidrio de la píldora (1.8.6): NO usar el desenfoque de Windows en ventanas chicas con forma (en Windows 11 se ve rectangular). 1.8.7: la píldora de vidrio es transparencia real (tinte con alfa, `IslandWindow.GlassTint`); `Core/WallpaperGlass` quedó sin uso.
- 1.9.0: tampoco en paneles (`PanelWindow`): NADA de `EnableBlur` en ventanas con esquinas redondeadas que no ocupen toda la pantalla. Transparencia real con `Mc.PanelCardBg` (+ `ThemeManager.MinAlpha`).
