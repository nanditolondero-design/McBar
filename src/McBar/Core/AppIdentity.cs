// McBar — Creado por Andrés Londero
// Identidad de una aplicación: un conjunto de "claves" que permiten saber que una ventana abierta
// pertenece a un ícono fijado en el Dock. Ejemplos de claves:
//   exe:c:\program files\google\chrome\application\chrome.exe
//   aumid:microsoft.windowscalculator_8wekyb3d8bbwe!app
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace McBar.Core
{
    public static class AppIdentity
    {
        public const string AppsFolderPrefix = @"shell:AppsFolder\";
        public const string Launchpad = "mcbar:launchpad";
        public const string Downloads = "mcbar:downloads";
        public const string Trash = "mcbar:trash";

        private static readonly Regex KnownFolderPath = new Regex(@"^\{([0-9A-Fa-f\-]{36})\}\\(.+)$", RegexOptions.Compiled);

        /// <summary>Resolución de carpetas conocidas ({GUID}\app.exe). Se puede reemplazar en pruebas.</summary>
        public static Func<Guid, string?> KnownFolderResolver { get; set; } = id => Native.Win32.KnownFolder(id);

        /// <summary>Resolución de accesos directos .lnk. Se puede reemplazar en pruebas.</summary>
        public static Func<string, string?> ShortcutResolver { get; set; } = p => Native.ShellCom.ResolveShortcut(p);

        public static string ExeKey(string path) => "exe:" + path.Trim().ToLowerInvariant();
        public static string AumidKey(string aumid) => "aumid:" + aumid.Trim().ToLowerInvariant();

        public static bool IsSpecial(string target) => target.StartsWith("mcbar:", StringComparison.OrdinalIgnoreCase);
        public static bool IsAppsFolder(string target) => target.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>Claves para un destino fijado en el Dock.</summary>
        public static HashSet<string> KeysForTarget(string target)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(target) || IsSpecial(target)) return keys;

            if (IsAppsFolder(target))
            {
                var id = target.Substring(AppsFolderPrefix.Length);
                keys.Add(AumidKey(id));
                var m = KnownFolderPath.Match(id);
                if (m.Success && Guid.TryParse(m.Groups[1].Value, out var g))
                {
                    var folder = KnownFolderResolver(g);
                    if (!string.IsNullOrEmpty(folder)) keys.Add(ExeKey(Path.Combine(folder, m.Groups[2].Value)));
                }
                else if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && Path.IsPathRooted(id))
                {
                    keys.Add(ExeKey(id));
                }
                return keys;
            }

            if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var resolved = ShortcutResolver(target);
                if (!string.IsNullOrEmpty(resolved)) keys.Add(ExeKey(resolved));
                return keys;
            }

            keys.Add(ExeKey(Environment.ExpandEnvironmentVariables(target)));
            return keys;
        }

        /// <summary>Nombre lindo para mostrar a partir de un destino.</summary>
        public static string FriendlyName(string target)
        {
            try
            {
                if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(target))
                {
                    var info = FileVersionInfo.GetVersionInfo(target);
                    if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription.Trim();
                }
            }
            catch { }
            var name = Path.GetFileNameWithoutExtension(target);
            return string.IsNullOrEmpty(name) ? target : name;
        }
    }
}
