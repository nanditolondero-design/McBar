// McBar — Creado por Andrés Londero
// 1.9.25: Marcado de capturas (como el editor rápido de macOS). Al tocar la miniatura de una captura se abre
// este editor: lápiz, flecha, rectángulo, resaltador y texto, con colores, deshacer, y copiar / guardar.
// El lienzo está a resolución real dentro de un Viewbox, así lo guardado sale con la misma calidad.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class MarkupWindow : Window
    {
        private readonly string _path;
        private readonly BitmapSource _img;
        private readonly Canvas _canvas = new Canvas();
        private readonly List<UIElement> _added = new List<UIElement>();
        private string _tool = "pen";
        private Color _color = Color.FromRgb(0xFF, 0x3B, 0x30); // rojo
        private readonly double _w, _h, _pen;
        private Point _start;
        private Polyline? _poly;
        private Shape? _live;
        private Canvas? _liveGroup; // 1.9.25: grupo de la flecha (línea + punta)

        public static void Open(string path, BitmapSource img)
        {
            try { new MarkupWindow(path, img).Show(); } catch (Exception ex) { Log.Write("Marcado: " + ex.Message); }
        }

        private MarkupWindow(string path, BitmapSource img)
        {
            _path = path; _img = img;
            _w = img.PixelWidth; _h = img.PixelHeight;
            _pen = Math.Max(2.5, _w / 500.0); // grosor según el tamaño de la imagen
            Title = "Marcar — " + System.IO.Path.GetFileName(path);
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22));
            FontFamily = (FontFamily)FindResource("Mc.Font");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            Icon = McDialog.Logo();

            _canvas.Width = _w; _canvas.Height = _h;
            _canvas.Background = new ImageBrush(img);
            _canvas.MouseLeftButtonDown += OnDown;
            _canvas.MouseMove += OnMove;
            _canvas.MouseLeftButtonUp += OnUp;

            double maxW = SystemParameters.WorkArea.Width * 0.9, maxH = SystemParameters.WorkArea.Height * 0.8;
            var box = new Viewbox { Child = _canvas, Stretch = Stretch.Uniform, MaxWidth = maxW, MaxHeight = maxH, Margin = new Thickness(12) };

            var root = new DockPanel { LastChildFill = true };
            var bar = BuildToolbar();
            DockPanel.SetDock(bar, System.Windows.Controls.Dock.Top);
            root.Children.Add(bar);
            root.Children.Add(box);
            Content = root;

            SourceInitialized += (_, _) => WindowOps.SetDarkTitleBar(this, true);
        }

        // ---------- barra de herramientas ----------
        private Border BuildToolbar()
        {
            var tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var (key, label) in new[] { ("pen", "✏️ Lápiz"), ("arrow", "➘ Flecha"), ("rect", "▭ Rectángulo"), ("marker", "🖍 Resaltar"), ("text", "T Texto") })
            {
                var k = key;
                var b = new ToggleButton { Content = label, Margin = new Thickness(3, 0, 3, 0), Padding = new Thickness(8, 4, 8, 4), IsChecked = key == _tool };
                b.Click += (_, _) => { _tool = k; foreach (var c in tools.Children) if (c is ToggleButton tb) tb.IsChecked = (string)tb.Tag == k; };
                b.Tag = k;
                tools.Children.Add(b);
            }
            var colors = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            foreach (var c in new[] { Color.FromRgb(0xFF, 0x3B, 0x30), Color.FromRgb(0xFF, 0xCC, 0x00), Color.FromRgb(0x34, 0xC7, 0x59), Color.FromRgb(0x0A, 0x84, 0xFF), Colors.White, Colors.Black })
            {
                var col = c;
                var dot = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(c), Margin = new Thickness(3, 0, 3, 0), Cursor = Cursors.Hand, BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent };
                dot.MouseLeftButtonUp += (_, _) => { _color = col; foreach (var e in colors.Children) if (e is Border bd) bd.BorderBrush = Brushes.Transparent; dot.BorderBrush = Brushes.White; };
                if (c == _color) dot.BorderBrush = Brushes.White;
                colors.Children.Add(dot);
            }
            var undo = TextButton("Deshacer", () => { if (_added.Count > 0) { _canvas.Children.Remove(_added[^1]); _added.RemoveAt(_added.Count - 1); } });
            var copy = TextButton("Copiar", () => { Screenshots.CopyToClipboard(Flatten()); });
            var save = TextButton("Guardar", Save);
            var done = TextButton("Listo", Close);

            var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(undo); right.Children.Add(copy); right.Children.Add(save); right.Children.Add(done);

            var grid = new Grid { Margin = new Thickness(10, 8, 10, 8) };
            grid.Children.Add(tools);
            grid.Children.Add(colors);
            Grid.SetColumn(colors, 0);
            colors.HorizontalAlignment = HorizontalAlignment.Center;
            grid.Children.Add(right);
            return new Border { Child = grid, Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)) };
        }

        private Button TextButton(string text, Action a)
        {
            var b = new Button { Content = text, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(10, 4, 10, 4) };
            b.Click += (_, _) => a();
            return b;
        }

        // ---------- dibujo ----------
        private void OnDown(object sender, MouseButtonEventArgs e)
        {
            _start = e.GetPosition(_canvas);
            var brush = new SolidColorBrush(_color);
            switch (_tool)
            {
                case "text":
                    var tb = new TextBox
                    {
                        MinWidth = 40, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                        Foreground = brush, FontSize = Math.Max(16, _w / 55.0), FontWeight = FontWeights.SemiBold, AcceptsReturn = true
                    };
                    Canvas.SetLeft(tb, _start.X); Canvas.SetTop(tb, _start.Y);
                    _canvas.Children.Add(tb); _added.Add(tb);
                    tb.Loaded += (_, _) => tb.Focus();
                    break;
                case "pen":
                case "marker":
                    _poly = new Polyline
                    {
                        Stroke = _tool == "marker" ? new SolidColorBrush(Color.FromArgb(0x66, _color.R, _color.G, _color.B)) : brush,
                        StrokeThickness = _tool == "marker" ? _pen * 5 : _pen,
                        StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                    };
                    _poly.Points.Add(_start);
                    _canvas.Children.Add(_poly);
                    break;
                case "rect":
                    _live = new Rectangle { Stroke = brush, StrokeThickness = _pen };
                    Canvas.SetLeft(_live, _start.X); Canvas.SetTop(_live, _start.Y);
                    _canvas.Children.Add(_live);
                    break;
                case "arrow":
                    _liveGroup = new Canvas();
                    _live = new Line { Stroke = brush, StrokeThickness = _pen, X1 = _start.X, Y1 = _start.Y, X2 = _start.X, Y2 = _start.Y, StrokeEndLineCap = PenLineCap.Round };
                    _liveGroup.Children.Add(_live);
                    _canvas.Children.Add(_liveGroup);
                    break;
            }
            _canvas.CaptureMouse();
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(_canvas);
            if (_poly != null) _poly.Points.Add(p);
            else if (_live is Rectangle r)
            {
                Canvas.SetLeft(r, Math.Min(_start.X, p.X)); Canvas.SetTop(r, Math.Min(_start.Y, p.Y));
                r.Width = Math.Abs(p.X - _start.X); r.Height = Math.Abs(p.Y - _start.Y);
            }
            else if (_live is Line l) { l.X2 = p.X; l.Y2 = p.Y; }
        }

        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            _canvas.ReleaseMouseCapture();
            if (_poly != null) { _added.Add(_poly); _poly = null; }
            else if (_live is Line l && _liveGroup != null)
            {
                foreach (var seg in Arrowhead(new Point(l.X1, l.Y1), new Point(l.X2, l.Y2), _pen * 4)) _liveGroup.Children.Add(seg);
                _added.Add(_liveGroup); _live = null; _liveGroup = null;
            }
            else if (_live != null) { _added.Add(_live); _live = null; }
        }

        private IEnumerable<Line> Arrowhead(Point from, Point to, double size)
        {
            double ang = Math.Atan2(to.Y - from.Y, to.X - from.X);
            foreach (double d in new[] { ang + Math.PI * 0.82, ang - Math.PI * 0.82 })
                yield return new Line { Stroke = new SolidColorBrush(_color), StrokeThickness = _pen, StrokeEndLineCap = PenLineCap.Round, X1 = to.X, Y1 = to.Y, X2 = to.X + size * Math.Cos(d), Y2 = to.Y + size * Math.Sin(d) };
        }

        // ---------- aplanar y guardar ----------
        private BitmapSource Flatten()
        {
            var rtb = new RenderTargetBitmap((int)_w, (int)_h, 96, 96, PixelFormats.Pbgra32);
            _canvas.Measure(new Size(_w, _h));
            _canvas.Arrange(new Rect(0, 0, _w, _h));
            rtb.Render(_canvas);
            rtb.Freeze();
            return rtb;
        }

        private void Save()
        {
            try
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(Flatten()));
                using (var fs = File.Create(_path)) enc.Save(fs);
                Log.Write("Marcado guardado: " + _path);
                Close();
            }
            catch (Exception ex) { Log.Write("Marcado (guardar): " + ex.Message); McDialog.Info("No se pudo guardar", ex.Message); }
        }
    }
}
