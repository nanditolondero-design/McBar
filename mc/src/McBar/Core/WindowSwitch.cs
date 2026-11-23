// McBar — Creado por Andrés Londero
// Lógica pura de las ventanas estilo Mac (0.8.0), con pruebas:
//  - AppsByRecency: apps agrupadas (como el Cmd+Tab de macOS), la usada más recientemente primero.
//  - Step: moverse en la lista dando la vuelta.
//  - GridLayout: acomodar N miniaturas en una pantalla (vista de todas las ventanas, Win+Tab).
using System;
using System.Collections.Generic;
using System.Linq;

namespace McBar.Core
{
    public readonly struct Cell
    {
        public double X { get; init; }
        public double Y { get; init; }
        public double W { get; init; }
        public double H { get; init; }
    }

    public static class WindowSwitch
    {
        /// <summary>Apps (cada una con sus ventanas, la más reciente primero), ordenadas por uso reciente.</summary>
        public static List<List<TrackedWindow>> AppsByRecency(IEnumerable<TrackedWindow> windows) =>
            windows.GroupBy(w => string.IsNullOrEmpty(w.PrimaryKey) ? w.Hwnd.ToString() : w.PrimaryKey)
                   .Select(g => g.OrderByDescending(w => w.LastActivated).ToList())
                   .OrderByDescending(g => g[0].LastActivated)
                   .ToList();

        public static int Step(int index, int count, bool back)
        {
            if (count <= 0) return 0;
            return ((index + (back ? -1 : 1)) % count + count) % count;
        }

        /// <summary>
        /// Grilla para <paramref name="n"/> ventanas dentro de un área: elige columnas para que las celdas
        /// queden lo más grandes posible con forma de pantalla (16:10), centradas.
        /// </summary>
        public static List<Cell> GridLayout(int n, double areaW, double areaH, double gap)
        {
            var cells = new List<Cell>();
            if (n <= 0 || areaW <= 0 || areaH <= 0) return cells;
            double best = -1; int bestCols = 1;
            for (int cols = 1; cols <= n; cols++)
            {
                int rows = (int)Math.Ceiling(n / (double)cols);
                double cw = (areaW - gap * (cols - 1)) / cols, ch = (areaH - gap * (rows - 1)) / rows;
                double w = Math.Min(cw, ch * 1.6);
                if (w > best) { best = w; bestCols = cols; }
            }
            int c = bestCols, r = (int)Math.Ceiling(n / (double)c);
            double cellW = (areaW - gap * (c - 1)) / c, cellH = (areaH - gap * (r - 1)) / r;
            double usedH = r * cellH + (r - 1) * gap;
            for (int i = 0; i < n; i++)
            {
                int row = i / c, col = i % c;
                int inRow = row == r - 1 ? n - row * c : c; // la última fila, centrada
                double rowW = inRow * cellW + (inRow - 1) * gap;
                double x0 = (areaW - rowW) / 2;
                cells.Add(new Cell { X = x0 + col * (cellW + gap), Y = (areaH - usedH) / 2 + row * (cellH + gap), W = cellW, H = cellH });
            }
            return cells;
        }
    }
}
