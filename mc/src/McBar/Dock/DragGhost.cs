// McBar — Creado por Andrés Londero
// 1.4.1 — "Fantasma" del ícono que se arrastra fuera del Dock. Antes el ícono se dibujaba dentro de la
// ventana del Dock (que mide poco de alto) y si se lo llevaba unos centímetros más arriba desaparecía.
// Ahora, apenas sale del Dock, el ícono sigue al mouse en una ventanita propia por TODA la pantalla, con el
// cartel "Quitar del Dock" / "Cerrar app", y al soltarlo hace la nubecita ahí mismo.
// La ventanita no recibe clics (WS_EX_TRANSPARENT) ni toma el foco.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using McBar.Core;
using McBar.Native;

namespace McBar.Dock
{
    internal sealed class DragGhost : Window
    {
        private const long WS_EX_TRANSPARENT = 0x20;
        private readonly Image _icon = new Image();
        private readonly Border _label = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 3, 9, 4), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Hidden };
        private readonly TextBlock _labelText = new TextBlock { Foreground = Brushes.White, FontSize = 12.5 };
        private readonly double _size;
        private bool _done;

        public DragGhost(ImageSource? icon, double size)
        {
            _size = size;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            SizeToContent = SizeToContent.Manual;
            Width = Math.Max(size, 160);
            Height = size + 40;
            SetResourceReference(FontFamilyProperty, "Mc.Font");

            _label.Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x1E, 0x1E, 0x22));
            _label.Child = _labelText;
            _icon.Source = icon;
            _icon.Width = _icon.Height = size;
            _icon.HorizontalAlignment = HorizontalAlignment.Center;
            _icon.RenderTransformOrigin = new Point(0.5, 0.5);
            _icon.RenderTransform = new ScaleTransform(1, 1);
            RenderOptions.SetBitmapScalingMode(_icon, BitmapScalingMode.HighQuality);
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            sp.Children.Add(_label);
            sp.Children.Add(_icon);
            Content = sp;

            SourceInitialized += (_, _) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                Win32.SetExStyle(h, Win32.GetExStyle(h) | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
            };
        }

        /// <summary>Lleva el ícono al mouse (centro del ícono = punta del mouse). Coordenadas de pantalla en píxeles.</summary>
        public void MoveTo(int screenX, int screenY, string? label, double opacity)
        {
            if (_done) return;
            // 1.5.0: en el monitor donde está el mouse, con su escala
            var m = Monitors.FromPoint(screenX, screenY) ?? Monitors.All()[0];
            double sc = m.Scale;
            WindowOps.PlaceOnMonitor(this, m, (screenX - m.Bounds.Left) / sc - Width / 2, (screenY - m.Bounds.Top) / sc - (Height - _size / 2));
            _icon.Opacity = opacity;
            if (string.IsNullOrEmpty(label)) _label.Visibility = Visibility.Hidden;
            else { _labelText.Text = label; _label.Visibility = Visibility.Visible; }
            if (!IsVisible) Show();
        }

        /// <summary>Nubecita: el ícono se agranda y se desvanece donde se soltó, y la ventanita se cierra.</summary>
        public void Poof()
        {
            if (_done) return;
            _done = true;
            _label.Visibility = Visibility.Hidden;
            var dur = TimeSpan.FromMilliseconds(280);
            var grow = new DoubleAnimation(1, 1.8, dur);
            var fade = new DoubleAnimation(_icon.Opacity, 0, dur);
            fade.Completed += (_, _) => Close();
            _icon.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            _icon.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            _icon.BeginAnimation(OpacityProperty, fade);
            // unas bolitas de "humo" alrededor, como en macOS
            if (Content is StackPanel sp)
            {
                var host = new Grid();
                Content = null;
                host.Children.Add(sp);
                var puff = new Canvas { IsHitTestVisible = false };
                host.Children.Add(puff);
                Content = host;
                var rnd = new Random();
                double cx = Width / 2, cy = Height - _size / 2;
                for (int i = 0; i < 8; i++)
                {
                    double r = _size * (0.16 + rnd.NextDouble() * 0.1);
                    var e = new Ellipse { Width = r * 2, Height = r * 2, Fill = new RadialGradientBrush(Color.FromArgb(230, 250, 250, 252), Color.FromArgb(0, 230, 230, 235)) };
                    Canvas.SetLeft(e, cx - r);
                    Canvas.SetTop(e, cy - r);
                    var t = new TranslateTransform();
                    e.RenderTransform = t;
                    puff.Children.Add(e);
                    double ang = i * Math.PI / 4 + rnd.NextDouble() * 0.4, dist = _size * 0.55;
                    t.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, Math.Cos(ang) * dist, dur));
                    t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, Math.Sin(ang) * dist, dur));
                    e.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, dur));
                }
            }
        }

        /// <summary>Se canceló (volvió al Dock o se soltó sin quitar): se cierra sin animación.</summary>
        public void Cancel()
        {
            if (_done) return;
            _done = true;
            Close();
        }
    }
}
