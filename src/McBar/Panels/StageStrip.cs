// McBar — Creado por Andrés Londero
// 1.8.0 — Franja del Organizador visual (ver Core/StageManager): miniaturas de las apps recientes a la
// izquierda de la pantalla, cada una con el ícono de la app. Clic = traer esa app. No toma el foco.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class StageStrip : Window
    {
        public const double WidthDip = 200;
        public MonitorInfo Monitor { get; }
        private readonly StackPanel _list = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 10, 0) };
        private bool _shown;

        public StageStrip(MonitorInfo m)
        {
            Monitor = m;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            FontFamily = (FontFamily)FindResource("Mc.Font");
            double sc = m.Scale;
            WindowOps.PlaceOnMonitor(this, m, (m.Work.Left - m.Bounds.Left) / sc, (m.Work.Top - m.Bounds.Top) / sc, WidthDip, m.Work.Height / sc);
            Content = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), Children = { _list } };
            Opacity = 0;
            Visibility = Visibility.Hidden;
            SourceInitialized += (_, _) => WindowOps.MakeToolNoActivate(this);
            Refresh();
        }

        public void SetVisible(bool v)
        {
            if (v == _shown) return;
            _shown = v;
            if (v) { Visibility = Visibility.Visible; BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160))); }
            else
            {
                var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
                a.Completed += (_, _) => { if (!_shown) Visibility = Visibility.Hidden; };
                BeginAnimation(OpacityProperty, a);
            }
        }

        public void Refresh()
        {
            _list.Children.Clear();
            foreach (var s in StageManager.Recent.Where(x => x.Monitor == Monitor.Device))
            {
                var stage = s;
                var grid = new Grid { Width = 160, Margin = new Thickness(0, 10, 0, 10), Cursor = Cursors.Hand, RenderTransformOrigin = new Point(0.5, 0.5) };
                var shot = new Border
                {
                    CornerRadius = new CornerRadius(8), Height = 100, ClipToBounds = true, BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)),
                    Background = new SolidColorBrush(Color.FromArgb(0x60, 0x20, 0x20, 0x24)),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.4 }
                };
                if (s.Snapshot != null)
                {
                    shot.Height = Math.Clamp(160.0 * s.Snapshot.PixelHeight / Math.Max(1, s.Snapshot.PixelWidth), 60, 130);
                    shot.Background = new ImageBrush(s.Snapshot) { Stretch = Stretch.UniformToFill };
                }
                grid.Children.Add(shot);
                if (s.Sample != null)
                {
                    var icon = WinUi.AppIcon(s.Sample, 34);
                    icon.HorizontalAlignment = HorizontalAlignment.Left;
                    icon.VerticalAlignment = VerticalAlignment.Bottom;
                    icon.Margin = new Thickness(-10, 0, 0, -10);
                    grid.Children.Add(icon);
                    grid.ToolTip = App.Tracker.AppNameFor(s.Sample.Hwnd);
                }
                var scale = new ScaleTransform(1, 1);
                grid.RenderTransform = scale;
                grid.MouseEnter += (_, _) => { scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(120))); scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(120))); };
                grid.MouseLeave += (_, _) => { scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))); scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))); };
                grid.MouseLeftButtonUp += (_, e) => { e.Handled = true; StageManager.Bring(stage); };
                _list.Children.Add(grid);
            }
        }
    }
}
