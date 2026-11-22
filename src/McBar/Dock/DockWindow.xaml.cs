// McBar — Creado por Andrés Londero
// Lógica del Dock (uno por monitor): ampliación, rebote al abrir apps, indicadores, arrastrar para
// reordenar o sacar (con "nubecita"), soltar archivos/apps/carpetas, menús, pilas de carpetas,
// Papelera, estilos (clásico, flotante, ancho completo) y ocultación automática.
//
// Consumo: no hay temporizadores corriendo en reposo. El bucle de animación (CompositionTarget.Rendering)
// se engancha solo mientras algo se mueve, y todo se mueve con transformaciones (sin recalcular diseño).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;
using McBar.Panels;

namespace McBar.Dock
{
    public partial class DockWindow : Window
    {
        private const double Pad = 7;            // espacio vertical entre íconos y borde de la barra
        private const double BarPadX = 9;        // espacio a los costados de la barra
        private const double LabelSpace = 46;    // lugar arriba para la etiqueta con el nombre
        private const double SeparatorWidth = 14;
        private const double PullOutDistance = 50; // cuánto hay que arrastrar hacia arriba para quitar
        private const string TrashParsingName = "::{645FF040-5081-101B-9F08-00AA002F954E}";

        private readonly MonitorInfo _monitor;
        private readonly List<DockItem> _items = new List<DockItem>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly DispatcherTimer _hideTimer;
        private readonly DockBackdrop _backdrop;
        private IntPtr _hwnd;
        private int _winLeftPx, _winTopPx, _winWidthPx, _winHeightPx;

        private bool _animating;
        private double _lastTick;
        private bool _hover;
        private double _mouseX;
        private double? _anchorX;
        private DockItem? _hoverItem;
        private bool _menuOpen;
        private bool _hidden;
        private bool _fullscreen;
        private double _barWidthCur = -1;
        private int _lastIconSize;

        // arrastre
        private DockItem? _pressItem;
        private Point _pressPoint;
        private bool _dragging;
        private Point _dragPos;

        public MonitorInfo Monitor => _monitor;

        private static AppSettings S => SettingsStore.Current;
        private double BaseSize => S.DockIconSize;
        private double MaxSize => S.Magnification ? S.MagnifiedSize : BaseSize;
        private double Gap => Math.Round(BaseSize * 0.1);
        private double BarHeight => BaseSize + Pad * 2;
        private double BottomMargin => S.DockStyle == "floating" ? 14 : 4;
        private double Radius => S.DockStyle == "floating" ? 22 : S.DockStyle == "full" ? 14 : 18;
        private double WindowHeightDip => Math.Max(MaxSize, BaseSize * 1.55) + Pad + BottomMargin + LabelSpace;
        private double BarTop => ActualHeight - BottomMargin - BarHeight;
        private double IconBottom => ActualHeight - BottomMargin - Pad;

        /// <summary>Píxeles a reservar abajo para que las ventanas maximizadas no queden tapadas.</summary>
        public int ReservePx => S.DockAutoHide ? 0 : (int)Math.Ceiling((BarHeight + BottomMargin) * App.Scale);

        internal DockWindow(MonitorInfo monitor)
        {
            InitializeComponent();
            _monitor = monitor;

            _backdrop = new DockBackdrop();
            _backdrop.Show();
            Owner = _backdrop; // así el Dock siempre queda por encima de su fondo desenfocado

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); TryAutoHide(); };

            Stage.MouseMove += Stage_MouseMove;
            Stage.MouseLeave += Stage_MouseLeave;
            Stage.MouseLeftButtonUp += Stage_MouseLeftButtonUp;
            Bar.MouseRightButtonUp += (_, e) => { ShowDockMenu(); e.Handled = true; };
            HotZone.MouseEnter += (_, _) => { if (_hidden) { _hidden = false; EnsureAnimating(); } };
            SizeChanged += (_, _) => EnsureAnimating();
            DragOver += Window_DragOver;
            DragLeave += (_, _) => { _hover = false; EnsureAnimating(); };
            Drop += Window_Drop;

