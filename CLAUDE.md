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
- La ampliación usa posiciones "sin ampliar" para calcular tamaños (`Core/Magnifier.cs`); no cambiar a posiciones reales porque tiembla.
