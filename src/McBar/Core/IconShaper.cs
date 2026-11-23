// McBar — Creado por Andrés Londero
// 1.3.2 — "Forma de Mac" para los íconos (Preferencias → Dock → Forma de los íconos → Como en Mac).
// Todas las apps quedan con la forma de los íconos de macOS: un cuadrado de esquinas suaves (squircle).
//  - Si el ícono ya ocupa todo su cuadro (por ejemplo apps de la Tienda con fondo de color), solo se le
//    recortan las esquinas con esa forma.
//  - Si es un ícono "suelto" (con transparencia alrededor, como casi todos los de Windows), se lo pone al
//    centro de una placa con esa forma, de un color muy claro sacado del propio ícono (como hace macOS
//    con los íconos que no siguen su forma).
//  - 1.8.1 (pedido de Andrés): la placa ya no es casi blanca para todos: toma el COLOR PRINCIPAL de cada
//    ícono (con más peso a lo más colorido) en un degradé suave, así cada app queda de un color distinto.
//    Los íconos grises o en blanco y negro quedan sobre una placa gris clara.
// Corre en el hilo del Shell (STA), que es donde IconLoader carga los íconos.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace McBar.Core
{
    public static class IconShaper
    {
        /// <summary>Parte del cuadro que ocupa la placa (deja un margen chico como los íconos de macOS).</summary>
        public const double PlateScale = 0.9;
        /// <summary>Tamaño del ícono "suelto" dentro de la placa.</summary>
        public const double GlyphScale = 0.66;

        /// <summary>
        /// Contorno de la placa: superelipse |x|^n + |y|^n = 1 (n = 5, parecida a la de macOS), centrada en
        /// (cx, cy) con radio r. Pura, se prueba.
        /// </summary>
        public static List<Point> SquirclePoints(double cx, double cy, double r, int steps = 96, double n = 5)
        {
            var pts = new List<Point>(steps);
            for (int i = 0; i < steps; i++)
            {
                double t = 2 * Math.PI * i / steps;
                double c = Math.Cos(t), s = Math.Sin(t);
                double x = Math.Sign(c) * Math.Pow(Math.Abs(c), 2 / n);
                double y = Math.Sign(s) * Math.Pow(Math.Abs(s), 2 / n);
                pts.Add(new Point(cx + r * x, cy + r * y));
            }
            return pts;
        }

        /// <summary>
        /// ¿El ícono ya llena su cuadro? (lo visible ocupa casi todo y las orillas son opacas). Recibe el canal
        /// alfa (0..255) fila por fila. Pura, se prueba.
        /// </summary>
        public static bool IsFullBleed(byte[] alpha, int w, int h)
        {
            if (w <= 0 || h <= 0 || alpha.Length < w * h) return false;
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (alpha[y * w + x] > 40)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
            if (maxX < 0) return false;
            double cover = (maxX - minX + 1) / (double)w * ((maxY - minY + 1) / (double)h);
            if (cover < 0.85) return false;
            // los bordes (a mitad de cada lado) tienen que ser opacos
            int mx = (minX + maxX) / 2, my = (minY + maxY) / 2;
            int[] edges = { alpha[my * w + minX], alpha[my * w + maxX], alpha[minY * w + mx], alpha[maxY * w + mx] };
            foreach (var a in edges) if (a < 200) return false;
            // y las esquinas (un poco hacia adentro) también: un ícono redondo NO llena su cuadro
            int ix = Math.Max(1, (maxX - minX) * 8 / 100), iy = Math.Max(1, (maxY - minY) * 8 / 100);
            int[] corners = { alpha[(minY + iy) * w + minX + ix], alpha[(minY + iy) * w + maxX - ix], alpha[(maxY - iy) * w + minX + ix], alpha[(maxY - iy) * w + maxX - ix] };
            foreach (var a in corners) if (a < 200) return false;
            return true;
        }

        /// <summary>Color promedio de lo visible, dando más peso a lo más colorido (BGRA). Pura, se prueba.</summary>
        public static Color Dominant(byte[] bgra)
        {
            double r = 0, g = 0, b = 0, wsum = 0;
            for (int i = 0; i + 3 < bgra.Length; i += 4)
            {
                double a = bgra[i + 3] / 255.0;
                if (a < 0.5) continue;
                double B = bgra[i], G = bgra[i + 1], R = bgra[i + 2];
                double max = Math.Max(R, Math.Max(G, B)), min = Math.Min(R, Math.Min(G, B));
                double sat = max <= 0 ? 0 : (max - min) / max;
                double w = a * (0.05 + sat * sat * 2); // 1.8.1: lo colorido manda más (que gane el color principal)
                r += R * w; g += G * w; b += B * w; wsum += w;
            }
            if (wsum <= 0) return Color.FromRgb(0x8E, 0x8E, 0x93);
            return Color.FromRgb((byte)(r / wsum), (byte)(g / wsum), (byte)(b / wsum));
        }

        /// <summary>
        /// 1.8.1: colores de la placa (arriba, abajo) a partir del color principal del ícono: mismo tono, con
        /// saturación y luz parejas para que se vea de color pero el ícono siga resaltando. Si el ícono es gris,
        /// placa gris clara. Pura, se prueba.
        /// </summary>
        public static (Color top, Color bottom) PlateColors(Color dom)
        {
            ToHsl(dom, out double hue, out double sat, out _);
            if (sat < 0.12) return (Color.FromRgb(0xF4, 0xF4, 0xF6), Color.FromRgb(0xD8, 0xD8, 0xDD));
            double s = Math.Clamp(sat, 0.45, 0.85);
            return (FromHsl(hue, s, 0.84), FromHsl(hue, s, 0.66));
        }

        public static void ToHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            if (max - min < 1e-9) { h = 0; s = 0; return; }
            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
            else if (max == g) h = ((b - r) / d + 2) / 6;
            else h = ((r - g) / d + 4) / 6;
        }

        public static Color FromHsl(double h, double s, double l)
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            double T(double t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1 / 6.0) return p + (q - p) * 6 * t;
                if (t < 0.5) return q;
                if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6;
                return p;
            }
            return Color.FromRgb((byte)Math.Round(T(h + 1 / 3.0) * 255), (byte)Math.Round(T(h) * 255), (byte)Math.Round(T(h - 1 / 3.0) * 255));
        }

        /// <summary>Mezcla un color con blanco (k = 0 el color, 1 blanco). Pura.</summary>
        public static Color Mix(Color c, double k) => Color.FromRgb(
            (byte)Math.Round(c.R + (255 - c.R) * k), (byte)Math.Round(c.G + (255 - c.G) * k), (byte)Math.Round(c.B + (255 - c.B) * k));

        /// <summary>Devuelve el ícono con forma de Mac, del mismo tamaño en píxeles.</summary>
        public static BitmapSource? Shape(BitmapSource src, int sizePx)
        {
            try
            {
                int size = Math.Max(16, sizePx);
                // leer los píxeles (a un tamaño chico alcanza para analizar)
                var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
                int w = conv.PixelWidth, h = conv.PixelHeight;
                var px = new byte[w * h * 4];
                conv.CopyPixels(px, w * 4, 0);
                var alpha = new byte[w * h];
                for (int i = 0; i < alpha.Length; i++) alpha[i] = px[i * 4 + 3];
                bool full = IsFullBleed(alpha, w, h);

                double plate = size * PlateScale;
                double cx = size / 2.0, cy = size / 2.0;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    var pts = SquirclePoints(cx, cy, plate / 2);
                    ctx.BeginFigure(pts[0], true, true);
                    ctx.PolyLineTo(pts.GetRange(1, pts.Count - 1), true, true);
                }
                geo.Freeze();

                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    var box = new Rect(cx - plate / 2, cy - plate / 2, plate, plate);
                    if (full)
                    {
                        dc.PushClip(geo);
                        dc.DrawImage(src, box);
                        dc.Pop();
                    }
                    else
                    {
                        var dom = Dominant(px);
                        var (top, bottom) = PlateColors(dom); // 1.8.1: un color por app
                        var bg = new LinearGradientBrush(top, bottom, 90);
                        bg.Freeze();
                        dc.DrawGeometry(bg, null, geo);
                        double g = plate * GlyphScale;
                        dc.DrawImage(src, new Rect(cx - g / 2, cy - g / 2, g, g));
                    }
                    var edge = new Pen(new SolidColorBrush(Color.FromArgb(0x26, 0, 0, 0)), Math.Max(0.6, size / 128.0));
                    edge.Freeze();
                    dc.DrawGeometry(null, edge, geo);
                }
                var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
                rtb.Render(dv);
                rtb.Freeze();
                return rtb;
            }
            catch (Exception ex) { Log.Write("Forma de ícono: " + ex.Message); return null; }
        }
    }
}
