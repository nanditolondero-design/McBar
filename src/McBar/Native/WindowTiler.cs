// McBar — Creado por Andrés Londero
// Aplica un mosaico (Core/Tiling) a una ventana real (1.0.0). Windows 10/11 dibuja un borde invisible
// alrededor de las ventanas: se compensa con DWMWA_EXTENDED_FRAME_BOUNDS para que queden justas.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using McBar.Core;

namespace McBar.Native
{
    internal static class WindowTiler
    {
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out Win32.RECT value, int size);

        // tamaño antes del mosaico, para "Volver al tamaño anterior"
        private static readonly Dictionary<IntPtr, Win32.RECT> Before = new Dictionary<IntPtr, Win32.RECT>();

        public static void Apply(IntPtr hwnd, TileLayout layout)
        {
            if (hwnd == IntPtr.Zero) return;
            var m = Monitors.FromWindow(hwnd);
            if (m == null) return;
            if (Win32.IsIconic(hwnd) || Win32.IsZoomed(hwnd)) Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
            if (Win32.GetWindowRect(hwnd, out var wr) && !Before.ContainsKey(hwnd)) Before[hwnd] = wr;
            var r = Tiling.Compute(layout, m.Work.Left, m.Work.Top, m.Work.Width, m.Work.Height, (int)Math.Round(6 * App.Scale));
            // borde invisible = diferencia entre GetWindowRect y el marco visible
            int bl = 0, bt = 0, br = 0, bb = 0;
            if (Win32.GetWindowRect(hwnd, out var full) && DwmGetWindowAttribute(hwnd, 9 /*EXTENDED_FRAME_BOUNDS*/, out var vis, Marshal.SizeOf<Win32.RECT>()) == 0)
            {
                bl = vis.left - full.left; bt = vis.top - full.top; br = full.right - vis.right; bb = full.bottom - vis.bottom;
            }
            Win32.SetWindowPos(hwnd, IntPtr.Zero, r.X - bl, r.Y - bt, r.W + bl + br, r.H + bt + bb, 0x0004 /*NOZORDER*/ | 0x0010 /*NOACTIVATE*/);
        }

        public static void Restore(IntPtr hwnd)
        {
            if (!Before.TryGetValue(hwnd, out var r)) return;
            Before.Remove(hwnd);
            Win32.SetWindowPos(hwnd, IntPtr.Zero, r.left, r.top, r.right - r.left, r.bottom - r.top, 0x0004 | 0x0010);
        }

        public static bool CanRestore(IntPtr hwnd) => Before.ContainsKey(hwnd);
    }
}
