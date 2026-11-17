// McBar — Creado por Andrés Londero
// Lógica del Dock: ampliación con el mouse, rebote al abrir apps, indicadores de apps abiertas,
// arrastrar para reordenar o quitar, soltar archivos/apps, menús contextuales, Descargas y Papelera.
//
// Consumo: no hay temporizadores corriendo en reposo. El bucle de animación (CompositionTarget.Rendering)
// se engancha solo mientras algo se mueve y se suelta solo cuando todo quedó quieto.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;
using McBar.Panels;

namespace McBar.Dock
{
    public partial class DockWindow : Window
    {
        // Medidas (en unidades de WPF, 1 = 1 píxel a 100 % de escala)
        private const double Pad = 7;            // espacio vertical entre íconos y borde de la barra
        private const double BarPadX = 9;        // espacio a los costados de la barra
        private const double BottomMargin = 5;   // separación entre la barra y el borde de la pantalla
        private const double LabelSpace = 46;    // lugar arriba para la etiqueta con el nombre
        private const double SeparatorWidth = 14;
        private const string TrashParsingName = "::{645FF040-5081-101B-9F08-00AA002F954E}";

        private readonly List<DockItem> _items = new List<DockItem>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly DispatcherTimer _hideTimer;
        private AppBar? _appBar;

        private bool _animating;
        private double _lastTick;
        private bool _hover;
        private double _mouseX;
        private double? _anchorX;
        private DockItem? _hoverItem;
        private bool _menuOpen;
        private bool _hidden;
        private bool _fullscreen;

        // arrastre
        private DockItem? _pressItem;
        private Point _pressPoint;
        private bool _dragging;
        private Point _dragPos;

        private double BaseSize => SettingsStore.Current.DockIconSize;
        private double MaxSize => SettingsStore.Current.Magnification ? SettingsStore.Current.MagnifiedSize : BaseSize;
        private double Gap => Math.Round(BaseSize * 0.1);
        private double BarHeight => BaseSize + Pad * 2;
        private double WindowHeightDip => Math.Max(MaxSize, BaseSize + BaseSize * 0.55) + Pad + BottomMargin + LabelSpace;
        private double BarTop => ActualHeight - BottomMargin - BarHeight;
        private double IconBottom => ActualHeight - BottomMargin - Pad;

        public DockWindow()
        {
            InitializeComponent();
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); TryAutoHide(); };

            Stage.MouseMove += Stage_MouseMove;
            Stage.MouseLeave += Stage_MouseLeave;
            Stage.MouseLeftButtonUp += Stage_MouseLeftButtonUp;
            Bar.MouseRightButtonUp += (_, e) => { ShowDockMenu(); e.Handled = true; };
            HotZone.MouseEnter += (_, _) => { if (_hidden) { _hidden = false; EnsureAnimating(); } };
            SizeChanged += (_, _) => EnsureAnimating();
            DragOver += Window_DragOver;
            Drop += Window_Drop;
            DragLeave += (_, _) => { _hover = false; EnsureAnimating(); };

