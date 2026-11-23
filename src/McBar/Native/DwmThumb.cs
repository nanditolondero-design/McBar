// McBar — Creado por Andrés Londero
// Miniaturas en vivo de ventanas (0.8.0) con DWM, lo mismo que usa la barra de Windows: la imagen la
// dibuja Windows (en vivo, sin copiar píxeles) encima de nuestra ventana, en el rectángulo que digamos.
using System;
using System.Runtime.InteropServices;

namespace McBar.Native
{
    internal sealed class DwmThumb : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Props
        {
            public uint dwFlags;
            public Win32.RECT rcDestination;
            public Win32.RECT rcSource;
            public byte opacity;
            public int fVisible;
            public int fSourceClientAreaOnly;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PSize { public int x, y; }

        [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
        [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(IntPtr thumb);
        [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref Props props);
        [DllImport("dwmapi.dll")] private static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out PSize size);

        private const uint RECTDESTINATION = 0x1, OPACITY = 0x4, VISIBLE = 0x8, SOURCECLIENTAREAONLY = 0x10;
        private IntPtr _h;

        public IntPtr Source { get; }

        public DwmThumb(IntPtr dest, IntPtr source)
        {
            Source = source;
            if (DwmRegisterThumbnail(dest, source, out _h) != 0) _h = IntPtr.Zero;
        }

        public bool Ok => _h != IntPtr.Zero;

        /// <summary>Tamaño real de la ventana de origen (píxeles), para mantener la proporción.</summary>
        public (int w, int h) SourceSize()
        {
            if (_h == IntPtr.Zero || DwmQueryThumbnailSourceSize(_h, out var s) != 0) return (0, 0);
            return (s.x, s.y);
        }

        /// <summary>Muestra la miniatura dentro del rectángulo (píxeles de nuestra ventana), con la proporción justa.</summary>
        public void Show(int left, int top, int width, int height, byte opacity = 255)
        {
            if (_h == IntPtr.Zero) return;
            var (sw, sh) = SourceSize();
            // 1.3.1: una ventana minimizada informa el tamaño del botoncito minimizado (≈160×28) y la
            // miniatura quedaba finita o vacía: se usa su tamaño normal (restaurada) para la proporción.
            if (Win32.IsIconic(Source))
            {
                var (nw, nh) = Win32.NormalSize(Source);
                if (nw > 0 && nh > 0) { sw = nw; sh = nh; }
            }
            if (sw > 0 && sh > 0)
            {
                var (x, y, w, h) = Fit(sw, sh, width, height);
                left += x; top += y; width = w; height = h;
            }
            var p = new Props
            {
                dwFlags = RECTDESTINATION | OPACITY | VISIBLE | SOURCECLIENTAREAONLY,
                rcDestination = new Win32.RECT { left = left, top = top, right = left + width, bottom = top + height },
                opacity = opacity,
                fVisible = 1,
                fSourceClientAreaOnly = 0
            };
            DwmUpdateThumbnailProperties(_h, ref p);
        }

        public void HideThumb()
        {
            if (_h == IntPtr.Zero) return;
            var p = new Props { dwFlags = VISIBLE, fVisible = 0 };
            DwmUpdateThumbnailProperties(_h, ref p);
        }

        /// <summary>Encaja un tamaño dentro de un cuadro sin deformarlo y centrado (pura, se prueba).</summary>
        public static (int x, int y, int w, int h) Fit(int srcW, int srcH, int boxW, int boxH)
        {
            if (srcW <= 0 || srcH <= 0 || boxW <= 0 || boxH <= 0) return (0, 0, boxW, boxH);
            double k = Math.Min(boxW / (double)srcW, boxH / (double)srcH);
            int w = (int)Math.Round(srcW * k), h = (int)Math.Round(srcH * k);
            return ((boxW - w) / 2, (boxH - h) / 2, w, h);
        }

        public void Dispose()
        {
            if (_h != IntPtr.Zero) DwmUnregisterThumbnail(_h);
            _h = IntPtr.Zero;
        }
    }
}
