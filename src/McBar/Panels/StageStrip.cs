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
using System.Windows.Threading;
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
        // 1.9.14: traer una ventana al frente al dejar un archivo arrastrado un momento sobre su miniatura
        private readonly DispatcherTimer _spring = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        private StageManager.Stage? _springOver;

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
            // 1.9.4: fondo 100 % transparente: solo las miniaturas reciben el mouse. Antes la franja entera (de arriba
            // a abajo, 200 de ancho) tapaba lo que había debajo: no se podían tocar los íconos del escritorio.
            Content = new Grid { Background = null, Children = { _list } };
            Opacity = 0;
            Visibility = Visibility.Hidden;
            SourceInitialized += (_, _) => WindowOps.MakeToolNoActivate(this);
            AllowDrop = true; // 1.9.14: recibir el archivo arrastrado (no lo toma; solo trae la ventana al frente)
            _spring.Tick += (_, _) => { _spring.Stop(); if (_springOver != null) StageManager.Bring(_springOver); };
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
                // 1.9.16: crucita para cerrar la app (aparece al pasar el mouse por la miniatura), como en macOS
                var close = new Border
                {
                    Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(-6, -6, 0, 0), Cursor = Cursors.Hand, Opacity = 0, Visibility = Visibility.Collapsed,
                    Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3E)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)), BorderThickness = new Thickness(1),
                    Child = new TextBlock { Text = "×", FontSize = 13, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -2, 0, 0) },
                    ToolTip = "Cerrar"
                };
                close.MouseLeftButtonUp += (_, e) => { e.Handled = true; StageManager.Close(stage); };
                grid.Children.Add(close);

                var scale = new ScaleTransform(1, 1);
                grid.RenderTransform = scale;
                grid.MouseEnter += (_, _) => { close.Visibility = Visibility.Visible; close.IsHitTestVisible = true; close.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))); scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(120))); scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(120))); };
                grid.MouseLeave += (_, _) => { close.IsHitTestVisible = false; close.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(120))); scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))); scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))); };
                grid.MouseLeftButtonUp += (_, e) => { e.Handled = true; StageManager.Bring(stage); };
                // 1.9.14: resorte al arrastrar (como la barra de Windows): se para un momento sobre la miniatura
                // con un archivo y esa ventana viene al frente para soltarlo adentro.
                grid.AllowDrop = true;
                grid.DragEnter += (_, e) => { _springOver = stage; _spring.Stop(); _spring.Start(); e.Effects = DragDropEffects.None; e.Handled = true; };
                grid.DragOver += (_, e) => { e.Effects = DragDropEffects.None; e.Handled = true; };
                grid.DragLeave += (_, _) => { if (_springOver == stage) { _springOver = null; _spring.Stop(); } };
                grid.Drop += (_, e) => { _spring.Stop(); _springOver = null; StageManager.Bring(stage); e.Handled = true; };
                _list.Children.Add(grid);
            }
        }
    }
}
