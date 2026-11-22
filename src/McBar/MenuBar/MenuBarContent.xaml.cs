// McBar — Creado por Andrés Londero
// Contenido de la barra de menú (menús, bandeja, clima, estado y reloj). Lo usan tanto la barra de menú
// clásica (MenuBarWindow, en una fila) como la isla dinámica expandida (IslandWindow, en dos filas).
// Los menús de la app activa (Archivo, Edición, Ver, Ventana, Ayuda) mandan los atajos de teclado
// estándar de Windows a esa app (Ctrl+C, Ctrl+S, etc.), porque las apps de Windows no exponen sus menús.
// A la derecha: íconos de la bandeja propia, clima, batería, red, volumen, búsqueda, Centro de control y reloj.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;
using McBar.Panels;

namespace McBar.MenuBar
{
    public partial class MenuBarContent : UserControl
    {
        private static readonly CultureInfo Es = new CultureInfo("es-AR");

        private readonly MonitorInfo _monitor;
        private readonly DispatcherTimer _clockTimer;
        private readonly Dictionary<string, Button> _trayButtons = new Dictionary<string, Button>();
        private readonly WeatherIcon _weatherIcon = new WeatherIcon { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };

        // manejadores guardados para poder desconectarlos al cerrar (si cambian los monitores)
        private readonly Action _onSettings, _onTray;
        private readonly Action<IntPtr> _onForeground;
        private readonly Action<WeatherData> _onWeather;
        private readonly NetworkAddressChangedEventHandler _onNetwork;
        private readonly Microsoft.Win32.PowerModeChangedEventHandler _onPower;

        public MonitorInfo Monitor => _monitor;

        /// <summary>true cuando este contenido está dentro de la isla: Wi-Fi, volumen, batería y Centro de control abren el cajón.</summary>
        public bool InIsland { get; set; }

        private IntPtr Target => App.Tracker.LastExternalForeground;
        private static AppSettings S => SettingsStore.Current;

        internal MenuBarContent(MonitorInfo monitor)
        {
            InitializeComponent();
            _monitor = monitor;
            _clockTimer = new DispatcherTimer(DispatcherPriority.Background);
            _clockTimer.Tick += (_, _) => { UpdateClock(); RefreshStatus(); ScheduleClock(); };
            WeatherContent.Children.Insert(0, _weatherIcon);

            LogoButton.Click += (_, _) => OpenMenu(LogoButton, BuildSystemMenu());
            AppNameButton.Click += (_, _) => OpenMenu(AppNameButton, BuildAppMenu());
            FileButton.Click += (_, _) => OpenMenu(FileButton, BuildFileMenu());
            EditButton.Click += (_, _) => OpenMenu(EditButton, BuildEditMenu());
            ViewButton.Click += (_, _) => OpenMenu(ViewButton, BuildViewMenu());
            WindowButton.Click += (_, _) => OpenMenu(WindowButton, BuildWindowMenu());
            HelpButton.Click += (_, _) => OpenMenu(HelpButton, BuildHelpMenu());
            TrayButton.Click += (_, _) => { if (TrayHost.Current != null) ShowTrayOverflow(); else Taskbar.ShowTemporarily(); };
            WeatherButton.Click += (_, _) => App.ToggleWidgets(_monitor);
            WifiButton.Click += (_, _) => { if (InIsland) App.ToggleControlCenter(_monitor); else OpenMenu(WifiButton, BuildNetworkMenu()); };
            BatteryButton.Click += (_, _) => { if (InIsland) App.ToggleControlCenter(_monitor); else OpenMenu(BatteryButton, BuildBatteryMenu()); };
            VolumeButton.Click += (_, _) => { if (InIsland) App.ToggleControlCenter(_monitor); else ShowVolumePopup(); };
            SearchButton.Click += (_, _) => App.ToggleLaunchpad(_monitor);
            ControlCenterButton.Click += (_, _) => App.ToggleControlCenter(_monitor);
            ClockButton.Click += (_, _) => App.ToggleWidgets(_monitor);

            _onSettings = () => { RefreshStatus(); BuildTray(); UpdateWeather(null); };
            _onTray = BuildTray;
            _onForeground = _ => UpdateAppName();
            _onWeather = w => UpdateWeather(w);
            _onNetwork = (_, _) => Dispatcher.BeginInvoke(new Action(RefreshStatus));
            _onPower = (_, _) => Dispatcher.BeginInvoke(new Action(RefreshStatus));
            SettingsStore.Changed += _onSettings;
            App.Tracker.ForegroundChanged += _onForeground;
            WeatherService.Updated += _onWeather;
            NetworkChange.NetworkAddressChanged += _onNetwork;
            Microsoft.Win32.SystemEvents.PowerModeChanged += _onPower;
            App.TrayChanged += _onTray;

            UpdateClock();
            RefreshStatus();
            BuildTray();
            UpdateWeather(null);
            ScheduleClock();
        }

