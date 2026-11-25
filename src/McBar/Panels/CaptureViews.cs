// McBar — Creado por Andrés Londero
// 1.8.0 — Ventanas de las capturas estilo Mac (ver Core/Screenshots):
//   - CaptureOverlay: la pantalla "congelada" para elegir una zona (o una ventana, con Espacio).
//   - CaptureBar: la barrita de Win+Mayús+5 (pantalla, ventana, zona).
//   - CaptureThumb: la miniatura que aparece abajo a la derecha después de capturar.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class CaptureOverlay : Window
    {
        private readonly MonitorInfo _m;
        private readonly BitmapSource _frozen;
        private bool _windowMode;
        private readonly Path _dim = new Path { Fill = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)), IsHitTestVisible = false };
        private readonly Rectangle _sel = new Rectangle { Stroke = Brushes.White, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        private readonly Rectangle _hover = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(0x40, 0x0A, 0x84, 0xFF)), Stroke = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)), StrokeThickness = 2, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        private readonly Border _sizeTip = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 3), Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1E, 0x1E, 0x22)), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        private readonly TextBlock _sizeText = new TextBlock { Foreground = Brushes.White, FontSize = 12 };
        private readonly Canvas _canvas = new Canvas();
        private Point? _start;
        private readonly List<(IntPtr hwnd, Win32.RECT rect)> _windows;
        private IntPtr _hoverHwnd;
        // 1.9.24: si está puesto, la zona elegida se le pasa acá (p. ej. para OCR) en vez de guardarse como captura
        private readonly Action<BitmapSource>? _onRegion;

        public CaptureOverlay(MonitorInfo m, BitmapSource frozen, bool windowMode, Action<BitmapSource>? onRegion = null)
        {
            _m = m; _frozen = frozen; _windowMode = windowMode; _onRegion = onRegion;
            _windows = ScreenCapture.TopWindows();
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Cursor = Cursors.Cross;
            FontFamily = (FontFamily)FindResource("Mc.Font");
            double w = m.Bounds.Width / m.Scale, h = m.Bounds.Height / m.Scale;
            WindowOps.PlaceOnMonitor(this, m, 0, 0, w, h);
            var bg = new Image { Source = frozen, Width = w, Height = h, Stretch = Stretch.Fill };
            _canvas.Width = w; _canvas.Height = h;
            _canvas.Children.Add(bg);
            _canvas.Children.Add(_dim);
            _canvas.Children.Add(_hover);
            _canvas.Children.Add(_sel);
            _sizeTip.Child = _sizeText;
            _canvas.Children.Add(_sizeTip);
            var hint = new Border
            {
                CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 6, 12, 7), Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1E, 0x1E, 0x22)), IsHitTestVisible = false,
                Child = new TextBlock { Text = "Arrastrá para elegir una zona · Espacio: elegir una ventana · Esc: cancelar", Foreground = Brushes.White, FontSize = 12.5 }
            };
            hint.Loaded += (_, _) => { Canvas.SetLeft(hint, (w - hint.ActualWidth) / 2); Canvas.SetTop(hint, 46); };
            _canvas.Children.Add(hint);
            Content = _canvas;
            UpdateDim(null);

            MouseLeftButtonDown += (_, e) =>
            {
                if (_windowMode) return;
                _start = e.GetPosition(_canvas);
                CaptureMouse();
            };
            MouseMove += (_, e) =>
            {
                var p = e.GetPosition(_canvas);
                if (_windowMode) { HoverWindow(p); return; }
                if (_start == null) return;
                var r = new Rect(_start.Value, p);
                Canvas.SetLeft(_sel, r.X); Canvas.SetTop(_sel, r.Y); _sel.Width = r.Width; _sel.Height = r.Height;
                _sel.Visibility = Visibility.Visible;
                UpdateDim(r);
                _sizeText.Text = $"{Math.Round(r.Width * _m.Scale)} × {Math.Round(r.Height * _m.Scale)}";
                _sizeTip.Visibility = Visibility.Visible;
                Canvas.SetLeft(_sizeTip, p.X + 14); Canvas.SetTop(_sizeTip, p.Y + 14);
            };
            MouseLeftButtonUp += (_, e) =>
            {
                if (_windowMode) { PickWindow(); return; }
                if (_start == null) return;
                ReleaseMouseCapture();
                var r = new Rect(_start.Value, e.GetPosition(_canvas));
                _start = null;
                int x = (int)Math.Round(r.X * _m.Scale), y = (int)Math.Round(r.Y * _m.Scale);
                int cw = (int)Math.Round(r.Width * _m.Scale), ch = (int)Math.Round(r.Height * _m.Scale);
                x = Math.Clamp(x, 0, _frozen.PixelWidth - 1); y = Math.Clamp(y, 0, _frozen.PixelHeight - 1);
                cw = Math.Min(cw, _frozen.PixelWidth - x); ch = Math.Min(ch, _frozen.PixelHeight - y);
                if (cw < 4 || ch < 4) { _sel.Visibility = Visibility.Collapsed; _sizeTip.Visibility = Visibility.Collapsed; UpdateDim(null); return; }
                var crop = new CroppedBitmap(_frozen, new Int32Rect(x, y, cw, ch));
                crop.Freeze();
                Close();
                if (_onRegion != null) _onRegion(crop); else Screenshots.Save(crop, _m);
            };
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
                else if (e.Key == Key.Space && _start == null)
                {
                    _windowMode = !_windowMode;
                    Cursor = _windowMode ? Cursors.Hand : Cursors.Cross;
                    _hover.Visibility = Visibility.Collapsed;
                    if (_windowMode && Win32.GetCursorPos(out var cp)) HoverWindow(new Point((cp.x - _m.Bounds.Left) / _m.Scale, (cp.y - _m.Bounds.Top) / _m.Scale));
                }
            };
            SourceInitialized += (_, _) => WindowOps.MakeTool(this);
            Loaded += (_, _) =>
            {
                WindowOps.Activate(new WindowInteropHelper(this).Handle);
                Focus();
                if (_windowMode && Win32.GetCursorPos(out var cp)) HoverWindow(new Point((cp.x - _m.Bounds.Left) / _m.Scale, (cp.y - _m.Bounds.Top) / _m.Scale));
            };
            Deactivated += (_, _) => { if (IsVisible) Close(); };
        }

        private void UpdateDim(Rect? hole)
        {
            var all = new RectangleGeometry(new Rect(0, 0, _canvas.Width, _canvas.Height));
            _dim.Data = hole == null ? all : new CombinedGeometry(GeometryCombineMode.Exclude, all, new RectangleGeometry(hole.Value));
        }

        private void HoverWindow(Point p)
        {
            int sx = _m.Bounds.Left + (int)Math.Round(p.X * _m.Scale), sy = _m.Bounds.Top + (int)Math.Round(p.Y * _m.Scale);
            foreach (var (h, r) in _windows)
            {
                if (sx < r.left || sx >= r.right || sy < r.top || sy >= r.bottom) continue;
                _hoverHwnd = h;
                double l = (r.left - _m.Bounds.Left) / _m.Scale, t = (r.top - _m.Bounds.Top) / _m.Scale;
                Canvas.SetLeft(_hover, l); Canvas.SetTop(_hover, t);
                _hover.Width = (r.right - r.left) / _m.Scale; _hover.Height = (r.bottom - r.top) / _m.Scale;
                _hover.Visibility = Visibility.Visible;
                UpdateDim(new Rect(l, t, _hover.Width, _hover.Height));
                return;
            }
            _hoverHwnd = IntPtr.Zero;
            _hover.Visibility = Visibility.Collapsed;
            UpdateDim(null);
        }

        private void PickWindow()
        {
            var h = _hoverHwnd;
            if (h == IntPtr.Zero) return;
            Close();
            var img = ScreenCapture.WindowImage(h);
            if (img == null)
            {
                // si la ventana no se deja (algunas apps), se usa lo que se veía en pantalla
                var r = ScreenCapture.VisibleBounds(h);
                int x = Math.Max(0, r.left - _m.Bounds.Left), y = Math.Max(0, r.top - _m.Bounds.Top);
                int cw = Math.Min(r.right - r.left, _frozen.PixelWidth - x), ch = Math.Min(r.bottom - r.top, _frozen.PixelHeight - y);
                if (cw < 4 || ch < 4) return;
                var c = new CroppedBitmap(_frozen, new Int32Rect(x, y, cw, ch)); c.Freeze();
                img = c;
            }
            if (_onRegion != null) _onRegion(img); else Screenshots.Save(Screenshots.WithShadow(img, _m.Scale), _m);
        }
    }

    /// <summary>Barrita de Win+Mayús+5, abajo al centro (como en macOS).</summary>
    internal sealed class CaptureBar : Window
    {
        private static CaptureBar? _open;

        public static void Toggle(MonitorInfo m)
        {
            if (_open != null) { _open.Close(); return; }
            _open = new CaptureBar(m);
            _open.Closed += (_, _) => _open = null;
            _open.Show();
        }

        private CaptureBar(MonitorInfo m)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = (FontFamily)FindResource("Mc.Font");
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            void Add(string text, Action a)
            {
                var b = new Button { Content = text, Margin = new Thickness(3), Padding = new Thickness(12, 6, 12, 6) };
                b.SetResourceReference(StyleProperty, "McFlatButton");
                b.Click += (_, _) =>
                {
                    Close();
                    // que la barrita ya no se vea en la captura
                    var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
                    t.Tick += (_, _) => { t.Stop(); a(); };
                    t.Start();
                };
                row.Children.Add(b);
            }
            Add("Capturar la pantalla", () => Screenshots.FullScreen(m));
            Add("Capturar una ventana", () => Screenshots.Interactive(m, windowMode: true));
            Add("Capturar una zona", () => Screenshots.Interactive(m, windowMode: false));
            var cancel = new Button { Content = "✕", Margin = new Thickness(3), Padding = new Thickness(10, 6, 10, 6), ToolTip = "Cancelar" };
            cancel.SetResourceReference(StyleProperty, "McFlatButton");
            cancel.Click += (_, _) => Close();
            row.Children.Add(cancel);
            var border = new Border { Child = row, Padding = new Thickness(6), Margin = new Thickness(16), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 4, Opacity = 0.35 } };
            border.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            border.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            Content = border;
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
            SourceInitialized += (_, _) => WindowOps.MakeTool(this);
            Loaded += (_, _) =>
            {
                double sc = m.Scale;
                WindowOps.PlaceOnMonitor(this, m, (m.Bounds.Width / sc - ActualWidth) / 2, m.Bounds.Height / sc - ActualHeight - 110);
                WindowOps.Activate(new WindowInteropHelper(this).Handle);
                Focus();
            };
            Deactivated += (_, _) => { if (IsVisible) Close(); };
        }
    }

    /// <summary>Miniatura de la última captura, abajo a la derecha. Se va sola a los 6 s (no si el mouse está encima).</summary>
    internal sealed class CaptureThumb : Window
    {
        private static CaptureThumb? _current;
        private readonly string _path;
        private readonly BitmapSource _img;
        private readonly DispatcherTimer _life = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        private readonly TranslateTransform _slide = new TranslateTransform(260, 0);
        private Point _down;
        private bool _pressed;

        public static void Pop(string path, BitmapSource img, MonitorInfo m)
        {
            _current?.Close();
            _current = new CaptureThumb(path, img, m);
            _current.Closed += (s, _) => { if (ReferenceEquals(_current, s)) _current = null; };
            _current.Show();
        }

        private CaptureThumb(string path, BitmapSource img, MonitorInfo m)
        {
            _path = path; _img = img;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            double tw = 210, th = Math.Clamp(tw * img.PixelHeight / Math.Max(1, img.PixelWidth), 60, 240);
            var pic = new Image { Source = img, Width = tw, Height = th, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(pic, BitmapScalingMode.HighQuality);
            var frame = new Border
            {
                Child = pic, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(3), BorderBrush = Brushes.White, Background = Brushes.White, Margin = new Thickness(18),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.4 },
                RenderTransform = _slide, Cursor = Cursors.Hand, ToolTip = System.IO.Path.GetFileName(path)
            };
            Content = frame;
            Width = tw + 36 + 6; Height = th + 36 + 6;
            var wa = m.Work;
            WindowOps.PlaceOnMonitor(this, m, (wa.Right - m.Bounds.Left) / m.Scale - Width - 4, (wa.Bottom - m.Bounds.Top) / m.Scale - Height - 4);

            frame.MouseLeftButtonDown += (_, e) => { _down = e.GetPosition(this); _pressed = true; };
            frame.MouseMove += (_, e) =>
            {
                if (!_pressed || e.LeftButton != MouseButtonState.Pressed) return;
                var p = e.GetPosition(this);
                if (Math.Abs(p.X - _down.X) < 6 && Math.Abs(p.Y - _down.Y) < 6) return;
                _pressed = false;
                // arrastrar la captura a otra app (como en macOS)
                _life.Stop();
                var data = new DataObject(DataFormats.FileDrop, new[] { _path });
                DragDrop.DoDragDrop(frame, data, DragDropEffects.Copy | DragDropEffects.Move);
                Close();
            };
            frame.MouseLeftButtonUp += (_, _) =>
            {
                if (!_pressed) return;
                _pressed = false;
                Close();
                Launcher.Launch(_path);
            };
            var cm = new ContextMenu();
            cm.Items.Add(Item("Abrir", () => { Close(); Launcher.Launch(_path); }));
            cm.Items.Add(Item("Copiar", () => { McBar.Core.Screenshots.CopyToClipboard(_img); Close(); }));
            cm.Items.Add(Item("Mostrar en la carpeta", () => { Close(); Launcher.ShowInExplorer(_path); }));
            cm.Items.Add(new Separator());
            cm.Items.Add(Item("Borrar", () => { Close(); SystemInfo.SendToRecycleBin(new[] { _path }); }));
            cm.Opened += (_, _) => _life.Stop();
            cm.Closed += (_, _) => { if (IsVisible) _life.Start(); };
            frame.ContextMenu = cm;
            frame.MouseEnter += (_, _) => _life.Stop();
            frame.MouseLeave += (_, _) => { if (cm.IsOpen) return; _life.Stop(); _life.Start(); };
            _life.Tick += (_, _) => SlideOut();
            SourceInitialized += (_, _) => WindowOps.MakeToolNoActivate(this);
            Loaded += (_, _) =>
            {
                _slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(260, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                _life.Start();
            };
            Closed += (_, _) => _life.Stop();
        }

        private static MenuItem Item(string text, Action a)
        {
            var mi = new MenuItem { Header = text };
            mi.Click += (_, _) => a();
            return mi;
        }

        private void SlideOut()
        {
            _life.Stop();
            var a = new DoubleAnimation(0, 260, TimeSpan.FromMilliseconds(220));
            a.Completed += (_, _) => Close();
            _slide.BeginAnimation(TranslateTransform.XProperty, a);
        }
    }
}
