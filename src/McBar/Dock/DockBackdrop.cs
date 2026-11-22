// McBar — Creado por Andrés Londero
// Desenfoque real (acrílico) detrás de la barra del Dock.
// Es una ventanita aparte, del tamaño justo de la barra y con las esquinas recortadas, porque el
// desenfoque de Windows se aplica a toda la ventana y la ventana del Dock es más grande que la barra.
// Solo se mueve cuando cambia el tamaño de la barra (no en cada cuadro de la ampliación).
//
// 0.3.2: ahora es una ventana COMÚN (no "en capas"): en las ventanas en capas Windows ignora el recorte
// redondeado y se veían las esquinas cuadradas del desenfoque. En una ventana común el recorte (región)
// sí se respeta. El fondo se hace transparente extendiendo el "vidrio" de Windows a toda la ventana.
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using McBar.Core;
using McBar.Native;

namespace McBar.Dock
{
    internal sealed class DockBackdrop : Window
    {
        private int _x, _y, _w, _h, _r;
        private IntPtr _hwnd;

        public DockBackdrop()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = false;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Width = 10;
            Height = 10;
            Left = -10000;
            Top = -10000;
            Title = "McBar Dock (fondo)";
            IsHitTestVisible = false;

            SourceInitialized += (_, _) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                WindowOps.MakeToolNoActivate(this);
                // Fondo transparente en una ventana común: vidrio de Windows en toda la ventana.
                var src = HwndSource.FromHwnd(_hwnd);
                if (src?.CompositionTarget != null) src.CompositionTarget.BackgroundColor = Colors.Transparent;
                var m = new Win32.MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
                Win32.DwmExtendFrameIntoClientArea(_hwnd, ref m);
                ApplyBlur();
            };
            ThemeManager.ThemeChanged += ApplyBlur;
            Closed += (_, _) => ThemeManager.ThemeChanged -= ApplyBlur;
        }

        private void ApplyBlur()
        {
            if (_hwnd == IntPtr.Zero) return;
            WindowOps.EnableBlur(this, ThemeManager.GetColor("Mc.BlurTint"));
        }

        /// <summary>Ubica el fondo (en píxeles de pantalla) con esquinas redondeadas de radio <paramref name="radius"/>.</summary>
        public void Place(int x, int y, int w, int h, int radius)
        {
            if (_hwnd == IntPtr.Zero) return;
            if (x == _x && y == _y && w == _w && h == _h && radius == _r) return;
            bool resized = w != _w || h != _h || radius != _r;
            _x = x; _y = y; _w = w; _h = h; _r = radius;
            Win32.SetWindowPos(_hwnd, IntPtr.Zero, x, y, Math.Max(1, w), Math.Max(1, h), Win32.SWP_NOACTIVATE | Win32.SWP_NOZORDER);
            if (resized)
            {
                var rgn = Win32.CreateRoundRectRgn(0, 0, w + 1, h + 1, radius * 2, radius * 2);
                Win32.SetWindowRgn(_hwnd, rgn, true); // Windows se queda con la región, no hay que borrarla
            }
        }
    }
}
