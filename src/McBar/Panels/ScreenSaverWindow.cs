// McBar — Creado por Andrés Londero
// 1.9.27: Protector de pantalla simple (como macOS): fondo negro con un reloj grande que cada tanto cambia de
// lugar (para no quemar la pantalla). Se cierra con cualquier tecla, clic o movimiento del mouse.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class ScreenSaverWindow : Window
    {
        private static ScreenSaverWindow? _current;
        public static void Show()
        {
            if (_current != null) return;
            try { _current = new ScreenSaverWindow(); _current.Closed += (_, _) => _current = null; _current.Show(); }
            catch (Exception ex) { Core.Log.Write("Protector: " + ex.Message); }
        }

        private readonly TextBlock _clock = new TextBlock { Foreground = Brushes.White, FontSize = 110, FontWeight = FontWeights.Light };
        private readonly TextBlock _date = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAE)), FontSize = 30, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        private readonly Grid _stack = new Grid();
        private readonly StackPanel _box = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        private readonly DispatcherTimer _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly Random _rng = new Random();
        private Point? _mouseStart;
        private DateTime _shown;
        private int _driftAt;

        private ScreenSaverWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = Brushes.Black;
            Topmost = true;
            ShowInTaskbar = false;
            FontFamily = (FontFamily)FindResource("Mc.Font");
            Cursor = Cursors.None;
            // cubrir todos los monitores (escritorio virtual)
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            _clock.HorizontalAlignment = HorizontalAlignment.Center;
            _box.Children.Add(_clock);
            _box.Children.Add(_date);
            _stack.Children.Add(_box);
            Content = _stack;

            _tick.Tick += (_, _) => Update();
            Loaded += (_, _) =>
            {
                _shown = DateTime.Now;
                Update();
                _tick.Start();
                WindowOps.Activate(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                Focus();
                PlaceBox();
            };
            KeyDown += (_, _) => Dismiss();
            MouseDown += (_, _) => Dismiss();
            MouseMove += (_, e) =>
            {
                var p = e.GetPosition(this);
                if (_mouseStart == null) { _mouseStart = p; return; }
                if ((DateTime.Now - _shown).TotalMilliseconds > 600 &&
                    (Math.Abs(p.X - _mouseStart.Value.X) > 10 || Math.Abs(p.Y - _mouseStart.Value.Y) > 10))
                    Dismiss();
            };
            Closed += (_, _) => _tick.Stop();
        }

        private void Update()
        {
            var now = DateTime.Now;
            _clock.Text = now.ToString("HH:mm");
            _date.Text = now.ToString("dddd d 'de' MMMM");
            if (++_driftAt >= 20) { _driftAt = 0; PlaceBox(); } // cada ~20 s cambia de lugar
        }

        private void PlaceBox()
        {
            // mueve el bloque a una posición al azar, con margen, animando
            double mw = Math.Max(0, Width - 700), mh = Math.Max(0, Height - 260);
            double x = _rng.NextDouble() * mw - mw / 2;
            double y = _rng.NextDouble() * mh - mh / 2;
            var tt = _box.RenderTransform as TranslateTransform ?? new TranslateTransform();
            _box.RenderTransform = tt;
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, TimeSpan.FromSeconds(2)) { EasingFunction = new QuadraticEase() });
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, TimeSpan.FromSeconds(2)) { EasingFunction = new QuadraticEase() });
        }

        private bool _closing;
        private void Dismiss()
        {
            if (_closing) return;
            _closing = true;
            Close();
        }
    }
}
