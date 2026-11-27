// McBar — Creado por Andrés Londero
// 1.9.54: resumen de diagnóstico para copiar y pegar (versión, Windows, monitores, ajustes clave, consumo y el final del registro).
using System;
using System.IO;
using System.Linq;
using System.Text;
using McBar.Native;

namespace McBar.Core
{
    internal static class DiagnosticReport
    {
        /// <summary>Arma el texto del resumen. Nunca lanza: si algo falla, esa parte queda anotada.</summary>
        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"McBar {Installer.CurrentVersion.ToString(3)} — {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            Part(sb, "Windows", () => $"{Environment.OSVersion.Version} ({(Environment.Is64BitOperatingSystem ? "64" : "32")} bits)");
            Part(sb, "Edición", () => Edition.IsStore ? "Microsoft Store" : "Instalador / portable");
            Part(sb, "Consumo", () => Diagnostics.Sample().Format());
            Part(sb, "Monitores", () => string.Join(" | ", Monitors.All().Select(m =>
                $"{m.Device}{(m.Primary ? " (principal)" : "")} {m.Bounds.Width}x{m.Bounds.Height} escala {m.Scale:0.##} área de trabajo {m.Work.Left},{m.Work.Top}-{m.Work.Right},{m.Work.Bottom}")));
            var s = SettingsStore.Current;
            Part(sb, "Ajustes", () => $"barra={s.TopBarMode}, dock={s.DockStyle}, ocultar barra de Windows={s.HideWindowsTaskbar}, " +
                $"Dock oculto con maximizadas={s.DockHideOnMaximized}, isla oculta con maximizadas={s.IslandHideOnMaximized}, " +
                $"bandeja propia={s.UseOwnTray}, ayudante de administrador={s.TrayAdminHelper}, reinicio si se traba={s.RestartIfHung}");
            Part(sb, "Registro (últimas líneas)", () => Environment.NewLine + Tail(60));
            return sb.ToString();
        }

        private static void Part(StringBuilder sb, string name, Func<string> f)
        {
            try { sb.AppendLine(name + ": " + f()); }
            catch (Exception ex) { sb.AppendLine(name + ": (no se pudo leer: " + ex.Message + ")"); }
        }

        /// <summary>Últimas líneas del registro sin el ruido de clics y avisos de la bandeja.</summary>
        private static string Tail(int lines)
        {
            var path = Path.Combine(SettingsStore.Folder, "mcbar.log");
            if (!File.Exists(path)) return "(sin registro)";
            string[] all;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var rd = new StreamReader(fs)) all = rd.ReadToEnd().Split('\n');
            var keep = all.Where(l => l.Length > 0 && !l.Contains(" Tray Left") && !l.Contains(" Tray Right") && !l.Contains("aviso TaskbarCreated"))
                          .Select(l => l.Length > 400 ? l.Substring(0, 400) + "…" : l.TrimEnd('\r'));
            return string.Join(Environment.NewLine, keep.Skip(Math.Max(0, keep.Count() - lines)));
        }
    }
}
