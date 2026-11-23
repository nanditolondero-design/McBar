// McBar — Creado por Andrés Londero
// 1.8.0 — Capturas de pantalla estilo Mac (opcional, Preferencias → Estilo Mac):
//   Win+Mayús+3 → toda la pantalla donde está el mouse.
//   Win+Mayús+4 → elegir una zona con el mouse (Espacio = elegir una ventana; Esc = cancelar).
//   Win+Mayús+5 → barrita con las opciones (pantalla, ventana, zona).
// Se guardan como PNG en el Escritorio (o la carpeta elegida) con el nombre "Captura de pantalla AAAA-MM-DD a
// las HH.MM.SS.png", suena el obturador y aparece una miniatura abajo a la derecha (clic = abrir, arrastrar =
// llevarla a otra app, clic derecho = copiar / mostrar / borrar). Las ventanas se guardan con sombra, como en Mac.
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using McBar.Native;
using McBar.Panels;

namespace McBar.Core
{
    public static class Screenshots
    {
        /// <summary>Nombre del archivo, como en macOS. Pura, se prueba.</summary>
        public static string FileName(DateTime t) => $"Captura de pantalla {t:yyyy-MM-dd} a las {t:HH.mm.ss}.png";

        /// <summary>Carpeta donde se guardan ("" = Escritorio).</summary>
        public static string Folder()
        {
            var f = SettingsStore.Current.ScreenshotFolder;
            if (!string.IsNullOrWhiteSpace(f) && Directory.Exists(f)) return f;
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        private static bool _busy;
        /// <summary>Mientras se elige la zona, McBar no vuelve a subir la barra y el Dock por encima.</summary>
        internal static bool Busy => _busy;

        /// <summary>0 = pantalla, 1 = zona, 2 = barra de opciones.</summary>
        internal static void Start(int which)
        {
            if (_busy) return;
            var m = App.ActiveMonitor() ?? Monitors.All()[0];
            if (Win32.GetCursorPos(out var p)) m = Monitors.FromPoint(p.x, p.y) ?? m;
            switch (which)
            {
                case 0: FullScreen(m); break;
                case 1: Interactive(m, windowMode: false); break;
                default: CaptureBar.Toggle(m); break;
            }
        }

        internal static void FullScreen(MonitorInfo m)
        {
            var img = ScreenCapture.Region(m.Bounds.Left, m.Bounds.Top, m.Bounds.Width, m.Bounds.Height);
            if (img != null) Save(img, m);
        }

        internal static void Interactive(MonitorInfo m, bool windowMode)
        {
            var frozen = ScreenCapture.Region(m.Bounds.Left, m.Bounds.Top, m.Bounds.Width, m.Bounds.Height);
            if (frozen == null) return;
            _busy = true;
            var o = new CaptureOverlay(m, frozen, windowMode);
            o.Closed += (_, _) => _busy = false;
            o.Show();
        }

        /// <summary>Ventana con sombra suave y esquinas redondeadas (como las capturas de ventanas de macOS).</summary>
        internal static BitmapSource WithShadow(BitmapSource src, double scale)
        {
            int w = src.PixelWidth, h = src.PixelHeight;
            int margin = (int)Math.Round(48 * scale);
            double radius = 8 * scale;
            var img = new Image { Source = src, Width = w, Height = h, Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
            img.Clip = new RectangleGeometry(new Rect(0, 0, w, h), radius, radius);
            var holder = new Border
            {
                Width = w, Height = h, Margin = new Thickness(margin), CornerRadius = new CornerRadius(radius), Background = Brushes.Black,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 36 * scale, ShadowDepth = 12 * scale, Direction = 270, Opacity = 0.45, RenderingBias = System.Windows.Media.Effects.RenderingBias.Quality },
            };
            var root = new Grid();
            root.Children.Add(holder);
            var top = new Border { Width = w, Height = h, Margin = new Thickness(margin), Child = img };
            root.Children.Add(top);
            var size = new Size(w + 2 * margin, h + 2 * margin);
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            var rtb = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            rtb.Freeze();
            return rtb;
        }

        internal static void Save(BitmapSource img, MonitorInfo m)
        {
            var path = Path.Combine(Folder(), FileName(DateTime.Now));
            MacSounds.Play("captura.wav");
            try
            {
                using (var fs = File.Create(path))
                {
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(img));
                    enc.Save(fs);
                }
                Log.Write("Captura guardada: " + path);
                CaptureThumb.Pop(path, img, m);
            }
            catch (Exception ex) { Log.Write("Captura: " + ex.Message); }
        }
    }
}
