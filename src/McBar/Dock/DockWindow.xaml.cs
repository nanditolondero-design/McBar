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
using System.Windows.Media.Animation;
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
        private const double LabelSpace = 58;    // lugar arriba para la etiqueta con el nombre
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

        // 0.8.0: vista previa de las ventanas al dejar el mouse sobre una app abierta
        private readonly System.Windows.Threading.DispatcherTimer _previewTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) }; // 1.3.1: antes 550

        private void ShowPreviewNow()
        {
            _previewTimer.Stop();
            var it = _hoverItem;
            if (it == null || it.Kind != DockItemKind.App || it.Windows.Count == 0 || _dragging || _menuOpen || it.Removing) return;
            double sc = _monitor.Scale;
            double cx = _winLeftPx / sc + it.LayoutLeft + it.LayoutSize / 2;
            double top = _winTopPx / sc + BarTop - Math.Max(0, it.LayoutSize - BaseSize);
            App.ShowDockPreview(it.Windows.ToList(), cx, top, _monitor);
        }
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
        /// <summary>Ocultación automática pedida en Preferencias o por el modo juego (1.3.0).</summary>
        private static bool AutoHide => S.DockAutoHide || GameMode.On;
        private double BaseSize => S.DockIconSize;
        private double MaxSize => S.Magnification && !IsTaskbar ? S.MagnifiedSize : BaseSize;
        private double Gap => Math.Round(BaseSize * 0.1);
        private double BarHeight => BaseSize + Pad * 2;
        // Estilos: "classic", "floating", "full", y desde 0.4.3 "taskbar" (barra ancha flotante, parecida a la
        // de Windows 11, sin lupa) e "icons" (sin fondo: solo íconos con sus indicadores).
        private bool IsTaskbar => S.DockStyle == "taskbar";
        private bool NoBackground => S.DockStyle == "icons";
        private double BottomMargin => S.DockStyle == "floating" || IsTaskbar ? 12 : 4;
        private double Radius => S.DockStyle == "floating" ? 22 : S.DockStyle == "full" ? 14 : IsTaskbar ? 14 : 18;
        /// <summary>El desenfoque de Windows va en una ventana aparte que no puede seguir cuadro a cuadro a la
        /// barra: con desenfoque el fondo queda quieto (0.4.3: antes se veía mal la animación y volvían las esquinas).</summary>
        private bool Stretch => S.DockBackgroundStretch && !S.BackdropBlur && !NoBackground;
        private double WindowHeightDip => Math.Max(MaxSize, BaseSize * 1.55) + Pad + BottomMargin + LabelSpace;
        private double BarTop => ActualHeight - BottomMargin - BarHeight;
        private double IconBottom => ActualHeight - BottomMargin - Pad;

        /// <summary>Píxeles a reservar abajo para que las ventanas maximizadas no queden tapadas.</summary>
        public int ReservePx => AutoHide ? 0 : (int)Math.Ceiling((BarHeight + BottomMargin) * _monitor.Scale);

        // Ventana rápida (común, con vidrio de DWM y forma recortada) en lugar de "en capas".
        private readonly bool _fast;
        private string _shapeKey = "";
        private int _puffActive;

        internal DockWindow(MonitorInfo monitor)
        {
            InitializeComponent();
            _previewTimer.Tick += (_, _) => ShowPreviewNow();
            _monitor = monitor;
            _fast = S.FastWindows;
            if (_fast) AllowsTransparency = false;

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
            DragEnter += (_, _) => { _hideTimer.Stop(); if (_hidden) { _hidden = false; } _hover = true; EnsureAnimating(); }; // 1.4.1
            DragLeave += (_, _) => { _hover = false; EnsureAnimating(); if (AutoHide) _hideTimer.Start(); };
            Drop += Window_Drop;

            SourceInitialized += (_, _) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                WindowOps.MakeToolNoActivate(this);
                if (_fast) WindowOps.MakeGlass(this);
                ApplyGeometry();
            };
            Loaded += (_, _) =>
            {
                BuildItems();
                App.Tracker.WindowsChanged += SyncWindows;
                SyncWindows();
                SettingsStore.Changed += OnSettingsChanged;
                SettingsStore.PinsChanged += OnPinsChanged;
                GameMode.Changed += OnGameMode;
            };
            // 1.5.0: al pasar a un monitor con otra escala, Windows le cambia el tamaño: se vuelve a acomodar.
            DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(ApplyGeometry), System.Windows.Threading.DispatcherPriority.Loaded);
            Closed += (_, _) => Teardown();
        }

        // ================== Geometría ==================

        private int ComputeWidthPx()
        {
            if (S.DockStyle == "full" || IsTaskbar) return _monitor.Bounds.Width;
            var baseSizes = _items.Select(BaseOf).ToArray();
            double need = Magnifier.TotalWidth(baseSizes, Gap) + BarPadX * 2 + (MaxSize - BaseSize) * 3 + 280;
            return (int)Math.Min(_monitor.Bounds.Width, Math.Ceiling(need * _monitor.Scale));
        }

        public void ApplyGeometry()
        {
            if (_hwnd == IntPtr.Zero) return;
            double sc = _monitor.Scale;
            _winHeightPx = (int)Math.Ceiling(WindowHeightDip * sc);
            _winWidthPx = ComputeWidthPx();
            var b = _monitor.Bounds;
            _winLeftPx = b.Left + (b.Width - _winWidthPx) / 2;
            _winTopPx = b.Bottom - _winHeightPx;
            Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, _winLeftPx, _winTopPx, _winWidthPx, _winHeightPx, Win32.SWP_NOACTIVATE);

            HotZone.Visibility = AutoHide ? Visibility.Visible : Visibility.Collapsed;
            if (!_fullscreen) _backdrop.Visibility = S.BackdropBlur && !NoBackground ? Visibility.Visible : Visibility.Hidden;
            if (!AutoHide) _hidden = false;

            Bar.CornerRadius = new CornerRadius(Radius);
            BarShadow1.CornerRadius = new CornerRadius(Radius + 1);
            BarShadow2.CornerRadius = new CornerRadius(Radius + 3);
            // 0.4.1: sin DropShadowEffect ni BitmapCache (se recalculaban en cada cuadro de la lupa).
            Bar.Effect = null;
            Bar.CacheMode = null;
            var shadow = (S.DockStyle == "floating" || IsTaskbar) ? Visibility.Visible : Visibility.Collapsed;
            Bar.Visibility = NoBackground ? Visibility.Hidden : Visibility.Visible;
            if (NoBackground) shadow = Visibility.Collapsed;
            foreach (var it in _items) { it.View.Dot.Width = IsTaskbar ? 14 : 4; it.View.Dot.Height = IsTaskbar ? 3 : 4; }
            BarShadow1.Visibility = shadow;
            BarShadow2.Visibility = shadow;
            // Con el desenfoque de Windows el fondo es el de siempre; sin él, el vidrio dibujado por McBar.
            Bar.SetResourceReference(Border.BackgroundProperty, S.BackdropBlur ? "Mc.DockBg" : "Mc.DockGlass");
            Bar.SetResourceReference(Border.BorderBrushProperty, S.BackdropBlur ? "Mc.DockBorder" : "Mc.DockGlassBorder");
            foreach (var it in _items) it.View.SetMaxSize(MaxSize, SeparatorWidth, BarHeight);
            _shapeKey = "";
            App.UpdateWorkAreas();
            EnsureAnimating();
        }

        private void CheckWindowWidth()
        {
            int need = ComputeWidthPx();
            if (need > _winWidthPx || need < _winWidthPx - BaseSize * 3 * _monitor.Scale) ApplyGeometry();
        }

        public void SetFullscreen(bool fullscreen)
        {
            if (_fullscreen == fullscreen) return;
            _fullscreen = fullscreen;
            var v = fullscreen ? Visibility.Hidden : Visibility.Visible;
            Visibility = v;
            _backdrop.Visibility = fullscreen || !S.BackdropBlur || NoBackground ? Visibility.Hidden : Visibility.Visible;
        }

        public void Teardown()
        {
            SettingsStore.Changed -= OnSettingsChanged;
            SettingsStore.PinsChanged -= OnPinsChanged;
            GameMode.Changed -= OnGameMode;
            App.Tracker.WindowsChanged -= SyncWindows;
            if (_animating) { CompositionTarget.Rendering -= OnRendering; _animating = false; }
            try { _backdrop.Close(); } catch { }
        }

        private void OnSettingsChanged()
        {
            ApplyGeometry();
            SyncWindows(); // por si cambió "apps por monitor"
            if (_lastIconSize != IconPixelSize()) ReloadIcons();
            EnsureAnimating();
        }

        /// <summary>1.3.0: el modo juego oculta el Dock (y al salir vuelve a como estaba).</summary>
        private void OnGameMode()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                OnSettingsChanged();
                if (AutoHide && !_hover && !_menuOpen) _hideTimer.Start();
            }));
        }

        private void OnPinsChanged(object? source)
        {
            if (source == this) return;
            RebuildItems();
        }

        private int IconPixelSize() // 1.5.0: ya no es static (usa la escala de su monitor)
        {
            double max = S.Magnification ? S.MagnifiedSize : S.DockIconSize;
            return (int)Math.Min(256, Math.Ceiling(max * Math.Max(1.0, _monitor.Scale) / 16.0) * 16);
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
            v.MouseEnter += (_, _) =>
            {
                _hoverItem = item; EnsureAnimating(); _previewTimer.Stop();
                // 1.3.1: si ya hay una vista previa abierta (pasando de un ícono a otro), se cambia al toque
                if (App.DockPreviewOpen) ShowPreviewNow(); else _previewTimer.Start();
            };
            v.MouseLeave += (_, _) =>
            {
                if (_hoverItem == item) { _hoverItem = null; EnsureAnimating(); }
                _previewTimer.Stop();
                App.HideDockPreview();
            };
            v.MouseLeftButtonDown += (_, e) => { OnItemDown(item, e); e.Handled = true; };
            v.MouseUp += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Right) { ShowItemMenu(item); e.Handled = true; }
                else if (e.ChangedButton == MouseButton.Middle && item.Kind == DockItemKind.App) { LaunchNew(item); e.Handled = true; }
            };
            Stage.Children.Add(v);
            v.Dot.Width = IsTaskbar ? 14 : 4;  // en el estilo Barra el indicador es una rayita, como en Windows
            v.Dot.Height = IsTaskbar ? 3 : 4;
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

        /// <summary>Vuelve a cargar todos los íconos (al cambiar el paquete de íconos).</summary>
        public void RefreshIcons() => ReloadIcons();

        private void ReloadIcons()
        {
            _lastIconSize = IconPixelSize();
            foreach (var it in _items) LoadIcon(it);
        }

        private void LoadIcon(DockItem it)
        {
            int px = _lastIconSize > 0 ? _lastIconSize : IconPixelSize();
            // 1.6.1: ícono elegido por el usuario (clic derecho → Ícono → Cambiar ícono…)
            var key = IconKey(it);
            if (key != null && S.CustomIcons.TryGetValue(key, out var custom) && File.Exists(custom))
            {
                var own = IconLoader.FromImageFile(custom, px);
                if (own != null) { it.View.SetIcon(own); return; }
            }
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
                    if (it.Target != null) IconLoader.Request(it.Target, px, img => it.View.SetIcon(img), displayName: it.Name);
                    break;
                case DockItemKind.App:
                    if (!string.IsNullOrEmpty(it.IconSource))
                    {
                        var hwnd = it.Windows.Count > 0 ? it.Windows[0].Hwnd : IntPtr.Zero;
                        IconLoader.Request(it.IconSource!, px, img =>
                        {
                            if (img == null && hwnd != IntPtr.Zero) img = IconLoader.FromWindow(hwnd);
                            it.View.SetIcon(img);
                        }, displayName: it.Name, appShape: true);
                    }
                    else if (it.Windows.Count > 0)
                    {
                        it.View.SetIcon(IconLoader.FromWindow(it.Windows[0].Hwnd));
                    }
                    break;
            }
        }

        private string _lastSyncKey = "";

        /// <summary>Vuelve a repartir las ventanas solo si cambió algo (lo llama el vigilante con "apps por monitor").</summary>
        public void Resync()
        {
            var key = string.Join(",", App.Tracker.Windows.Select(w => w.Hwnd.ToInt64() + "@" + (Monitors.FromWindow(w.Hwnd)?.Device ?? "")));
            if (key == _lastSyncKey) return;
            _lastSyncKey = key;
            SyncWindows();
        }

        /// <summary>Reparte las ventanas abiertas entre los íconos fijados y agrega/quita apps abiertas sin fijar.</summary>
        private void SyncWindows()
        {
            // 1.6.1: los marcos de apps de la Tienda que todavía no se reconocieron no se muestran (eran un
            // ícono vacío); aparecen en cuanto se sabe qué app son.
            IEnumerable<TrackedWindow> all = App.Tracker.Windows.Where(w => !WindowTracker.IsUnidentifiedUwp(w));
            if (S.DockAppsPerMonitor && App.Docks.Count > 1)
                all = all.Where(w => Monitors.FromWindow(w.Hwnd)?.Device == _monitor.Device);
            var windows = all.ToList();
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
            double sc = _monitor.Scale;
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
            Perf.DockFrame(now - _lastTick); // 1.5.0: medición (Preferencias → Diagnóstico)
            double dt = Math.Clamp(now - _lastTick, 0.001, 0.05);
            _lastTick = now;
            bool more;
            try { more = Step(dt, now); }
            catch (Exception ex)
            {
                // Nunca dejar un error repitiéndose en cada cuadro (eso hacía lenta la app).
                Log.Write("Dock (animación): " + ex.Message);
                more = false;
            }
            if (!more)
            {
                CompositionTarget.Rendering -= OnRendering;
                _animating = false;
                Perf.DockAnimationEnded();
            }
        }

        /// <summary>
        /// ¿El ícono que se arrastra está FUERA del Dock? 1.4.1: no solo hacia arriba: también si se lo lleva
        /// a los costados, más allá de la barra (en cualquier punto de la pantalla).
        /// </summary>
        private bool IsPulledOut(DockItem it) =>
            _dragging && it == _pressItem && (it.Removable || ClosesOnDrag(it)) &&
            OutsideDock(_pressPoint.Y - _dragPos.Y, _dragPos.X, _lastBarLeft, _lastBarW);

        private double _lastBarLeft, _lastBarW;
        private bool _overLaunchpad;

        /// <summary>
        /// 1.6.2: con el Launchpad abierto el Dock se ve SIEMPRE encima de él (como en macOS), aunque esté en
        /// ocultación automática; al cerrar el Launchpad vuelve a como estaba.
        /// </summary>
        public void ShowOverLaunchpad(bool on)
        {
            _overLaunchpad = on;
            if (on)
            {
                _hideTimer.Stop();
                if (_hidden) _hidden = false;
                if (_hwnd != IntPtr.Zero) Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            }
            else if (AutoHide && !_hover) _hideTimer.Start();
            EnsureAnimating();
        }

        /// <summary>Alto que ocupa el Dock abajo (para que el Launchpad no ponga íconos debajo).</summary>
        public double VisibleHeightDip => BarHeight + BottomMargin + 6;
        private DragGhost? _ghost;
        private bool _suppressPuff;

        /// <summary>Fuera del Dock: más de 50 arriba, o más de 40 a un costado de la barra. Pura, se prueba.</summary>
        public static bool OutsideDock(double up, double x, double barLeft, double barW, double pull = PullOutDistance) =>
            up > pull || (barW > 0 && (x < barLeft - 40 || x > barLeft + barW + 40));

        /// <summary>1.4.1: con el ícono fuera del Dock, lo dibuja una ventanita que sigue al mouse por toda la pantalla.</summary>
        private void UpdateGhost()
        {
            var it = _pressItem;
            if (it == null || !_dragging || !IsPulledOut(it))
            {
                _ghost?.Cancel();
                _ghost = null;
                return;
            }
            if (!Win32.GetCursorPos(out var p)) return;
            _ghost ??= new DragGhost(it.View.Icon.Source, Math.Max(BaseSize, it.LayoutSize));
            string label = ClosesOnDrag(it) ? "Cerrar app" : "Quitar del Dock";
            _ghost.MoveTo(p.x, p.y, label, 0.9);
        }

        /// <summary>
        /// 0.4.1: para quitar o cerrar hay que mantener el ícono afuera un momento (se ve el cartel) — así no
        /// se pierde la Papelera u otro ícono por un tirón sin querer. Como en macOS.
        /// </summary>
        private DateTime _pulledSince = DateTime.MaxValue;
        private bool ConfirmedPullOut(DockItem it) => IsPulledOut(it) && (DateTime.Now - _pulledSince).TotalMilliseconds >= 450;

        /// <summary>
        /// 0.4.0 (pedido de Andrés): una app abierta que NO está fijada se puede arrastrar fuera del Dock
        /// y al soltarla se cierra (se les pide cerrar a sus ventanas, como "Salir").
        /// </summary>
        private static bool ClosesOnDrag(DockItem it) =>
            it.Kind == DockItemKind.App && !it.IsPinned && !it.IsStack && it.IsRunning;

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
            // Copia fija de la lista para este cuadro: si algo la cambia mientras tanto, no se rompe nada.
            var items = _items.ToArray();
            n = items.Length;
            if (n == 0) return false;

            var baseSizes = new double[n];
            var scalable = new bool[n];
            for (int i = 0; i < n; i++)
            {
                baseSizes[i] = BaseOf(items[i]);
                scalable[i] = items[i].Kind != DockItemKind.Separator;
            }

            // Con un menú abierto se mantiene la ampliación (antes el ícono se achicaba).
            double? mouse = (_hover || _menuOpen) && !_dragging ? _mouseX : (double?)null;
            var targets = S.Magnification && !IsTaskbar
                ? Magnifier.TargetSizes(baseSizes, scalable, Gap, mouse, MaxSize, BaseSize * 2.7)
                : baseSizes;

            var cur = new double[n];
            for (int i = 0; i < n; i++)
            {
                var it = items[i];
                double v = Magnifier.Approach(it.CurrentSize, targets[i], dt, mouse != null ? 18 : 12);
                if (v != targets[i]) moving = true;
                it.CurrentSize = v;
                cur[i] = v;
            }

            var lefts = Magnifier.Layout(cur, baseSizes, Gap, _anchorX, out double start, out double width);
            double cx = ActualWidth / 2;
            double sc = _monitor.Scale;

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
            else if (IsTaskbar)
            {
                barLeft = 12;
                barW = ActualWidth - 24;
            }
            else if (Stretch)
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
            _lastBarLeft = barLeft; _lastBarW = barW;
            if (Math.Abs(Bar.Width - barW) > 0.01 || double.IsNaN(Bar.Width)) Bar.Width = Math.Max(0, barW);
            if (Math.Abs(Bar.Height - BarHeight) > 0.01 || double.IsNaN(Bar.Height)) Bar.Height = BarHeight;
            if (BarShadow1.Visibility == Visibility.Visible)
            {
                Canvas.SetLeft(BarShadow1, barLeft - 1); Canvas.SetTop(BarShadow1, BarTop + 1.5);
                BarShadow1.Width = Math.Max(0, barW + 2); BarShadow1.Height = BarHeight + 1.5;
                Canvas.SetLeft(BarShadow2, barLeft - 3); Canvas.SetTop(BarShadow2, BarTop + 2);
                BarShadow2.Width = Math.Max(0, barW + 6); BarShadow2.Height = BarHeight + 4;
            }
            // El desenfoque va 1 px adentro del borde de la barra: así sus esquinas (que Windows no suaviza)
            // quedan tapadas por el borde dibujado y no se ven escalones.
            _backdrop.Place(_winLeftPx + (int)Math.Round((barLeft + 1) * sc), _winTopPx + (int)Math.Round((BarTop + hy + 1) * sc),
                            (int)Math.Round((barW - 2) * sc), (int)Math.Round((BarHeight - 2) * sc), (int)Math.Round((Radius - 1) * sc));

            bool showDots = S.ShowIndicators;
            double dotY = ActualHeight - BottomMargin - Pad / 2 - 2;
            double zl = double.MaxValue, zr = double.MinValue, zt = double.MaxValue; // zona ocupada por los íconos
            for (int i = 0; i < n; i++)
            {
                var it = items[i];
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
                    if (IsPulledOut(it)) opacity = _ghost != null ? 0 : 0.55; // 1.4.1: lo muestra el fantasma
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
                if (it.Kind != DockItemKind.Separator && cur[i] > 0.5)
                {
                    double grow = (extraScale - 1) * cur[i] / 2;
                    zl = Math.Min(zl, left - grow);
                    zr = Math.Max(zr, left + cur[i] + grow);
                    zt = Math.Min(zt, IconBottom - extraUp - cur[i] * extraScale - bounce);
                }
            }

            // resaltado bajo el mouse (0.4.3): en el estilo Barra o cuando no hay lupa
            bool plate = (IsTaskbar || !S.Magnification) && _hover && !_dragging && _hoverItem != null
                         && _hoverItem.Kind != DockItemKind.Separator && !_hoverItem.Removing;
            if (plate)
            {
                Canvas.SetLeft(HoverPlate, _hoverItem!.LayoutLeft - 5);
                Canvas.SetTop(HoverPlate, BarTop + 3);
                HoverPlate.Width = _hoverItem.LayoutSize + 10;
                HoverPlate.Height = Math.Max(0, BarHeight - 6);
            }
            double plateTarget = plate ? 1 : 0;
            double po = Magnifier.Approach(HoverPlate.Opacity, plateTarget, dt, 20);
            if (po != plateTarget) moving = true;
            HoverPlate.Opacity = po;

            // etiqueta: nombre al pasar el mouse, o "Quitar del Dock" al arrastrar afuera
            DockItem? labelFor = null;
            string? labelText = null;
            if (_pressItem != null && IsPulledOut(_pressItem))
            {
                if (_pulledSince == DateTime.MaxValue) _pulledSince = DateTime.Now;
            }
            else _pulledSince = DateTime.MaxValue;
            if (_pressItem != null && IsPulledOut(_pressItem)) { if (_ghost == null) { labelFor = _pressItem; labelText = ClosesOnDrag(_pressItem) ? "Cerrar app" : "Quitar del Dock"; } }
            else if (_hoverItem != null && !_dragging && !_menuOpen && _hover && _hoverItem.Kind != DockItemKind.Separator && !_hoverItem.Removing)
            { labelFor = _hoverItem; labelText = _hoverItem.Name; }

            if (labelFor != null && labelText != null)
            {
                LabelText.Text = labelText;
                Label.Visibility = Visibility.Visible;
                Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var ls = Label.DesiredSize;
                double up = labelFor == _pressItem && _dragging ? Math.Max(0, _pressPoint.Y - _dragPos.Y) : 0;
                double lx = labelFor.LayoutLeft + labelFor.LayoutSize / 2 - ls.Width / 2;
                double ly = IconBottom - up - labelFor.LayoutSize - 12 - ls.Height;
                Canvas.SetLeft(Label, lx);
                Canvas.SetTop(Label, ly);
                zl = Math.Min(zl, lx); zr = Math.Max(zr, lx + ls.Width); zt = Math.Min(zt, ly);
            }
            else Label.Visibility = Visibility.Collapsed;

            bool active = mouse != null || moving || labelFor != null || _dragging || _menuOpen;
            UpdateShape(barLeft, barW, hy, zl, zr, zt, active);

            return moving;
        }

        /// <summary>
        /// Forma de la ventana rápida: la barra (redondeada) + la zona de los íconos agrandados/etiqueta +
        /// la franja de abajo si se oculta sola. Fuera de esa forma los clics pasan a las ventanas de atrás.
        ///
        /// 0.4.0: mientras el mouse está encima (o algo se mueve) la zona extra es AMPLIA y FIJA: todo el
        /// ancho que pueden ocupar los íconos agrandados y toda la altura hasta la etiqueta. Antes se
        /// recalculaba cuadro a cuadro al tamaño justo y, como Windows aplica la forma un cuadro antes de que
        /// se dibuje el contenido nuevo, los íconos agrandados aparecían cortados arriba y a los costados.
        /// Recién cuando todo queda quieto y el mouse se fue, la forma vuelve a ser solo la barra.
        /// </summary>
        private void UpdateShape(double barLeft, double barW, double hy, double zl, double zr, double zt, bool active)
        {
            if (!_fast || _hwnd == IntPtr.Zero || _puffActive > 0) return;
            double sc = _monitor.Scale;
            int R(double v) => (int)Math.Round(v * sc);
            double barTop = BarTop + hy;
            const double m = 6; // margen para las esquinas suavizadas y la sombra (las dibuja WPF)
            var rects = new List<(int x, int y, int w, int h)>();
            if (active)
            {
                // UNA zona fija calculada con la barra en reposo: no cambia mientras la lupa se mueve, así
                // Windows no recorta los íconos ni hay que cambiar la forma en cada cuadro.
                double baseW = Magnifier.TotalWidth(_items.Select(BaseOf).ToArray(), Gap) + BarPadX * 2;
                double restLeft = S.DockStyle == "full" || IsTaskbar ? barLeft : ActualWidth / 2 - baseW / 2;
                double grow = (MaxSize - BaseSize) * 3 + 60;
                double l = Math.Max(0, Math.Min(restLeft, barLeft) - grow);
                double r = Math.Min(ActualWidth, Math.Max(restLeft + baseW, barLeft + barW) + grow);
                double t = Math.Max(0, barTop - (MaxSize - BaseSize) - BaseSize * 0.6 - LabelSpace);
                if (zr > zl) { l = Math.Max(0, Math.Min(l, zl - 4)); r = Math.Min(ActualWidth, Math.Max(r, zr + 4)); t = Math.Max(0, Math.Min(t, zt + hy - 4)); }
                double bottom = Math.Min(ActualHeight, barTop + BarHeight + m);
                rects.Add((R(l), R(t), R(r - l), R(bottom - t)));
            }
            else
            {
                rects.Add((R(barLeft - m), R(barTop - m), R(barW + m * 2), R(BarHeight + m * 2)));
                if (zr > zl && zt < barTop - 0.5)
                    rects.Add((R(zl), R(zt + hy) - 2, R(zr - zl) + 1, R(barTop - zt) + 6));
            }
            if (AutoHide)
                rects.Add((0, R(ActualHeight) - R(3) - 1, R(ActualWidth), R(3) + 2));
            string key = string.Join(";", rects);
            if (key == _shapeKey) return;
            _shapeKey = key;
            var first = rects[0];
            WindowOps.SetShape(_hwnd, first.x, first.y, first.w, first.h, 0, rects.Skip(1).ToArray());
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
                    UpdateGhost();
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
            if (AutoHide) _hideTimer.Start();
        }

        private void OnItemDown(DockItem item, MouseButtonEventArgs e)
        {
            _previewTimer.Stop();
            App.HideDockPreview(now: true);
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
            bool pulledOut = item != null && ConfirmedPullOut(item);
            _pulledSince = DateTime.MaxValue;
            _pressItem = null;
            _dragging = false;
            if (Stage.IsMouseCaptured) Stage.ReleaseMouseCapture();
            // 1.4.1: la nubecita la hace el fantasma donde se soltó (en cualquier lugar de la pantalla)
            var ghost = _ghost;
            _ghost = null;
            if (ghost != null && pulledOut) ghost.Poof(); else ghost?.Cancel();
            if (item == null) return;

            if (wasDragging)
            {
                _suppressPuff = ghost != null && pulledOut;
                try
                {
                    if (pulledOut && ClosesOnDrag(item)) CloseByDrag(item);
                    else if (pulledOut) RemoveFromDock(item, poof: true);
                    else if (item.Kind == DockItemKind.App && !item.IsPinned && _items.IndexOf(item) < PinnedEnd) PinHere(item); // 1.6.1
                    else SavePins();
                }
                finally { _suppressPuff = false; }
            }
            else if (HitItem(e.GetPosition(Stage)) == item)
            {
                OnItemClick(item);
            }
            if (!Stage.IsMouseOver && !_menuOpen) { _hover = false; _hoverItem = null; if (AutoHide) _hideTimer.Start(); }
            EnsureAnimating();
        }

        private static bool CanDrag(DockItem item) =>
            item.Kind != DockItemKind.Separator && (item.IsStack || ClosesOnDrag(item) || (item.IsPinned && (item.Kind == DockItemKind.App || item.Kind == DockItemKind.Launchpad)));

        /// <summary>Cierra la app soltada fuera del Dock: nubecita y se les pide cerrar a todas sus ventanas.</summary>
        private void CloseByDrag(DockItem item)
        {
            double up = Math.Max(0, _pressPoint.Y - _dragPos.Y);
            double size = Math.Max(item.LayoutSize, BaseSize);
            Puff(item.LayoutLeft + item.LayoutSize / 2, IconBottom - up - item.LayoutSize / 2, size);
            item.View.Visibility = Visibility.Hidden;
            foreach (var w in item.Windows.ToArray())
            {
                try { WindowOps.Close(w.Hwnd); } catch { }
            }
            // Si la app no se cierra (por ejemplo pregunta "¿Guardar cambios?"), el ícono vuelve a aparecer.
            var back = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            back.Tick += (_, _) =>
            {
                back.Stop();
                if (_items.Contains(item) && !item.Removing) item.View.Visibility = Visibility.Visible;
                EnsureAnimating();
            };
            back.Start();
            EnsureAnimating();
        }

        private void ReorderWhileDragging(Point p)
        {
            var item = _pressItem;
            if (item == null) return;
            int from = _items.IndexOf(item);
            if (from < 0) return;
            int sep = SeparatorIndex;
            int lo, hi; // sección donde se puede mover: [lo, hi)
            if (item.IsStack) { lo = sep + 1; hi = _items.Count; }
            // 1.6.1: una app abierta SIN fijar también se puede llevar entre las fijas (al soltarla, queda fijada)
            else if (item.Kind == DockItemKind.App && !item.IsPinned) { lo = 0; hi = sep >= 0 ? sep : _items.Count; }
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
            if (!AutoHide || _hover || _menuOpen || _dragging || _overLaunchpad) return;
            _hidden = true;
            EnsureAnimating();
        }

        // ================== Acciones ==================

        private void OnItemClick(DockItem item)
        {
            // 1.6.2: con el Launchpad abierto, tocar otra cosa del Dock lo cierra (como en macOS)
            if (item.Kind != DockItemKind.Launchpad) App.CloseLaunchpad();
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

        /// <summary>1.6.1: fija una app abierta en el lugar donde se la soltó (entre las apps fijas).</summary>
        private void PinHere(DockItem item)
        {
            int at = _items.IndexOf(item);
            Pin(item);
            if (!item.IsPinned) return;
            _items.Remove(item);
            _items.Insert(Math.Clamp(at, 0, _items.Count), item);
            SavePins();
        }

        // ================== Ícono propio (1.6.1) ==================

        /// <summary>Clave del ícono propio: el destino fijado, el Launchpad o el .exe de una app abierta.</summary>
        private static string? IconKey(DockItem it) =>
            it.Kind == DockItemKind.Launchpad ? AppIdentity.Launchpad : it.Target ?? it.Windows.FirstOrDefault()?.ExePath;

        private void ChooseIcon(DockItem item)
        {
            var key = IconKey(item);
            if (key == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Elegir ícono para " + item.Name, Filter = "Imágenes|*.png;*.ico;*.icns;*.jpg;*.jpeg;*.bmp;*.webp|Todos los archivos|*.*" }; // 1.8.2: también .icns
            if (dlg.ShowDialog() != true) return;
            try
            {
                // se guarda una copia en %AppData%\McBar\iconos-propios (así sigue andando si se mueve el original)
                var dir = Path.Combine(SettingsStore.Folder, "iconos-propios");
                Directory.CreateDirectory(dir);
                var dest = Path.Combine(dir, MacIcons.SafeFileName(item.Name) + "-" + Math.Abs(key.GetHashCode()).ToString("x") + Path.GetExtension(dlg.FileName));
                File.Copy(dlg.FileName, dest, true);
                S.CustomIcons[key] = dest;
                SettingsStore.Save();
                LoadIcon(item);
                SettingsStore.NotifyPinsChanged(this);
            }
            catch (Exception ex) { Log.Write("Ícono propio: " + ex.Message); McDialog.Info("No se pudo usar esa imagen", ex.Message); }
        }

        private void ResetIcon(DockItem item)
        {
            var key = IconKey(item);
            if (key == null || !S.CustomIcons.Remove(key)) return;
            SettingsStore.Save();
            LoadIcon(item);
        }

        private MenuItem IconMenu(DockItem item)
        {
            var key = IconKey(item);
            var m = new MenuItem { Header = "Ícono" };
            m.Items.Add(Mi("Cambiar ícono…", () => ChooseIcon(item), key != null));
            m.Items.Add(Mi("Volver al ícono original", () => ResetIcon(item), key != null && S.CustomIcons.ContainsKey(key)));
            return m;
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
                // Como en macOS: el ícono desaparece en una nubecita donde se soltó.
                double up = Math.Max(0, _pressPoint.Y - _dragPos.Y);
                double size = Math.Max(item.LayoutSize, BaseSize);
                Puff(item.LayoutLeft + item.LayoutSize / 2, IconBottom - up - item.LayoutSize / 2, size);
                item.View.Visibility = Visibility.Hidden;
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

        /// <summary>Vuelve a poner en el Dock la Papelera y Descargas si se habían quitado.</summary>
        public void RestoreDefaultStacks()
        {
            foreach (var d in AppSettings.DefaultStacks()) AddStack(d.Name, d.Target);
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

        /// <summary>Nubecita de humo (animación de "quitar del Dock"). Se borra sola al terminar.</summary>
        private void Puff(double cx, double cy, double size)
        {
            if (_suppressPuff) return; // 1.4.1: ya la hizo el fantasma
            var rnd = new Random();
            if (_fast && _hwnd != IntPtr.Zero) { _puffActive++; WindowOps.ClearShape(_hwnd); _shapeKey = ""; }
            var layer = new Canvas { IsHitTestVisible = false };
            Panel.SetZIndex(layer, 2000);
            Stage.Children.Add(layer);
            var dur = TimeSpan.FromMilliseconds(520);
            var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            const int count = 10;
            for (int i = 0; i < count; i++)
            {
                double r = size * (0.2 + rnd.NextDouble() * 0.14);
                var brush = new RadialGradientBrush(Color.FromArgb(235, 250, 250, 252), Color.FromArgb(0, 230, 230, 235));
                var e = new System.Windows.Shapes.Ellipse { Width = r * 2, Height = r * 2, Fill = brush, RenderTransformOrigin = new Point(0.5, 0.5) };
                Canvas.SetLeft(e, cx - r);
                Canvas.SetTop(e, cy - r);
                var scale = new ScaleTransform(0.35, 0.35);
                var move = new TranslateTransform();
                var group = new TransformGroup();
                group.Children.Add(scale);
                group.Children.Add(move);
                e.RenderTransform = group;
                layer.Children.Add(e);

                double angle = i * 2 * Math.PI / count + rnd.NextDouble() * 0.5;
                double dist = size * (0.35 + rnd.NextDouble() * 0.35);
                var grow = new System.Windows.Media.Animation.DoubleAnimation(0.35, 1.5 + rnd.NextDouble() * 0.4, dur) { EasingFunction = ease };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
                move.BeginAnimation(TranslateTransform.XProperty, new System.Windows.Media.Animation.DoubleAnimation(0, Math.Cos(angle) * dist, dur) { EasingFunction = ease });
                move.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(0, Math.Sin(angle) * dist - size * 0.2, dur) { EasingFunction = ease });
                e.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0.95, 0, dur) { BeginTime = TimeSpan.FromMilliseconds(60) });
            }
            var cleanup = new DispatcherTimer { Interval = dur + TimeSpan.FromMilliseconds(120) };
            cleanup.Tick += (_, _) =>
            {
                cleanup.Stop();
                Stage.Children.Remove(layer);
                if (_puffActive > 0) _puffActive--;
                _shapeKey = "";
                EnsureAnimating(); // vuelve a calcular la forma
            };
            cleanup.Start();
        }

        // ================== Menús ==================

        private ContextMenu NewMenu(UIElement target)
        {
            var cm = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Top, VerticalOffset = 16 }; // 1.6.0: compensa el margen nuevo de la sombra
            PopupGuard.Attach(cm);
            cm.Opened += (_, _) => { _menuOpen = true; EnsureAnimating(); };
            cm.Closed += (_, _) =>
            {
                _menuOpen = false;
                if (!Stage.IsMouseOver) { _hover = false; _hoverItem = null; if (AutoHide) _hideTimer.Start(); }
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
                    cm.Items.Add(StackStyleMenu(item)); // 1.8.1
                    cm.Items.Add(new Separator());
                    cm.Items.Add(Mi("Quitar del Dock", () => RemoveFromDock(item, false)));
                    break;
                case DockItemKind.Folder:
                case DockItemKind.File:
                    cm.Items.Add(Mi("Abrir", () => { if (item.Target != null) Launcher.Launch(item.Target); }));
                    cm.Items.Add(Mi("Mostrar en el Explorador", () => { if (item.Target != null) Launcher.ShowInExplorer(item.Target); }));
                    if (item.Kind == DockItemKind.Folder) cm.Items.Add(StackStyleMenu(item)); // 1.8.1
                    cm.Items.Add(new Separator());
                    cm.Items.Add(Mi("Quitar del Dock", () => RemoveFromDock(item, false)));
                    break;
                case DockItemKind.Launchpad:
                    cm.Items.Add(Mi("Abrir", () => App.ToggleLaunchpad(_monitor)));
                    cm.Items.Add(IconMenu(item)); // 1.6.1
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
                    options.Items.Add(IconMenu(item)); // 1.6.1
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
            foreach (var (key, name) in new[] { ("classic", "Clásico"), ("floating", "Flotante"), ("full", "Ancho completo"), ("taskbar", "Barra"), ("icons", "Solo íconos") })
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

        /// <summary>1.8.1: clic derecho en una carpeta del Dock → "Mostrar como" (abanico, cuadrícula, lista o la general).</summary>
        private MenuItem StackStyleMenu(DockItem item)
        {
            var menu = new MenuItem { Header = "Mostrar como" };
            var s = SettingsStore.Current;
            string? own = null;
            if (item.Target != null) s.StackStyles.TryGetValue(item.Target, out own);
            foreach (var (key, name) in new[] { ("", "Como en Preferencias"), ("fan", "Abanico"), ("grid", "Cuadrícula"), ("list", "Lista") })
            {
                var k = key;
                var mi = new MenuItem { Header = name, IsCheckable = true, IsChecked = (own ?? "") == k };
                mi.Click += (_, _) =>
                {
                    if (item.Target == null) return;
                    if (k.Length == 0) s.StackStyles.Remove(item.Target);
                    else s.StackStyles[item.Target] = k;
                    SettingsStore.Save();
                };
                menu.Items.Add(mi);
            }
            return menu;
        }

        internal void AddFolderFromDialog()
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

            // 1.8.0: pilas en abanico o en lista, como en macOS (opcional; "grid" = como venía)
            var ss = SettingsStore.Current;
            string? own = null;
            if (item.Target != null) ss.StackStyles.TryGetValue(item.Target, out own);
            var style = StackFan.Effective(own, ss.StackStyle); // 1.8.1: cada pila puede tener la suya
            if (style == "fan" && files.Count > 0) { ShowStackFan(item, dir, files); return; }
            bool asList = style == "list";
            if (asList)
            {
                try
                {
                    files = new DirectoryInfo(dir).EnumerateFileSystemInfos()
                        .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                        .OrderByDescending(f => f.LastWriteTime).Take(30).ToList();
                }
                catch { }
            }

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

            Panel wrap = asList ? new StackPanel() : new WrapPanel();
            if (files.Count == 0)
            {
                var empty = new TextBlock { Text = "La carpeta está vacía", Margin = new Thickness(6, 6, 6, 12) };
                empty.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
                wrap.Children.Add(empty);
            }
            foreach (var f in files)
            {
                if (asList)
                {
                    var li = new Image { Width = 22, Height = 22 };
                    RenderOptions.SetBitmapScalingMode(li, BitmapScalingMode.HighQuality);
                    var ln = new TextBlock { Text = f.Name, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12.5, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    ln.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(li);
                    row.Children.Add(ln);
                    var lb = new Button { Content = row, Padding = new Thickness(6, 3, 6, 3), HorizontalContentAlignment = HorizontalAlignment.Left, ToolTip = f.Name };
                    lb.SetResourceReference(StyleProperty, "McFlatButton");
                    var lpath = f.FullName;
                    lb.Click += (_, _) => { popup.IsOpen = false; Launcher.Launch(lpath); };
                    wrap.Children.Add(lb);
                    IconLoader.Request(lpath, 48, src => li.Source = src, useCache: false);
                    continue;
                }
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
            if (asList) panel.Children.Add(new ScrollViewer { Content = wrap, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            else panel.Children.Add(wrap);

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

        /// <summary>
        /// 1.8.0: pila en abanico, como en macOS: los archivos más nuevos suben en una curva desde el ícono, con
        /// el nombre en una etiqueta a la izquierda; arriba de todo, "Abrir en el Explorador".
        /// </summary>
        private void ShowStackFan(DockItem item, string dir, List<FileSystemInfo> files)
        {
            var list = files.Take(10).ToList();
            const double step = 58, iconSz = 48, width = 360;
            int n = list.Count;
            double height = n * step + 70;
            var popup = new Popup
            {
                PlacementTarget = item.View,
                Placement = PlacementMode.Top,
                AllowsTransparency = true,
                StaysOpen = false,
                HorizontalOffset = MaxSize / 2 - (width - 60),
                VerticalOffset = 0
            };
            PopupGuard.Attach(popup);
            var canvas = new Canvas { Width = width, Height = height, Background = Brushes.Transparent };
            for (int i = 0; i < n; i++)
            {
                var f = list[i];
                var (dx, angle) = StackFan.Position(i);
                double cx = width - 60 + dx;             // centro del ícono
                double cy = height - 36 - i * step;      // de abajo hacia arriba
                var img = new Image { Width = iconSz, Height = iconSz };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                IconLoader.Request(f.FullName, 96, src => img.Source = src, useCache: false);
                var label = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(9, 3, 9, 4), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220 };
                label.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1E, 0x1E, 0x22));
                label.Child = new TextBlock { Text = f.Name, Foreground = Brushes.White, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(label);
                row.Children.Add(img);
                var b = new Button { Content = row, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = f.Name, Cursor = Cursors.Hand };
                b.SetResourceReference(StyleProperty, "McFlatButton");
                var path = f.FullName;
                b.Click += (_, _) => { popup.IsOpen = false; Launcher.Launch(path); };
                b.Loaded += (_, _) =>
                {
                    // queda alineado a la derecha: el ícono en cx
                    Canvas.SetLeft(b, cx + iconSz / 2 - b.ActualWidth);
                    Canvas.SetTop(b, cy - iconSz / 2);
                };
                b.RenderTransformOrigin = new Point(1, 0.5);
                var rot = new RotateTransform(angle);
                var tr = new TranslateTransform(0, (i + 1) * step);
                b.RenderTransform = new TransformGroup { Children = { rot, tr } };
                b.Opacity = 0;
                var delay = TimeSpan.FromMilliseconds(18 * i);
                var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation((i + 1) * step, 0, TimeSpan.FromMilliseconds(220)) { BeginTime = delay, EasingFunction = ease });
                b.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { BeginTime = delay });
                canvas.Children.Add(b);
            }
            var open = new Button { Content = "Abrir en el Explorador  ›", Padding = new Thickness(10, 4, 10, 4) };
            open.SetResourceReference(StyleProperty, "McButton");
            open.Click += (_, _) => { popup.IsOpen = false; Launcher.ShowInExplorer(dir); };
            var (odx, _) = StackFan.Position(n);
            open.Loaded += (_, _) =>
            {
                Canvas.SetLeft(open, width - 60 + odx + iconSz / 2 - open.ActualWidth);
                Canvas.SetTop(open, height - 36 - n * step - 14);
            };
            canvas.Children.Add(open);
            popup.Child = canvas;
            popup.Opened += (_, _) => { _menuOpen = true; EnsureAnimating(); };
            popup.Closed += (_, _) => { _menuOpen = false; EnsureAnimating(); App.ScheduleTrim(); };
            popup.IsOpen = true;
        }

        // ================== Soltar archivos ==================

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(McBar.Panels.LaunchpadWindow.DockPinFormat)
                ? DragDropEffects.Link : DragDropEffects.None;
            var p = e.GetPosition(Stage);
            _hover = true;
            _hideTimer.Stop();
            if (_hidden) _hidden = false; // 1.4.1: si estaba oculto, sube entero al arrastrar algo encima
            _mouseX = p.X - ActualWidth / 2;
            _anchorX = _mouseX;
            EnsureAnimating();
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            // 0.4.3: app arrastrada desde el Launchpad
            if (e.Data.GetDataPresent(McBar.Panels.LaunchpadWindow.DockPinFormat) && e.Data.GetData(McBar.Panels.LaunchpadWindow.DockPinFormat) is string pin)
            {
                var parts = pin.Split('\n');
                if (parts.Length == 2) PinTarget(parts[0], parts[1]);
                _hover = false;
                EnsureAnimating();
                if (AutoHide) _hideTimer.Start();
                return;
            }
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
