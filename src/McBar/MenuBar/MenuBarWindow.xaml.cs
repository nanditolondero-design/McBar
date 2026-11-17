// McBar — Creado por Andrés Londero
// Barra de menú superior estilo macOS.
// Los menús de la app activa (Archivo, Edición, Ver, Ventana, Ayuda) mandan los atajos de teclado
// estándar de Windows a esa app (Ctrl+C, Ctrl+S, etc.), porque las apps de Windows no exponen sus menús.
using System;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;
using McBar.Panels;

namespace McBar.MenuBar
{
    public partial class MenuBarWindow : Window
    {
        public const double BarHeightDip = 28;
        private static readonly CultureInfo Es = new CultureInfo("es-AR");

        private AppBar? _appBar;
        private readonly DispatcherTimer _clockTimer;
        private uint _taskbarCreatedMsg;
        private bool _fullscreen;
        private bool _wantVisible;

        private IntPtr Target => App.Tracker.LastExternalForeground;

        public MenuBarWindow()
        {
            InitializeComponent();
            _clockTimer = new DispatcherTimer(DispatcherPriority.Background);
            _clockTimer.Tick += (_, _) => { UpdateClock(); RefreshStatus(); ScheduleClock(); };

            SourceInitialized += (_, _) =>
            {
                WindowOps.MakeToolNoActivate(this);
                ApplyBlur();
                _appBar = new AppBar(this, AppBarEdge.Top);
                _appBar.FullscreenChanged += fs => App.OnFullscreenChanged(fs);
                _taskbarCreatedMsg = Win32.RegisterWindowMessage("TaskbarCreated");
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
            };

            LogoButton.Click += (_, _) => OpenMenu(LogoButton, BuildSystemMenu());
            AppNameButton.Click += (_, _) => OpenMenu(AppNameButton, BuildAppMenu());
            FileButton.Click += (_, _) => OpenMenu(FileButton, BuildFileMenu());
            EditButton.Click += (_, _) => OpenMenu(EditButton, BuildEditMenu());
            ViewButton.Click += (_, _) => OpenMenu(ViewButton, BuildViewMenu());
            WindowButton.Click += (_, _) => OpenMenu(WindowButton, BuildWindowMenu());
            HelpButton.Click += (_, _) => OpenMenu(HelpButton, BuildHelpMenu());
            TrayButton.Click += (_, _) => Taskbar.ShowTemporarily();
            WifiButton.Click += (_, _) => OpenMenu(WifiButton, BuildNetworkMenu());
            BatteryButton.Click += (_, _) => OpenMenu(BatteryButton, BuildBatteryMenu());
            VolumeButton.Click += (_, _) => ShowVolumePopup();
            SearchButton.Click += (_, _) => App.ToggleLaunchpad();
            ControlCenterButton.Click += (_, _) => App.ToggleControlCenter();
            ClockButton.Click += (_, _) => App.ToggleWidgets();

            ThemeManager.ThemeChanged += ApplyBlur;
            SettingsStore.Changed += RefreshStatus;
            NetworkChange.NetworkAddressChanged += (_, _) => Dispatcher.BeginInvoke(new Action(RefreshStatus));
            Microsoft.Win32.SystemEvents.PowerModeChanged += (_, _) => Dispatcher.BeginInvoke(new Action(RefreshStatus));
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(new Action(ApplyGeometry));
            App.Tracker.ForegroundChanged += _ => UpdateAppName();

            UpdateClock();
            RefreshStatus();
            ScheduleClock();
        }

        // ================== Ventana ==================

        public void SetVisible(bool visible)
        {
            _wantVisible = visible;
            if (visible)
            {
                if (!_fullscreen) Show();
                ApplyGeometry();
            }
            else
            {
                new WindowInteropHelper(this).EnsureHandle(); // seguimos escuchando avisos del Explorador
                _appBar?.Remove();
                Hide();
            }
        }

        public void ApplyGeometry()
        {
            if (_appBar == null || !_wantVisible) return;
            int px = (int)Math.Ceiling(BarHeightDip * WindowOps.DpiScale(this));
            _appBar.Register();
            _appBar.Apply(px, px);
        }

        public void ReRegisterAppBar()
        {
            if (_appBar == null || !_wantVisible) return;
            _appBar.ReRegister();
            ApplyGeometry();
        }

        public void SetFullscreen(bool fullscreen)
        {
            _fullscreen = fullscreen;
            if (!_wantVisible) return;
            if (fullscreen) Hide(); else Show();
        }

        public void Teardown() => _appBar?.Remove();

        private void ApplyBlur()
        {
            if (new WindowInteropHelper(this).Handle == IntPtr.Zero) return;
            WindowOps.EnableBlur(this, ThemeManager.GetColor("Mc.BlurTint"));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_taskbarCreatedMsg != 0 && msg == (int)_taskbarCreatedMsg)
                Dispatcher.BeginInvoke(new Action(App.OnExplorerRestarted));
            return IntPtr.Zero;
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
            popup.Closed += (_, _) => { VolumeButton.Tag = null; App.ScheduleTrim(); };
            popup.IsOpen = true;
        }
    }
}
