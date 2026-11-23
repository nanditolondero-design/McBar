// McBar — Creado por Andrés Londero
// 1.8.0 — "Estilo Mac": opciones para que Windows se parezca más a macOS. TODAS son opcionales y vienen
// apagadas (Preferencias → Estilo Mac). Este archivo coordina:
//   - MacLook.Apply(): se llama desde App.OnSettingsChanged y prende/apaga cada parte.
//   - Bundled: copia a %AppData%\McBar\<carpeta> los archivos que vienen dentro del .exe (cursores, sonidos).
//   - RegBackup: guarda los valores del registro que McBar cambia (cursores, sonidos) para dejarlos como
//     estaban al apagar la opción.
// Las partes están en MacSystemLook.cs (modo oscuro, cursores, sonidos), MacInput.cs (gancho del mouse,
// sacudir, clic en el fondo), Screenshots.cs, StageManager.cs y AppMenus.cs.
// (Los "botones de colores" de la 1.8.0 se sacaron en la 1.8.1 a pedido de Andrés: no quedaban bien.)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using McBar.Native;
using Microsoft.Win32;

namespace McBar.Core
{
    public static class MacLook
    {
        /// <summary>Prende o apaga cada opción según los ajustes. No tira excepciones.</summary>
        public static void Apply()
        {
            Try(AutoAppearance.Apply, "modo oscuro automático");
            Try(MacCursors.Apply, "cursores");
            Try(MacSounds.Apply, "sonidos");
            Try(MouseHook.Apply, "gancho del mouse");
            Try(StageManager.Apply, "organizador visual");
        }

        /// <summary>Al salir de McBar: se apagan los temporizadores y ganchos. Cursores y sonidos quedan puestos
        /// (son ajustes de Windows); se devuelven al apagar la opción o al desinstalar.</summary>
        public static void Stop()
        {
            Try(AutoAppearance.Stop, "modo oscuro automático");
            Try(MouseHook.Stop, "gancho del mouse");
            Try(StageManager.Stop, "organizador visual");
        }

        /// <summary>Al desinstalar: deja cursores y sonidos como estaban antes de McBar.</summary>
        public static void RestoreAll()
        {
            Try(() => MacCursors.Restore(), "cursores");
            Try(() => MacSounds.Restore(), "sonidos");
        }

        private static void Try(Action a, string what)
        {
            try { a(); } catch (Exception ex) { Log.Write("Estilo Mac (" + what + "): " + ex.Message); }
        }
    }

    /// <summary>Archivos que vienen dentro de McBar.exe (EmbeddedResource "McBar.&lt;Grupo&gt;.&lt;archivo&gt;").</summary>
    internal static class Bundled
    {
        public static string Folder(string name) => Path.Combine(SettingsStore.Folder, name);

        /// <summary>Copia el archivo a la carpeta (solo si falta o cambió de tamaño). Devuelve la ruta o null.</summary>
        public static string? Extract(string group, string file, string folderName)
        {
            try
            {
                var dir = Folder(folderName);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, file);
                using var src = typeof(Bundled).Assembly.GetManifestResourceStream("McBar." + group + "." + file);
                if (src == null) { Log.Write("Falta dentro del .exe: " + group + "/" + file); return null; }
                var fi = new FileInfo(path);
                if (fi.Exists && fi.Length == src.Length) return path;
                var tmp = path + ".tmp";
                using (var dst = File.Create(tmp)) src.CopyTo(dst);
                File.Move(tmp, path, overwrite: true);
                return path;
            }
            catch (Exception ex) { Log.Write("No se pudo copiar " + file + ": " + ex.Message); return null; }
        }
    }

    /// <summary>
    /// Guarda en un .json los valores del registro (de HKEY_CURRENT_USER) antes de cambiarlos, para poder
    /// volver a dejarlos igual. Clave: "ruta|nombre" ("" = valor predeterminado). Valor: texto, "dword:N" para
    /// números o null si no existía.
    /// </summary>
    internal static class RegBackup
    {
        public static string Encode(object? v) => v switch
        {
            null => "\u0000",
            int i => "dword:" + i,
            _ => v.ToString() ?? ""
        };

        /// <summary>Decodifica lo guardado: (existía, es número, valor). Pura, se prueba.</summary>
        public static (bool exists, bool isDword, string text, int number) Decode(string? stored)
        {
            if (stored == null || stored == "\u0000") return (false, false, "", 0);
            if (stored.StartsWith("dword:", StringComparison.Ordinal) && int.TryParse(stored.AsSpan(6), out var n)) return (true, true, "", n);
            return (true, false, stored, 0);
        }

        public static bool Exists(string file) => File.Exists(Path.Combine(SettingsStore.Folder, file));

        /// <summary>Guarda los valores actuales (solo si todavía no hay copia: la primera es la original).</summary>
        public static void Save(string file, IEnumerable<(string key, string name)> values)
        {
            var path = Path.Combine(SettingsStore.Folder, file);
            if (File.Exists(path)) return;
            var d = new Dictionary<string, string>();
            foreach (var (key, name) in values)
            {
                using var k = Registry.CurrentUser.OpenSubKey(key);
                d[key + "|" + name] = Encode(k?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames));
            }
            Directory.CreateDirectory(SettingsStore.Folder);
            File.WriteAllText(path, JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>Vuelve a poner los valores guardados y borra la copia. Devuelve false si no había copia.</summary>
        public static bool Restore(string file)
        {
            var path = Path.Combine(SettingsStore.Folder, file);
            if (!File.Exists(path)) return false;
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>();
            foreach (var kv in d)
            {
                int bar = kv.Key.LastIndexOf('|');
                if (bar < 0) continue;
                string key = kv.Key.Substring(0, bar), name = kv.Key.Substring(bar + 1);
                var (exists, isDword, text, number) = Decode(kv.Value);
                using var k = Registry.CurrentUser.CreateSubKey(key, true);
                if (k == null) continue;
                if (!exists) { try { k.DeleteValue(name, false); } catch { } }
                else if (isDword) k.SetValue(name, number, RegistryValueKind.DWord);
                else k.SetValue(name, text, text.Contains('%') ? RegistryValueKind.ExpandString : RegistryValueKind.String);
            }
            File.Delete(path);
            return true;
        }
    }
}
