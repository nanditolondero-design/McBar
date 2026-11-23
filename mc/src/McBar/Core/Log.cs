// McBar — Creado por Andrés Londero
// Registro de errores mínimo en %AppData%\McBar\mcbar.log (se recorta solo para no crecer).
using System;
using System.IO;

namespace McBar.Core
{
    public static class Log
    {
        private static readonly object Lock = new object();

        public static void Write(string message)
        {
            try
            {
                lock (Lock)
                {
                    Directory.CreateDirectory(SettingsStore.Folder);
                    var path = Path.Combine(SettingsStore.Folder, "mcbar.log");
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                    File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }
}
