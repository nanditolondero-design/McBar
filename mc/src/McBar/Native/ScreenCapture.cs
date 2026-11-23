// McBar — Creado por Andrés Londero
// 1.8.0 — Sacar imágenes de la pantalla (para las capturas estilo Mac): una zona de la pantalla con BitBlt, o
// una ventana entera con PrintWindow (aunque esté tapada por otra). Todo en píxeles reales.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace McBar.Native
{
    internal static class ScreenCapture
    {
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out Win32.RECT value, int size);
        private const uint SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000, PW_RENDERFULLCONTENT = 2;
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        /// <summary>Zona de la pantalla (coordenadas de pantalla en píxeles).</summary>
        public static BitmapSource? Region(int x, int y, int w, int h)
        {
            if (w <= 0 || h <= 0) return null;
            var screen = Win32.GetDC(IntPtr.Zero);
            var mem = CreateCompatibleDC(screen);
            var bmp = CreateCompatibleBitmap(screen, w, h);
            var old = SelectObject(mem, bmp);
            try
            {
                BitBlt(mem, 0, 0, w, h, screen, x, y, SRCCOPY | CAPTUREBLT);
                SelectObject(mem, old);
                return ToSource(bmp);
            }
            finally
            {
                DeleteDC(mem);
                Win32.ReleaseDC(IntPtr.Zero, screen);
                Win32.DeleteObject(bmp);
            }
        }

        /// <summary>Lo que se ve de la ventana (sin el borde invisible de Windows), en píxeles de pantalla.</summary>
        public static Win32.RECT VisibleBounds(IntPtr hwnd)
        {
            if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var r, Marshal.SizeOf<Win32.RECT>()) == 0 && r.right > r.left) return r;
            Win32.GetWindowRect(hwnd, out r);
            return r;
        }

        /// <summary>La ventana entera, aunque esté tapada (PrintWindow). Si no se puede, null.</summary>
        public static BitmapSource? WindowImage(IntPtr hwnd)
        {
            if (!Win32.GetWindowRect(hwnd, out var wr)) return null;
            var vis = VisibleBounds(hwnd);
            int w = wr.right - wr.left, h = wr.bottom - wr.top;
            if (w <= 0 || h <= 0) return null;
            var screen = Win32.GetDC(IntPtr.Zero);
            var mem = CreateCompatibleDC(screen);
            var bmp = CreateCompatibleBitmap(screen, w, h);
            var old = SelectObject(mem, bmp);
            try
            {
                if (!PrintWindow(hwnd, mem, PW_RENDERFULLCONTENT)) return null;
                SelectObject(mem, old);
                var full = ToSource(bmp);
                if (full == null) return null;
                // recortar el borde invisible (las ventanas de Windows 10/11 tienen ~7 px transparentes alrededor)
                int cx = Math.Max(0, vis.left - wr.left), cy = Math.Max(0, vis.top - wr.top);
                int cw = Math.Min(w - cx, vis.right - vis.left), ch = Math.Min(h - cy, vis.bottom - vis.top);
                if (cw <= 0 || ch <= 0) return full;
                var crop = new CroppedBitmap(full, new Int32Rect(cx, cy, cw, ch));
                crop.Freeze();
                return crop;
            }
            catch { return null; }
            finally
            {
                DeleteDC(mem);
                Win32.ReleaseDC(IntPtr.Zero, screen);
                Win32.DeleteObject(bmp);
            }
        }

        private static BitmapSource? ToSource(IntPtr hbmp)
        {
            var src = Imaging.CreateBitmapSourceFromHBitmap(hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }

        /// <summary>Ventanas visibles de arriba hacia abajo (para elegir una con el mouse). No incluye las de McBar.</summary>
        public static List<(IntPtr hwnd, Win32.RECT rect)> TopWindows()
        {
            var list = new List<(IntPtr, Win32.RECT)>();
            uint own = (uint)Environment.ProcessId;
            Win32.EnumWindows((h, _) =>
            {
                if (!Win32.IsWindowVisible(h) || Win32.IsIconic(h)) return true;
                if (Win32.ProcessOf(h) == own) return true;
                if (Win32.DwmGetWindowAttribute(h, Win32.DWMWA_CLOAKED, out int cloaked, 4) == 0 && cloaked != 0) return true;
                var cls = WindowOps.GetClass(h);
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return true;
                var r = VisibleBounds(h);
                if (r.right - r.left < 40 || r.bottom - r.top < 30) return true;
                list.Add((h, r));
                return true;
            }, IntPtr.Zero);
            return list;
        }
    }
}
