// McBar — Creado por Andrés Londero
// 1.3.0 — Fondo de pantalla según la hora: una imagen para la mañana, la tarde, el atardecer y la noche
// (como los fondos dinámicos de macOS). Si a un momento no se le eligió imagen, se usa la del momento
// anterior que tenga. Se revisa una vez por minuto (no gasta) y solo se cambia el fondo cuando hace falta.
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

        private static DispatcherTimer? _timer;
        private static string? _applied;
        private static string? _lastKey;

        /// <summary>Prende o apaga el cambio automático según los ajustes (se llama al arrancar y al cambiar ajustes).</summary>
        public static void Apply()
        {
            var s = SettingsStore.Current;
            if (!s.WallpaperByTime) { _timer?.Stop(); _applied = null; _lastKey = null; return; }
            if (_timer == null)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(1) };
                _timer.Tick += (_, _) => Check();
            }
            _timer.Start();
            var key = string.Join("|", s.WallpaperImages);
            if (key != _lastKey) { _lastKey = key; _applied = null; } // cambiaron las imágenes: volver a aplicar
            Check();
        }

        public static void Stop() => _timer?.Stop();

        private static void Check()
        {
            var path = PathFor(SettingsStore.Current.WallpaperImages, DateTime.Now.Hour);
            if (path == null || path == _applied) return;
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

        private const uint SPI_SETDESKWALLPAPER = 0x0014;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, string vparam, uint winIni);
    }
}
