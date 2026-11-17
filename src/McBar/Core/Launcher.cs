// McBar — Creado por Andrés Londero
// Abrir apps, carpetas, archivos y páginas de Configuración de Windows; acciones de energía.
using System;
using System.Diagnostics;
using System.IO;
using McBar.Native;

namespace McBar.Core
{
    public static class Launcher
    {
        public static bool Launch(string target)
        {
            try
            {
                if (AppIdentity.IsAppsFolder(target))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + target + "\"") { UseShellExecute = true });
                    return true;
                }
                var expanded = Environment.ExpandEnvironmentVariables(target);
                var psi = new ProcessStartInfo(expanded) { UseShellExecute = true };
                var dir = Path.GetDirectoryName(expanded);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) psi.WorkingDirectory = dir;
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                Log.Write($"No se pudo abrir {target}: {ex.Message}");
                return false;
            }
        }

        /// <summary>Abre una página de Configuración de Windows, p. ej. "ms-settings:bluetooth".</summary>
        public static void Open(string uri)
        {
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Write($"No se pudo abrir {uri}: {ex.Message}"); }
        }

        public static void ShowInExplorer(string path)
        {
            try
            {
                if (File.Exists(path)) Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else if (Directory.Exists(path)) Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch { }
        }

        public static void Sleep() => Win32.SetSuspendState(false, false, false);
        public static void Restart() => Run("shutdown.exe", "/r /t 0");
        public static void Shutdown() => Run("shutdown.exe", "/s /t 0");
        public static void LogOff() => Run("shutdown.exe", "/l");
        public static void Lock() => Win32.LockWorkStation();

        private static void Run(string exe, string args)
        {
            try { Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true }); }
            catch (Exception ex) { Log.Write($"{exe} {args}: {ex.Message}"); }
        }
    }
}
