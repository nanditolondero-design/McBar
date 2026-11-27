// McBar — Creado por Andrés Londero
// 1.3.0 — Perfiles (por ejemplo "Trabajo" y "Juegos") y copia de seguridad de los ajustes.
//  - Un perfil es una foto de TODOS los ajustes (Dock, apps fijadas, isla, tema, widgets…). Al pasar a
//    otro perfil, primero se actualiza la foto del perfil actual (así no se pierden los cambios) y después
//    se cargan los ajustes del nuevo. Un perfil puede prender el modo juego.
//  - Exportar / importar: un .json con los ajustes (y los perfiles) para pasarlos a otra PC o guardarlos.
// Nunca se pisan los datos propios de esta PC (estado original de la barra de Windows, ruta portátil…).
// Los valores se copian sobre el MISMO objeto de ajustes (Preferencias y los Docks lo tienen agarrado).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McBar.Core
{
    public sealed class SettingsProfile
    {
        public string Name { get; set; } = "";
        /// <summary>Prender el modo juego al usar este perfil.</summary>
        public bool GameMode { get; set; }
        /// <summary>Los ajustes guardados (JSON de AppSettings, sin la lista de perfiles).</summary>
        public string Data { get; set; } = "";
    }

    public static class Profiles
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>Datos de esta PC: nunca vienen de un perfil ni de un archivo importado.</summary>
        public static readonly string[] MachineFields =
        {
            nameof(AppSettings.TaskbarModified), nameof(AppSettings.OriginalTaskbarState), nameof(AppSettings.OriginalMinAnimate),
            nameof(AppSettings.PortablePath), nameof(AppSettings.PortableVersion), nameof(AppSettings.FirstRunDone), nameof(AppSettings.LookVersion),
            nameof(AppSettings.StartWithWindows),
            nameof(AppSettings.MacIconsKey), nameof(AppSettings.MacIconsMonth), nameof(AppSettings.MacIconsUsed),
        };

        /// <summary>La lista de perfiles en sí (no se copia al cambiar de perfil).</summary>
        public static readonly string[] ProfileFields = { nameof(AppSettings.Profiles), nameof(AppSettings.ActiveProfile), nameof(AppSettings.ProfileOneMonitor), nameof(AppSettings.ProfileManyMonitors) };

        /// <summary>Foto de los ajustes para guardar en un perfil (sin los perfiles adentro). Pura.</summary>
        public static string Capture(AppSettings s)
        {
            var copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s, Options), Options) ?? new AppSettings();
            copy.Profiles = new List<SettingsProfile>();
            copy.ActiveProfile = "";
            copy.MacIconsKey = ""; // 1.4.0: la clave nunca va dentro de un perfil
            return JsonSerializer.Serialize(copy, Options);
        }

        /// <summary>Copia los valores de <paramref name="source"/> sobre <paramref name="target"/>, salvo los campos indicados. Pura.</summary>
        public static void CopyInto(AppSettings target, AppSettings source, IEnumerable<string> keep)
        {
            var skip = new HashSet<string>(keep);
            foreach (var p in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || !p.CanWrite || skip.Contains(p.Name)) continue;
                if (p.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                p.SetValue(target, p.GetValue(source));
            }
        }

        public static SettingsProfile? Find(AppSettings s, string name) =>
            s.Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));

        /// <summary>Guarda los ajustes actuales como perfil (nuevo o reemplazando uno con ese nombre). Pura.</summary>
        public static SettingsProfile SaveAs(AppSettings s, string name, bool? gameMode = null)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) throw new ArgumentException("El perfil necesita un nombre.");
            var p = Find(s, name);
            if (p == null) { p = new SettingsProfile { Name = name }; s.Profiles.Add(p); }
            if (gameMode != null) p.GameMode = gameMode.Value;
            p.Data = Capture(s);
            s.ActiveProfile = p.Name;
            return p;
        }

        /// <summary>Pasa a otro perfil: actualiza la foto del actual y carga la del nuevo. Pura (no avisa a nadie).</summary>
        public static bool Switch(AppSettings s, string name)
        {
            var next = Find(s, name);
            if (next == null || string.IsNullOrEmpty(next.Data)) return false;
            var cur = Find(s, s.ActiveProfile);
            if (cur != null && cur != next) cur.Data = Capture(s);
            AppSettings? loaded;
            try { loaded = JsonSerializer.Deserialize<AppSettings>(next.Data, Options); }
            catch { return false; }
            if (loaded == null) return false;
            CopyInto(s, SettingsStore.Sanitize(loaded), MachineFields.Concat(ProfileFields));
            s.ActiveProfile = next.Name;
            return true;
        }

        // ================== Con efecto en la app ==================

        /// <summary>Usar un perfil: carga sus ajustes, avisa a todo McBar y prende/apaga el modo juego.</summary>
        public static bool Use(string name)
        {
            var s = SettingsStore.Current;
            if (!Switch(s, name)) return false;
            SettingsStore.NotifyChanged();
            SettingsStore.NotifyPinsChanged(null);
            GameMode.Set(Find(s, name)?.GameMode == true);
            ProfileChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 1.9.55: perfil que corresponde según la cantidad de monitores, o null si no hay que cambiar nada (no hay perfil
        /// elegido, no existe, o ya es el que está en uso). Pura, se prueba.
        /// </summary>
        public static string? ForMonitorCount(AppSettings s, int monitors)
        {
            string want = monitors > 1 ? s.ProfileManyMonitors : s.ProfileOneMonitor;
            if (string.IsNullOrEmpty(want)) return null;
            var p = Find(s, want);
            if (p == null || p.Name == s.ActiveProfile) return null;
            return p.Name;
        }

        /// <summary>Se dispara al cambiar de perfil o importar (para rehacer widgets del escritorio y Preferencias).</summary>
        public static event Action? ProfileChanged;

        /// <summary>Archivo exportado: marca, versión y ajustes. Pura.</summary>
        public static string Export(AppSettings s)
        {
            var copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s, Options), Options) ?? new AppSettings();
            copy.MacIconsKey = ""; // 1.4.0: la clave de macOSicons no se exporta (es personal y está cifrada para esta PC)
            return JsonSerializer.Serialize(new ExportFile
            {
                App = "McBar",
                Version = typeof(Profiles).Assembly.GetName().Version?.ToString(3) ?? "",
                Exported = DateTime.Now,
                Settings = copy,
            }, Options);
        }

        /// <summary>Lee un archivo exportado (o un settings.json suelto). Devuelve null si no es de McBar. Pura.</summary>
        public static AppSettings? ReadExport(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;
                if (root.TryGetProperty("App", out var app) && app.GetString() == "McBar" && root.TryGetProperty("Settings", out var st))
                    return st.ValueKind == JsonValueKind.Object ? SettingsStore.Sanitize(st.Deserialize<AppSettings>(Options)!) : null;
                // settings.json directo: tiene que parecerse a los ajustes de McBar
                if (root.TryGetProperty(nameof(AppSettings.DockIconSize), out _) || root.TryGetProperty(nameof(AppSettings.Pinned), out _))
                    return SettingsStore.Sanitize(root.Deserialize<AppSettings>(Options)!);
            }
            catch { }
            return null;
        }

        public static void ExportTo(string path) => File.WriteAllText(path, Export(SettingsStore.Current));

        /// <summary>Importa un archivo: reemplaza los ajustes (y los perfiles) salvo los datos de esta PC.</summary>
        public static bool ImportFrom(string path)
        {
            var loaded = ReadExport(File.ReadAllText(path));
            if (loaded == null) return false;
            CopyInto(SettingsStore.Current, loaded, MachineFields);
            SettingsStore.NotifyChanged();
            SettingsStore.NotifyPinsChanged(null);
            ProfileChanged?.Invoke();
            return true;
        }

        private sealed class ExportFile
        {
            public string App { get; set; } = "";
            public string Version { get; set; } = "";
            public DateTime Exported { get; set; }
            public AppSettings? Settings { get; set; }
        }
    }
}
