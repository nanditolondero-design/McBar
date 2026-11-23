// McBar — Creado por Andrés Londero
// "Desinstalar…" del Launchpad (0.4.3), como el menú Inicio de Windows:
//  - Apps de la Tienda (shell:AppsFolder\Paquete!App): abre la página de esa app en Configuración, con
//    su botón Desinstalar.
//  - Apps de escritorio: busca el desinstalador registrado (Agregar o quitar programas) por el nombre y lo
//    abre (pide permisos si hace falta, igual que Windows).
//  - Si no se encuentra: abre Configuración → Aplicaciones instaladas.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;

namespace McBar.Core
{
    public static class Uninstaller
    {
        public static void Uninstall(string name, string target)
        {
            try
            {
                var pfn = StorePackageFamily(target);
                if (pfn != null) { Launcher.Open("ms-settings:appsfeatures-app?" + pfn); return; }

                var cmd = FindUninstallCommand(name);
                if (cmd != null)
                {
                    var (exe, args) = SplitCommand(cmd);
                    Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
                    return;
                }
            }
            catch (Exception ex) { Log.Write("Desinstalar: " + ex.Message); }
            Launcher.Open("ms-settings:appsfeatures");
        }

        /// <summary>"shell:AppsFolder\Familia_xxx!App" → "Familia_xxx" (solo apps de la Tienda). Pura, se prueba.</summary>
        public static string? StorePackageFamily(string target)
        {
            const string prefix = "shell:AppsFolder\\";
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            var id = target.Substring(prefix.Length);
            int bang = id.IndexOf('!');
            if (bang <= 0) return null;
            var pfn = id.Substring(0, bang);
            return pfn.Contains('_') ? pfn : null;
        }

        /// <summary>Separa "programa + argumentos" de un UninstallString (con o sin comillas). Pura, se prueba.</summary>
        public static (string exe, string args) SplitCommand(string cmd)
        {
            cmd = cmd.Trim();
            if (cmd.StartsWith("\""))
            {
                int end = cmd.IndexOf('"', 1);
                if (end > 0) return (cmd.Substring(1, end - 1), cmd.Substring(end + 1).Trim());
            }
            int exe = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe > 0) return (cmd.Substring(0, exe + 4), cmd.Substring(exe + 4).Trim());
            int sp = cmd.IndexOf(' ');
            return sp > 0 ? (cmd.Substring(0, sp), cmd.Substring(sp + 1).Trim()) : (cmd, "");
        }

        /// <summary>El nombre registrado que mejor coincide (igual; si no, uno contiene al otro). Pura, se prueba.</summary>
        public static string? BestMatch(string name, IEnumerable<string> registered)
        {
            var list = registered.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
            var exact = list.FirstOrDefault(r => string.Equals(r.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            if (name.Trim().Length < 4) return null;
            return list.Where(r => r.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase) || name.Contains(r.Trim(), StringComparison.OrdinalIgnoreCase))
                       .OrderBy(r => Math.Abs(r.Length - name.Length)).FirstOrDefault();
        }

        private static string? FindUninstallCommand(string name)
        {
            var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (var path in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                using var root = hive.OpenSubKey(path);
                if (root == null) continue;
                foreach (var sub in root.GetSubKeyNames())
                {
                    using var k = root.OpenSubKey(sub);
                    if (k?.GetValue("DisplayName") is string dn && k.GetValue("UninstallString") is string us && !string.IsNullOrWhiteSpace(us))
                        entries.TryAdd(dn, us);
                }
            }
            var match = BestMatch(name, entries.Keys);
            return match != null ? entries[match] : null;
        }
    }
}
