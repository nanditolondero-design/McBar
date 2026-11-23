// McBar — Creado por Andrés Londero
// Ventanas en mosaico (1.0.0), como "Ventana → Mover y cambiar tamaño" de macOS Sequoia: mitades,
// cuartos, tercios, llenar y centrar. Se usan desde el menú "Ventana" de la barra y con atajos
// (Ctrl+Alt+flechas / letras, ver Native/KeyboardHook). La cuenta del rectángulo es pura (con pruebas);
// aplicarlo a la ventana está en Native/WindowTiler.
using System;

namespace McBar.Core
{
    public enum TileLayout
    {
        Left, Right, Top, Bottom,
        TopLeft, TopRight, BottomLeft, BottomRight,
        LeftThird, CenterThird, RightThird, LeftTwoThirds, RightTwoThirds,
        Fill, Center
    }

    public readonly struct TileRect
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int W { get; init; }
        public int H { get; init; }
    }

    public static class Tiling
    {
        /// <summary>Rectángulo dentro del área de trabajo, con un margen chico entre ventanas (como macOS).</summary>
        public static TileRect Compute(TileLayout l, int left, int top, int width, int height, int gap = 6)
        {
            double x = 0, y = 0, w = 1, h = 1;
            switch (l)
            {
                case TileLayout.Left: w = 0.5; break;
                case TileLayout.Right: x = 0.5; w = 0.5; break;
                case TileLayout.Top: h = 0.5; break;
                case TileLayout.Bottom: y = 0.5; h = 0.5; break;
                case TileLayout.TopLeft: w = 0.5; h = 0.5; break;
                case TileLayout.TopRight: x = 0.5; w = 0.5; h = 0.5; break;
                case TileLayout.BottomLeft: y = 0.5; w = 0.5; h = 0.5; break;
                case TileLayout.BottomRight: x = 0.5; y = 0.5; w = 0.5; h = 0.5; break;
                case TileLayout.LeftThird: w = 1 / 3.0; break;
                case TileLayout.CenterThird: x = 1 / 3.0; w = 1 / 3.0; break;
                case TileLayout.RightThird: x = 2 / 3.0; w = 1 / 3.0; break;
                case TileLayout.LeftTwoThirds: w = 2 / 3.0; break;
                case TileLayout.RightTwoThirds: x = 1 / 3.0; w = 2 / 3.0; break;
                case TileLayout.Fill: break;
                case TileLayout.Center: x = 0.15; y = 0.1; w = 0.7; h = 0.8; break;
            }
            int px = left + (int)Math.Round(x * width), py = top + (int)Math.Round(y * height);
            int pr = left + (int)Math.Round((x + w) * width), pb = top + (int)Math.Round((y + h) * height);
            // margen: completo contra los bordes de la pantalla, la mitad entre ventanas
            int gl = x <= 0.001 ? gap : gap / 2, gt = y <= 0.001 ? gap : gap / 2;
            int gr = x + w >= 0.999 ? gap : gap / 2, gb = y + h >= 0.999 ? gap : gap / 2;
            return new TileRect { X = px + gl, Y = py + gt, W = Math.Max(100, pr - px - gl - gr), H = Math.Max(80, pb - py - gt - gb) };
        }
    }
}
