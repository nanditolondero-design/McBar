// McBar — Creado por Andrés Londero
// Launchpad (como el de macOS): todas las apps instaladas en páginas de 7 × 5, fondo desenfocado,
// búsqueda arriba, puntitos de página abajo y animaciones al abrir y al pasar de página.
// Rueda del mouse o flechas: cambiar de página. Esc o clic en el fondo: cerrar. Enter: abrir la primera.
// Clic derecho en una app: "Agregar al Dock".
//
// 0.3.1: se dibuja SOLO la página visible (antes había una tira con todas las páginas que en algunas
// PCs quedaba vacía). Los íconos son tarjetas simples, sin efectos pesados.
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
        private const int Cols = 7, Rows = 5, PerPage = Cols * Rows;
        private static readonly Brush HoverBrush = Frozen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        private static readonly Brush LabelBrush = Frozen(Colors.White);

        private readonly Grid _root = new Grid();
        private readonly Grid _content = new Grid();
        private readonly ScaleTransform _zoom = new ScaleTransform(1.08, 1.08);
        private readonly Grid _pageHost = new Grid { ClipToBounds = true, Margin = new Thickness(70, 0, 70, 0) };
        private readonly StackPanel _dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        private readonly TextBox _search = new TextBox();
        private readonly TextBlock _placeholder = new TextBlock();
        private readonly Dictionary<string, ImageSource> _icons = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Image>> _waiting = new Dictionary<string, List<Image>>(StringComparer.OrdinalIgnoreCase);

        private List<CatalogApp> _all = new List<CatalogApp>();
        private List<CatalogApp> _shown = new List<CatalogApp>();
        private UIElement? _currentPage;
        private int _page, _pageCount = 1;
        private DateTime _lastWheel = DateTime.MinValue;
        private bool _closing, _loaded;

        public DateTime ClosedAt { get; private set; } = DateTime.MinValue;

        public LaunchpadWindow(MonitorInfo monitor)
        {
            Title = "Launchpad";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            double sc = App.Scale;
            Left = monitor.Bounds.Left / sc;
            Top = monitor.Bounds.Top / sc;
            Width = monitor.Bounds.Width / sc;
            Height = monitor.Bounds.Height / sc;
            SetResourceReference(FontFamilyProperty, "Mc.Font");
            Opacity = 0;

            _root.SetResourceReference(Panel.BackgroundProperty, "Mc.LaunchpadBg");
            _root.Children.Add(_content);
            _content.RenderTransformOrigin = new Point(0.5, 0.5);
            _content.RenderTransform = _zoom;
            _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(110) });
            _content.RowDefinitions.Add(new RowDefinition());
            _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(90) });

            // Búsqueda (píldora arriba al centro)
            var searchHost = new Grid { Width = 260, Height = 32, VerticalAlignment = VerticalAlignment.Center };
            _search.SetResourceReference(StyleProperty, "McTextBox");
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

            Grid.SetRow(_pageHost, 1);
            _content.Children.Add(_pageHost);
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
                else ShowPage(0, 0);
                var fresh = await AppCatalog.LoadAsync();
                _loaded = true;
                if (_closing) return;
                int keep = _page;
                _all = fresh;
                Filter(keep);
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

        private static bool IsInside<T>(DependencyObject? d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T) return true;
                d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return false;
        }

        private void Filter(int keepPage = 0)
        {
            var q = _search.Text.Trim();
            _shown = q.Length == 0 ? _all : _all.Where(a => a.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
            _pageCount = Math.Max(1, (int)Math.Ceiling(_shown.Count / (double)PerPage));
            BuildDots();
            ShowPage(Math.Clamp(q.Length == 0 ? keepPage : 0, 0, _pageCount - 1), 0);
        }

        /// <summary>Muestra una página. <paramref name="direction"/>: 1 = viene de la derecha, -1 = de la izquierda, 0 = sin animación.</summary>
        private void ShowPage(int page, int direction)
        {
            _page = Math.Clamp(page, 0, _pageCount - 1);
            UIElement next;
            if (_shown.Count == 0)
            {
                next = new TextBlock
                {
                    Text = _loaded || _search.Text.Length > 0 ? "No se encontraron apps" : "Cargando apps…",
                    Foreground = LabelBrush, FontSize = 18, TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 80, 0, 0)
                };
            }
            else
            {
                var grid = new UniformGrid { Columns = Cols, Rows = Rows };
                foreach (var app in _shown.Skip(_page * PerPage).Take(PerPage)) grid.Children.Add(Tile(app));
                next = grid;
            }

            var old = _currentPage;
            _currentPage = next;
            _pageHost.Children.Add(next);
            double w = Math.Max(1, _pageHost.ActualWidth > 0 ? _pageHost.ActualWidth : Width - 140);

            if (old != null)
            {
                if (direction == 0) _pageHost.Children.Remove(old);
                else
                {
                    var tOld = new TranslateTransform();
                    old.RenderTransform = tOld;
                    var outAnim = new DoubleAnimation(0, -direction * w * 0.35, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
                    outAnim.Completed += (_, _) => _pageHost.Children.Remove(old);
                    tOld.BeginAnimation(TranslateTransform.XProperty, outAnim);
                    old.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220)));
                    old.IsHitTestVisible = false;
                }
            }
            if (direction != 0)
            {
                var tNew = new TranslateTransform();
                next.RenderTransform = tNew;
                tNew.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(direction * w * 0.35, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                next.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
            }
            UpdateDots();
        }

        private UIElement Tile(CatalogApp app)
        {
            var img = new Image { Width = 80, Height = 80 };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            var label = new TextBlock
            {
                Text = app.Name,
                Foreground = LabelBrush,
                FontSize = 12.5,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 120,
                Margin = new Thickness(0, 6, 0, 0)
            };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(img);
            sp.Children.Add(label);
            var card = new Border
            {
                Child = sp,
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(16),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = app.Name,
                Cursor = Cursors.Hand
            };
            card.MouseEnter += (_, _) => card.Background = HoverBrush;
            card.MouseLeave += (_, _) => card.Background = Brushes.Transparent;
            card.MouseLeftButtonUp += (_, e) => { e.Handled = true; Launcher.Launch(app.Target); CloseAnimated(); };

            var cm = new ContextMenu();
            var add = new MenuItem { Header = "Agregar al Dock" };
            add.Click += (_, _) => App.Dock?.PinTarget(app.Name, app.Target);
            cm.Items.Add(add);
            card.ContextMenu = cm;

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
            return card;
        }

        private void BuildDots()
        {
            _dots.Children.Clear();
            if (_pageCount <= 1) return;
            for (int i = 0; i < _pageCount; i++)
            {
                int p = i;
                var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(7), Background = LabelBrush, Cursor = Cursors.Hand };
                var hit = new Border { Child = dot, Background = Brushes.Transparent };
                hit.MouseLeftButtonUp += (_, e) => { e.Handled = true; if (p != _page) ShowPage(p, p > _page ? 1 : -1); };
                _dots.Children.Add(hit);
            }
        }

        private void UpdateDots()
        {
            for (int i = 0; i < _dots.Children.Count; i++)
                if (_dots.Children[i] is Border b && b.Child is Border d) d.Opacity = i == _page ? 1 : 0.35;
        }

        private void Turn(int delta)
        {
            int target = Math.Clamp(_page + delta, 0, _pageCount - 1);
            if (target != _page) ShowPage(target, delta > 0 ? 1 : -1);
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            if ((DateTime.Now - _lastWheel).TotalMilliseconds < 320) return;
            _lastWheel = DateTime.Now;
            Turn(e.Delta < 0 ? 1 : -1);
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
                case Key.PageDown: Turn(1); e.Handled = true; break;
                case Key.PageUp: Turn(-1); e.Handled = true; break;
                case Key.Right when _search.Text.Length == 0: Turn(1); e.Handled = true; break;
                case Key.Left when _search.Text.Length == 0: Turn(-1); e.Handled = true; break;
            }
        }

        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    }
}
