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
        /// <summary>
        /// 1.9.50: ShellExecute en un hilo aparte (STA). Abrir una app de la Store, un archivo en red o un programa que
        /// tarda en arrancar podía dejar la interfaz de McBar congelada; ahora solo se dispara y se sigue.
        /// Devuelve true si se disparó (los errores se anotan en el registro).
        /// </summary>
        public static bool Launch(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return false;
            OnOwnThread("abrir " + target, () =>
            {
                if (AppIdentity.IsAppsFolder(target))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + target + "\"") { UseShellExecute = true });
                    return;
                }
                var expanded = Environment.ExpandEnvironmentVariables(target);
                var psi = new ProcessStartInfo(expanded) { UseShellExecute = true };
                var dir = Path.GetDirectoryName(expanded);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) psi.WorkingDirectory = dir;
                Process.Start(psi);
            });
            return true;
        }

        private static void OnOwnThread(string what, Action work)
        {
            var t = new System.Threading.Thread(() =>
            {
                try { work(); }
                catch (Exception ex) { Log.Write($"No se pudo {what}: {ex.Message}"); }
            }) { IsBackground = true, Name = "McBar.Abrir" };
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
        }

        /// <summary>Abre una página de Configuración de Windows, p. ej. "ms-settings:bluetooth".</summary>
        public static void Open(string uri)
        {
            OnOwnThread("abrir " + uri, () => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }));
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
