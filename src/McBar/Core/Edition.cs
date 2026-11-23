// McBar — Creado por Andrés Londero
// 1.9.2 — Edición de McBar: la de siempre (McBar.exe, con su propio instalador) o la de Microsoft Store
// (paquete MSIX, compilada con -p:McBarStore=true). Diferencias de la edición Store:
//   - No muestra el instalador propio ni "Instalar McBar…" (instala y actualiza la Store).
//   - "Abrir McBar al iniciar Windows" usa la tarea de inicio del paquete (StartupTask "McBarStartup").
//   - Para reiniciarse sola (HangWatch) usa el alias McBar.exe del paquete.
//   - Los ajustes quedan en la carpeta propia del paquete (Windows la separa sola) y se borran al desinstalar.
// Todo lo demás es igual.
using System;
using System.IO;

namespace McBar.Core
{
    public static class Edition
    {
#if STORE
        public const bool IsStore = true;
#else
        public const bool IsStore = false;
#endif
        public const string StartupTaskId = "McBarStartup";

        /// <summary>Alias del paquete (uap5:AppExecutionAlias) para abrir McBar Store con parámetros.</summary>
        public static string StoreAlias => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "McBar.exe");
    }
}
