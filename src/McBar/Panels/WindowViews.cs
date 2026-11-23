// McBar — Creado por Andrés Londero
// Ventanas estilo Mac (0.8.0):
//  - SwitcherWindow: Alt+Tab propio como el Cmd+Tab de macOS: íconos de las apps abiertas en una fila;
//    Tab/Mayús+Tab o flechas para elegir, soltar Alt para ir, Esc para cancelar.
//  - PreviewWindow: al dejar el mouse sobre una app abierta del Dock, miniaturas EN VIVO de sus ventanas;
//    clic para ir a esa ventana, ✕ para cerrarla.
//  - MissionControlWindow: Win+Tab, todas las ventanas del monitor en una grilla con miniaturas en vivo,
//    como Mission Control. Clic para ir; Esc o clic en el fondo para salir; flechas + Enter.
// Las miniaturas las dibuja Windows (DWM) encima de la ventana: ThumbHost las ubica sobre unos recuadros.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    /// <summary>Ubica miniaturas de DWM sobre recuadros WPF de una ventana y las mantiene al día.</summary>
    internal sealed class ThumbHost : IDisposable
    {
        private readonly Window _w;
        private readonly List<(FrameworkElement box, DwmThumb thumb)> _items = new List<(FrameworkElement, DwmThumb)>();

        public ThumbHost(Window w)
        {
            _w = w;
            _w.LayoutUpdated += (_, _) => Sync();
            _w.Closed += (_, _) => Dispose();
        }

        public void Add(FrameworkElement box, IntPtr source)
        {
            var h = new WindowInteropHelper(_w).Handle;
            if (h == IntPtr.Zero) { _w.SourceInitialized += (_, _) => Add(box, source); return; }
            var t = new DwmThumb(h, source);
            if (t.Ok) _items.Add((box, t));
        }

        public void Sync()
        {
            if (!_w.IsLoaded) return;
            double sc = WindowOps.DpiScale(_w);
            foreach (var (box, thumb) in _items)
            {
                if (!box.IsVisible || box.ActualWidth < 2) { thumb.HideThumb(); continue; }
                try
                {
                    var p = box.TransformToAncestor(_w).Transform(new Point(0, 0));
                    thumb.Show((int)Math.Round(p.X * sc), (int)Math.Round(p.Y * sc), (int)Math.Round(box.ActualWidth * sc), (int)Math.Round(box.ActualHeight * sc));
                }
                catch { thumb.HideThumb(); }
            }
        }

        public void Dispose()
        {
            foreach (var (_, t) in _items) t.Dispose();
            _items.Clear();
        }
    }

    internal static class WinUi
    {
        public static Image AppIcon(TrackedWindow w, double size)
        {
            var img = new Image { Width = size, Height = size };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            var hwnd = w.Hwnd;
            if (!string.IsNullOrEmpty(w.IconSource))
                IconLoader.Request(w.IconSource, (int)Math.Max(size, 48), src => img.Source = src ?? IconLoader.FromWindow(hwnd), appShape: true);
            else img.Source = IconLoader.FromWindow(hwnd);
            return img;
        }

        public static TextBlock Label(string text, double size, bool bold = false)
        {
            var t = new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Brushes.White };
            t.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.6 };
            return t;
        }

        private static string _wallPath = "";
        private static ImageBrush? _wallBrush;

        /// <summary>1.8.0: el fondo de pantalla actual, chiquito (para los escritorios de "Ver todas las ventanas").</summary>
        public static ImageBrush? WallpaperBrush()
        {
            try
            {
                var sb = new System.Text.StringBuilder(520);
                if (!GetWallpaper(0x0073 /*SPI_GETDESKWALLPAPER*/, (uint)sb.Capacity, sb, 0)) return null;
                var path = sb.ToString();
                if (path == _wallPath) return _wallBrush;
                _wallPath = path;
                _wallBrush = null;
                if (!System.IO.File.Exists(path)) return null;
                var bi = new System.Windows.Media.Imaging.BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path);
                bi.DecodePixelWidth = 320;
                bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                _wallBrush = new ImageBrush(bi) { Stretch = Stretch.UniformToFill };
                _wallBrush.Freeze();
                return _wallBrush;
            }
            catch { return null; }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool GetWallpaper(uint action, uint param, System.Text.StringBuilder value, uint winIni);

        /// <summary>Ventanas visibles (no minimizadas) de un monitor, la más reciente primero.</summary>
        public static List<TrackedWindow> WindowsOn(MonitorInfo m) =>
            App.Tracker.Windows
               .Where(w => Win32.IsWindow(w.Hwnd) && Win32.IsWindowVisible(w.Hwnd) && !Win32.IsIconic(w.Hwnd))
               .Where(w => Monitors.FromWindow(w.Hwnd)?.Device == m.Device)
               .OrderByDescending(w => w.LastActivated)
               .ToList();
    }

    // ================== Alt+Tab ==================

    internal sealed class SwitcherWindow : Window
    {
        private readonly List<List<TrackedWindow>> _apps;
        private readonly StackPanel _row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        private readonly TextBlock _name;
        private int _sel;

        public bool HasApps => _apps.Count > 0;

        public SwitcherWindow(MonitorInfo m)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = (FontFamily)FindResource("Mc.Font");

            _apps = WindowSwitch.AppsByRecency(App.Tracker.Windows.Where(w => Win32.IsWindow(w.Hwnd) && Win32.IsWindowVisible(w.Hwnd)));
            foreach (var app in _apps.Take(14))
            {
                var cell = new Border { Width = 96, Height = 96, CornerRadius = new CornerRadius(18), Margin = new Thickness(4), Child = WinUi.AppIcon(app[0], 72) };
                _row.Children.Add(cell);
            }
            _name = new TextBlock { FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
            _name.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var sp = new StackPanel();
            sp.Children.Add(_row);
            sp.Children.Add(_name);
            var card = new Border { Child = sp, Padding = new Thickness(14, 14, 14, 10), CornerRadius = new CornerRadius(26), BorderThickness = new Thickness(1), MaxWidth = m.Bounds.Width / m.Scale - 80 };
            card.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            card.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            Content = card;
            SourceInitialized += (_, _) => WindowOps.MakeToolNoActivate(this);
            Loaded += (_, _) =>
            {
                double sc = m.Scale;
                WindowOps.PlaceOnMonitor(this, m, (m.Bounds.Width / sc - ActualWidth) / 2, (m.Bounds.Height / sc - ActualHeight) / 2); // 1.5.0
            };
            // empieza en la app anterior (como Alt+Tab)
            _sel = _apps.Count > 1 ? 1 : 0;
            Render();
        }

        public void Step(bool back)
        {
            _sel = WindowSwitch.Step(_sel, Math.Min(_apps.Count, 14), back);
            Render();
        }

        private void Render()
        {
            for (int i = 0; i < _row.Children.Count; i++)
            {
                var b = (Border)_row.Children[i];
                if (i == _sel) b.SetResourceReference(Border.BackgroundProperty, "Mc.Hover");
                else b.Background = Brushes.Transparent;
            }
            if (_sel < _apps.Count) _name.Text = App.Tracker.AppNameFor(_apps[_sel][0].Hwnd);
        }

        /// <summary>Va a la app elegida (su ventana más reciente) y cierra.</summary>
        public void Commit()
        {
            if (_sel < _apps.Count) WindowOps.Activate(_apps[_sel][0].Hwnd);
            Close();
        }
    }

    // ================== Vista previa del Dock ==================

    internal sealed class PreviewWindow : Window
    {
        private readonly ThumbHost _thumbs;
        private readonly DispatcherTimer _hide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
        public IntPtr[] Sources { get; }

        public PreviewWindow(IReadOnlyList<TrackedWindow> windows, double centerX, double bottomY, MonitorInfo m)
        {
            Sources = windows.Select(w => w.Hwnd).ToArray();
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = (FontFamily)FindResource("Mc.Font");
            _thumbs = new ThumbHost(this);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var w in windows.Take(5))
            {
                var hwnd = w.Hwnd;
                var title = WinUi.Label(WindowOps.GetTitle(hwnd), 12);
                title.MaxWidth = 170;
                var close = new TextBlock { Text = "", FontSize = 10, Foreground = Brushes.White, Padding = new Thickness(6), Cursor = Cursors.Hand, ToolTip = "Cerrar ventana", HorizontalAlignment = HorizontalAlignment.Right };
                close.SetResourceReference(TextBlock.FontFamilyProperty, "Mc.Icons");
                close.MouseLeftButtonUp += (_, e) => { e.Handled = true; WindowOps.Close(hwnd); Close(); };
                var head = new Grid { Margin = new Thickness(2, 0, 0, 6) };
                head.Children.Add(title);
                head.Children.Add(close);
                // 1.3.1: el ícono de la app queda DETRÁS de la miniatura: si Windows tarda en darla (o no la da,
                // por ejemplo ventanas de otro escritorio), se ve el ícono en lugar de un cuadro vacío.
                var box = new Border { Width = 200, Height = 125, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)) };
                var fallback = WinUi.AppIcon(w, 48);
                fallback.HorizontalAlignment = HorizontalAlignment.Center;
                fallback.VerticalAlignment = VerticalAlignment.Center;
                fallback.Opacity = 0.85;
                box.Child = fallback;
                var tile = new StackPanel { Margin = new Thickness(6) };
                tile.Children.Add(head);
                tile.Children.Add(box);
                var cell = new Border { Child = tile, CornerRadius = new CornerRadius(12), Background = Brushes.Transparent, Cursor = Cursors.Hand };
                cell.MouseEnter += (_, _) => cell.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
                cell.MouseLeave += (_, _) => cell.Background = Brushes.Transparent;
                cell.MouseLeftButtonUp += (_, _) => { WindowOps.Activate(hwnd); Close(); };
                row.Children.Add(cell);
                _thumbs.Add(box, hwnd);
            }
            var card = new Border { Child = row, Padding = new Thickness(6), CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            card.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            Content = card;
            SourceInitialized += (_, _) => { WindowOps.MakeToolNoActivate(this); WindowOps.MakeGlass(this); };
            Loaded += (_, _) =>
            {
                // 1.5.0: centerX/bottomY vienen en unidades de la escala de ESTE monitor (las calcula el Dock)
                double sc = m.Scale, ox = m.Bounds.Left / sc, oy = m.Bounds.Top / sc;
                double minL = 8, maxL = m.Bounds.Width / sc - ActualWidth - 8;
                WindowOps.PlaceOnMonitor(this, m, Math.Clamp(centerX - ox - ActualWidth / 2, minL, Math.Max(minL, maxL)), bottomY - oy - ActualHeight - 8);
                _thumbs.Sync();
                // 1.3.1: Windows a veces entrega la miniatura (sobre todo de minimizadas) un momento después
                foreach (var ms in new[] { 120, 450 })
                {
                    var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
                    t.Tick += (_, _) => { t.Stop(); if (IsLoaded) _thumbs.Sync(); };
                    t.Start();
                }
            };
            MouseEnter += (_, _) => _hide.Stop();
            MouseLeave += (_, _) => HideSoon();
            _hide.Tick += (_, _) => { _hide.Stop(); if (!IsMouseOver) Close(); };
        }

        /// <summary>Se cierra en un ratito salvo que el mouse entre a la vista previa.</summary>
        public void HideSoon() { _hide.Stop(); _hide.Start(); }
        public void KeepOpen() => _hide.Stop();
    }

    // ================== Win+Tab: todas las ventanas ==================

    internal sealed class MissionControlWindow : Window
    {
        private readonly ThumbHost _thumbs;
        private readonly List<(Border cell, IntPtr hwnd)> _cells = new List<(Border, IntPtr)>();
        private readonly Canvas _canvas = new Canvas();
        private int _sel = -1;
        private bool _closing;
        public DateTime ClosedAt { get; private set; } = DateTime.MinValue;
        public bool IsClosing => _closing;

        public MissionControlWindow(MonitorInfo m)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = (FontFamily)FindResource("Mc.Font");
            double sc = m.Scale;
            WindowOps.PlaceOnMonitor(this, m, 0, 0, m.Bounds.Width / sc, m.Bounds.Height / sc); // 1.5.0
            _thumbs = new ThumbHost(this);

            var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(0x8C, 0x08, 0x08, 0x0C)) };
            root.Children.Add(_canvas);
            Content = root;
            root.MouseLeftButtonUp += (_, _) => CloseAnimated();

            var windows = WinUi.WindowsOn(m);
            double top = 92, side = 60, bottom = 110;

            // 1.0.0: franja de escritorios arriba, como Spaces en Mission Control
            var desks = VirtualDesktops.List();
            int cur = Math.Max(0, desks.FindIndex(d => d.Current));
            var strip = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 22, 0, 0) };
            bool tiles = SettingsStore.Current.MissionDesktopTiles; // 1.8.0: escritorios con el fondo, como macOS
            var wallBrush = tiles ? WinUi.WallpaperBrush() : null;
            for (int i = 0; i < desks.Count; i++)
            {
                int target = i;
                if (tiles)
                {
                    var tile = new StackPanel { Margin = new Thickness(8, 0, 8, 0), Cursor = Cursors.Hand };
                    var pic = new Border
                    {
                        Width = 150, Height = 150 * m.Bounds.Height / Math.Max(1.0, m.Bounds.Width), CornerRadius = new CornerRadius(8),
                        BorderThickness = new Thickness(desks[i].Current ? 3 : 1),
                        Background = (Brush?)wallBrush ?? new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)),
                        Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.4 }
                    };
                    if (desks[i].Current) pic.SetResourceReference(Border.BorderBrushProperty, "Mc.Accent");
                    else pic.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
                    var name = WinUi.Label(desks[i].Name, 12.5, desks[i].Current);
                    name.HorizontalAlignment = HorizontalAlignment.Center;
                    name.Margin = new Thickness(0, 6, 0, 0);
                    tile.Children.Add(pic);
                    tile.Children.Add(name);
                    tile.MouseLeftButtonUp += (_, e) =>
                    {
                        e.Handled = true;
                        CloseAnimated();
                        if (target != cur) VirtualDesktops.SwitchTo(cur, target);
                    };
                    strip.Children.Add(desks.Count > 1 ? WithCloseButton(tile, () => { CloseAnimated(); VirtualDesktops.Remove(cur, target); }) : tile);
                    continue;
                }
                var pill = new Border { Padding = new Thickness(16, 7, 16, 7), Margin = new Thickness(5, 0, 5, 0), CornerRadius = new CornerRadius(12), Cursor = Cursors.Hand, BorderThickness = new Thickness(1) };
                pill.Child = WinUi.Label(desks[i].Name, 13, desks[i].Current);
                if (desks[i].Current) pill.SetResourceReference(Border.BackgroundProperty, "Mc.Accent");
                else pill.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                pill.BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF));
                pill.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    CloseAnimated();
                    if (target != cur) VirtualDesktops.SwitchTo(cur, target);
                };
                strip.Children.Add(desks.Count > 1 ? WithCloseButton(pill, () => { CloseAnimated(); VirtualDesktops.Remove(cur, target); }) : pill);
            }
            var add = new Border { Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(5, 0, 5, 0), CornerRadius = new CornerRadius(12), Cursor = Cursors.Hand, ToolTip = "Nuevo escritorio",
                                   Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), Child = WinUi.Label("+", 14, true) };
            add.MouseLeftButtonUp += (_, e) => { e.Handled = true; CloseAnimated(); VirtualDesktops.Add(); };
            if (tiles) { add.VerticalAlignment = VerticalAlignment.Top; add.Margin = new Thickness(8, 30, 8, 0); }
            strip.Children.Add(add);
            root.Children.Add(strip);
            if (tiles) top = 92 + 150 * m.Bounds.Height / Math.Max(1.0, m.Bounds.Width) + 20; // más lugar arriba para los escritorios
            if (windows.Count == 0)
            {
                var t = WinUi.Label("No hay ventanas abiertas en esta pantalla", 18);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.VerticalAlignment = VerticalAlignment.Center;
                root.Children.Add(t);
            }
            var cells = WindowSwitch.GridLayout(windows.Count, Width - side * 2, Height - top - bottom, 34);
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                var c = cells[i];
                var hwnd = w.Hwnd;
                var box = new Border { Height = Math.Max(40, c.H - 34) };
                var title = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
                title.Children.Add(WinUi.AppIcon(w, 20));
                var tl = WinUi.Label(WindowOps.GetTitle(hwnd), 13, true);
                tl.Margin = new Thickness(6, 0, 0, 0);
                tl.MaxWidth = Math.Max(60, c.W - 40);
                title.Children.Add(tl);
                var sp = new StackPanel();
                sp.Children.Add(box);
                sp.Children.Add(title);
                var cell = new Border { Child = sp, Width = c.W, Padding = new Thickness(6), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(3), BorderBrush = Brushes.Transparent, Cursor = Cursors.Hand };
                int index = i;
                cell.MouseEnter += (_, _) => Select(index);
                cell.MouseLeftButtonUp += (_, e) => { e.Handled = true; Go(hwnd); };
                Canvas.SetLeft(cell, side + c.X - 6);
                Canvas.SetTop(cell, top + c.Y - 6);
                _canvas.Children.Add(cell);
                _cells.Add((cell, hwnd));
                _thumbs.Add(box, hwnd);
            }
            SourceInitialized += (_, _) => { WindowOps.MakeTool(this); WindowOps.MakeGlass(this); WindowOps.EnableBlur(this, Color.FromArgb(0x30, 0, 0, 0)); };
            Loaded += (_, _) =>
            {
                WindowOps.Activate(new WindowInteropHelper(this).Handle);
                Focus();
                _thumbs.Sync();
                root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            };
            Deactivated += (_, _) => CloseAnimated();
            PreviewKeyDown += (_, e) =>
            {
                int n = _cells.Count;
                switch (e.Key)
                {
                    case Key.Escape: CloseAnimated(); break;
                    case Key.Right: case Key.Down: case Key.Tab: if (n > 0) Select(WindowSwitch.Step(_sel, n, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))); break;
                    case Key.Left: case Key.Up: if (n > 0) Select(WindowSwitch.Step(_sel < 0 ? 0 : _sel, n, true)); break;
                    case Key.Enter: if (_sel >= 0 && _sel < n) Go(_cells[_sel].hwnd); break;
                    default: return;
                }
                e.Handled = true;
            };
        }

        /// <summary>1.8.1: "×" arriba a la izquierda al pasar el mouse (como en macOS) para cerrar ese escritorio.</summary>
        private static FrameworkElement WithCloseButton(FrameworkElement content, Action close)
        {
            var g = new Grid();
            g.Children.Add(content);
            var x = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Cursor = Cursors.Hand, ToolTip = "Cerrar escritorio",
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(-2, -8, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x3A, 0x3A, 0x3E)), BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1), Visibility = Visibility.Hidden,
                Child = new TextBlock { Text = "×", Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -2, 0, 0) }
            };
            x.MouseLeftButtonUp += (_, e) => { e.Handled = true; close(); };
            g.Children.Add(x);
            g.MouseEnter += (_, _) => x.Visibility = Visibility.Visible;
            g.MouseLeave += (_, _) => x.Visibility = Visibility.Hidden;
            var cm = new ContextMenu();
            var mi = new MenuItem { Header = "Cerrar escritorio" };
            mi.Click += (_, _) => close();
            cm.Items.Add(mi);
            g.ContextMenu = cm;
            return g;
        }

        private void Select(int i)
        {
            _sel = i;
            for (int k = 0; k < _cells.Count; k++)
            {
                if (k == i) _cells[k].cell.SetResourceReference(Border.BorderBrushProperty, "Mc.Accent");
                else _cells[k].cell.BorderBrush = Brushes.Transparent;
            }
        }

        private void Go(IntPtr hwnd)
        {
            CloseAnimated();
            WindowOps.Activate(hwnd);
        }

        public void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            ClosedAt = DateTime.Now;
            _thumbs.Dispose();
            Close();
            App.ScheduleTrim();
        }
    }
}
