// McBar — Creado por Andrés Londero
// 1.3.0 — Fondo de pantalla según la hora: una imagen para la mañana, la tarde, el atardecer y la noche
// (como los fondos dinámicos de macOS). Si a un momento no se le eligió imagen, se usa la del momento
// anterior que tenga. Se revisa una vez por minuto (no gasta) y solo se cambia el fondo cuando hace falta.
// 1.7.0 — Fondos propios de McBar ("Ondas", originales en 4K, uno por momento) que vienen dentro del .exe
// y se copian a %AppData%\McBar\fondos al usarlos. Opción para cambiar también la imagen de la pantalla
// de bloqueo (que Windows puede mostrar también al iniciar sesión) con la API de Windows LockScreen.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace McBar.Core
{
    public static class DynamicWallpaper
    {
        /// <summary>Momentos del día, en orden: clave, nombre y hora en que empieza.</summary>
        public static readonly (string key, string name, int startHour)[] Slots =
        {
            ("morning", "Mañana", 6),
            ("afternoon", "Tarde", 12),
            ("evening", "Atardecer", 18),
            ("night", "Noche", 21),
        };

        /// <summary>Índice del momento del día para una hora (0..23). Pura, se prueba.</summary>
        public static int SlotFor(int hour)
        {
            for (int i = Slots.Length - 1; i >= 0; i--)
                if (hour >= Slots[i].startHour) return i;
            return Slots.Length - 1; // de 0 a 5: noche
        }

        /// <summary>Imagen que corresponde a esa hora; si el momento no tiene, la del anterior que tenga. Pura.</summary>
        public static string? PathFor(IList<string> images, int hour)
        {
            if (images == null || images.Count == 0) return null;
            int start = SlotFor(hour);
            for (int k = 0; k < Slots.Length; k++)
            {
                int i = ((start - k) % Slots.Length + Slots.Length) % Slots.Length;
                if (i < images.Count && !string.IsNullOrWhiteSpace(images[i])) return images[i];
            }
            return null;
        }

        // ---------- Fondos de McBar (1.7.0) ----------

        /// <summary>Archivos de los fondos de McBar, en el mismo orden que <see cref="Slots"/>.</summary>
        public static readonly string[] BundledNames =
        {
            "McBar-ondas-manana-4k.jpg",
            "McBar-ondas-tarde-4k.jpg",
            "McBar-ondas-atardecer-4k.jpg",
            "McBar-ondas-noche-4k.jpg",
        };

        /// <summary>Carpeta donde se copian: %AppData%\McBar\fondos.</summary>
        public static string BundledFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "McBar", "fondos");

        /// <summary>Rutas de los fondos de McBar dentro de una carpeta, una por momento. Pura, se prueba.</summary>
        public static List<string> BundledPaths(string folder)
        {
            var list = new List<string>();
            foreach (var n in BundledNames) list.Add(Path.Combine(folder, n));
            return list;
        }

        /// <summary>¿Las imágenes elegidas son justo los fondos de McBar de esa carpeta? Pura, se prueba.</summary>
        public static bool UsesBundled(IList<string> images, string folder)
        {
            var b = BundledPaths(folder);
            if (images == null || images.Count < b.Count) return false;
            for (int i = 0; i < b.Count; i++)
                if (!string.Equals(images[i], b[i], StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        /// <summary>Copia los fondos que vienen dentro del .exe a <see cref="BundledFolder"/> (solo los que
        /// faltan o están incompletos) y devuelve sus rutas; null si no se pudo.</summary>
        public static List<string>? ExtractBundled()
        {
            try
            {
                var folder = BundledFolder;
                Directory.CreateDirectory(folder);
                var asm = typeof(DynamicWallpaper).Assembly;
                var paths = BundledPaths(folder);
                for (int i = 0; i < BundledNames.Length; i++)
                {
                    using var src = asm.GetManifestResourceStream("McBar.Fondos." + BundledNames[i]);
                    if (src == null) { Log.Write("Fondos de McBar: falta dentro del .exe " + BundledNames[i]); return null; }
                    var fi = new FileInfo(paths[i]);
                    if (fi.Exists && fi.Length == src.Length) continue; // ya estaba
                    var tmp = paths[i] + ".tmp";
                    using (var dst = File.Create(tmp)) src.CopyTo(dst);
                    File.Move(tmp, paths[i], overwrite: true);
                }
                return paths;
            }
            catch (Exception ex) { Log.Write("Fondos de McBar: " + ex.Message); return null; }
        }

        /// <summary>Pone los fondos de McBar en los 4 momentos y prende el cambio según la hora.</summary>
        public static bool UseBundled()
        {
            var paths = ExtractBundled();
            if (paths == null) return false;
            var s = SettingsStore.Current;
            s.WallpaperImages = paths;
            s.WallpaperByTime = true;
            return true;
        }

        private static DispatcherTimer? _timer;
        private static string? _applied;
        private static string? _lockApplied;
        private static string? _lastKey;

        /// <summary>Prende o apaga el cambio automático según los ajustes (se llama al arrancar y al cambiar ajustes).</summary>
        public static void Apply()
        {
            var s = SettingsStore.Current;
            if (!s.WallpaperByTime) { _timer?.Stop(); _applied = null; _lockApplied = null; _lastKey = null; return; }
            if (_timer == null)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(1) };
                _timer.Tick += (_, _) => Check();
            }
            _timer.Start();
            var key = string.Join("|", s.WallpaperImages) + "|" + s.WallpaperLockScreen;
            if (key != _lastKey) { _lastKey = key; _applied = null; _lockApplied = null; } // cambiaron: volver a aplicar
            Check();
        }

        public static void Stop() => _timer?.Stop();

        private static void Check()
        {
            var path = PathFor(SettingsStore.Current.WallpaperImages, DateTime.Now.Hour);
            if (path == null) return;
            if (SettingsStore.Current.WallpaperLockScreen && path != _lockApplied && File.Exists(path))
            {
                _lockApplied = path;
                _ = SetLockScreenAsync(path);
            }
            if (path == _applied) return;
            if (!File.Exists(path)) { Log.Write("Fondo según la hora: no existe " + path); _applied = path; return; }
            _applied = path;
            Task.Run(() =>
            {
                try
                {
                    if (!SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE))
                        Log.Write("Fondo según la hora: Windows no aceptó " + path);
                }
                catch (Exception ex) { Log.Write("Fondo según la hora: " + ex.Message); }
            });
        }

        /// <summary>Cambia la imagen de la pantalla de bloqueo (Windows la muestra también al iniciar sesión
        /// si está prendido "Mostrar la imagen de fondo de la pantalla de bloqueo en la pantalla de inicio de
        /// sesión"). No pide administrador. Si una política de la empresa lo bloquea, solo queda en el registro.</summary>
        private static async Task SetLockScreenAsync(string path)
        {
            try
            {
                var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(path);
                await global::Windows.System.UserProfile.LockScreen.SetImageFileAsync(file);
            }
            catch (Exception ex) { Log.Write("Pantalla de bloqueo: " + ex.Message); }
        }

        private const uint SPI_SETDESKWALLPAPER = 0x0014;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, string vparam, uint winIni);
    }
}
