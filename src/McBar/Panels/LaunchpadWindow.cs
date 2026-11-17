// McBar — Creado por Andrés Londero
// Launchpad: todas las apps instaladas en páginas, con fondo desenfocado, búsqueda y animaciones.
// Rueda del mouse o flechas: cambiar de página. Esc o clic en el fondo: cerrar. Enter: abrir la primera.
// Clic derecho en una app: "Agregar al Dock".
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class LaunchpadWindow : Window
    {
        private const int Cols = 7, Rows = 5;
        private readonly Grid _root = new Grid();
        private readonly Grid _content = new Grid();
        private readonly ScaleTransform _zoom = new ScaleTransform(1.08, 1.08);
        private readonly StackPanel _strip = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly TranslateTransform _stripShift = new TranslateTransform();
        private readonly StackPanel _dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        private readonly TextBox _search = new TextBox();
        private readonly TextBlock _placeholder = new TextBlock();
        private readonly Dictionary<string, ImageSource> _icons = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Image>> _waiting = new Dictionary<string, List<Image>>(StringComparer.OrdinalIgnoreCase);

        private List<CatalogApp> _all = new List<CatalogApp>();
        private List<CatalogApp> _shown = new List<CatalogApp>();
        private int _page, _pageCount = 1;
        private double _pageWidth;
        private DateTime _lastWheel = DateTime.MinValue;
        private bool _closing;

        public DateTime ClosedAt { get; private set; } = DateTime.MinValue;

        public LaunchpadWindow()
        {
            Title = "Launchpad";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            SetResourceReference(FontFamilyProperty, "Mc.Font");
            Opacity = 0;

            _root.SetResourceReference(Panel.BackgroundProperty, "Mc.LaunchpadBg");
            _root.Children.Add(_content);
            _content.RenderTransformOrigin = new Point(0.5, 0.5);
            _content.RenderTransform = _zoom;
            _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(110) });
            _content.RowDefinitions.Add(new RowDefinition());
            _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(90) });

            // Búsqueda
            var searchHost = new Grid { Width = 260, Height = 32, VerticalAlignment = VerticalAlignment.Center };
            _search.SetResourceReference(StyleProperty, "McTextBox");
            _search.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            _search.Foreground = Brushes.White;
            _search.CaretBrush = Brushes.White;
            _search.VerticalContentAlignment = VerticalAlignment.Center;
            _search.TextChanged += (_, _) => { _placeholder.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Filter(); };
            _placeholder.Text = "🔍  Buscar";
            _placeholder.Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
            _placeholder.IsHitTestVisible = false;
            _placeholder.HorizontalAlignment = HorizontalAlignment.Center;
            _placeholder.VerticalAlignment = VerticalAlignment.Center;
            searchHost.Children.Add(_search);
            searchHost.Children.Add(_placeholder);
            _content.Children.Add(searchHost);

            // Páginas
            var viewport = new Grid { ClipToBounds = true, Margin = new Thickness(60, 0, 60, 0) };
            _strip.RenderTransform = _stripShift;
            _strip.HorizontalAlignment = HorizontalAlignment.Left;
            viewport.Children.Add(_strip);
            viewport.SizeChanged += (_, _) => { _pageWidth = viewport.ActualWidth; Rebuild(); };
            Grid.SetRow(viewport, 1);
            _content.Children.Add(viewport);

            Grid.SetRow(_dots, 2);
            _dots.VerticalAlignment = VerticalAlignment.Center;
            _content.Children.Add(_dots);

            Content = _root;

            SourceInitialized += (_, _) =>
            {
                WindowOps.MakeTool(this);
                WindowOps.EnableBlur(this, ThemeManager.GetColor("Mc.LaunchpadTint"));
            };
            Deactivated += (_, _) => CloseAnimated();
            PreviewKeyDown += OnKey;
            MouseWheel += OnWheel;
            _root.MouseLeftButtonUp += (_, e) =>
            {
                if (e.OriginalSource is DependencyObject d && IsInside<TextBox>(d)) return;
                CloseAnimated();
            };
            Loaded += async (_, _) =>
            {
                _search.Focus();
                if (AppCatalog.Cached != null) { _all = AppCatalog.Cached.ToList(); Filter(); }
                var fresh = await AppCatalog.LoadAsync();
                if (_closing) return;
                if (fresh.Count != _all.Count || AppCatalog.Cached == null) { _all = fresh; Filter(); }
            };
        }

        public void ShowAnimated()
        {
            Show();
            Activate();
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            _zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.08, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            _zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.08, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        }

        public void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            ClosedAt = DateTime.Now;
            var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(160));
            fade.Completed += (_, _) => { Close(); _icons.Clear(); App.ScheduleTrim(); };
            BeginAnimation(OpacityProperty, fade);
            _zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.06, TimeSpan.FromMilliseconds(160)));
            _zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.06, TimeSpan.FromMilliseconds(160)));
        }

        private static bool IsInside<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T) return true;
                d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return false;
        }

        private void Filter()
        {
            var q = _search.Text.Trim();
            _shown = q.Length == 0 ? _all : _all.Where(a => a.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
            _page = 0;
            Rebuild();
        }

        private void Rebuild()
        {
            if (_pageWidth <= 0) return;
            _strip.Children.Clear();
            _waiting.Clear();
            int perPage = Cols * Rows;
            _pageCount = Math.Max(1, (int)Math.Ceiling(_shown.Count / (double)perPage));
            for (int p = 0; p < _pageCount; p++)
            {
                var grid = new UniformGrid { Columns = Cols, Rows = Rows, Width = _pageWidth, VerticalAlignment = VerticalAlignment.Top };
                foreach (var app in _shown.Skip(p * perPage).Take(perPage)) grid.Children.Add(Tile(app));
                _strip.Children.Add(grid);
            }
            if (_shown.Count == 0)
            {
                var none = new TextBlock { Text = "No se encontraron apps", Foreground = Brushes.White, FontSize = 18, Width = _pageWidth, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 80, 0, 0) };
                _strip.Children.Clear();
                _strip.Children.Add(none);
            }
            BuildDots();
            GoTo(Math.Min(_page, _pageCount - 1), false);
        }

        private UIElement Tile(CatalogApp app)
        {
            var img = new Image { Width = 72, Height = 72 };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            var label = new TextBlock
            {
                Text = app.Name,
                Foreground = Brushes.White,
                FontSize = 12.5,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 120,
                Margin = new Thickness(0, 6, 0, 0)
            };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(img);
            sp.Children.Add(label);
            var b = new Button { Content = sp, Padding = new Thickness(10, 8, 10, 8), Foreground = Brushes.White, ToolTip = app.Name, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            b.SetResourceReference(StyleProperty, "McFlatButton");
            b.Click += (_, _) => { Launcher.Launch(app.Target); CloseAnimated(); };

            var cm = new ContextMenu();
            var add = new MenuItem { Header = "Agregar al Dock" };
            add.Click += (_, _) => App.Dock?.PinTarget(app.Name, app.Target);
            cm.Items.Add(add);
            b.ContextMenu = cm;

            if (_icons.TryGetValue(app.Target, out var cached)) img.Source = cached;
            else
            {
                if (!_waiting.TryGetValue(app.Target, out var list))
                {
                    list = new List<Image>();
                    _waiting[app.Target] = list;
                    IconLoader.Request(app.Target, 96, src =>
                    {
                        if (src == null) return;
                        _icons[app.Target] = src;
                        if (_waiting.TryGetValue(app.Target, out var l)) foreach (var i in l) i.Source = src;
                    }, useCache: false);
                }
                list.Add(img);
            }
            return b;
        }

        private void BuildDots()
        {
            _dots.Children.Clear();
            if (_pageCount <= 1) return;
            for (int i = 0; i < _pageCount; i++)
            {
                int p = i;
                var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(5), Background = Brushes.White };
                var b = new Button { Content = dot, Padding = new Thickness(2) };
                b.SetResourceReference(StyleProperty, "McFlatButton");
                b.Click += (_, _) => GoTo(p, true);
                _dots.Children.Add(b);
            }
        }

        private void GoTo(int page, bool animate)
        {
            _page = Math.Clamp(page, 0, _pageCount - 1);
            double x = -_page * _pageWidth;
            if (animate)
                _stripShift.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(x, TimeSpan.FromMilliseconds(380)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
            else
            {
                _stripShift.BeginAnimation(TranslateTransform.XProperty, null);
                _stripShift.X = x;
            }
            for (int i = 0; i < _dots.Children.Count; i++)
                if (_dots.Children[i] is Button b && b.Content is Border d) d.Opacity = i == _page ? 1 : 0.35;
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            if ((DateTime.Now - _lastWheel).TotalMilliseconds < 300) return;
            _lastWheel = DateTime.Now;
            GoTo(_page + (e.Delta < 0 ? 1 : -1), true);
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    if (_search.Text.Length > 0) _search.Text = ""; else CloseAnimated();
                    e.Handled = true;
                    break;
                case Key.Enter:
                    var first = _shown.FirstOrDefault();
                    if (first != null) { Launcher.Launch(first.Target); CloseAnimated(); }
                    e.Handled = true;
                    break;
                case Key.PageDown:
                    GoTo(_page + 1, true); e.Handled = true; break;
                case Key.PageUp:
                    GoTo(_page - 1, true); e.Handled = true; break;
                case Key.Right when _search.Text.Length == 0:
                    GoTo(_page + 1, true); e.Handled = true; break;
                case Key.Left when _search.Text.Length == 0:
                    GoTo(_page - 1, true); e.Handled = true; break;
            }
        }
    }
}