            SourceInitialized += (_, _) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                WindowOps.MakeToolNoActivate(this);
                ApplyGeometry();
            };
            Loaded += (_, _) =>
            {
                BuildItems();
                App.Tracker.WindowsChanged += SyncWindows;
                SyncWindows();
                SettingsStore.Changed += OnSettingsChanged;
                SettingsStore.PinsChanged += OnPinsChanged;
            };
            Closed += (_, _) => Teardown();
        }

        // ================== Geometría ==================

        private int ComputeWidthPx()
        {
            if (S.DockStyle == "full") return _monitor.Bounds.Width;
            var baseSizes = _items.Select(BaseOf).ToArray();
            double need = Magnifier.TotalWidth(baseSizes, Gap) + BarPadX * 2 + (MaxSize - BaseSize) * 3 + 280;
            return (int)Math.Min(_monitor.Bounds.Width, Math.Ceiling(need * App.Scale));
        }

        public void ApplyGeometry()
        {
            if (_hwnd == IntPtr.Zero) return;
            double sc = App.Scale;
            _winHeightPx = (int)Math.Ceiling(WindowHeightDip * sc);
            _winWidthPx = ComputeWidthPx();
            var b = _monitor.Bounds;
            _winLeftPx = b.Left + (b.Width - _winWidthPx) / 2;
            _winTopPx = b.Bottom - _winHeightPx;
            Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, _winLeftPx, _winTopPx, _winWidthPx, _winHeightPx, Win32.SWP_NOACTIVATE);

            HotZone.Visibility = S.DockAutoHide ? Visibility.Visible : Visibility.Collapsed;
            if (!S.DockAutoHide) _hidden = false;

            Bar.CornerRadius = new CornerRadius(Radius);
            if (S.DockStyle == "floating")
            {
                Bar.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.35 };
                Bar.CacheMode = new BitmapCache();
            }
            else
            {
                Bar.Effect = null;
                Bar.CacheMode = null;
            }
            foreach (var it in _items) it.View.SetMaxSize(MaxSize, SeparatorWidth, BarHeight);
            App.UpdateWorkAreas();
            EnsureAnimating();
        }

        private void CheckWindowWidth()
        {
            int need = ComputeWidthPx();
            if (need > _winWidthPx || need < _winWidthPx - BaseSize * 3 * App.Scale) ApplyGeometry();
        }

        public void SetFullscreen(bool fullscreen)
        {
            if (_fullscreen == fullscreen) return;
            _fullscreen = fullscreen;
            var v = fullscreen ? Visibility.Hidden : Visibility.Visible;
            Visibility = v;
            _backdrop.Visibility = v;
        }

        public void Teardown()
        {
            SettingsStore.Changed -= OnSettingsChanged;
            SettingsStore.PinsChanged -= OnPinsChanged;
            App.Tracker.WindowsChanged -= SyncWindows;
            if (_animating) { CompositionTarget.Rendering -= OnRendering; _animating = false; }
            try { _backdrop.Close(); } catch { }
        }

        private void OnSettingsChanged()
        {
            ApplyGeometry();
            if (_lastIconSize != IconPixelSize()) ReloadIcons();
            EnsureAnimating();
        }

        private void OnPinsChanged(object? source)
        {
            if (source == this) return;
            RebuildItems();
        }

        private static int IconPixelSize()
        {
            double max = S.Magnification ? S.MagnifiedSize : S.DockIconSize;
            return (int)Math.Min(256, Math.Ceiling(max * Math.Max(1.0, App.Scale) / 16.0) * 16);
        }

        // ================== Elementos ==================

        private double BaseOf(DockItem i) => i.Removing ? 0 : i.Kind == DockItemKind.Separator ? SeparatorWidth : BaseSize;

        private void BuildItems()
        {
            foreach (var p in S.Pinned)
            {
                var item = CreatePinned(p, false);
                if (item != null) AddItem(item, _items.Count);
            }
            AddItem(new DockItem(DockItemKind.Separator), _items.Count);
            foreach (var p in S.Stacks)
            {
                var item = CreatePinned(p, true);
                if (item != null) AddItem(item, _items.Count);
            }
            foreach (var it in _items) it.CurrentSize = BaseOf(it);
            ReloadIcons();
            CheckWindowWidth();
        }

        private void RebuildItems()
        {
            foreach (var it in _items.ToList()) RemoveNow(it);
            _barWidthCur = -1;
            BuildItems();
            SyncWindows();
        }

        private static DockItem? CreatePinned(PinnedApp p, bool isStack)
        {
            var t = p.Target;
            if (t == AppIdentity.Launchpad)
                return new DockItem(DockItemKind.Launchpad, isStack) { Name = "Launchpad", Target = t, IsPinned = true };
            if (t == AppIdentity.Downloads)
                return new DockItem(DockItemKind.Downloads, isStack) { Name = "Descargas", Target = t, IsPinned = true };
            if (t == AppIdentity.Trash)
                return new DockItem(DockItemKind.Trash, isStack) { Name = "Papelera", Target = t, IsPinned = true };
            if (AppIdentity.IsSpecial(t)) return null;
            if (isStack)
            {
                bool folder = Directory.Exists(t);
                return new DockItem(folder ? DockItemKind.Folder : DockItemKind.File, true)
                {
                    Name = string.IsNullOrWhiteSpace(p.Name) ? Path.GetFileName(t.TrimEnd('\\')) : p.Name,
                    Target = t,
                    IsPinned = true,
                    IconSource = t
                };
            }
            return new DockItem(DockItemKind.App)
            {
                Name = string.IsNullOrWhiteSpace(p.Name) ? AppIdentity.FriendlyName(t) : p.Name,
                Target = t,
                IsPinned = true,
                Keys = AppIdentity.KeysForTarget(t),
                IconSource = t
            };
        }

        private void AddItem(DockItem item, int index)
        {
            _items.Insert(Math.Clamp(index, 0, _items.Count), item);
            var v = item.View;
            v.SetMaxSize(MaxSize, SeparatorWidth, BarHeight);
            v.MouseEnter += (_, _) => { _hoverItem = item; EnsureAnimating(); };
            v.MouseLeave += (_, _) => { if (_hoverItem == item) { _hoverItem = null; EnsureAnimating(); } };
            v.MouseLeftButtonDown += (_, e) => { OnItemDown(item, e); e.Handled = true; };
            v.MouseUp += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Right) { ShowItemMenu(item); e.Handled = true; }
                else if (e.ChangedButton == MouseButton.Middle && item.Kind == DockItemKind.App) { LaunchNew(item); e.Handled = true; }
            };
            Stage.Children.Add(v);
            Stage.Children.Add(v.Dot);
        }

        private void RemoveNow(DockItem it)
        {
            Stage.Children.Remove(it.View);
            Stage.Children.Remove(it.View.Dot);
            _items.Remove(it);
            if (_hoverItem == it) _hoverItem = null;
        }

        private int SeparatorIndex => _items.FindIndex(i => i.Kind == DockItemKind.Separator);
        private int PinnedEnd => _items.FindLastIndex(i => !i.IsStack && i.IsPinned && (i.Kind == DockItemKind.App || i.Kind == DockItemKind.Launchpad)) + 1;

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
                case DockItemKind.Folder:
                case DockItemKind.File:
                    if (it.Target != null) IconLoader.Request(it.Target, px, img => it.View.SetIcon(img));
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

            foreach (var it in _items.Where(i => i.Kind == DockItemKind.App && i.IsPinned && !i.IsStack))
            {
                bool wasRunning = it.IsRunning;
                it.Windows = windows.Where(w => w.Keys.Overlaps(it.Keys)).ToList();
                foreach (var w in it.Windows) claimed.Add(w.Hwnd);
                if (!wasRunning && it.IsRunning) StopBounceSoon(it);
            }

            var groups = windows.Where(w => !claimed.Contains(w.Hwnd)).GroupBy(w => w.PrimaryKey).ToList();
            var groupKeys = new HashSet<string>(groups.Select(g => g.Key));

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
            CheckWindowWidth();
            EnsureAnimating();
        }

        private void StopBounceSoon(DockItem it)
        {
            if (it.BounceStart < 0) return;
            double elapsed = _clock.Elapsed.TotalSeconds - it.BounceStart;
            const double period = 0.62;
            double endOfCurrent = Math.Ceiling(elapsed / period) * period;
            it.BounceStart = _clock.Elapsed.TotalSeconds - (3 * period - (endOfCurrent - elapsed));
        }

        /// <summary>Rectángulo (en píxeles) del ícono de la app dueña de esa ventana, para la animación de minimizar.</summary>
        public PxRect? IconRectFor(IntPtr hwnd)
        {
            var it = _items.FirstOrDefault(i => i.Windows.Any(w => w.Hwnd == hwnd));
            if (it == null || !IsVisible) return null;
            double sc = App.Scale;
            double size = BaseSize;
            double left = it.LayoutLeft + (it.LayoutSize - size) / 2;
            int x = _winLeftPx + (int)(left * sc);
            int y = _winTopPx + (int)((IconBottom - size + HideShift.Y) * sc);
            return new PxRect(x, y, x + (int)(size * sc), y + (int)(size * sc));
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

        private bool IsPulledOut(DockItem it) =>
            _dragging && it == _pressItem && it.Removable && _pressPoint.Y - _dragPos.Y > PullOutDistance;

        /// <summary>Un cuadro de animación. Devuelve true si algo se sigue moviendo.</summary>
        private bool Step(double dt, double now)
        {
            bool moving = false;
            int n = _items.Count;
            if (n == 0 || ActualWidth <= 0) return false;

            // quitar los que terminaron (achicarse o "nubecita")
            foreach (var it in _items.ToList())
            {
                if (it.PoofStart >= 0 && now - it.PoofStart >= 0.28) RemoveNow(it);
                else if (it.Removing && it.PoofStart < 0 && it.CurrentSize <= 0.5) RemoveNow(it);
            }
            n = _items.Count;
            if (n == 0) return false;

            var baseSizes = new double[n];
            var scalable = new bool[n];
            for (int i = 0; i < n; i++)
            {
                baseSizes[i] = BaseOf(_items[i]);
                scalable[i] = _items[i].Kind != DockItemKind.Separator;
            }

            // Con un menú abierto se mantiene la ampliación (antes el ícono se achicaba).
            double? mouse = (_hover || _menuOpen) && !_dragging ? _mouseX : (double?)null;
            var targets = S.Magnification
                ? Magnifier.TargetSizes(baseSizes, scalable, Gap, mouse, MaxSize, BaseSize * 2.7)
                : baseSizes;

            var cur = new double[n];
            for (int i = 0; i < n; i++)
            {
                var it = _items[i];
                double v = Magnifier.Approach(it.CurrentSize, targets[i], dt, mouse != null ? 18 : 12);
                if (v != targets[i]) moving = true;
                it.CurrentSize = v;
                cur[i] = v;
            }

            var lefts = Magnifier.Layout(cur, baseSizes, Gap, _anchorX, out double start, out double width);
            double cx = ActualWidth / 2;
            double sc = App.Scale;

            // ocultación automática
            double hideTarget = _hidden ? BarHeight + BottomMargin + 8 : 0;
            double hy = Magnifier.Approach(HideShift.Y, hideTarget, dt, 12);
            if (hy != hideTarget) moving = true;
            HideShift.Y = hy;

            // fondo de la barra
            double barLeft, barW;
            if (S.DockStyle == "full")
            {
                barLeft = 8;
                barW = ActualWidth - 16;
            }
            else if (S.DockBackgroundStretch)
            {
                barLeft = cx + start - BarPadX;
                barW = width + BarPadX * 2;
            }
            else
            {
                double baseW = Magnifier.TotalWidth(baseSizes, Gap);
                if (_barWidthCur < 0) _barWidthCur = baseW;
                _barWidthCur = Magnifier.Approach(_barWidthCur, baseW, dt, 12);
                if (_barWidthCur != baseW) moving = true;
                barLeft = cx - _barWidthCur / 2 - BarPadX;
                barW = _barWidthCur + BarPadX * 2;
            }
            Canvas.SetLeft(Bar, barLeft);
            Canvas.SetTop(Bar, BarTop);
            if (Math.Abs(Bar.Width - barW) > 0.01 || double.IsNaN(Bar.Width)) Bar.Width = Math.Max(0, barW);
            if (Math.Abs(Bar.Height - BarHeight) > 0.01 || double.IsNaN(Bar.Height)) Bar.Height = BarHeight;
            _backdrop.Place(_winLeftPx + (int)Math.Round(barLeft * sc), _winTopPx + (int)Math.Round((BarTop + hy) * sc),
                            (int)Math.Round(barW * sc), (int)Math.Round(BarHeight * sc), (int)Math.Round(Radius * sc));

            bool showDots = S.ShowIndicators;
            double dotY = ActualHeight - BottomMargin - Pad / 2 - 2;
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
                double extraUp = 0, extraScale = 1, opacity = it.Removing ? Math.Clamp(cur[i] / 40.0, 0, 1) : 1;
                if (_dragging && it == _pressItem)
                {
                    left = _dragPos.X - cur[i] / 2;
                    extraUp = Math.Max(0, _pressPoint.Y - _dragPos.Y);
                    if (IsPulledOut(it)) opacity = 0.55;
                }
                if (it.PoofStart >= 0)
                {
                    double e = now - it.PoofStart;
                    extraScale = 1 + e * 2.2;
                    opacity = Math.Clamp(1 - e / 0.28, 0, 1);
                    left = it.LayoutLeft;
                    extraUp = it.PoofUp;
                    moving = true;
                }
                else
                {
                    it.LayoutLeft = left;
                    it.LayoutSize = cur[i];
                }
                it.View.Place(left, cur[i], IconBottom - extraUp, bounce, extraScale, opacity,
                              showDots && it.IsRunning && it.Kind == DockItemKind.App, dotY, BarTop);
            }

            // etiqueta: nombre al pasar el mouse, o "Quitar del Dock" al arrastrar afuera
            DockItem? labelFor = null;
            string? labelText = null;
            if (_pressItem != null && IsPulledOut(_pressItem)) { labelFor = _pressItem; labelText = "Quitar del Dock"; }
            else if (_hoverItem != null && !_dragging && !_menuOpen && _hover && _hoverItem.Kind != DockItemKind.Separator && !_hoverItem.Removing)
            { labelFor = _hoverItem; labelText = _hoverItem.Name; }

            if (labelFor != null && labelText != null)
            {
                LabelText.Text = labelText;
                Label.Visibility = Visibility.Visible;
                Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var ls = Label.DesiredSize;
                double up = labelFor == _pressItem && _dragging ? Math.Max(0, _pressPoint.Y - _dragPos.Y) : 0;
                Canvas.SetLeft(Label, labelFor.LayoutLeft + labelFor.LayoutSize / 2 - ls.Width / 2);
                Canvas.SetTop(Label, IconBottom - up - labelFor.LayoutSize - 12 - ls.Height);
            }
            else Label.Visibility = Visibility.Collapsed;

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
                    _dragging = true;
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
            if (_dragging || _menuOpen) return;
            _hover = false;
            _hoverItem = null;
            EnsureAnimating();
            if (S.DockAutoHide) _hideTimer.Start();
        }

        private void OnItemDown(DockItem item, MouseButtonEventArgs e)
        {
            _pressItem = item;
            _pressPoint = e.GetPosition(Stage);
            _dragPos = _pressPoint;
            _dragging = false;
            Stage.CaptureMouse();
        }

        private void Stage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = _pressItem;
            bool wasDragging = _dragging;
            bool pulledOut = item != null && IsPulledOut(item);
            _pressItem = null;
            _dragging = false;
            if (Stage.IsMouseCaptured) Stage.ReleaseMouseCapture();
            if (item == null) return;

            if (wasDragging)
            {
                if (pulledOut) RemoveFromDock(item, poof: true);
                else SavePins();
            }
            else if (HitItem(e.GetPosition(Stage)) == item)
            {
                OnItemClick(item);
            }
            if (!Stage.IsMouseOver && !_menuOpen) { _hover = false; _hoverItem = null; if (S.DockAutoHide) _hideTimer.Start(); }
            EnsureAnimating();
        }

        private static bool CanDrag(DockItem item) =>
            item.Kind != DockItemKind.Separator && (item.IsStack || (item.IsPinned && (item.Kind == DockItemKind.App || item.Kind == DockItemKind.Launchpad)));

        private void ReorderWhileDragging(Point p)
        {
            var item = _pressItem;
            if (item == null) return;
            int from = _items.IndexOf(item);
            if (from < 0) return;
            int sep = SeparatorIndex;
            int lo, hi; // sección donde se puede mover: [lo, hi)
            if (item.IsStack) { lo = sep + 1; hi = _items.Count; }
            else { lo = 0; hi = PinnedEnd; }
            if (from < lo || from >= hi) return;

            var centers = Magnifier.BaseCenters(_items.Select(BaseOf).ToArray(), Gap);
            double mx = p.X - ActualWidth / 2;
            int to = from;
            for (int i = lo; i < hi; i++)
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
                if (it.Kind == DockItemKind.Separator) continue;
                double l = it.LayoutLeft, s = it.LayoutSize;
                if (p.X >= l && p.X <= l + s && p.Y >= IconBottom - s && p.Y <= IconBottom + Pad) return it;
            }
            return null;
        }

        private void TryAutoHide()
        {
            if (!S.DockAutoHide || _hover || _menuOpen || _dragging) return;
            _hidden = true;
            EnsureAnimating();
        }

        // ================== Acciones ==================

        private void OnItemClick(DockItem item)
        {
            switch (item.Kind)
            {
                case DockItemKind.Launchpad:
                    App.ToggleLaunchpad(_monitor);
                    break;
                case DockItemKind.Downloads:
                    var dl = Win32.KnownFolder(Win32.FOLDERID_Downloads);
                    if (dl != null) ShowStack(item, dl, "Descargas");
                    break;
                case DockItemKind.Folder:
                    if (item.Target != null) ShowStack(item, item.Target, item.Name);
                    break;
                case DockItemKind.File:
                    if (item.Target != null) Launcher.Launch(item.Target);
                    break;
                case DockItemKind.Trash:
                    Launcher.Open("shell:RecycleBinFolder");
                    break;
                case DockItemKind.App:
                    if (item.IsRunning)
                    {
                        var fg = Win32.GetForegroundWindow();
                        bool isFront = item.Windows.Any(w => w.Hwnd == fg);
                        if (isFront && S.ClickMinimizes)
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
            if (Launcher.Launch(target) && S.BounceOnLaunch && !item.IsRunning)
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

        /// <summary>Quita un elemento del Dock. Si es una app abierta, sigue visible como app abierta sin fijar.</summary>
        private void RemoveFromDock(DockItem item, bool poof)
        {
            if (!item.Removable) { SavePins(); return; }
            if (item.Kind == DockItemKind.App && item.IsRunning && !item.IsStack)
            {
                item.IsPinned = false;
                item.GroupKey = item.Windows[0].PrimaryKey;
                _items.Remove(item);
                _items.Insert(SeparatorIndex, item);
                SavePins();
                SyncWindows();
                return;
            }
            item.IsPinned = false;
            item.Removing = true;
            if (poof)
            {
                item.PoofStart = _clock.Elapsed.TotalSeconds;
                item.PoofUp = Math.Max(0, _pressPoint.Y - _dragPos.Y);
            }
            SavePins();
            EnsureAnimating();
        }

        public void PinTarget(string name, string target)
        {
            if (_items.Any(i => i.IsPinned && !i.Removing && string.Equals(i.Target, target, StringComparison.OrdinalIgnoreCase))) return;
            var item = CreatePinned(new PinnedApp { Name = name, Target = target }, false);
            if (item == null) return;
            item.CurrentSize = 0;
            AddItem(item, PinnedEnd);
            LoadIcon(item);
            SavePins();
            SyncWindows();
        }

        public void AddStack(string name, string target)
        {
            if (_items.Any(i => i.IsStack && !i.Removing && string.Equals(i.Target, target, StringComparison.OrdinalIgnoreCase))) return;
            var item = CreatePinned(new PinnedApp { Name = name, Target = target }, true);
            if (item == null) return;
            item.CurrentSize = 0;
            // Papelera siempre al final, como en macOS
            int trash = _items.FindIndex(i => i.Kind == DockItemKind.Trash && !i.Removing);
            AddItem(item, item.Kind == DockItemKind.Trash || trash < 0 ? _items.Count : trash);
            LoadIcon(item);
            SavePins();
            CheckWindowWidth();
            EnsureAnimating();
        }

        private void SavePins()
        {
            S.Pinned = _items
                .Where(i => !i.IsStack && i.IsPinned && !i.Removing && (i.Kind == DockItemKind.App || i.Kind == DockItemKind.Launchpad) && i.Target != null)
                .Select(i => new PinnedApp { Name = i.Name, Target = i.Target! })
                .ToList();
            S.Stacks = _items
                .Where(i => i.IsStack && !i.Removing && i.Target != null)
                .Select(i => new PinnedApp { Name = i.Name, Target = i.Target! })
                .ToList();
            SettingsStore.NotifyPinsChanged(this);
        }

        // ================== Menús ==================

        private ContextMenu NewMenu(UIElement target)
        {
            var cm = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Top };
            PopupGuard.Attach(cm);
            cm.Opened += (_, _) => { _menuOpen = true; EnsureAnimating(); };
            cm.Closed += (_, _) =>
            {
                _menuOpen = false;
                if (!Stage.IsMouseOver) { _hover = false; _hoverItem = null; if (S.DockAutoHide) _hideTimer.Start(); }
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
            if (item.Kind == DockItemKind.Separator) { ShowDockMenu(); return; }
            var cm = NewMenu(item.View);
            switch (item.Kind)
            {
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
                    cm.Items.Add(new Separator());
                    cm.Items.Add(Mi("Quitar del Dock", () => RemoveFromDock(item, false)));
                    break;
                case DockItemKind.Downloads:
                    cm.Items.Add(Mi("Abrir carpeta Descargas", () => { var d = Win32.KnownFolder(Win32.FOLDERID_Downloads); if (d != null) Launcher.ShowInExplorer(d); }));
                    cm.Items.Add(new Separator());
                    cm.Items.Add(Mi("Quitar del Dock", () => RemoveFromDock(item, false)));
                    break;
                case DockItemKind.Folder:
                case DockItemKind.File:
                    cm.Items.Add(Mi("Abrir", () => { if (item.Target != null) Launcher.Launch(item.Target); }));
                    cm.Items.Add(Mi("Mostrar en el Explorador", () => { if (item.Target != null) Launcher.ShowInExplorer(item.Target); }));
                    cm.Items.Add(new Separator());
                    cm.Items.Add(Mi("Quitar del Dock", () => RemoveFromDock(item, false)));
                    break;
                case DockItemKind.Launchpad:
                    cm.Items.Add(Mi("Abrir", () => App.ToggleLaunchpad(_monitor)));
                    cm.Items.Add(new Separator());
                    cm.Items.Add(Mi("Quitar del Dock", () => RemoveFromDock(item, false)));
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
                    keep.Click += (_, _) => { if (item.IsPinned) RemoveFromDock(item, false); else Pin(item); };
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
            var mag = new MenuItem { Header = "Activar ampliación", IsCheckable = true, IsChecked = S.Magnification };
            mag.Click += (_, _) => { S.Magnification = !S.Magnification; SettingsStore.NotifyChanged(); };
            var hide = new MenuItem { Header = "Activar ocultación automática", IsCheckable = true, IsChecked = S.DockAutoHide };
            hide.Click += (_, _) => { S.DockAutoHide = !S.DockAutoHide; SettingsStore.NotifyChanged(); };
            cm.Items.Add(mag);
            cm.Items.Add(hide);

            var style = new MenuItem { Header = "Estilo" };
            foreach (var (key, name) in new[] { ("classic", "Clásico"), ("floating", "Flotante"), ("full", "Ancho completo") })
            {
                var k = key;
                var mi = new MenuItem { Header = name, IsCheckable = true, IsChecked = S.DockStyle == key };
                mi.Click += (_, _) => { S.DockStyle = k; SettingsStore.NotifyChanged(); };
                style.Items.Add(mi);
            }
            cm.Items.Add(style);

            var add = new MenuItem { Header = "Agregar al Dock" };
            add.Items.Add(Mi("Aplicación…", AddAppFromDialog));
            add.Items.Add(Mi("Carpeta…", AddFolderFromDialog));
            if (!_items.Any(i => i.Kind == DockItemKind.Downloads && !i.Removing))
                add.Items.Add(Mi("Descargas", () => AddStack("Descargas", AppIdentity.Downloads)));
            if (!_items.Any(i => i.Kind == DockItemKind.Trash && !i.Removing))
                add.Items.Add(Mi("Papelera", () => AddStack("Papelera", AppIdentity.Trash)));
            if (!_items.Any(i => i.Kind == DockItemKind.Launchpad && !i.Removing))
                add.Items.Add(Mi("Launchpad", () => PinTarget("Launchpad", AppIdentity.Launchpad)));
            cm.Items.Add(add);

            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Preferencias del Dock…", App.OpenSettings));
            cm.IsOpen = true;
        }

        private void AddAppFromDialog()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Agregar una aplicación al Dock",
                Filter = "Aplicaciones (*.exe;*.lnk)|*.exe;*.lnk|Todos los archivos (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            var f = dlg.FileName;
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext == ".exe" || ext == ".lnk")
                PinTarget(ext == ".lnk" ? Path.GetFileNameWithoutExtension(f) : AppIdentity.FriendlyName(f), f);
            else AddStack(Path.GetFileName(f), f);
        }

        private void AddFolderFromDialog()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Agregar una carpeta al Dock" };
            if (dlg.ShowDialog() != true) return;
            AddStack(Path.GetFileName(dlg.FolderName.TrimEnd('\\')), dlg.FolderName);
        }

        // ================== Pilas (Descargas y carpetas) ==================

        private void ShowStack(DockItem item, string dir, string titleText)
        {
            if (!Directory.Exists(dir)) return;
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
                HorizontalOffset = (MaxSize - width) / 2,
                VerticalOffset = -6
            };
            PopupGuard.Attach(popup);

            var panel = new StackPanel();
            var title = new TextBlock { Text = titleText, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(6, 0, 0, 8) };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            panel.Children.Add(title);

            var wrap = new WrapPanel();
            if (files.Count == 0)
            {
                var empty = new TextBlock { Text = "La carpeta está vacía", Margin = new Thickness(6, 6, 6, 12) };
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
            var p = e.GetPosition(Stage);
            var target = HitItem(p);
            if (target?.Kind == DockItemKind.Trash)
            {
                SystemInfo.SendToRecycleBin(files);
                LoadIcon(target);
                return;
            }
            foreach (var f in files)
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".exe" || ext == ".lnk")
                    PinTarget(ext == ".lnk" ? Path.GetFileNameWithoutExtension(f) : AppIdentity.FriendlyName(f), f);
                else
                    AddStack(Path.GetFileName(f.TrimEnd('\\')), f); // carpetas y archivos van al lado derecho
            }
            _hover = false;
            EnsureAnimating();
        }
    }
}
