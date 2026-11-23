// McBar — Creado por Andrés Londero
// 1.6.1 — "Manijita" de la isla en pantalla completa. Con un video o juego en pantalla completa la píldora
// se esconde, pero queda esta rayita arriba al centro (como la barra de inicio del iPhone) para saber que
// está ahí: al pasar el mouse por encima, la píldora vuelve; al sacar el mouse, se vuelve a esconder.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using McBar.Native;

namespace McBar.MenuBar
{
    internal sealed class PeekHandle : Window
    {
        private const double W = 180, H = 12;
        private readonly MonitorInfo _monitor;
        public event Action? Peek;

        public PeekHandle(MonitorInfo monitor)
        {
            _monitor = monitor;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            // casi transparente (no del todo) para que reciba el mouse en toda la franja
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            Width = W;
            Height = H;
            var line = new Border
            {
                Width = 54, Height = 5, CornerRadius = new CornerRadius(2.5),
                Background = new SolidColorBrush(Color.FromArgb(0xB0, 0xF2, 0xF2, 0xF5)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0)),
                BorderThickness = new Thickness(0.5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 0, 0),
                Opacity = 0.75
            };
            Content = line;
            SourceInitialized += (_, _) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                Win32.SetExStyle(h, Win32.GetExStyle(h) | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW);
            };
            MouseEnter += (_, _) => Peek?.Invoke();
        }

        public void ShowOnTop()
        {
            WindowOps.PlaceOnMonitor(this, _monitor, (_monitor.Bounds.Width / _monitor.Scale - W) / 2, 0);
            if (!IsVisible) Show();
            var h = new WindowInteropHelper(this).Handle;
            if (h != IntPtr.Zero) Win32.SetWindowPos(h, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
    }
}
