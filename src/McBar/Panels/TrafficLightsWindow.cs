// McBar — Creado por Andrés Londero
// 1.8.0 — Los tres botones de colores que McBar pone sobre la ventana activa (ver Core/TrafficLights).
// Ventanita propia, siempre encima, que no toma el foco. Al pasar el mouse muestran ×, − y ⤢ como en macOS.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class TrafficLightsWindow : Window
    {
        public IntPtr Target { get; set; }
        public IntPtr Handle => new WindowInteropHelper(this).Handle;
        private readonly TextBlock[] _glyphs = new TextBlock[3];
        private int _x = int.MinValue, _y;

        public TrafficLightsWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Width = 62; Height = 18;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
            (Color fill, Color edge, string glyph, string tip, Action act)[] b =
            {
                (Color.FromRgb(0xFF, 0x5F, 0x57), Color.FromRgb(0xE0, 0x44, 0x3E), "×", "Cerrar", () => WindowOps.Close(Target)),
                (Color.FromRgb(0xFE, 0xBC, 0x2E), Color.FromRgb(0xDE, 0xA1, 0x23), "−", "Minimizar", () => WindowOps.Minimize(Target)),
                (Color.FromRgb(0x28, 0xC8, 0x40), Color.FromRgb(0x1A, 0xAB, 0x29), "+", "Maximizar", () => WindowOps.ToggleZoom(Target)),
            };
            for (int i = 0; i < 3; i++)
            {
                var (fill, edge, glyph, tip, act) = b[i];
                var g = new Grid { Width = 12, Height = 12, Margin = new Thickness(i == 0 ? 2 : 8, 3, 0, 3), Cursor = Cursors.Arrow, ToolTip = tip };
                g.Children.Add(new Ellipse { Fill = new SolidColorBrush(fill), Stroke = new SolidColorBrush(edge), StrokeThickness = 0.6 });
                var t = new TextBlock { Text = glyph, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 0x40, 0x10, 0x00)),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -2, 0, 0), Visibility = Visibility.Hidden };
                _glyphs[i] = t;
                g.Children.Add(t);
                var action = act;
                g.MouseLeftButtonUp += (_, e) => { e.Handled = true; try { action(); } catch { } };
                row.Children.Add(g);
            }
            row.MouseEnter += (_, _) => { foreach (var t in _glyphs) t.Visibility = Visibility.Visible; };
            row.MouseLeave += (_, _) => { foreach (var t in _glyphs) t.Visibility = Visibility.Hidden; };
            Content = row;
            SourceInitialized += (_, _) => WindowOps.MakeToolNoActivate(this);
        }

        /// <summary>Ubica la ventanita en píxeles de pantalla (esquina de arriba a la izquierda).</summary>
        public void PlaceAt(int x, int y)
        {
            if (!IsVisible) { Show(); _x = int.MinValue; }
            if (x == _x && y == _y) return;
            _x = x; _y = y;
            var h = Handle;
            if (h != IntPtr.Zero) Win32.SetWindowPos(h, (IntPtr)(-1) /*TOPMOST*/, x, y, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
    }
}
