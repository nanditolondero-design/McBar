// McBar — Creado por Andrés Londero
// 1.8.6 — "Vidrio" dibujado por McBar con el fondo de pantalla (para la píldora).
// El desenfoque de Windows no respeta la forma redondeada de una ventana chica en Windows 11 (en la 1.8.5 se
// veía un rectángulo claro alrededor de la píldora). En cambio McBar toma el fondo de pantalla, lo achica
// mucho (al agrandarlo de nuevo queda borroso, como un vidrio esmerilado) y pinta adentro de la píldora
// justo la parte del fondo que está detrás, con un tinte encima. Así la forma es perfecta y no gasta nada.
// Como la isla se esconde con ventanas maximizadas, casi siempre lo que está detrás es el fondo de pantalla.
// 1.8.7: SIN USO por ahora — se eligió transparencia real (se ve lo que está detrás, sin desenfoque).
// Queda por si se quiere volver a ofrecer como opción.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using McBar.Native;
using Microsoft.Win32;

namespace McBar.Core
{
    public static class WallpaperGlass
    {
        private static string _path = "";
        private static DateTime _stamp;
        private static BitmapSource? _small;

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)]
        private static extern bool GetWallpaper(uint action, uint param, StringBuilder value, uint winIni);

        /// <summary>Fondo de pantalla actual, achicado (borroso). Null si no hay imagen (color sólido, etc.).</summary>
        private static BitmapSource? Small()
        {
            try
            {
                var sb = new StringBuilder(520);
                if (!GetWallpaper(0x0073 /*SPI_GETDESKWALLPAPER*/, (uint)sb.Capacity, sb, 0)) return null;
                var path = sb.ToString();
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) { _small = null; _path = ""; return null; }
                var stamp = File.GetLastWriteTimeUtc(path);
                if (path == _path && stamp == _stamp) return _small;
                _path = path; _stamp = stamp;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path);
                bi.DecodePixelWidth = 96; // muy chico: al estirarlo queda difuminado
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.EndInit();
                bi.Freeze();
                _small = bi;
                return _small;
            }
            catch (Exception ex) { Log.Write("Vidrio de la píldora: " + ex.Message); _small = null; return null; }
        }

        /// <summary>
        /// Parte de la imagen (en sus píxeles) que se ve en un monitor de esa proporción, como hace Windows con
        /// "Rellenar" (cubrir y centrar) o "Expandir" (estirar entera). Pura, se prueba.
        /// </summary>
        public static Rect VisiblePart(double imgW, double imgH, double monW, double monH, bool stretch)
        {
            if (stretch || imgW <= 0 || imgH <= 0 || monW <= 0 || monH <= 0) return new Rect(0, 0, imgW, imgH);
            double k = Math.Max(monW / imgW, monH / imgH); // cubrir
            double w = monW / k, h = monH / k;
            return new Rect((imgW - w) / 2, (imgH - h) / 2, w, h);
        }

        private static bool IsStretch()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                return (k?.GetValue("WallpaperStyle") as string) == "2";
            }
            catch { return false; }
        }

        /// <summary>
        /// Pincel para el fondo de un elemento que está en (left, top) del monitor (en DIP), con el tinte del vidrio
        /// según la transparencia (0..100). Null si no hay fondo de pantalla con imagen.
        /// </summary>
        public static Brush? BrushFor(MonitorInfo m, double left, double top, int transparency)
        {
            var img = Small();
            if (img == null) return null;
            double monW = m.Bounds.Width / m.Scale, monH = m.Bounds.Height / m.Scale;
            var part = VisiblePart(img.PixelWidth, img.PixelHeight, monW, monH, IsStretch());
            var crop = new CroppedBitmap(img, new Int32Rect((int)part.X, (int)part.Y, Math.Max(1, (int)part.Width), Math.Max(1, (int)part.Height)));
            var g = new DrawingGroup();
            var full = new Rect(0, 0, monW, monH);
            g.Children.Add(new ImageDrawing(crop, full));
            // tinte: más opaco con poca transparencia; un brillo claro arriba como el canto de un vidrio
            // (nunca menos de ~25 % de tinte, para que el texto blanco se siga leyendo sobre fondos claros)
            byte a1 = Math.Max((byte)0x40, ThemeManager.ScaleAlpha(0x70, transparency)), a2 = Math.Max((byte)0x48, ThemeManager.ScaleAlpha(0x88, transparency));
            var tint = new LinearGradientBrush(Color.FromArgb(a1, 0x30, 0x30, 0x36), Color.FromArgb(a2, 0x18, 0x18, 0x1C), 90);
            g.Children.Add(new GeometryDrawing(tint, null, new RectangleGeometry(full)));
            var brush = new DrawingBrush(g)
            {
                Stretch = Stretch.Fill,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = full,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(-left, -top, monW, monH),
                TileMode = TileMode.None,
            };
            RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.Linear);
            brush.Freeze();
            return brush;
        }
    }
}
