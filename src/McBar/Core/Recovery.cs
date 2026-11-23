// McBar — Creado por Andrés Londero
// Estabilidad (0.9.0):
//  - Si McBar se cierra por un error: guarda un informe en %AppData%\McBar\errores, devuelve la barra de
//    Windows y el área de trabajo (SafeCleanup) y Windows lo vuelve a abrir solo (RegisterApplicationRestart,
//    el mismo mecanismo que usa Office). Si se cae 3 veces en 10 minutos, no se reinicia más (para no
//    quedar en un círculo).
//  - Al abrir de nuevo, si hubo un error reciente, avisa en las notificaciones con dónde quedó el informe.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace McBar.Core
{
    public static class Recovery
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterApplicationRestart(string? commandLine, int flags);

        public static string Folder => Path.Combine(SettingsStore.Folder, "errores");

        /// <summary>Pide a Windows que reabra McBar si se cierra por un error o se cuelga.</summary>
        public static void Register()
        {
            try
            {
                if (TooManyRecentCrashes(CrashTimes(), DateTime.Now)) { Log.Write("Recuperación: demasiados errores seguidos, no se reinicia solo"); return; }
                RegisterApplicationRestart("--reiniciado", 0);
            }
            catch { }
        }

        /// <summary>Guarda el informe del error. Devuelve la ruta.</summary>
        public static string? SaveReport(object? error)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, $"error-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                var v = typeof(Recovery).Assembly.GetName().Version;
                File.WriteAllText(path,
                    $"McBar {v} — {DateTime.Now:dd/MM/yyyy HH:mm:ss}\r\nWindows {Environment.OSVersion.Version}\r\n" +
                    $"Memoria: {Diagnostics.Sample().Format()}\r\n\r\n{error}\r\n");
                // se guardan los últimos 20
                foreach (var old in Directory.GetFiles(Folder, "error-*.txt").OrderByDescending(f => f).Skip(20)) File.Delete(old);
                return path;
            }
            catch { return null; }
        }

        private static List<DateTime> CrashTimes()
        {
            try
            {
                if (!Directory.Exists(Folder)) return new List<DateTime>();
                return Directory.GetFiles(Folder, "error-*.txt").Select(File.GetCreationTime).ToList();
            }
            catch { return new List<DateTime>(); }
        }

        /// <summary>¿3 o más errores en los últimos 10 minutos? (pura, se prueba)</summary>
        public static bool TooManyRecentCrashes(IEnumerable<DateTime> crashes, DateTime now) =>
            crashes.Count(t => (now - t).TotalMinutes <= 10) >= 3;

        /// <summary>El informe más reciente si es de la última media hora (para avisar al abrir).</summary>
        public static string? RecentReport()
        {
            try
            {
                if (!Directory.Exists(Folder)) return null;
                var f = Directory.GetFiles(Folder, "error-*.txt").OrderByDescending(x => x).FirstOrDefault();
                if (f == null || (DateTime.Now - File.GetCreationTime(f)).TotalMinutes > 30) return null;
                var seen = Path.Combine(Folder, "avisado.txt");
                if (File.Exists(seen) && File.ReadAllText(seen).Trim() == Path.GetFileName(f)) return null;
                File.WriteAllText(seen, Path.GetFileName(f));
                return f;
            }
            catch { return null; }
        }
    }
}
