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
        private List<LaunchpadEntry<CatalogApp>> _shown = new List<LaunchpadEntry<CatalogApp>>();
        private readonly Grid _folderLayer = new Grid { Visibility = Visibility.Collapsed };
        private LaunchpadFolder? _openFolder;
        private Point _dragStart;
        private CatalogApp? _dragApp;
        private bool _dragged;
        private static List<LaunchpadFolder> Folders => SettingsStore.Current.LaunchpadFolders;
        private UIElement? _currentPage;
        private int _page, _pageCount = 1;
        private DateTime _lastWheel = DateTime.MinValue;
        private bool _closing, _loaded;
        private readonly bool _fast;
        /// <summary>Lo que se funde al abrir/cerrar: la ventana (modo en capas) o su contenido (modo rápido).</summary>
        private UIElement Fader => _fast ? _root : this;

        public DateTime ClosedAt { get; private set; } = DateTime.MinValue;

        public LaunchpadWindow(MonitorInfo monitor)
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
            double sc = App.Scale;
            Left = monitor.Bounds.Left / sc;
            Top = monitor.Bounds.Top / sc;
            Width = monitor.Bounds.Width / sc;
            Height = monitor.Bounds.Height / sc;
            SetResourceReference(FontFamilyProperty, "Mc.Font");
            if (_fast) _root.Opacity = 0; else Opacity = 0;

            _root.Background = DimBrush();
            _root.Children.Add(_content);
            _root.Children.Add(_folderLayer);
            _content.RenderTransformOrigin = new Point(0.5, 0.5);
            _content.RenderTransform = _zoom;
            _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(124) });
            _content.RowDefinitions.Add(new RowDefinition());
            _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(90) });

            // Búsqueda (píldora arriba al centro)
            // (abajo de la fila para no quedar debajo de la isla dinámica)
            var searchHost = new Grid { Width = 260, Height = 32, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 20) };
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
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Fader.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            _zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.08, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            _zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.08, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        }

        public void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            ClosedAt = DateTime.Now;
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            fade.Completed += (_, _) => { Close(); _icons.Clear(); App.ScheduleTrim(); };
            Fader.BeginAnimation(OpacityProperty, fade);
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
            _shown = q.Length == 0
                ? LaunchpadLayout.Arrange(_all, Folders, a => a.Target)
                : LaunchpadLayout.Search(_all, Folders, a => a.Target, a => a.Name, q);
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
                    }, useCache: false);
                }
                list.Add(img);
            }
            return img;
        }

        private Border Card(UIElement icon, string text)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = LabelBrush,
                FontSize = 12.5,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 120,
                Margin = new Thickness(0, 6, 0, 0)
            };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(icon);
            sp.Children.Add(label);
            var card = new Border
            {
                Child = sp,
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(16),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = text,
                Cursor = Cursors.Hand,
                AllowDrop = true
            };
            card.MouseEnter += (_, _) => card.Background = HoverBrush;
            card.MouseLeave += (_, _) => card.Background = Brushes.Transparent;
            card.DragEnter += (_, _) => card.Background = HoverBrush;
            card.DragLeave += (_, _) => card.Background = Brushes.Transparent;
            return card;
        }

        /// <summary>Ícono de una app. <paramref name="inFolder"/>: la carpeta abierta donde está (o null).</summary>
        private UIElement Tile(CatalogApp app, LaunchpadFolder? inFolder)
        {
            var card = Card(IconImage(app, 80), app.Name);
            card.PreviewMouseLeftButtonDown += (_, e) => { _dragApp = app; _dragStart = e.GetPosition(this); _dragged = false; };
            card.PreviewMouseMove += (_, e) => TryStartDrag(card, app, e);
            card.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (_dragged) return;
                Launcher.Launch(app.Target);
                CloseAnimated();
            };
            // soltar otra app encima: carpeta nueva con las dos
            card.Drop += (_, e) =>
            {
                e.Handled = true;
                card.Background = Brushes.Transparent;
                if (e.Data.GetData(typeof(string)) is not string dragged || dragged == app.Target) return;
                if (inFolder != null) LaunchpadLayout.AddToFolder(Folders, inFolder.Id, dragged);
                else
                {
                    var existing = LaunchpadLayout.FolderOf(Folders, app.Target);
                    if (existing != null) LaunchpadLayout.AddToFolder(Folders, existing.Id, dragged);
                    else LaunchpadLayout.CreateFolder(Folders, app.Target, dragged);
                }
                SaveFolders();
            };

            var cm = new ContextMenu();
            var add = new MenuItem { Header = "Agregar al Dock" };
            add.Click += (_, _) => App.Dock?.PinTarget(app.Name, app.Target);
            cm.Items.Add(add);
            cm.Items.Add(new Separator());
            var current = LaunchpadLayout.FolderOf(Folders, app.Target);
            if (current != null)
            {
                var outItem = new MenuItem { Header = "Sacar de la carpeta" };
                outItem.Click += (_, _) => { LaunchpadLayout.RemoveFromFolder(Folders, app.Target); SaveFolders(); };
                cm.Items.Add(outItem);
            }
            var others = Folders.Where(f => f != current).ToList();
            if (others.Count > 0)
            {
                var to = new MenuItem { Header = "Agregar a carpeta" };
                foreach (var f in others)
                {
                    var fi = new MenuItem { Header = f.Name };
                    var id = f.Id;
                    fi.Click += (_, _) => { LaunchpadLayout.AddToFolder(Folders, id, app.Target); SaveFolders(); };
                    to.Items.Add(fi);
                }
                cm.Items.Add(to);
            }
            var nf = new MenuItem { Header = "Nueva carpeta" };
            nf.Click += (_, _) =>
            {
                var f = LaunchpadLayout.CreateFolder(Folders, app.Target);
                SaveFolders();
                OpenFolder(f, focusName: true);
            };
            cm.Items.Add(nf);
            card.ContextMenu = cm;
            return card;
        }

        /// <summary>Carpeta: miniaturas de hasta 9 apps en un cuadrado redondeado.</summary>
        private UIElement FolderTile(LaunchpadEntry<CatalogApp> entry)
        {
            var folder = entry.Folder!;
            var minis = new UniformGrid { Columns = 3, Rows = 3, Margin = new Thickness(7) };
            foreach (var a in entry.Apps.Take(9)) minis.Children.Add(IconImage(a, 20));
            var box = new Border
            {
                Width = 80, Height = 80,
                CornerRadius = new CornerRadius(18),
                Background = Frozen(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                BorderBrush = Frozen(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Child = minis
            };
            var card = Card(box, folder.Name);
            card.MouseLeftButtonUp += (_, e) => { e.Handled = true; OpenFolder(folder); };
            card.Drop += (_, e) =>
            {
                e.Handled = true;
                card.Background = Brushes.Transparent;
                if (e.Data.GetData(typeof(string)) is string dragged) { LaunchpadLayout.AddToFolder(Folders, folder.Id, dragged); SaveFolders(); }
            };
            var cm = new ContextMenu();
            var open = new MenuItem { Header = "Abrir" };
            open.Click += (_, _) => OpenFolder(folder);
            var rename = new MenuItem { Header = "Renombrar" };
            rename.Click += (_, _) => OpenFolder(folder, focusName: true);
            var undo = new MenuItem { Header = "Deshacer carpeta" };
            undo.Click += (_, _) => { LaunchpadLayout.Dissolve(Folders, folder.Id); SaveFolders(); };
            cm.Items.Add(open);
            cm.Items.Add(rename);
            cm.Items.Add(new Separator());
            cm.Items.Add(undo);
            card.ContextMenu = cm;
            return card;
        }

        // ================== Arrastrar ==================

        private bool _dragging;

        private void TryStartDrag(FrameworkElement source, CatalogApp app, MouseEventArgs e)
        {
            if (_dragApp != app || e.LeftButton != MouseButtonState.Pressed || _dragging) return;
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - _dragStart.X) < 8 && Math.Abs(p.Y - _dragStart.Y) < 8) return;
            _dragged = true;
            _dragging = true;
            source.Opacity = 0.45;
            try { DragDrop.DoDragDrop(source, new DataObject(typeof(string), app.Target), DragDropEffects.Move); }
            catch (Exception ex) { Log.Write("Launchpad arrastre: " + ex.Message); }
            finally
            {
                _dragging = false;
                _dragApp = null;
                source.Opacity = 1;
            }
        }

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
                    if (_folderLayer.Visibility == Visibility.Visible) CloseFolder();
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
