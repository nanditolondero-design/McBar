// McBar — Creado por Andrés Londero
// Ajustes de la app, guardados en %AppData%\McBar\settings.json.
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace McBar.Core
{
    public sealed class PinnedApp
    {
        public string Name { get; set; } = "";
        /// <summary>Ruta a .exe o .lnk, "shell:AppsFolder\&lt;id&gt;" o un elemento especial "mcbar:launchpad".</summary>
        public string Target { get; set; } = "";
    }

    /// <summary>Widget puesto en el escritorio (1.1.0): tipo y posición (DIP de pantalla).</summary>
    public sealed class DesktopWidget
    {
        public string Kind { get; set; } = "clock";
        public double X { get; set; }
        public double Y { get; set; }
    }

    public sealed class LaunchpadFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Carpeta";
        /// <summary>Apps de la carpeta (el mismo "Target" del catálogo: shell:AppsFolder\...).</summary>
        public List<string> Targets { get; set; } = new List<string>();
    }

    public sealed class AppSettings
    {
        // Dock
        public int DockIconSize { get; set; } = 52;
        public bool Magnification { get; set; } = true;
        public int MagnifiedSize { get; set; } = 96;
        public bool DockAutoHide { get; set; } = false;
        public bool ClickMinimizes { get; set; } = false;
        public bool ShowIndicators { get; set; } = true;
        public bool BounceOnLaunch { get; set; } = true;
        public List<PinnedApp> Pinned { get; set; } = new List<PinnedApp>();
        /// <summary>Lado derecho del Dock (después del separador): Descargas, Papelera, carpetas y archivos.</summary>
        public List<PinnedApp> Stacks { get; set; } = DefaultStacks();
        /// <summary>"classic" (pegado abajo), "floating" (separado del borde, con sombra) o "full" (ancho completo).</summary>
        public string DockStyle { get; set; } = "classic";
        /// <summary>false: el fondo del Dock queda quieto y solo se agrandan los íconos. true: el fondo se estira (como macOS).</summary>
        public bool DockBackgroundStretch { get; set; } = true;
        public bool DockOnAllMonitors { get; set; } = true;
        /// <summary>Desenfoque real detrás del Dock y de la isla en estilo Vidrio.</summary>
        public bool BackdropBlur { get; set; } = false;
        /// <summary>Dock e isla como ventanas aceleradas por la tarjeta de video. Apagar solo si se ven fondos negros.</summary>
        public bool FastWindows { get; set; } = true;
        /// <summary>Con varios monitores: cada Dock muestra solo las apps abiertas en su monitor (las fijadas se ven siempre).</summary>
        public bool DockAppsPerMonitor { get; set; } = false;
        public bool AnimateWindows { get; set; } = true;
        /// <summary>Valor original de "animar ventanas al minimizar y maximizar" de Windows (para restaurarlo al salir).</summary>
        public int? OriginalMinAnimate { get; set; }

        /// <summary>
        /// Versión del "aspecto" ya aplicada a estos ajustes (0.4.1): sirve para pasar UNA sola vez a los
        /// valores nuevos elegidos por Andrés (fondo del Dock que crece, vidrio dibujado por McBar) sin pisar
        /// lo que el usuario cambie después.
        /// </summary>
        public int LookVersion { get; set; }

        /// <summary>Isla o gota (las dos usan la ventana de la isla).</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsIslandLike => TopBarMode == "island";

        public static List<PinnedApp> DefaultStacks() => new List<PinnedApp>
        {
            new PinnedApp { Name = "Descargas", Target = "mcbar:downloads" },
            new PinnedApp { Name = "Papelera", Target = "mcbar:trash" }
        };

        // Barra de menú
        public bool ShowMenuBar { get; set; } = true;
        public bool ShowBatteryPercent { get; set; } = true;
        public bool ShowDateInClock { get; set; } = true;
        public bool MenuBarOnAllMonitors { get; set; } = true;
        /// <summary>"menubar" (barra de menú clásica) o "island" (isla dinámica). La "gota" de la 0.4.2 se quitó en la 0.4.3 (pasa a isla).</summary>
        public string TopBarMode { get; set; } = "menubar";
        /// <summary>Reservar arriba el alto de la isla para que las ventanas maximizadas no queden debajo.</summary>
        /// <summary>Reservar una franja arriba para la isla. 1.3.1: apagado por defecto (la píldora flota sobre las ventanas, que usan toda la pantalla).</summary>
        public bool IslandReserveSpace { get; set; } = false;
        public bool IslandMedia { get; set; } = true;
        /// <summary>"black" (negro, como el iPhone), "graphite", "glass" (vidrio con desenfoque) o "accent".</summary>
        public string IslandStyle { get; set; } = "black";
        /// <summary>"small", "normal" o "large".</summary>
        public string IslandSize { get; set; } = "normal";
        public bool IslandShowDate { get; set; } = false;
        public bool IslandGlow { get; set; } = false;
        public bool IslandVolume { get; set; } = true;
        public bool IslandBattery { get; set; } = true;
        public bool IslandNotifications { get; set; } = true;

        // Centro de notificaciones propio
        public bool OwnNotifications { get; set; } = true;
        /// <summary>Esconder el cartelito de notificación de Windows (McBar lo muestra a su manera). Experimental.</summary>
        public bool HideWindowsToasts { get; set; } = true;
        /// <summary>Win+N abre el centro de notificaciones de McBar y Win+A su Centro de control.</summary>
        public bool OwnShortcuts { get; set; } = true;

        // Launchpad
        /// <summary>0 = fondo bien oscuro, 100 = fondo totalmente transparente.</summary>
        public int LaunchpadTransparency { get; set; } = 50;
        public bool LaunchpadBlur { get; set; } = true;
        public List<LaunchpadFolder> LaunchpadFolders { get; set; } = new List<LaunchpadFolder>();
        /// <summary>Apps ocultas del Launchpad (Target). 0.4.2.</summary>
        public List<string> LaunchpadHidden { get; set; } = new List<string>();
        /// <summary>Orden de la grilla del Launchpad (claves de apps y carpetas) cuando el usuario mueve algo. 0.4.3.</summary>
        public List<string> LaunchpadOrder { get; set; } = new List<string>();
        /// <summary>Carpeta del paquete de íconos (vacío = íconos de Windows). 0.4.3.</summary>
        public string IconPackPath { get; set; } = "";
        /// <summary>Ruta del .exe que el usuario eligió "Probar sin instalar" (no se le vuelve a ofrecer instalar). 0.5.0.</summary>
        public string PortablePath { get; set; } = "";
        /// <summary>Versión que se eligió "probar sin instalar" (una más nueva vuelve a mostrar el instalador). 1.3.1.</summary>
        public string PortableVersion { get; set; } = "";
        /// <summary>Si la interfaz se traba 20 s, McBar se reinicia solo (Core/HangWatch). 1.3.1.</summary>
        public bool RestartIfHung { get; set; } = true;
        /// <summary>La tecla Windows sola abre el Launchpad (en vez del menú Inicio). 0.6.0.</summary>
        public bool WinKeyLaunchpad { get; set; } = true;
        /// <summary>Win+S abre el buscador de McBar. 0.6.0.</summary>
        public bool OwnSearch { get; set; } = true;
        /// <summary>Las teclas de volumen las maneja McBar (cartel propio en vez del de Windows). 0.6.0.</summary>
        public bool OwnVolumeKeys { get; set; } = true;
        /// <summary>Alt+Tab propio estilo Cmd+Tab de macOS. 0.8.0.</summary>
        public bool OwnAltTab { get; set; } = true;
        /// <summary>Win+Tab: todas las ventanas (Mission Control). 0.8.0.</summary>
        public bool OwnWinTab { get; set; } = true;
        /// <summary>Vista previa de las ventanas al dejar el mouse sobre una app del Dock. 0.8.0.</summary>
        public bool DockPreviews { get; set; } = true;
        /// <summary>Quick Look con la barra espaciadora en el Explorador. 1.0.0.</summary>
        public bool QuickLook { get; set; } = true;
        /// <summary>Widgets en el escritorio. 1.1.0.</summary>
        public List<DesktopWidget> DesktopWidgets { get; set; } = new List<DesktopWidget>();
        /// <summary>Enlaces iCal (.ics) de Google Calendar / Outlook. 1.1.0.</summary>
        public List<string> CalendarUrls { get; set; } = new List<string>();
        /// <summary>Avisar 10 minutos antes de cada evento del calendario. 1.1.0.</summary>
        public bool EventReminders { get; set; } = true;
        /// <summary>Recordatorios en el panel del reloj. 1.1.0.</summary>
        public bool WidgetReminders { get; set; } = true;
        /// <summary>Temporizador, cronómetro y descargas en la isla y la barra. 1.2.0.</summary>
        public bool LiveActivities { get; set; } = true;
        /// <summary>Puntitos naranja/verde cuando una app usa el micrófono o la cámara. 1.2.0.</summary>
        public bool PrivacyDots { get; set; } = true;
        /// <summary>Mostrar el progreso de las descargas (carpeta Descargas). 1.2.0.</summary>
        public bool LiveDownloads { get; set; } = true;
        /// <summary>Últimos minutos usados en el temporizador (atajos rápidos). 1.2.0.</summary>
        public List<int> TimerPresets { get; set; } = new List<int> { 1, 5, 10, 25 };
        /// <summary>Perfiles guardados (Trabajo, Juegos…). 1.3.0.</summary>
        public List<SettingsProfile> Profiles { get; set; } = new List<SettingsProfile>();
        /// <summary>Nombre del perfil en uso ("" = ninguno). 1.3.0.</summary>
        public string ActiveProfile { get; set; } = "";
        /// <summary>Cambiar el fondo de pantalla según la hora. 1.3.0.</summary>
        public bool WallpaperByTime { get; set; } = false;
        /// <summary>Imágenes de mañana, tarde, atardecer y noche (ver Core/DynamicWallpaper.Slots). 1.3.0.</summary>
        public List<string> WallpaperImages { get; set; } = new List<string> { "", "", "", "" };
        /// <summary>Atajos de mosaico Ctrl+Alt+flechas. 1.0.0.</summary>
        public bool TileShortcuts { get; set; } = true;
        /// <summary>Esquinas activas: acción de cada esquina (ver Core/HotCorners.Actions). 1.0.0.</summary>
        public string CornerTopLeft { get; set; } = "none";
        public string CornerTopRight { get; set; } = "none";
        public string CornerBottomLeft { get; set; } = "none";
        public string CornerBottomRight { get; set; } = "none";
        /// <summary>¿Hace falta el gancho de teclado?</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool NeedsKeyboardHook => OwnShortcuts || WinKeyLaunchpad || OwnSearch || OwnVolumeKeys || OwnAltTab || OwnWinTab || QuickLook || TileShortcuts;
        public bool ShowWeatherInMenuBar { get; set; } = true;

        // Bandeja del sistema propia
        public bool UseOwnTray { get; set; } = true;
        public bool TrayMonochrome { get; set; } = false;
        /// <summary>Íconos de bandeja que van al menú ⌃ en lugar de verse en la barra.</summary>
        public List<string> TrayHiddenIds { get; set; } = new List<string>();

        // General
        public bool HideWindowsTaskbar { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        /// <summary>"auto" (sigue a Windows), "dark" o "light".</summary>
        public string Theme { get; set; } = "auto";

        // Widgets
        public string WeatherCity { get; set; } = "Buenos Aires";
        public double? WeatherLat { get; set; }
        public double? WeatherLon { get; set; }
        public string? WeatherResolvedFor { get; set; }
        public string? WeatherPlaceName { get; set; }
        public bool WidgetCalendar { get; set; } = true;
        public bool WidgetWeather { get; set; } = true;
        public bool WidgetBattery { get; set; } = true;
        public bool WidgetSystem { get; set; } = true;
        public bool WidgetNotes { get; set; } = true;

        // Estado interno de la barra de Windows (para poder restaurarla siempre)
        public bool TaskbarModified { get; set; }
        public int OriginalTaskbarState { get; set; }

        public bool FirstRunDone { get; set; }
    }

    public static class SettingsStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

        public static string Folder { get; private set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "McBar");

        public static string FilePath => Path.Combine(Folder, "settings.json");

        public static AppSettings Current { get; private set; } = new AppSettings();

        /// <summary>Se dispara cuando algo cambia desde Preferencias (el Dock y la barra se actualizan solos).</summary>
        public static event Action? Changed;

        /// <summary>Se dispara cuando un Dock cambió los íconos fijados (para que los Docks de otros monitores se actualicen).</summary>
        public static event Action<object?>? PinsChanged;

        public static void NotifyPinsChanged(object? source)
        {
            Save();
            PinsChanged?.Invoke(source);
        }

        /// <summary>Solo para pruebas: usar otra carpeta.</summary>
        public static void UseFolder(string folder) => Folder = folder;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options);
                    if (loaded != null) { Current = Migrate(Sanitize(loaded)); return Current; }
                }
            }
            catch (Exception ex) { Log.Write("No se pudieron leer los ajustes: " + ex.Message); }
            Current = new AppSettings { LookVersion = CurrentLook };
            return Current;
        }

        public const int CurrentLook = 2;

        /// <summary>
        /// Cambios de una sola vez al actualizar (pura, se prueba).
        /// 0.4.1: Dock con fondo que crece como en macOS y sin el desenfoque de Windows (dejaba esquinas
        /// dentadas); si la Papelera y Descargas habían desaparecido del Dock, vuelven.
        /// </summary>
        public static AppSettings Migrate(AppSettings s, bool save = true)
        {
            if (s.LookVersion >= CurrentLook) return s;
            if (s.LookVersion < 1)
            {
                s.DockBackgroundStretch = true;
                s.BackdropBlur = false;
                if (s.Stacks.Count == 0) s.Stacks = AppSettings.DefaultStacks();
            }
            // 1.3.1: la franja reservada para la isla dejaba un hueco de lado a lado arriba (las ventanas
            // maximizadas no llegaban arriba). Ahora la píldora flota sobre las ventanas, como en el iPhone.
            if (s.LookVersion < 2) s.IslandReserveSpace = false;
            s.LookVersion = CurrentLook;
            if (save) { try { File.WriteAllText(FilePath, JsonSerializer.Serialize(s, Options)); } catch { } }
            return s;
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Options));
                File.Move(tmp, FilePath, true);
            }
            catch (Exception ex) { Log.Write("No se pudieron guardar los ajustes: " + ex.Message); }
        }

        public static void NotifyChanged()
        {
            Save();
            Changed?.Invoke();
        }

        /// <summary>Corrige valores fuera de rango (por si el archivo se editó a mano).</summary>
        private static string ValidCorner(string? v) =>
            v != null && HotCorners.Actions.Any(a => a.key == v) ? v : "none";

        public static AppSettings Sanitize(AppSettings s)
        {
            s.DockIconSize = Math.Clamp(s.DockIconSize, 32, 96);
            s.TimerPresets = (s.TimerPresets ?? new List<int>()).Where(m => m > 0 && m <= 24 * 60).Distinct().Take(6).ToList();
            if (s.TimerPresets.Count == 0) s.TimerPresets = new List<int> { 1, 5, 10, 25 };
            s.MagnifiedSize = Math.Clamp(s.MagnifiedSize, s.DockIconSize, 160);
            if (s.Theme != "dark" && s.Theme != "light" && s.Theme != "glass") s.Theme = "auto";
            s.Pinned ??= new List<PinnedApp>();
            s.Pinned.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.Target));
            s.Stacks ??= AppSettings.DefaultStacks();
            s.Stacks.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.Target));
            s.TrayHiddenIds ??= new List<string>();
            if (s.DockStyle != "floating" && s.DockStyle != "full" && s.DockStyle != "taskbar" && s.DockStyle != "icons") s.DockStyle = "classic";
            if (s.TopBarMode == "drop") s.TopBarMode = "island"; // la gota se quitó en la 0.4.3
            if (s.TopBarMode != "island") s.TopBarMode = "menubar";
            s.LaunchpadTransparency = Math.Clamp(s.LaunchpadTransparency, 0, 100);
            s.LaunchpadFolders ??= new List<LaunchpadFolder>();
            s.LaunchpadHidden ??= new List<string>();
            s.LaunchpadOrder ??= new List<string>();
            s.IconPackPath ??= "";
            s.PortablePath ??= "";
            s.PortableVersion ??= "";
            s.DesktopWidgets ??= new List<DesktopWidget>();
            s.CalendarUrls ??= new List<string>();
            s.CornerTopLeft = ValidCorner(s.CornerTopLeft);
            s.CornerTopRight = ValidCorner(s.CornerTopRight);
            s.CornerBottomLeft = ValidCorner(s.CornerBottomLeft);
            s.CornerBottomRight = ValidCorner(s.CornerBottomRight);
            s.LaunchpadFolders.RemoveAll(f => f == null || f.Targets == null || f.Targets.Count == 0);
            if (s.IslandStyle != "graphite" && s.IslandStyle != "glass" && s.IslandStyle != "accent") s.IslandStyle = "black";
            if (s.IslandSize != "small" && s.IslandSize != "large") s.IslandSize = "normal";
            s.WeatherCity ??= "";
            s.Profiles ??= new List<SettingsProfile>();
            s.Profiles.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.Name));
            s.ActiveProfile ??= "";
            if (s.ActiveProfile.Length > 0 && !s.Profiles.Any(p => p.Name == s.ActiveProfile)) s.ActiveProfile = "";
            s.WallpaperImages ??= new List<string>();
            s.WallpaperImages = s.WallpaperImages.Select(x => x ?? "").Take(4).ToList();
            while (s.WallpaperImages.Count < 4) s.WallpaperImages.Add("");
            return s;
        }
    }
}
