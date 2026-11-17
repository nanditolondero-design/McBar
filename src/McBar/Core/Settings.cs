// McBar — Creado por Andrés Londero
// Ajustes de la app, guardados en %AppData%\McBar\settings.json.
using System;
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

        // Barra de menú
        public bool ShowMenuBar { get; set; } = true;
        public bool ShowBatteryPercent { get; set; } = true;
        public bool ShowDateInClock { get; set; } = true;

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

        /// <summary>Solo para pruebas: usar otra carpeta.</summary>
        public static void UseFolder(string folder) => Folder = folder;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options);
                    if (loaded != null) { Current = Sanitize(loaded); return Current; }
                }
            }
            catch (Exception ex) { Log.Write("No se pudieron leer los ajustes: " + ex.Message); }
            Current = new AppSettings();
            return Current;
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
        public static AppSettings Sanitize(AppSettings s)
        {
            s.DockIconSize = Math.Clamp(s.DockIconSize, 32, 96);
            s.MagnifiedSize = Math.Clamp(s.MagnifiedSize, s.DockIconSize, 160);
            if (s.Theme != "dark" && s.Theme != "light") s.Theme = "auto";
            s.Pinned ??= new List<PinnedApp>();
            s.Pinned.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.Target));
            s.WeatherCity ??= "";
            return s;
        }
    }
}