            SourceInitialized += (_, _) =>
            {
                WindowOps.MakeToolNoActivate(this);
                _appBar = new AppBar(this, AppBarEdge.Bottom);
                _appBar.FullscreenChanged += fs => App.OnFullscreenChanged(fs);
                ApplyGeometry();
            };
            Loaded += (_, _) =>
            {
                ApplyGeometry();
                BuildItems();
                App.Tracker.WindowsChanged += SyncWindows;
                SyncWindows();
                SettingsStore.Changed += OnSettingsChanged;
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(new Action(ApplyGeometry));
            };
        }

        // ================== Geometría ==================

        public void ApplyGeometry()
        {
            if (_appBar == null) return;
            double scale = WindowOps.DpiScale(this);
            int heightPx = (int)Math.Ceiling(WindowHeightDip * scale);
            int reservePx = (int)Math.Ceiling((BarHeight + BottomMargin) * scale);
            if (SettingsStore.Current.DockAutoHide)
            {
                _appBar.PlaceWithoutReserving(heightPx);
                HotZone.Visibility = Visibility.Visible;
            }
            else
            {
                _appBar.Register();
                _appBar.Apply(reservePx, heightPx);
                HotZone.Visibility = Visibility.Collapsed;
                _hidden = false;
            }
            EnsureAnimating();
        }

        public void ReRegisterAppBar()
        {
            if (_appBar == null) return;
            if (SettingsStore.Current.DockAutoHide) ApplyGeometry();
            else { _appBar.ReRegister(); ApplyGeometry(); }
        }

        public void SetFullscreen(bool fullscreen)
        {
            if (_fullscreen == fullscreen) return;
            _fullscreen = fullscreen;
            Visibility = fullscreen ? Visibility.Hidden : Visibility.Visible;
        }

        public void Teardown()
        {
            _appBar?.Remove();
            SettingsStore.Changed -= OnSettingsChanged;
        }

        private int _lastIconSize;

        private void OnSettingsChanged()
        {
            ApplyGeometry();
            if (_lastIconSize != IconPixelSize()) ReloadIcons();
            foreach (var it in _items) it.View.Visibility = Visibility.Visible;
            EnsureAnimating();
        }

        private int IconPixelSize()
        {
            double scale = Math.Max(1.0, WindowOps.DpiScale(this));
            return (int)Math.Min(256, Math.Ceiling(MaxSize * scale / 16.0) * 16);
        }

        // ================== Elementos ==================

        private void BuildItems()
        {
            foreach (var p in SettingsStore.Current.Pinned)
            {
                var item = CreatePinned(p);
                if (item != null) AddItem(item, _items.Count);
            }
            AddItem(new DockItem(DockItemKind.Separator) { Name = "" }, _items.Count);
            AddItem(new DockItem(DockItemKind.Downloads) { Name = "Descargas", IsPinned = true }, _items.Count);
            AddItem(new DockItem(DockItemKind.Trash) { Name = "Papelera", IsPinned = true }, _items.Count);
            foreach (var it in _items) it.CurrentSize = it.Kind == DockItemKind.Separator ? SeparatorWidth : BaseSize;
            ReloadIcons();
        }

        private static DockItem? CreatePinned(PinnedApp p)
        {
            if (p.Target == AppIdentity.Launchpad)
                return new DockItem(DockItemKind.Launchpad) { Name = "Launchpad", Target = p.Target, IsPinned = true };
            if (AppIdentity.IsSpecial(p.Target)) return null;
            return new DockItem(DockItemKind.App)
            {
                Name = string.IsNullOrWhiteSpace(p.Name) ? AppIdentity.FriendlyName(p.Target) : p.Name,
                Target = p.Target,
                IsPinned = true,
                Keys = AppIdentity.KeysForTarget(p.Target),
                IconSource = p.Target
            };
        }

        private void AddItem(DockItem item, int index)
        {
            _items.Insert(Math.Clamp(index, 0, _items.Count), item);
            var v = item.View;
            v.MouseEnter += (_, _) => { _hoverItem = item; EnsureAnimating(); };
            v.MouseLeave += (_, _) => { if (_hoverItem == item) { _hoverItem = null; EnsureAnimating(); } };
            v.MouseLeftButtonDown += (_, e) => { OnItemDown(item, e); e.Handled = true; };
            v.MouseUp += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Right) { ShowItemMenu(item); e.Handled = true; }
                else if (e.ChangedButton == MouseButton.Middle && item.Kind == DockItemKind.App) { LaunchNew(item); e.Handled = true; }
            };
            Stage.Children.Add(v);
        }

        private int SeparatorIndex => _items.FindIndex(i => i.Kind == DockItemKind.Separator);
        private int PinnedEnd => _items.FindLastIndex(i => i.IsPinned && (i.Kind == DockItemKind.App || i.Kind == DockItemKind.Launchpad)) + 1;

        private void ReloadIcons()
        {
            _lastIconSize = IconPixelSize();
            foreach (var it in _items) LoadIcon(it);
        }

        private void LoadIcon(DockItem it)
        {
            int px = _lastIconSize > 0 ? _lastIconSize : IconPixelSize();
            switch (it.Kind)
            {
                case DockItemKind.Launchpad:
                    it.View.SetIcon(TryFindResource("Mc.LaunchpadIcon") as ImageSource);
                    break;
                case DockItemKind.Downloads:
                    var dl = Win32.KnownFolder(Win32.FOLDERID_Downloads);
                    if (dl != null) IconLoader.Request(dl, px, img => it.View.SetIcon(img));
                    break;
                case DockItemKind.Trash:
                    IconLoader.Forget(TrashParsingName);
                    IconLoader.Request(TrashParsingName, px, img => it.View.SetIcon(img));
                    break;
                case DockItemKind.App:
                    if (!string.IsNullOrEmpty(it.IconSource))
                    {
                        var hwnd = it.Windows.Count > 0 ? it.Windows[0].Hwnd : IntPtr.Zero;
                        IconLoader.Request(it.IconSource!, px, img =>
                        {
                            if (img == null && hwnd != IntPtr.Zero) img = IconLoader.FromWindow(hwnd);
                            it.View.SetIcon(img);
                        });
                    }
                    else if (it.Windows.Count > 0)
                    {
                        it.View.SetIcon(IconLoader.FromWindow(it.Windows[0].Hwnd));
                    }
                    break;
            }
        }

        /// <summary>Reparte las ventanas abiertas entre los íconos fijados y agrega/quita apps abiertas sin fijar.</summary>
        private void SyncWindows()
        {
            var windows = App.Tracker.Windows;
            var claimed = new HashSet<IntPtr>();

            foreach (var it in _items.Where(i => i.Kind == DockItemKind.App && i.IsPinned))
            {
                bool wasRunning = it.IsRunning;
                it.Windows = windows.Where(w => w.Keys.Overlaps(it.Keys)).ToList();
                foreach (var w in it.Windows) claimed.Add(w.Hwnd);
                if (!wasRunning && it.IsRunning) StopBounceSoon(it);
            }

            var groups = windows.Where(w => !claimed.Contains(w.Hwnd)).GroupBy(w => w.PrimaryKey).ToList();
            var groupKeys = new HashSet<string>(groups.Select(g => g.Key));

            // quitar apps sin fijar que ya se cerraron
            foreach (var it in _items.Where(i => i.Kind == DockItemKind.App && !i.IsPinned && !i.Removing).ToList())
            {
                if (it.GroupKey == null || !groupKeys.Contains(it.GroupKey)) { it.Windows.Clear(); it.Removing = true; }
            }

            foreach (var g in groups)
            {
                var existing = _items.FirstOrDefault(i => !i.IsPinned && i.GroupKey == g.Key && i.Kind == DockItemKind.App);
                if (existing != null)
                {
                    existing.Windows = g.ToList();
                    existing.Removing = false;
                    continue;
                }
                var first = g.First();
                var item = new DockItem(DockItemKind.App)
                {
                    Name = App.Tracker.AppNameFor(first.Hwnd),
                    GroupKey = g.Key,
                    Keys = new HashSet<string>(first.Keys),
                    Windows = g.ToList(),
                    IconSource = string.IsNullOrEmpty(first.IconSource) ? null : first.IconSource,
                    CurrentSize = 0
                };
                AddItem(item, SeparatorIndex);
                LoadIcon(item);
            }
            EnsureAnimating();
        }

        private void StopBounceSoon(DockItem it)
        {
            // deja terminar el salto actual y no empieza otro
            if (it.BounceStart < 0) return;
            double elapsed = _clock.Elapsed.TotalSeconds - it.BounceStart;
            const double period = 0.62;
            double endOfCurrent = Math.Ceiling(elapsed / period) * period;
            it.BounceStart = _clock.Elapsed.TotalSeconds - (3 * period - (endOfCurrent - elapsed));
        }

        // ================== Animación ==================

        private void EnsureAnimating()
        {
            if (_animating) return;
            _animating = true;
            _lastTick = _clock.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            double now = _clock.Elapsed.TotalSeconds;
            double dt = Math.Clamp(now - _lastTick, 0.001, 0.05);
            _lastTick = now;
            if (!Step(dt, now))
            {
                CompositionTarget.Rendering -= OnRendering;
                _animating = false;
            }
        }

        /// <summary>Un cuadro de animación. Devuelve true si algo se sigue moviendo.</summary>
        private bool Step(double dt, double now)
        {
            bool moving = false;
            int n = _items.Count;
            if (n == 0 || ActualWidth <= 0) return false;

            var baseSizes = new double[n];
            var scalable = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var it = _items[i];
                baseSizes[i] = it.Removing ? 0 : it.Kind == DockItemKind.Separator ? SeparatorWidth : BaseSize;
                scalable[i] = it.Kind != DockItemKind.Separator;
            }

            double? mouse = _hover && !_dragging && !_menuOpen ? _mouseX : (double?)null;
            var targets = SettingsStore.Current.Magnification
                ? Magnifier.TargetSizes(baseSizes, scalable, Gap, mouse, MaxSize, BaseSize * 2.7)
                : baseSizes;

            var cur = new double[n];
            for (int i = 0; i < n; i++)
            {
                var it = _items[i];
                double v = Magnifier.Approach(it.CurrentSize, targets[i], dt, mouse != null ? 22 : 14);
                if (v != targets[i]) moving = true;
                it.CurrentSize = v;
                cur[i] = v;
            }

            // quitar los que terminaron de achicarse
            for (int i = n - 1; i >= 0; i--)
            {
                var it = _items[i];
                if (it.Removing && it.CurrentSize <= 0.5)
                {
                    Stage.Children.Remove(it.View);
                    _items.RemoveAt(i);
                    if (_hoverItem == it) _hoverItem = null;
                    moving = true;
                }
            }
            if (_items.Count != n) return true; // recalcular en el próximo cuadro

            var lefts = Magnifier.Layout(cur, baseSizes, Gap, _anchorX, out double start, out double width);
            double cx = ActualWidth / 2;

            Canvas.SetLeft(Bar, cx + start - BarPadX);
            Canvas.SetTop(Bar, BarTop);
            Bar.Width = Math.Max(0, width + BarPadX * 2);
            Bar.Height = BarHeight;

            bool showDots = SettingsStore.Current.ShowIndicators;
            for (int i = 0; i < n; i++)
            {
                var it = _items[i];
                double bounce = 0;
                if (it.BounceStart >= 0)
                {
                    double el = now - it.BounceStart;
                    bounce = Magnifier.Bounce(el, BaseSize * 0.5);
                    if (el >= 3 * 0.62) it.BounceStart = -1; else moving = true;
                }
                double left = cx + lefts[i];
                double extraUp = 0;
                if (_dragging && it == _pressItem)
                {
                    left = _dragPos.X - cur[i] / 2;
                    extraUp = Math.Max(0, _pressPoint.Y - _dragPos.Y);
                }
                it.View.Place(left, cur[i], IconBottom - extraUp, Pad, bounce,
                              showDots && it.IsRunning && it.Kind == DockItemKind.App, BarHeight, BarTop);
            }

            // etiqueta con el nombre
            var hi = _hoverItem;
            if (hi != null && !_dragging && !_menuOpen && hi.Kind != DockItemKind.Separator && !hi.Removing && _hover)
            {
                LabelText.Text = hi.Name;
                Label.Visibility = Visibility.Visible;
                Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var ls = Label.DesiredSize;
                int k = _items.IndexOf(hi);
                if (k >= 0)
                {
                    Canvas.SetLeft(Label, cx + lefts[k] + cur[k] / 2 - ls.Width / 2);
                    Canvas.SetTop(Label, IconBottom - cur[k] - 12 - ls.Height);
                }
            }
            else Label.Visibility = Visibility.Collapsed;

            // ocultación automática
            double hideTarget = _hidden ? BarHeight + BottomMargin + 8 : 0;
            double hy = Magnifier.Approach(HideShift.Y, hideTarget, dt, 12);
            if (hy != hideTarget) moving = true;
            HideShift.Y = hy;

            return moving;
        }

        // ================== Mouse ==================

        private void Stage_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(Stage);
            _hover = true;
            _hideTimer.Stop();
            _mouseX = p.X - ActualWidth / 2;
            if (!_dragging) _anchorX = _mouseX;

            if (_pressItem != null && e.LeftButton == MouseButtonState.Pressed)
            {
                if (!_dragging && (Math.Abs(p.X - _pressPoint.X) > 6 || Math.Abs(p.Y - _pressPoint.Y) > 6) && CanDrag(_pressItem))
                {
                    _dragging = true;
                }
                if (_dragging)
                {
                    _dragPos = p;
                    ReorderWhileDragging(p);
                }
            }
            EnsureAnimating();
        }

        private void Stage_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_dragging) return;
            _hover = false;
            _hoverItem = null;
            EnsureAnimating();
            if (SettingsStore.Current.DockAutoHide) _hideTimer.Start();
        }

        private void OnItemDown(DockItem item, MouseButtonEventArgs e)
        {
            _pressItem = item;
            _pressPoint = e.GetPosition(Stage);
            _dragging = false;
            Stage.CaptureMouse();
        }

        private void Stage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = _pressItem;
            bool wasDragging = _dragging;
            _pressItem = null;
            _dragging = false;
            if (Stage.IsMouseCaptured) Stage.ReleaseMouseCapture();
            if (item == null) return;

            if (wasDragging)
            {
                var p = e.GetPosition(Stage);
                if (p.Y < BarTop - 70 && item.Kind != DockItemKind.Trash && item.Kind != DockItemKind.Downloads)
                    Unpin(item);
                SavePins();
            }
            else
            {
                if (HitItem(e.GetPosition(Stage)) == item) OnItemClick(item);
            }
            // si el mouse ya no está sobre el Dock
            if (!Stage.IsMouseOver) { _hover = false; _hoverItem = null; if (SettingsStore.Current.DockAutoHide) _hideTimer.Start(); }
            EnsureAnimating();
        }

        private static bool CanDrag(DockItem item) =>
            item.IsPinned && (item.Kind == DockItemKind.App || item.Kind == DockItemKind.Launchpad);

        private void ReorderWhileDragging(Point p)
        {
            var item = _pressItem;
            if (item == null) return;
            int from = _items.IndexOf(item);
            int pinnedEnd = PinnedEnd; // los fijados están en [0, pinnedEnd)
            if (from < 0 || from >= pinnedEnd) return;

            int n = _items.Count;
            var baseSizes = _items.Select(i => i.Removing ? 0 : i.Kind == DockItemKind.Separator ? SeparatorWidth : BaseSize).ToArray();
            var centers = Magnifier.BaseCenters(baseSizes, Gap);
            double mx = p.X - ActualWidth / 2;
            int to = from;
            for (int i = 0; i < pinnedEnd; i++)
            {
                if (i == from) continue;
                if (i < from && mx < centers[i]) { to = i; break; }
                if (i > from && mx > centers[i]) to = i;
            }
            if (to != from)
            {
                _items.RemoveAt(from);
                _items.Insert(to, item);
            }
        }

        private DockItem? HitItem(Point p)
        {
            foreach (var it in _items)
            {
                double l = Canvas.GetLeft(it.View), t = Canvas.GetTop(it.View);
                if (p.X >= l && p.X <= l + it.View.Width && p.Y >= t && p.Y <= t + it.View.Height) return it;
            }
            return null;
        }

        private void TryAutoHide()
        {
            if (!SettingsStore.Current.DockAutoHide || _hover || _menuOpen || _dragging) return;
            _hidden = true;
            EnsureAnimating();
        }

        // ================== Acciones ==================

        private void OnItemClick(DockItem item)
        {
            switch (item.Kind)
            {
                case DockItemKind.Launchpad:
                    App.ToggleLaunchpad();
                    break;
                case DockItemKind.Downloads:
                    ShowDownloadsStack(item);
                    break;
                case DockItemKind.Trash:
                    Launcher.Open("shell:RecycleBinFolder");
                    break;
                case DockItemKind.App:
                    if (item.IsRunning)
                    {
                        var fg = Win32.GetForegroundWindow();
                        bool isFront = item.Windows.Any(w => w.Hwnd == fg);
                        if (isFront && SettingsStore.Current.ClickMinimizes)
                        {
                            foreach (var w in item.Windows) WindowOps.Minimize(w.Hwnd);
                        }
                        else
                        {
                            var target = item.Windows.OrderByDescending(w => w.LastActivated).First();
                            WindowOps.Activate(target.Hwnd);
                        }
                    }
                    else LaunchNew(item);
                    break;
            }
        }

        private void LaunchNew(DockItem item)
        {
            var target = item.Target ?? item.Windows.FirstOrDefault()?.ExePath;
            if (string.IsNullOrEmpty(target)) return;
            if (Launcher.Launch(target) && SettingsStore.Current.BounceOnLaunch && !item.IsRunning)
            {
                item.BounceStart = _clock.Elapsed.TotalSeconds;
                EnsureAnimating();
            }
        }

        private void Pin(DockItem item)
        {
            if (item.IsPinned) return;
            var w = item.Windows.FirstOrDefault();
            string? target = null;
            if (w != null)
                target = w.IconSource.StartsWith(AppIdentity.AppsFolderPrefix, StringComparison.OrdinalIgnoreCase) ? w.IconSource : w.ExePath;
            if (string.IsNullOrEmpty(target)) return;
            item.IsPinned = true;
            item.Target = target;
            item.GroupKey = null;
            item.Keys.UnionWith(AppIdentity.KeysForTarget(target));
            int idx = PinnedEnd;
            _items.Remove(item);
            _items.Insert(Math.Min(idx, _items.Count), item);
            SavePins();
            EnsureAnimating();
        }

        private void Unpin(DockItem item)
        {
            if (!item.IsPinned || item.Kind == DockItemKind.Trash || item.Kind == DockItemKind.Downloads) return;
            item.IsPinned = false;
            if (item.IsRunning && item.Kind == DockItemKind.App)
            {
                item.GroupKey = item.Windows[0].PrimaryKey;
                _items.Remove(item);
                _items.Insert(SeparatorIndex, item);
            }
            else item.Removing = true;
            SavePins();
            SyncWindows();
        }

        public void PinTarget(string name, string target)
        {
            if (_items.Any(i => i.IsPinned && string.Equals(i.Target, target, StringComparison.OrdinalIgnoreCase))) return;
            var item = CreatePinned(new PinnedApp { Name = name, Target = target });
            if (item == null) return;
            item.CurrentSize = 0;
            AddItem(item, PinnedEnd);
            LoadIcon(item);
            SavePins();
            SyncWindows();
        }

        private void SavePins()
        {
            SettingsStore.Current.Pinned = _items
                .Where(i => i.IsPinned && !i.Removing && (i.Kind == DockItemKind.App || i.Kind == DockItemKind.Launchpad) && i.Target != null)
                .Select(i => new PinnedApp { Name = i.Name, Target = i.Target! })
                .ToList();
            SettingsStore.Save();
        }

        // ================== Menús ==================

        private ContextMenu NewMenu(UIElement target)
        {
            var cm = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Top };
            cm.Opened += (_, _) => { _menuOpen = true; EnsureAnimating(); };
            cm.Closed += (_, _) =>
            {
                _menuOpen = false;
                if (!Stage.IsMouseOver) { _hover = false; _hoverItem = null; if (SettingsStore.Current.DockAutoHide) _hideTimer.Start(); }
                EnsureAnimating();
            };
            return cm;
        }

        private static MenuItem Mi(string header, Action action, bool enabled = true)
        {
            var mi = new MenuItem { Header = header, IsEnabled = enabled };
            mi.Click += (_, _) => action();
            return mi;
        }

        private void ShowItemMenu(DockItem item)
        {
            var cm = NewMenu(item.View);
            switch (item.Kind)
            {
                case DockItemKind.Separator:
                    ShowDockMenu();
                    return;
                case DockItemKind.Trash:
                    cm.Items.Add(Mi("Abrir", () => Launcher.Open("shell:RecycleBinFolder")));
                    cm.Items.Add(new Separator());
                    cm.Items.Add(Mi("Vaciar papelera…", () =>
                    {
                        if (McDialog.Confirm("¿Vaciar la papelera?", "Los elementos se van a borrar para siempre. Esta acción no se puede deshacer.", "Vaciar", true))
                        {
                            SystemInfo.EmptyRecycleBin();
                            LoadIcon(item);
                        }
                    }, SystemInfo.RecycleBinHasItems()));
                    break;
                case DockItemKind.Downloads:
                    cm.Items.Add(Mi("Abrir carpeta Descargas", () => { var d = Win32.KnownFolder(Win32.FOLDERID_Downloads); if (d != null) Launcher.ShowInExplorer(d); }));
                    break;
                case DockItemKind.Launchpad:
                    cm.Items.Add(Mi("Abrir", App.ToggleLaunchpad));
                    cm.Items.Add(new Separator());
                    cm.Items.Add(Mi("Quitar del Dock", () => Unpin(item)));
                    break;
                case DockItemKind.App:
                    if (item.IsRunning)
                    {
                        foreach (var w in item.Windows.Take(12))
                        {
                            var title = WindowOps.GetTitle(w.Hwnd);
                            if (title.Length > 50) title = title.Substring(0, 49) + "…";
                            var h = w.Hwnd;
                            cm.Items.Add(Mi(string.IsNullOrEmpty(title) ? item.Name : title, () => WindowOps.Activate(h)));
                        }
                        cm.Items.Add(new Separator());
                    }
                    var options = new MenuItem { Header = "Opciones" };
                    var keep = new MenuItem { Header = "Mantener en el Dock", IsCheckable = true, IsChecked = item.IsPinned };
                    keep.Click += (_, _) => { if (item.IsPinned) Unpin(item); else Pin(item); };
                    options.Items.Add(keep);
                    var path = item.Windows.FirstOrDefault()?.ExePath;
                    if (string.IsNullOrEmpty(path) && item.Target != null && File.Exists(item.Target)) path = item.Target;
                    if (!string.IsNullOrEmpty(path)) options.Items.Add(Mi("Mostrar en el Explorador", () => Launcher.ShowInExplorer(path!)));
                    cm.Items.Add(options);
                    cm.Items.Add(new Separator());
                    cm.Items.Add(Mi(item.IsRunning ? "Nueva ventana" : "Abrir", () => LaunchNew(item)));
                    if (item.IsRunning)
                    {
                        cm.Items.Add(Mi("Ocultar", () => { foreach (var w in item.Windows) WindowOps.Minimize(w.Hwnd); }));
                        cm.Items.Add(Mi("Salir", () => { foreach (var w in item.Windows) WindowOps.Close(w.Hwnd); }));
                    }
                    break;
            }
            cm.IsOpen = true;
        }

        private void ShowDockMenu()
        {
            var cm = NewMenu(Bar);
            cm.Placement = PlacementMode.MousePoint;
            var s = SettingsStore.Current;
            var mag = new MenuItem { Header = "Activar ampliación", IsCheckable = true, IsChecked = s.Magnification };
            mag.Click += (_, _) => { s.Magnification = !s.Magnification; SettingsStore.NotifyChanged(); };
            var hide = new MenuItem { Header = "Activar ocultación automática", IsCheckable = true, IsChecked = s.DockAutoHide };
            hide.Click += (_, _) => { s.DockAutoHide = !s.DockAutoHide; SettingsStore.NotifyChanged(); };
            cm.Items.Add(mag);
            cm.Items.Add(hide);
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Preferencias del Dock…", App.OpenSettings));
            cm.IsOpen = true;
        }

        // ================== Descargas (pila) ==================

        private void ShowDownloadsStack(DockItem item)
        {
            var dir = Win32.KnownFolder(Win32.FOLDERID_Downloads);
            if (dir == null || !Directory.Exists(dir)) return;
            List<FileSystemInfo> files;
            try
            {
                files = new DirectoryInfo(dir).EnumerateFileSystemInfos()
                    .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                    .OrderByDescending(f => f.LastWriteTime).Take(12).ToList();
            }
            catch { files = new List<FileSystemInfo>(); }

            const double width = 372;
            var popup = new Popup
            {
                PlacementTarget = item.View,
                Placement = PlacementMode.Top,
                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = PopupAnimation.Fade,
                HorizontalOffset = (item.View.ActualWidth - width) / 2,
                VerticalOffset = -6
            };

            var panel = new StackPanel();
            var title = new TextBlock { Text = "Descargas", FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(6, 0, 0, 8) };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            panel.Children.Add(title);

            var wrap = new WrapPanel();
            if (files.Count == 0)
            {
                var empty = new TextBlock { Text = "No hay descargas", Margin = new Thickness(6, 6, 6, 12) };
                empty.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
                wrap.Children.Add(empty);
            }
            foreach (var f in files)
            {
                var img = new Image { Width = 48, Height = 48 };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                var name = new TextBlock { Text = f.Name, Width = 76, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, FontSize = 11, Margin = new Thickness(0, 4, 0, 0) };
                name.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
                var sp = new StackPanel { Width = 80 };
                sp.Children.Add(img);
                sp.Children.Add(name);
                var b = new Button { Content = sp, Padding = new Thickness(2, 6, 2, 6), ToolTip = f.Name, Height = 96 };
                b.SetResourceReference(StyleProperty, "McFlatButton");
                var path = f.FullName;
                b.Click += (_, _) => { popup.IsOpen = false; Launcher.Launch(path); };
                wrap.Children.Add(b);
                IconLoader.Request(path, 96, s => img.Source = s, useCache: false);
            }
            panel.Children.Add(wrap);

            var open = new Button { Content = "Abrir en el Explorador  ›", Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            open.SetResourceReference(StyleProperty, "McButton");
            open.Click += (_, _) => { popup.IsOpen = false; Launcher.ShowInExplorer(dir); };
            panel.Children.Add(open);

            var border = new Border
            {
                Width = width - 20,
                Margin = new Thickness(10),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                Child = panel,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.35 }
            };
            border.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            border.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            popup.Child = border;
            popup.Opened += (_, _) => { _menuOpen = true; EnsureAnimating(); };
            popup.Closed += (_, _) => { _menuOpen = false; EnsureAnimating(); App.ScheduleTrim(); };
            popup.IsOpen = true;
        }

        // ================== Soltar archivos ==================

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
            var p = e.GetPosition(Stage);
            _hover = true;
            _mouseX = p.X - ActualWidth / 2;
            _anchorX = _mouseX;
            EnsureAnimating();
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
            var target = HitItem(e.GetPosition(Stage));
            if (target?.Kind == DockItemKind.Trash)
            {
                SystemInfo.SendToRecycleBin(files);
                LoadIcon(target);
                return;
            }
            foreach (var f in files)
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".exe" || ext == ".lnk" || Directory.Exists(f))
                    PinTarget(ext == ".lnk" ? Path.GetFileNameWithoutExtension(f) : AppIdentity.FriendlyName(f), f);
            }
            _hover = false;
            EnsureAnimating();
        }
    }
}
