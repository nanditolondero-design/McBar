// McBar — Creado por Andrés Londero
// Launchpad (como el de macOS): todas las apps instaladas en páginas de 7 × 5, fondo desenfocado,
// búsqueda arriba, puntitos de página abajo y animaciones al abrir y al pasar de página.
// Rueda del mouse o flechas: cambiar de página. Esc o clic en el fondo: cerrar. Enter: abrir la primera.
// Clic derecho en una app: "Agregar al Dock".
//
// 0.3.1: se dibuja SOLO la página visible (antes había una tira con todas las páginas que en algunas
// PCs quedaba vacía). Los íconos son tarjetas simples, sin efectos pesados.
//
// 0.4.0:
//  - Transparencia del fondo regulable (Preferencias → Launchpad) y desenfoque que se puede apagar.
//  - CARPETAS: arrastrar una app sobre otra crea una carpeta; sobre una carpeta, la agrega. Clic en la
//    carpeta la abre (nombre editable arriba). Arrastrar una app fuera del recuadro de la carpeta la saca.
//    Clic derecho: "Agregar a carpeta", "Nueva carpeta", "Sacar de la carpeta", "Renombrar",
//    "Deshacer carpeta". La lógica está en Core/LaunchpadLayout.cs (con pruebas).
//  - El buscador baja un poco para no quedar tapado por la isla dinámica.
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
        // 1.9.10: columnas, filas y tamaño de íconos según el tamaño de la pantalla (antes 7×5 de 80 px fijos:
        // en pantallas chicas, como las de 1366×768, los nombres quedaban cortados y todo se veía apretado)
        private readonly int Cols = 7, Rows = 5;
        private int PerPage => Cols * Rows;
        private readonly double _iconSize = 80, _labelMax = 120, _labelFont = 12.5;

        /// <summary>
        /// Medidas del Launchpad para una pantalla (en DIP). Busca la grilla más parecida a la de macOS (7×5) que
        /// deje los íconos de al menos 52 px con el nombre entero abajo. Pura, se prueba.
        /// </summary>
        public static (int cols, int rows, double icon, double top, double bottom) Fit(double w, double h, double dock)
        {
            bool small = h < 860;
            double top = small ? 84 : 124, bottom = small ? 50 : 90;
            double availH = Math.Max(200, h - top - bottom - Math.Max(0, dock));
            double availW = Math.Max(300, w - 140);
            foreach (var rows in new[] { 5, 4, 3 })
                foreach (var cols in new[] { 7, 6, 5 })
                {
                    double icon = Math.Min(80, Math.Min(availH / rows - 52, availW / cols - 44));
                    if (icon >= 52) return (cols, rows, Math.Floor(icon), top, bottom);
                }
            return (5, 3, Math.Max(40, Math.Floor(Math.Min(availH / 3 - 52, availW / 5 - 44))), top, bottom);
        }
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
        private List<LaunchpadEntry<CatalogApp>> _shown = new List<LaunchpadEntry<CatalogApp>>();
        private readonly Grid _folderLayer = new Grid { Visibility = Visibility.Collapsed };
        private LaunchpadFolder? _openFolder;
        private Point _dragStart;
        private bool _dragged;
        private static List<LaunchpadFolder> Folders => SettingsStore.Current.LaunchpadFolders;
        private static List<string> Hidden => SettingsStore.Current.LaunchpadHidden;
        private bool _showHidden;
        private readonly TextBlock _hiddenToggle = new TextBlock { FontSize = 12, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 40, 0) };
        private UIElement? _currentPage;
        private int _page, _pageCount = 1;
        private DateTime _lastWheel = DateTime.MinValue;
        private bool _closing, _loaded;
        private readonly bool _fast;
        /// <summary>Lo que se funde al abrir/cerrar: la ventana (modo en capas) o su contenido (modo rápido).</summary>
        private UIElement Fader => _fast ? _root : this;

        public DateTime ClosedAt { get; private set; } = DateTime.MinValue;

        public LaunchpadWindow(MonitorInfo monitor, double dockHeight = 0)
        {
            Title = "Launchpad";
            WindowStyle = WindowStyle.None;
            // 0.4.1: con "Animaciones rápidas" es una ventana común con vidrio de DWM (una ventana "en capas"
            // de pantalla completa obligaba a Windows a copiar toda la pantalla en cada cuadro: lento).
            _fast = SettingsStore.Current.FastWindows;
            AllowsTransparency = !_fast;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            double sc = monitor.Scale;
            WindowOps.PlaceOnMonitor(this, monitor, 0, 0, monitor.Bounds.Width / sc, monitor.Bounds.Height / sc); // 1.5.0
            SetResourceReference(FontFamilyProperty, "Mc.Font");
            if (_fast) _root.Opacity = 0; else Opacity = 0;

            _root.Background = DimBrush();
            _root.Children.Add(_content);
            _root.Children.Add(_folderLayer);
            _content.RenderTransformOrigin = new Point(0.5, 0.5);
            _content.RenderTransform = _zoom;
            var fit = Fit(monitor.Bounds.Width / sc, monitor.Bounds.Height / sc, dockHeight); // 1.9.10
            // 1.9.43: Preferencias → Launchpad: tamaño de íconos, de letra y cantidad de columnas y filas (0 = automático).
            var lps = SettingsStore.Current;
            Cols = lps.LaunchpadColumns > 0 ? Math.Clamp(lps.LaunchpadColumns, 3, 10) : fit.cols;
            Rows = lps.LaunchpadRows > 0 ? Math.Clamp(lps.LaunchpadRows, 2, 8) : fit.rows;
            _iconSize = fit.icon;
            if (lps.LaunchpadColumns > 0 || lps.LaunchpadRows > 0 || lps.LaunchpadIconSize > 0)
            {
                double aw = Math.Max(300, monitor.Bounds.Width / sc - 140);
                double ah = Math.Max(200, monitor.Bounds.Height / sc - fit.top - fit.bottom - Math.Max(0, dockHeight));
                double room = Math.Min(aw / Cols - 44, ah / Rows - 52);           // lo que entra sin cortarse
                double want = lps.LaunchpadIconSize > 0 ? lps.LaunchpadIconSize : 80;
                _iconSize = Math.Floor(Math.Clamp(Math.Min(want, room), 32, 128));
            }
            _labelFont = lps.LaunchpadLabelSize > 0 ? Math.Clamp(lps.LaunchpadLabelSize, 9, 20) : (_iconSize < 66 ? 11.5 : 12.5);
            _labelMax = Math.Max(84, Math.Min(140, (monitor.Bounds.Width / sc - 140) / Cols - 24));
            _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(fit.top) });
            _content.RowDefinitions.Add(new RowDefinition());
            _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(fit.bottom) });
            _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Max(0, dockHeight)) }); // 1.6.2: lugar del Dock (queda a la vista encima)

            // Búsqueda (píldora arriba al centro)
            // (abajo de la fila para no quedar debajo de la isla dinámica)
            // 1.9.43: la fila de arriba nunca es más chica que la isla (6 + 42) más un respiro, así el cuadro no queda debajo de ella.
            double searchTop = Math.Max(fit.top - 52, 6 + McBar.MenuBar.IslandWindow.CompactHeightFor(lps) + 10);
            var searchHost = new Grid { Width = 260, Height = 32, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, searchTop, 0, 0) };
            _search.SetResourceReference(StyleProperty, "McTextBox");
            // 1.9.10: píldora de vidrio oscuro propia (con el tema Claro el cuadro salía blanco con letras blancas)
            _search.Template = SearchTemplate();
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
            // 0.4.2: mostrar/esconder las apps ocultas
            Grid.SetRow(_hiddenToggle, 2);
            _hiddenToggle.Foreground = new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF));
            _hiddenToggle.MouseLeftButtonUp += (_, e) => { e.Handled = true; _showHidden = !_showHidden; Filter(_page); };
            _content.Children.Add(_hiddenToggle);
            // 0.4.3: barra de selección (varias apps a la vez)
            Grid.SetRow(_selectionBar, 2);
            _content.Children.Add(_selectionBar);
            Content = _root;

            SourceInitialized += (_, _) =>
            {
                WindowOps.MakeTool(this);
                if (_fast) WindowOps.MakeGlass(this);
                if (SettingsStore.Current.LaunchpadBlur) WindowOps.EnableBlur(this, Scaled(ThemeManager.GetColor("Mc.LaunchpadTint")));
            };
            Deactivated += (_, _) => { if (!_dragging) CloseAnimated(); };
            PreviewKeyDown += OnKey;
            MouseWheel += OnWheel;
            _root.MouseLeftButtonUp += (_, e) =>
            {
                if (e.OriginalSource is DependencyObject d && IsInside<TextBox>(d)) return;
                if (_folderLayer.Visibility == Visibility.Visible) { CloseFolder(); return; }
                CloseAnimated();
            };
            // 1.9.43: con el Launchpad abierto la isla queda chica y no se despliega encima del cuadro de búsqueda
            bool islandLocked = false;
            Closed += (_, _) => { if (islandLocked) { islandLocked = false; McBar.MenuBar.IslandWindow.Suppress(false); } };
            Loaded += async (_, _) =>
            {
                if (!islandLocked) { islandLocked = true; McBar.MenuBar.IslandWindow.Suppress(true); }
                _search.Focus();
                if (AppCatalog.Cached != null) { _all = AppCatalog.Cached.ToList(); Filter(); }
                else ShowPage(0, 0);
                var fresh = await AppCatalog.LoadAsync();
                _loaded = true;
                if (_closing) return;
                int keep = _page;
                _all = fresh;
                if (_all.Count > 0)
                {
                    int before = Folders.Sum(f => f.Targets.Count);
                    LaunchpadLayout.Clean(Folders, _all.Select(a => a.Target));
                    if (Folders.Sum(f => f.Targets.Count) != before) SettingsStore.Save();
                }
                Filter(keep);
            };
        }

        public void ShowAnimated()
        {
            Show();
            Activate();
            var ease = PanelLook.Spring; // 1.6.0: con resorte, como el resto de McBar
            Fader.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            var zx = new DoubleAnimation(1.08, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease };
            CacheWhile(_content, zx);
            _zoom.BeginAnimation(ScaleTransform.ScaleXProperty, zx);
            _zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.08, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease });
        }

        public void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            ClosedAt = DateTime.Now;
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            fade.Completed += (_, _) => { Close(); _icons.Clear(); App.ScheduleTrim(); };
            Fader.BeginAnimation(OpacityProperty, fade);
            var zx = new DoubleAnimation(1, 1.06, TimeSpan.FromMilliseconds(160));
            CacheWhile(_content, zx);
            _zoom.BeginAnimation(ScaleTransform.ScaleXProperty, zx);
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

        /// <summary>1.9.10: cuadro de búsqueda como el de macOS: píldora de vidrio oscuro con borde suave.</summary>
        private static ControlTemplate SearchTemplate()
        {
            var t = new ControlTemplate(typeof(TextBox));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            bd.SetValue(Border.BackgroundProperty, Frozen(Color.FromArgb(0x40, 0x00, 0x00, 0x00)));
            bd.SetValue(Border.BorderBrushProperty, Frozen(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)));
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            var host = new FrameworkElementFactory(typeof(ScrollViewer)) { Name = "PART_ContentHost" };
            host.SetValue(MarginProperty, new Thickness(10, 0, 10, 0));
            host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            bd.AppendChild(host);
            t.VisualTree = bd;
            return t;
        }

        private void Filter(int keepPage = 0)
        {
            var q = _search.Text.Trim();
            // 0.4.3: al buscar también aparecen las ocultas (atenuadas); sin buscar, solo si se piden.
            bool all = _showHidden || q.Length > 0;
            if (q.Length == 0)
            {
                var arranged = LaunchpadLayout.ApplyOrder(LaunchpadLayout.Arrange(_all, Folders, a => a.Target), Order, a => a.Target);
                _shown = LaunchpadLayout.WithoutHidden(arranged, Hidden, a => a.Target, all);
            }
            else _shown = LaunchpadLayout.Search(_all, Folders, a => a.Target, a => a.Name, q);
            var allKeys = new HashSet<string>(_all.Select(a => a.Target).Concat(Folders.Select(LaunchpadLayout.FolderKey)), StringComparer.OrdinalIgnoreCase);
            int hiddenCount = Hidden.Count(h => allKeys.Contains(h));
            _hiddenToggle.Visibility = q.Length == 0 && (hiddenCount > 0 || _showHidden) ? Visibility.Visible : Visibility.Collapsed;
            _hiddenToggle.Text = _showHidden ? "Esconder lo oculto" : $"Mostrar lo oculto ({hiddenCount})";
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
                foreach (var entry in _shown.Skip(_page * PerPage).Take(PerPage))
                    grid.Children.Add(entry.IsFolder ? FolderTile(entry) : Tile(entry.App!, null));
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
                    CacheWhile(old, outAnim);
                    tOld.BeginAnimation(TranslateTransform.XProperty, outAnim);
                    old.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220)));
                    old.IsHitTestVisible = false;
                }
            }
            if (direction != 0)
            {
                var tNew = new TranslateTransform();
                next.RenderTransform = tNew;
                var inAnim = new DoubleAnimation(direction * w * 0.35, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                CacheWhile(next, inAnim);
                tNew.BeginAnimation(TranslateTransform.XProperty, inAnim);
                next.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
            }
            UpdateDots();
        }

        // ================== Fondo ==================

        /// <summary>
        /// Opacidad según Preferencias → Launchpad → Transparencia. 0.4.1: rango más amplio y parejo
        /// (0 = casi opaco, 100 = casi nada); antes el máximo era el doble del tono del tema y se veía muy claro.
        /// </summary>
        public static byte AlphaFor(int transparency) =>
            (byte)Math.Clamp(Math.Round(245 - Math.Clamp(transparency, 0, 100) * 2.25), 0, 255);

        private static Color Scaled(Color c) => Color.FromArgb(AlphaFor(SettingsStore.Current.LaunchpadTransparency), c.R, c.G, c.B);

        private static Brush DimBrush()
        {
            var c = Application.Current.TryFindResource("Mc.LaunchpadBg") is SolidColorBrush b ? b.Color : Color.FromArgb(0x59, 0, 0, 0);
            var s = Scaled(c);
            // Con desenfoque el tinte del desenfoque ya oscurece: el velo va a la mitad.
            if (SettingsStore.Current.LaunchpadBlur) s = Color.FromArgb((byte)(s.A / 2), s.R, s.G, s.B);
            // Totalmente transparente no recibiría clics: se deja un mínimo invisible.
            if (s.A == 0) s = Color.FromArgb(1, 0, 0, 0);
            return Frozen(s);
        }

        // ================== Íconos ==================

        private static List<string> Order => SettingsStore.Current.LaunchpadOrder;
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Border> _cards = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase);
        private static readonly Brush SelectedBrush = Frozen(Color.FromArgb(0x40, 0x0A, 0x84, 0xFF));
        private static readonly Brush SelectedBorder = Frozen(Color.FromArgb(0xE0, 0x0A, 0x84, 0xFF));
        private static readonly Brush InsertBrush = Frozen(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));

        private Image IconImage(CatalogApp app, double size)
        {
            var img = new Image { Width = size, Height = size };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
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
                    }, useCache: true, displayName: app.Name, appShape: true); // 1.5.0: los íconos quedan en memoria
                }
                list.Add(img);
            }
            return img;
        }

        /// <summary>
        /// Tarjeta de un ícono. 0.4.3: al pasar el mouse el ícono crece suave y aparece un brillo; el nombre
        /// tiene una sombrita para leerse sobre cualquier fondo.
        /// </summary>
        private Border Card(UIElement icon, string text, string key, bool hidden)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = LabelBrush,
                FontSize = _labelFont,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = _labelMax,
                Margin = new Thickness(0, 7, 0, 0)
            };
            var iconHost = new Grid { HorizontalAlignment = HorizontalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5) };
            var zoom = new ScaleTransform(1, 1);
            iconHost.RenderTransform = zoom;
            iconHost.Children.Add(icon);
            var check = new Border
            {
                Width = 22, Height = 22, CornerRadius = new CornerRadius(11),
                Background = SelectedBorder, BorderBrush = Brushes.White, BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -6, -6, 0),
                Child = new TextBlock { Text = "✓", Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                Visibility = _selected.Contains(key) ? Visibility.Visible : Visibility.Collapsed
            };
            iconHost.Children.Add(check);

            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(iconHost);
            sp.Children.Add(Shadowed(label));
            var card = new Border
            {
                Child = sp,
                Padding = new Thickness(12, 9, 12, 9),
                CornerRadius = new CornerRadius(18),
                Background = _selected.Contains(key) ? SelectedBrush : Brushes.Transparent,
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = text,
                Cursor = Cursors.Hand,
                AllowDrop = true,
                Tag = key,
                Opacity = hidden ? 0.45 : 1
            };
            _cards[key] = card;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            void Zoom(double to)
            {
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
            }
            card.MouseEnter += (_, _) => { Zoom(1.1); if (!_selected.Contains(key)) card.Background = HoverBrush; };
            card.MouseLeave += (_, _) => { Zoom(1); if (!_selected.Contains(key)) card.Background = Brushes.Transparent; card.BorderBrush = Brushes.Transparent; };
            card.DragLeave += (_, _) => { card.BorderBrush = Brushes.Transparent; if (!_selected.Contains(key)) card.Background = Brushes.Transparent; };
            card.DragOver += (_, e) => ShowDropHint(card, key, e);
            card.Drop += (_, e) => OnCardDrop(card, key, e);
            return card;
        }

        /// <summary>
        /// 1.5.0: sombra del nombre de la app SIN efecto de sombra (DropShadowEffect): una copia del texto en
        /// negro, corrida un píxel, detrás. Se ve igual y no hay que recalcular 81 sombras en cada cuadro de la
        /// animación.
        /// </summary>
        private static Grid Shadowed(TextBlock label)
        {
            var shadow = new TextBlock
            {
                Foreground = ShadowBrush, FontSize = label.FontSize, TextAlignment = label.TextAlignment,
                TextTrimming = label.TextTrimming, MaxWidth = label.MaxWidth, Margin = new Thickness(label.Margin.Left + 0.8, label.Margin.Top + 1.2, 0, 0)
            };
            shadow.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = label });
            var g = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
            g.Children.Add(shadow);
            g.Children.Add(label);
            return g;
        }

        private static readonly Brush ShadowBrush = MakeShadowBrush();
        private static Brush MakeShadowBrush()
        {
            var b = new SolidColorBrush(Color.FromArgb(0x8C, 0, 0, 0));
            b.Freeze();
            return b;
        }

        /// <summary>1.5.0: mientras dura una animación, el contenido se dibuja como una sola imagen (mucho más liviano).</summary>
        private static void CacheWhile(UIElement el, AnimationTimeline anim)
        {
            el.CacheMode = new BitmapCache { EnableClearType = false, SnapsToDevicePixels = false };
            anim.Completed += (_, _) => el.CacheMode = null;
        }

        // ---------- Soltar sobre una tarjeta: dentro = carpeta; en el borde = mover ----------

        /// <summary>Dónde cae lo arrastrado: -1 = antes, 0 = adentro (carpeta), 1 = después.</summary>
        private static int Zone(FrameworkElement card, DragEventArgs e, bool canInto)
        {
            double x = e.GetPosition(card).X, w = Math.Max(1, card.ActualWidth);
            if (!canInto) return x < w / 2 ? -1 : 1;
            if (x < w * 0.25) return -1;
            if (x > w * 0.75) return 1;
            return 0;
        }

        private static string? DraggedKey(DragEventArgs e) =>
            e.Data.GetDataPresent(KeyFormat) ? e.Data.GetData(KeyFormat) as string : null;

        private void ShowDropHint(Border card, string key, DragEventArgs e)
        {
            var dragged = DraggedKey(e);
            if (dragged == null || dragged == key) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
            bool canInto = !dragged.StartsWith("folder:") && _openFolder == null;
            int z = Zone(card, e, canInto);
            card.BorderThickness = z < 0 ? new Thickness(3, 0, 0, 0) : z > 0 ? new Thickness(0, 0, 3, 0) : new Thickness(2);
            card.BorderBrush = z == 0 ? SelectedBorder : InsertBrush;
            card.Background = z == 0 ? HoverBrush : Brushes.Transparent;
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void OnCardDrop(Border card, string key, DragEventArgs e)
        {
            e.Handled = true;
            card.BorderBrush = Brushes.Transparent;
            card.BorderThickness = new Thickness(2);
            var dragged = DraggedKey(e);
            if (dragged == null || dragged == key) return;
            bool draggedIsFolder = dragged.StartsWith("folder:");
            if (_openFolder != null)
            {
                // dentro de una carpeta abierta: soltar sobre otra app de la misma carpeta = ordenar ahí
                var f = _openFolder;
                int from = f.Targets.FindIndex(t => string.Equals(t, dragged, StringComparison.OrdinalIgnoreCase));
                int to = f.Targets.FindIndex(t => string.Equals(t, key, StringComparison.OrdinalIgnoreCase));
                if (from >= 0 && to >= 0)
                {
                    var moved = LaunchpadLayout.Move(f.Targets, dragged, key, Zone(card, e, false) > 0);
                    f.Targets.Clear(); f.Targets.AddRange(moved);
                }
                else if (!draggedIsFolder) LaunchpadLayout.AddToFolder(Folders, f.Id, dragged);
                SaveFolders();
                return;
            }
            int z = Zone(card, e, !draggedIsFolder);
            if (z == 0)
            {
                if (key.StartsWith("folder:")) LaunchpadLayout.AddToFolder(Folders, key.Substring(7), dragged);
                else
                {
                    var existing = LaunchpadLayout.FolderOf(Folders, key);
                    if (existing != null) LaunchpadLayout.AddToFolder(Folders, existing.Id, dragged);
                    else LaunchpadLayout.CreateFolder(Folders, key, dragged);
                }
            }
            else
            {
                // mover: si venía de una carpeta, primero sale
                if (!draggedIsFolder) LaunchpadLayout.RemoveFromFolder(Folders, dragged);
                var keys = LaunchpadLayout.ApplyOrder(LaunchpadLayout.Arrange(_all, Folders, a => a.Target), Order, a => a.Target)
                                          .Select(x => x.Key(a => a.Target));
                var newOrder = LaunchpadLayout.Move(keys, dragged, key, z > 0);
                Order.Clear();
                Order.AddRange(newOrder);
            }
            SaveFolders();
        }

        /// <summary>Ícono de una app. <paramref name="inFolder"/>: la carpeta abierta donde está (o null).</summary>
        private UIElement Tile(CatalogApp app, LaunchpadFolder? inFolder)
        {
            bool isHidden = Hidden.Contains(app.Target, StringComparer.OrdinalIgnoreCase);
            var card = Card(IconImage(app, _iconSize), app.Name, app.Target, isHidden);
            card.PreviewMouseLeftButtonDown += (_, e) => { _dragKey = app.Target; _dragStart = e.GetPosition(this); _dragged = false; };
            card.PreviewMouseMove += (_, e) => TryStartDrag(card, app.Target, app, e);
            card.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (_dragged) return;
                if (_selected.Count > 0 || Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                { ToggleSelect(app.Target); return; }
                Launcher.Launch(app.Target);
                CloseAnimated();
            };
            card.ContextMenu = _selected.Count > 1 && _selected.Contains(app.Target) ? SelectionMenu() : AppMenu(app, isHidden);
            card.ContextMenuOpening += (_, _) => card.ContextMenu = _selected.Count > 1 && _selected.Contains(app.Target) ? SelectionMenu() : AppMenu(app, isHidden);
            return card;
        }

        private ContextMenu AppMenu(CatalogApp app, bool isHidden)
        {
            var cm = new ContextMenu();
            cm.Items.Add(Mi("Abrir", () => { Launcher.Launch(app.Target); CloseAnimated(); }));
            cm.Items.Add(Mi("Agregar al Dock", () => App.Dock?.PinTarget(app.Name, app.Target)));
            cm.Items.Add(new Separator());
            var current = LaunchpadLayout.FolderOf(Folders, app.Target);
            if (current != null)
                cm.Items.Add(Mi("Sacar de la carpeta", () => { LaunchpadLayout.RemoveFromFolder(Folders, app.Target); SaveFolders(); }));
            var others = Folders.Where(f => f != current).ToList();
            if (others.Count > 0)
            {
                var to = new MenuItem { Header = "Agregar a carpeta" };
                foreach (var f in others)
                {
                    var id = f.Id;
                    to.Items.Add(Mi(f.Name, () => { LaunchpadLayout.AddToFolder(Folders, id, app.Target); SaveFolders(); }));
                }
                cm.Items.Add(to);
            }
            cm.Items.Add(Mi("Nueva carpeta", () =>
            {
                var f = LaunchpadLayout.CreateFolder(Folders, app.Target);
                SaveFolders();
                OpenFolder(f, focusName: true);
            }));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Seleccionar", () => ToggleSelect(app.Target)));
            cm.Items.Add(Mi(isHidden ? "Dejar de ocultar" : "Ocultar", () => { LaunchpadLayout.ToggleHidden(Hidden, app.Target); SettingsStore.Save(); Filter(_page); }));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Desinstalar…", () =>
            {
                if (McDialog.Confirm($"¿Desinstalar {app.Name}?", "Se va a abrir el desinstalador de la app (como en el menú Inicio de Windows).", "Desinstalar"))
                { CloseAnimated(); Uninstaller.Uninstall(app.Name, app.Target); }
            }));
            return cm;
        }

        private static MenuItem Mi(string header, Action click)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, _) => click();
            return mi;
        }

        /// <summary>Carpeta: miniaturas de hasta 9 apps en un cuadrado de vidrio redondeado.</summary>
        private UIElement FolderTile(LaunchpadEntry<CatalogApp> entry)
        {
            var folder = entry.Folder!;
            string key = LaunchpadLayout.FolderKey(folder);
            bool isHidden = Hidden.Contains(key, StringComparer.OrdinalIgnoreCase);
            var minis = new UniformGrid { Columns = 3, Rows = 3, Margin = new Thickness(_iconSize / 10) };
            foreach (var a in entry.Apps.Take(9)) minis.Children.Add(IconImage(a, Math.Round(_iconSize * 0.24)));
            var box = new Border
            {
                Width = _iconSize, Height = _iconSize,
                CornerRadius = new CornerRadius(_iconSize / 4),
                Background = new LinearGradientBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF), 90),
                BorderBrush = new LinearGradientBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF), 90),
                BorderThickness = new Thickness(1),
                Child = minis
            };
            var card = Card(box, folder.Name, key, isHidden);
            card.PreviewMouseLeftButtonDown += (_, e) => { _dragKey = key; _dragStart = e.GetPosition(this); _dragged = false; };
            card.PreviewMouseMove += (_, e) => TryStartDrag(card, key, null, e);
            card.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (_dragged) return;
                if (_selected.Count > 0 || Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                { ToggleSelect(key); return; }
                OpenFolder(folder);
            };
            ContextMenu Menu()
            {
                if (_selected.Count > 1 && _selected.Contains(key)) return SelectionMenu();
                var cm = new ContextMenu();
                cm.Items.Add(Mi("Abrir", () => OpenFolder(folder)));
                cm.Items.Add(Mi("Renombrar", () => OpenFolder(folder, focusName: true)));
                cm.Items.Add(new Separator());
                cm.Items.Add(Mi("Seleccionar", () => ToggleSelect(key)));
                cm.Items.Add(Mi(isHidden ? "Dejar de ocultar" : "Ocultar carpeta", () => { LaunchpadLayout.ToggleHidden(Hidden, key); SettingsStore.Save(); Filter(_page); }));
                cm.Items.Add(new Separator());
                cm.Items.Add(Mi("Deshacer carpeta", () => { LaunchpadLayout.Dissolve(Folders, folder.Id); SaveFolders(); }));
                return cm;
            }
            card.ContextMenu = Menu();
            card.ContextMenuOpening += (_, _) => card.ContextMenu = Menu();
            return card;
        }

        // ================== Selección de varias (0.4.3) ==================

        private readonly Border _selectionBar = new Border { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 6, 8, 6) };

        private void ToggleSelect(string key)
        {
            if (!_selected.Remove(key)) _selected.Add(key);
            if (_cards.TryGetValue(key, out var card))
            {
                card.Background = _selected.Contains(key) ? SelectedBrush : Brushes.Transparent;
                if (card.Child is StackPanel sp && sp.Children[0] is Grid g && g.Children.Count > 1)
                    g.Children[1].Visibility = _selected.Contains(key) ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdateSelectionBar();
        }

        private void ClearSelection()
        {
            foreach (var k in _selected.ToList()) ToggleSelect(k);
        }

        private void UpdateSelectionBar()
        {
            if (_selected.Count == 0) { _selectionBar.Visibility = Visibility.Collapsed; _dots.Visibility = Visibility.Visible; return; }
            _dots.Visibility = Visibility.Collapsed;
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = $"{_selected.Count} seleccionada(s)", Foreground = LabelBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), FontWeight = FontWeights.SemiBold });
            row.Children.Add(BarButton("Ocultar", () => SetHidden(true)));
            row.Children.Add(BarButton("Mostrar", () => SetHidden(false)));
            row.Children.Add(BarButton("Nueva carpeta", GroupSelected));
            row.Children.Add(BarButton("Cancelar", ClearSelection));
            _selectionBar.Child = row;
            _selectionBar.Background = Frozen(Color.FromArgb(0x66, 0x20, 0x20, 0x24));
            _selectionBar.BorderBrush = Frozen(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            _selectionBar.BorderThickness = new Thickness(1);
            _selectionBar.Visibility = Visibility.Visible;
        }

        private Button BarButton(string text, Action click)
        {
            var b = new Button { Content = text, Foreground = LabelBrush, Margin = new Thickness(2, 0, 2, 0) };
            b.SetResourceReference(StyleProperty, "McFlatButton");
            b.Click += (_, e) => { click(); };
            b.MouseLeftButtonUp += (_, e) => e.Handled = true;
            return b;
        }

        private ContextMenu SelectionMenu()
        {
            var cm = new ContextMenu();
            cm.Items.Add(Mi($"Ocultar {_selected.Count}", () => SetHidden(true)));
            cm.Items.Add(Mi($"Dejar de ocultar {_selected.Count}", () => SetHidden(false)));
            cm.Items.Add(Mi("Nueva carpeta con las seleccionadas", GroupSelected));
            if (Folders.Count > 0)
            {
                var to = new MenuItem { Header = "Agregar a carpeta" };
                foreach (var f in Folders.ToList())
                {
                    var id = f.Id;
                    to.Items.Add(Mi(f.Name, () =>
                    {
                        foreach (var k in _selected.Where(k => !k.StartsWith("folder:")).ToList()) LaunchpadLayout.AddToFolder(Folders, id, k);
                        _selected.Clear(); UpdateSelectionBar(); SaveFolders();
                    }));
                }
                cm.Items.Add(to);
            }
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Cancelar selección", ClearSelection));
            return cm;
        }

        private void SetHidden(bool hide)
        {
            foreach (var k in _selected)
            {
                bool isHidden = Hidden.Contains(k, StringComparer.OrdinalIgnoreCase);
                if (isHidden != hide) LaunchpadLayout.ToggleHidden(Hidden, k);
            }
            _selected.Clear();
            UpdateSelectionBar();
            SettingsStore.Save();
            Filter(_page);
        }

        private void GroupSelected()
        {
            var apps = _selected.Where(k => !k.StartsWith("folder:")).ToList();
            _selected.Clear();
            UpdateSelectionBar();
            if (apps.Count == 0) { Filter(_page); return; }
            var f = LaunchpadLayout.CreateFolder(Folders, apps[0], apps.Count > 1 ? apps[1] : null);
            foreach (var k in apps.Skip(2)) LaunchpadLayout.AddToFolder(Folders, f.Id, k);
            SaveFolders();
            OpenFolder(f, focusName: true);
        }

        // ================== Arrastrar ==================

        private const string KeyFormat = "McBar.LaunchpadKey";
        private string? _dragKey;
        private bool _dragging, _droppedOutside;

        /// <summary>
        /// Arrastrar: dentro del Launchpad mueve o arma carpetas. 0.4.3: como en los cajones de Android, si se
        /// lleva una app hacia abajo (al Dock) o a los bordes, el Launchpad se esconde y se puede soltar en el
        /// Dock (se fija) o en el escritorio (Windows crea un acceso directo).
        /// </summary>
        private void TryStartDrag(FrameworkElement source, string key, CatalogApp? app, MouseEventArgs e)
        {
            if (_dragKey != key || e.LeftButton != MouseButtonState.Pressed || _dragging) return;
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - _dragStart.X) < 8 && Math.Abs(p.Y - _dragStart.Y) < 8) return;
            _dragged = true;
            _dragging = true;
            _droppedOutside = false;
            source.Opacity = 0.45;
            var data = new DataObject();
            data.SetData(KeyFormat, key);
            if (app != null)
            {
                data.SetData(typeof(string), app.Target);
                data.SetData(DockPinFormat, app.Name + "\n" + app.Target);
                // 1.4.1: para el escritorio (o cualquier carpeta) va un acceso directo .lnk de verdad, armado al
                // empezar a arrastrar. Antes se pasaba la lista de ítems del Shell y el escritorio no lo aceptaba.
                var lnk = TempLinkFor(app);
                if (lnk != null) data.SetFileDropList(new System.Collections.Specialized.StringCollection { lnk });
            }
            QueryContinueDragEventHandler watch = (_, qe) =>
            {
                if (app == null || _droppedOutside || !Win32.GetCursorPos(out var pt)) return;
                var local = PointFromScreen(new Point(pt.x, pt.y)); // 1.5.0: bien con cualquier escala
                double x = local.X, y = local.Y;
                // 1.6.1: zonas más grandes (abajo, donde están los puntitos y el Dock; arriba, la búsqueda; y los
                // costados): apenas se lleva la app ahí, el Launchpad se cierra y se la puede soltar en el Dock o el escritorio
                if (LaunchpadLayout.LeavesGrid(x, y, ActualWidth, ActualHeight))
                {
                    // fuera de la grilla: se esconde el Launchpad para soltar en el Dock o el escritorio
                    _droppedOutside = true;
                    Hide();
                }
            };
            source.QueryContinueDrag += watch;
            try { DragDrop.DoDragDrop(source, data, DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link); }
            catch (Exception ex) { Log.Write("Launchpad arrastre: " + ex.Message); }
            finally
            {
                source.QueryContinueDrag -= watch;
                _dragging = false;
                _dragKey = null;
                source.Opacity = 1;
            }
            if (_droppedOutside) { _closing = true; ClosedAt = DateTime.Now; Close(); App.ScheduleTrim(); }
        }

        /// <summary>Acceso directo temporal "Nombre.lnk" para soltar la app en el escritorio (null si no se pudo).</summary>
        private static string? TempLinkFor(CatalogApp app)
        {
            try
            {
                var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "McBar", "arrastre");
                // limpiar los de arrastres viejos (más de un día)
                if (System.IO.Directory.Exists(root))
                    foreach (var old in System.IO.Directory.GetDirectories(root))
                        try { if ((DateTime.Now - System.IO.Directory.GetCreationTime(old)).TotalDays > 1) System.IO.Directory.Delete(old, true); } catch { }
                var dir = System.IO.Path.Combine(root, Guid.NewGuid().ToString("N").Substring(0, 8));
                System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, MacIcons.SafeFileName(app.Name) + ".lnk");
                return ShellCom.CreateLinkTo(app.Target, path, app.Name) ? path : null;
            }
            catch (Exception ex) { Log.Write("Launchpad acceso directo: " + ex.Message); return null; }
        }

        /// <summary>Formato que entiende el Dock para fijar una app arrastrada desde el Launchpad ("nombre\ntarget").</summary>
        public const string DockPinFormat = "McBar.Pin";

        private void SaveFolders()
        {
            SettingsStore.Save();
            var reopen = _openFolder != null ? Folders.FirstOrDefault(f => f.Id == _openFolder.Id) : null;
            Filter(_page);
            if (_openFolder != null)
            {
                if (reopen != null) OpenFolder(reopen); else CloseFolder();
            }
        }

        // ================== Carpeta abierta ==================

        private void OpenFolder(LaunchpadFolder folder, bool focusName = false)
        {
            bool already = _folderLayer.Visibility == Visibility.Visible;
            _openFolder = folder;
            _folderLayer.Children.Clear();
            _folderLayer.Background = Frozen(Color.FromArgb(0x50, 0, 0, 0));
            _folderLayer.AllowDrop = true;
            // soltar una app fuera del recuadro = sacarla de la carpeta
            _folderLayer.Drop -= FolderLayer_Drop;
            _folderLayer.Drop += FolderLayer_Drop;
            _folderLayer.MouseLeftButtonUp -= FolderLayer_Click;
            _folderLayer.MouseLeftButtonUp += FolderLayer_Click;

            var apps = LaunchpadLayout.Arrange(_all, new List<LaunchpadFolder> { folder }, a => a.Target)
                                      .FirstOrDefault(x => x.IsFolder)?.Apps ?? new List<CatalogApp>();

            var name = new TextBox
            {
                Text = folder.Name,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 14)
            };
            void Commit()
            {
                if (name.Text.Trim() == folder.Name) return;
                LaunchpadLayout.Rename(Folders, folder.Id, name.Text);
                SettingsStore.Save();
                Filter(_page);
            }
            name.LostKeyboardFocus += (_, _) => Commit();
            name.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); Keyboard.Focus(_search); e.Handled = true; } };

            int cols = Math.Clamp(apps.Count, 3, 5);
            var grid = new UniformGrid { Columns = cols };
            foreach (var a in apps) grid.Children.Add(Tile(a, folder));

            var sp = new StackPanel();
            sp.Children.Add(name);
            sp.Children.Add(new ScrollViewer
            {
                Content = grid,
                MaxHeight = Math.Max(240, Height * 0.55),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            });
            var panel = new Border
            {
                Child = sp,
                Padding = new Thickness(26, 18, 26, 22),
                CornerRadius = new CornerRadius(30),
                Background = Frozen(Color.FromArgb(0x55, 0x30, 0x30, 0x36)),
                BorderBrush = Frozen(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 420
            };
            panel.MouseLeftButtonUp += (_, e) => e.Handled = true; // clic adentro no cierra
            panel.Drop += (_, e) => e.Handled = true;              // soltar adentro no saca la app
            _folderLayer.Children.Add(panel);
            _folderLayer.Visibility = Visibility.Visible;

            if (!already)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var zoom = new ScaleTransform(0.85, 0.85);
                panel.RenderTransformOrigin = new Point(0.5, 0.5);
                panel.RenderTransform = zoom;
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
                _folderLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                _content.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(180)));
            }
            if (focusName) Dispatcher.BeginInvoke(new Action(() => { name.Focus(); name.SelectAll(); }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void FolderLayer_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            CloseFolder();
        }

        private void FolderLayer_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (e.Data.GetData(typeof(string)) is string dragged && LaunchpadLayout.RemoveFromFolder(Folders, dragged))
                SaveFolders();
        }

        private void CloseFolder()
        {
            if (_folderLayer.Visibility != Visibility.Visible) return;
            _openFolder = null;
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
            fade.Completed += (_, _) => { if (_openFolder == null) { _folderLayer.Visibility = Visibility.Collapsed; _folderLayer.Children.Clear(); } };
            _folderLayer.BeginAnimation(OpacityProperty, fade);
            _content.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
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
                    if (_selected.Count > 0) ClearSelection();
                    else if (_folderLayer.Visibility == Visibility.Visible) CloseFolder();
                    else if (_search.Text.Length > 0) _search.Text = ""; else CloseAnimated();
                    e.Handled = true;
                    break;
                case Key.Enter:
                    if (_folderLayer.Visibility == Visibility.Visible) { e.Handled = true; break; }
                    var first = _shown.FirstOrDefault(x => !x.IsFolder);
                    if (first?.App != null) { Launcher.Launch(first.App.Target); CloseAnimated(); }
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