        // ================== Diseño ==================

        /// <summary>Una fila (barra de menú) o dos filas centradas (isla dinámica expandida).</summary>
        public void SetTwoRows(bool twoRows)
        {
            LayoutGrid.RowDefinitions.Clear();
            LayoutGrid.ColumnDefinitions.Clear();
            if (twoRows)
            {
                LayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
                LayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
                Grid.SetRow(LeftGroup, 0); Grid.SetColumn(LeftGroup, 0);
                Grid.SetRow(RightGroup, 1); Grid.SetColumn(RightGroup, 0);
                LeftGroup.HorizontalAlignment = HorizontalAlignment.Center;
                RightGroup.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                LayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                LayoutGrid.ColumnDefinitions.Add(new ColumnDefinition());
                LayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetRow(LeftGroup, 0); Grid.SetColumn(LeftGroup, 0);
                Grid.SetRow(RightGroup, 0); Grid.SetColumn(RightGroup, 2);
                LeftGroup.HorizontalAlignment = HorizontalAlignment.Left;
                RightGroup.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }

        public void Teardown()
        {
            _clockTimer.Stop();
            SettingsStore.Changed -= _onSettings;
            App.Tracker.ForegroundChanged -= _onForeground;
            WeatherService.Updated -= _onWeather;
            NetworkChange.NetworkAddressChanged -= _onNetwork;
            Microsoft.Win32.SystemEvents.PowerModeChanged -= _onPower;
            App.TrayChanged -= _onTray;
        }

        // ================== Bandeja propia ==================

        private void BuildTray()
        {
            TrayPanel.Children.Clear();
            _trayButtons.Clear();
            var host = TrayHost.Current;
            if (host == null)
            {
                TrayButton.ToolTip = "Íconos de la bandeja de Windows";
                TrayButton.Visibility = Taskbar.IsHiddenByUs ? Visibility.Visible : Visibility.Collapsed;
                return;
            }
            var hidden = new HashSet<string>(S.TrayHiddenIds);
            var visible = host.Icons.Where(i => !i.Hidden && i.Icon != null && !i.IsWindowsSystem).ToList();
            foreach (var ic in visible.Where(i => !hidden.Contains(i.StableId)))
            {
                var b = TrayButtonFor(ic, host);
                _trayButtons[ic.Id] = b;
                TrayPanel.Children.Add(b);
            }
            TrayButton.ToolTip = "Más íconos de la bandeja";
            TrayButton.Visibility = visible.Any(i => hidden.Contains(i.StableId)) ? Visibility.Visible : Visibility.Collapsed;
        }

        private Button TrayButtonFor(TrayIcon ic, TrayHost host)
        {
            FrameworkElement content;
            if (S.TrayMonochrome)
            {
                // Estilo Mac: el ícono toma el color del texto de la barra (se adapta a claro/oscuro).
                var r = new System.Windows.Shapes.Rectangle { Width = 16, Height = 16, OpacityMask = new ImageBrush(ic.Icon) { Stretch = Stretch.Uniform } };
                r.SetBinding(System.Windows.Shapes.Shape.FillProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1) });
                content = r;
            }
            else
            {
                var img = new Image { Source = ic.Icon, Width = 16, Height = 16 };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                content = img;
            }
            var b = new Button { Content = content, Padding = new Thickness(6, 0, 6, 0), ToolTip = string.IsNullOrWhiteSpace(ic.Tooltip) ? null : ic.Tooltip };
            b.SetResourceReference(StyleProperty, "McFlatButton");
            b.PreviewMouseDown += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Left) host.Send(ic, e.ClickCount >= 2 ? TrayHost.Click.LeftDouble : TrayHost.Click.LeftDown, RectOf(b));
                else if (e.ChangedButton == MouseButton.Right) host.Send(ic, TrayHost.Click.RightDown, RectOf(b));
                e.Handled = true;
            };
            b.PreviewMouseUp += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Left) host.Send(ic, TrayHost.Click.LeftUp, RectOf(b));
                else if (e.ChangedButton == MouseButton.Right) host.Send(ic, TrayHost.Click.RightUp, RectOf(b));
                e.Handled = true;
            };
            return b;
        }

        private static PxRect RectOf(FrameworkElement el)
        {
            try
            {
                var p = el.PointToScreen(new Point(0, 0));
                double sc = App.Scale;
                return new PxRect((int)p.X, (int)p.Y, (int)(p.X + el.ActualWidth * sc), (int)(p.Y + el.ActualHeight * sc));
            }
            catch { return new PxRect(0, 0, 16, 16); }
        }

        /// <summary>Dónde está dibujado un ícono de la bandeja en esta barra (o null).</summary>
        public PxRect? TrayIconRect(TrayIcon ic) =>
            IsVisible && _trayButtons.TryGetValue(ic.Id, out var b) && b.IsLoaded ? RectOf(b) : (PxRect?)null;

        private void ShowTrayOverflow()
        {
            var host = TrayHost.Current;
            if (host == null) return;
            var hidden = new HashSet<string>(S.TrayHiddenIds);
            var wrap = new WrapPanel { MaxWidth = 220 };
            foreach (var ic in host.Icons.Where(i => !i.Hidden && i.Icon != null && !i.IsWindowsSystem && hidden.Contains(i.StableId)))
            {
                var b = TrayButtonFor(ic, host);
                b.Height = 30;
                wrap.Children.Add(b);
            }
            var border = new Border
            {
                Margin = new Thickness(10),
                Padding = new Thickness(6),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Child = wrap,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.35 }
            };
            border.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            border.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            var popup = new Popup
            {
                PlacementTarget = TrayButton,
                Placement = PlacementMode.Bottom,
                HorizontalOffset = -10,
                VerticalOffset = -8,
                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = PopupAnimation.Fade,
                Child = border
            };
            PopupGuard.Attach(popup);
            TrayButton.Tag = "open";
            popup.Closed += (_, _) => TrayButton.Tag = null;
            popup.IsOpen = true;
        }

        // ================== Clima ==================

        private void UpdateWeather(WeatherData? w)
        {
            if (!S.ShowWeatherInMenuBar) { WeatherButton.Visibility = Visibility.Collapsed; return; }
            w ??= App.LastWeather;
            if (w == null) { WeatherButton.Visibility = Visibility.Collapsed; return; }
            _weatherIcon.Set(WeatherCodes.Kind(w.Code), w.IsDay);
            WeatherText.Text = $"{Math.Round(w.Temp)}°";
            WeatherButton.ToolTip = $"{w.Place}: {WeatherCodes.Describe(w.Code).text}";
            WeatherButton.Visibility = Visibility.Visible;
        }

        // ================== Reloj y estado ==================

        private void ScheduleClock()
        {
            var now = DateTime.Now;
            _clockTimer.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50);
            _clockTimer.Start();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            string time = now.ToString("HH:mm", Es);
            if (SettingsStore.Current.ShowDateInClock)
            {
                string day = now.ToString("ddd", Es).TrimEnd('.');
                string month = now.ToString("MMM", Es).TrimEnd('.');
                ClockText.Text = $"{day} {now.Day} {month}  {time}";
            }
            else ClockText.Text = time;
        }

        public void RefreshStatus()
        {
            // Batería
            var b = SystemInfo.Battery();
            BatteryButton.Visibility = b.HasBattery ? Visibility.Visible : Visibility.Collapsed;
            if (b.HasBattery)
            {
                BatteryText.Text = SettingsStore.Current.ShowBatteryPercent ? b.Percent + " %" : "";
                BatteryFill.Width = Math.Max(1.5, 18.0 * b.Percent / 100.0);
                BatteryBolt.Visibility = b.Charging ? Visibility.Visible : Visibility.Collapsed;
                if (b.Percent <= 20 && !b.Charging) BatteryFill.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));
                else BatteryFill.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Foreground") { Source = BatteryButton });
                BatteryButton.ToolTip = $"Batería: {b.Percent} %" + (b.Charging ? " (cargando)" : "");
            }

            // Red
            var (kind, name) = SystemInfo.Network();
            WifiGlyph.Text = kind == NetKind.Ethernet ? "" : "";
            WifiGlyph.Opacity = kind == NetKind.None ? 0.35 : 1;
            WifiButton.ToolTip = kind == NetKind.None ? "Sin conexión" : name;

            // Volumen
            RefreshVolumeGlyph();
            UpdateAppName();
            TrayHost.Current?.Prune();
        }

        private void RefreshVolumeGlyph()
        {
            var v = CoreAudio.GetVolume();
            bool mute = CoreAudio.GetMute();
            VolumeButton.Visibility = v == null ? Visibility.Collapsed : Visibility.Visible;
            VolumeGlyph.Text = mute || (v ?? 0) < 0.01 ? "" : "";
            VolumeButton.ToolTip = v == null ? null : $"Volumen: {Math.Round(v.Value * 100)} %";
        }

        private void UpdateAppName()
        {
            AppNameText.Text = App.Tracker.AppNameFor(Target);
        }

        // ================== Menús ==================

        private void OpenMenu(Button owner, ContextMenu cm)
        {
            cm.PlacementTarget = owner;
            cm.Placement = PlacementMode.Bottom;
            cm.HorizontalOffset = -10;
            cm.VerticalOffset = -9;
            owner.Tag = "open";
            PopupGuard.Attach(cm);
            cm.Closed += (_, _) => owner.Tag = null;
            cm.IsOpen = true;
        }

        private static MenuItem Mi(string header, Action action, string? gesture = null, bool enabled = true)
        {
            var mi = new MenuItem { Header = header, IsEnabled = enabled };
            if (gesture != null) mi.InputGestureText = gesture;
            mi.Click += (_, _) => action();
            return mi;
        }

        private static MenuItem Info(string header) => new MenuItem { Header = header, IsEnabled = false };

        private void Keys(params ushort[] keys) => WindowOps.SendChordTo(Target, keys);

        private ContextMenu BuildSystemMenu()
        {
            var cm = new ContextMenu();
            cm.Items.Add(Mi("Acerca de este equipo", () => Launcher.Open("ms-settings:about")));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Configuración del sistema…", () => Launcher.Open("ms-settings:")));
            cm.Items.Add(Mi("Preferencias de McBar…", App.OpenSettings));
            cm.Items.Add(Mi("Acerca de McBar", App.ShowAbout));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Forzar salida…", () => Launcher.Launch("taskmgr.exe"), "Ctrl+Mayús+Esc"));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Suspender", Launcher.Sleep));
            cm.Items.Add(Mi("Reiniciar…", () =>
            {
                if (McDialog.Confirm("¿Querés reiniciar el equipo ahora?", "Las apps abiertas se van a cerrar.", "Reiniciar")) Launcher.Restart();
            }));
            cm.Items.Add(Mi("Apagar…", () =>
            {
                if (McDialog.Confirm("¿Querés apagar el equipo ahora?", "Las apps abiertas se van a cerrar.", "Apagar")) Launcher.Shutdown();
            }));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Bloquear pantalla", Launcher.Lock, "Win+L"));
            cm.Items.Add(Mi("Cerrar sesión…", () =>
            {
                if (McDialog.Confirm("¿Querés cerrar la sesión?", "Las apps abiertas se van a cerrar.", "Cerrar sesión")) Launcher.LogOff();
            }));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Salir de McBar", () =>
            {
                if (McDialog.Confirm("¿Salir de McBar?", "Se va a volver a mostrar la barra de tareas de Windows.", "Salir")) App.Quit();
            }));
            return cm;
        }

        private ContextMenu BuildAppMenu()
        {
            var cm = new ContextMenu();
            var name = AppNameText.Text;
            var target = Target;
            var sameApp = App.Tracker.Windows.Where(w => SameApp(w.Hwnd, target)).Select(w => w.Hwnd).ToList();
            bool hasApp = target != IntPtr.Zero && sameApp.Count > 0;
            cm.Items.Add(Mi($"Ocultar {name}", () => { foreach (var h in sameApp) WindowOps.Minimize(h); }, null, hasApp));
            cm.Items.Add(Mi("Ocultar otros", () =>
            {
                foreach (var w in App.Tracker.Windows) if (!sameApp.Contains(w.Hwnd)) WindowOps.Minimize(w.Hwnd);
            }, null, hasApp));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi($"Salir de {name}", () => { foreach (var h in sameApp) WindowOps.Close(h); }, "Alt+F4", hasApp));
            return cm;
        }

        private static bool SameApp(IntPtr a, IntPtr b)
        {
            if (a == IntPtr.Zero || b == IntPtr.Zero) return false;
            var wa = App.Tracker.Windows.FirstOrDefault(w => w.Hwnd == a);
            var wb = App.Tracker.Windows.FirstOrDefault(w => w.Hwnd == b);
            return wa != null && wb != null && wa.PrimaryKey == wb.PrimaryKey;
        }

        private ContextMenu BuildFileMenu()
        {
            const ushort C = WindowOps.VK_CONTROL, S = WindowOps.VK_SHIFT;
            bool ok = Target != IntPtr.Zero;
            var cm = new ContextMenu();
            cm.Items.Add(Mi("Nueva ventana", () => Keys(C, 0x4E), "Ctrl+N", ok));
            cm.Items.Add(Mi("Abrir…", () => Keys(C, 0x4F), "Ctrl+O", ok));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Guardar", () => Keys(C, 0x53), "Ctrl+S", ok));
            cm.Items.Add(Mi("Guardar como…", () => Keys(C, S, 0x53), "Ctrl+Mayús+S", ok));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Imprimir…", () => Keys(C, 0x50), "Ctrl+P", ok));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Cerrar ventana", () => WindowOps.Close(Target), "Alt+F4", ok));
            return cm;
        }

        private ContextMenu BuildEditMenu()
        {
            const ushort C = WindowOps.VK_CONTROL;
            bool ok = Target != IntPtr.Zero;
            var cm = new ContextMenu();
            cm.Items.Add(Mi("Deshacer", () => Keys(C, 0x5A), "Ctrl+Z", ok));
            cm.Items.Add(Mi("Rehacer", () => Keys(C, 0x59), "Ctrl+Y", ok));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Cortar", () => Keys(C, 0x58), "Ctrl+X", ok));
            cm.Items.Add(Mi("Copiar", () => Keys(C, 0x43), "Ctrl+C", ok));
            cm.Items.Add(Mi("Pegar", () => Keys(C, 0x56), "Ctrl+V", ok));
            cm.Items.Add(Mi("Seleccionar todo", () => Keys(C, 0x41), "Ctrl+A", ok));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Buscar…", () => Keys(C, 0x46), "Ctrl+F", ok));
            cm.Items.Add(Mi("Emojis y símbolos", () => Keys(WindowOps.VK_LWIN, 0xBE /* . */), "Win+.", ok));
            return cm;
        }

        private ContextMenu BuildViewMenu()
        {
            const ushort C = WindowOps.VK_CONTROL;
            bool ok = Target != IntPtr.Zero;
            var cm = new ContextMenu();
            cm.Items.Add(Mi("Pantalla completa", () => Keys(WindowOps.VK_F11), "F11", ok));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Acercar", () => Keys(C, WindowOps.VK_OEM_PLUS), "Ctrl++", ok));
            cm.Items.Add(Mi("Alejar", () => Keys(C, WindowOps.VK_OEM_MINUS), "Ctrl+-", ok));
            cm.Items.Add(Mi("Tamaño real", () => Keys(C, 0x30), "Ctrl+0", ok));
            return cm;
        }

        private ContextMenu BuildWindowMenu()
        {
            bool ok = Target != IntPtr.Zero;
            var cm = new ContextMenu();
            cm.Items.Add(Mi("Minimizar", () => WindowOps.Minimize(Target), null, ok));
            cm.Items.Add(Mi("Zoom", () => WindowOps.ToggleZoom(Target), null, ok));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Mover a la izquierda de la pantalla", () => Keys(WindowOps.VK_LWIN, WindowOps.VK_LEFT), "Win+←", ok));
            cm.Items.Add(Mi("Mover a la derecha de la pantalla", () => Keys(WindowOps.VK_LWIN, WindowOps.VK_RIGHT), "Win+→", ok));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Vista de tareas", () => WindowOps.SendChord(WindowOps.VK_LWIN, 0x09 /* Tab */), "Win+Tab"));
            cm.Items.Add(Mi("Mostrar escritorio", () => WindowOps.SendChord(WindowOps.VK_LWIN, 0x44 /* D */), "Win+D"));
            return cm;
        }

        private ContextMenu BuildHelpMenu()
        {
            bool ok = Target != IntPtr.Zero;
            var cm = new ContextMenu();
            cm.Items.Add(Mi($"Ayuda de {AppNameText.Text}", () => Keys(WindowOps.VK_F1), "F1", ok));
            cm.Items.Add(Mi("Ayuda de Windows", () => Launcher.Open("ms-contact-support:")));
            return cm;
        }

        private ContextMenu BuildNetworkMenu()
        {
            var (kind, name) = SystemInfo.Network();
            var cm = new ContextMenu();
            cm.Items.Add(Info(kind switch
            {
                NetKind.Wifi => $"Wi-Fi: conectado ({name})",
                NetKind.Ethernet => $"Ethernet: conectado ({name})",
                NetKind.Other => $"Conectado ({name})",
                _ => "Sin conexión"
            }));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Mostrar redes disponibles", () => Launcher.Open("ms-availablenetworks:")));
            cm.Items.Add(Mi("Configuración de red…", () => Launcher.Open("ms-settings:network")));
            return cm;
        }

        private ContextMenu BuildBatteryMenu()
        {
            var b = SystemInfo.Battery();
            var cm = new ContextMenu();
            cm.Items.Add(Info($"Batería: {b.Percent} %"));
            cm.Items.Add(Info(b.Charging ? "Conectada a la corriente" : "Usando la batería"));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Configuración de batería…", () => Launcher.Open("ms-settings:batterysaver")));
            return cm;
        }

        private void ShowVolumePopup()
        {
            var vol = CoreAudio.GetVolume();
            if (vol == null) return;

            var title = new TextBlock { Text = "Sonido", FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 10) };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var slider = new Slider { Minimum = 0, Maximum = 100, Value = vol.Value * 100, Width = 220 };
            slider.SetResourceReference(StyleProperty, "McSlider");
            slider.ValueChanged += (_, e) => { CoreAudio.SetVolume((float)(e.NewValue / 100.0)); RefreshVolumeGlyph(); };
            var settings = new Button { Content = "Configuración de sonido…", Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            settings.SetResourceReference(StyleProperty, "McButton");

            var sp = new StackPanel();
            sp.Children.Add(title);
            sp.Children.Add(slider);
            sp.Children.Add(settings);

            var border = new Border
            {
                Margin = new Thickness(10),
                Padding = new Thickness(14),
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Child = sp,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.35 }
            };
            border.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            border.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");

            var popup = new Popup
            {
                PlacementTarget = VolumeButton,
                Placement = PlacementMode.Bottom,
                HorizontalOffset = -120,
                VerticalOffset = -8,
                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = PopupAnimation.Fade,
                Child = border
            };
            settings.Click += (_, _) => { popup.IsOpen = false; Launcher.Open("ms-settings:sound"); };
            VolumeButton.Tag = "open";
            PopupGuard.Attach(popup);
            popup.Closed += (_, _) => { VolumeButton.Tag = null; App.ScheduleTrim(); };
            popup.IsOpen = true;
        }
    }
}
