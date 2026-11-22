// McBar — Creado por Andrés Londero
// Punto de entrada y coordinación.
// Arranca el seguimiento de ventanas, oculta la barra de Windows (si está activado), crea una barra de
// menú y un Dock por monitor, fija el área de trabajo de cada monitor, la bandeja propia, la animación
// de minimizar y el clima. Garantiza que todo vuelva a como estaba al salir, al cerrar sesión o ante un error.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using McBar.Core;
using McBar.Dock;
using McBar.MenuBar;
using McBar.Native;
using McBar.Panels;

namespace McBar
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private static DispatcherTimer? _trimTimer;
        private static DispatcherTimer? _weatherTimer;
        private static DispatcherTimer? _watchdog;
        private static DispatcherTimer? _workAreaDebounce;
        private static int _watchdogTicks;
        private static bool _cleanedUp;

        /// <summary>Escala de pantalla del sistema (1 = 100 %, 1.25 = 125 %...).</summary>
        public static double Scale { get; private set; } = 1.0;

        public static WindowTracker Tracker { get; private set; } = null!;
        internal static FullscreenWatcher? Fullscreen { get; private set; }
        internal static ShellHook? Hook { get; private set; }

        public static List<DockWindow> Docks { get; } = new List<DockWindow>();
        public static List<MenuBarWindow> MenuBars { get; } = new List<MenuBarWindow>();
        internal static List<IslandWindow> Islands { get; } = new List<IslandWindow>();
        /// <summary>El Dock del monitor principal (para "Agregar al Dock" desde el Launchpad).</summary>
        public static DockWindow? Dock => Docks.FirstOrDefault(d => d.Monitor.Primary) ?? Docks.FirstOrDefault();

        public static WeatherData? LastWeather { get; private set; }
        /// <summary>Cambió la bandeja propia (íconos agregados, quitados o cambiados).</summary>
        public static event Action? TrayChanged;

        private static LaunchpadWindow? _launchpad;
        private static ControlCenterWindow? _controlCenter;
        private static WidgetsWindow? _widgets;
        private static SettingsWindow? _settings;
        private static string _lastLayoutKey = "";
        private static bool _lastHideTaskbar, _lastStartWithWindows, _lastAnimate, _lastOwnTray;
        private static string _lastIslandKey = "";

        private static string IslandKey(AppSettings s) => $"{s.IslandMedia}{s.IslandVolume}{s.IslandBattery}{s.IslandNotifications}";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            SettingsStore.Load();

            // "McBar.exe --restaurar": devuelve la barra de Windows (y el área de trabajo) a como estaba y sale.
            if (e.Args.Any(a => a.Equals("--restaurar", StringComparison.OrdinalIgnoreCase)))
            {
                WorkArea.Reset();
                Taskbar.Restore();
                ShellHook.RestoreAnimation();
                Shutdown();
                return;
            }

            _mutex = new Mutex(true, "McBar.InstanciaUnica", out bool created);
            if (!created) { Shutdown(); return; }

            DispatcherUnhandledException += (_, a) =>
            {
                Log.Write("Error: " + a.Exception);
                a.Handled = true; // seguir funcionando ante errores menores
            };
            AppDomain.CurrentDomain.UnhandledException += (_, a) =>
            {
                Log.Write("Error fatal: " + a.ExceptionObject);
                SafeCleanup();
            };
            Microsoft.Win32.SystemEvents.SessionEnding += (_, _) => SafeCleanup();

            Scale = Win32.SystemScale();
            var s = SettingsStore.Current;
            if (!s.FirstRunDone)
            {
                s.Pinned = DefaultPins();
                s.FirstRunDone = true;
                SettingsStore.Save();
            }

            ThemeManager.Start();
            Tracker = new WindowTracker();
            Fullscreen = new FullscreenWatcher();
            Fullscreen.Changed += ApplyFullscreen;
            Tracker.ShellWindowShown += _ => { Taskbar.EnforceBurst(); TrayHost.Current?.Raise(); };
            Tracker.ForegroundChanged += hwnd =>
            {
                if (Taskbar.TemporarilyShown && !Taskbar.IsShellTrayClass(WindowOps.GetClass(hwnd))) Taskbar.EndTemporary();
                PopupGuard.OnForeground(hwnd);
                Fullscreen?.OnForeground(hwnd);
            };
            Tracker.Start();

            if (s.HideWindowsTaskbar) Taskbar.Hide();
            else if (s.TaskbarModified) Taskbar.Restore(); // quedó modificada de una sesión anterior
            if (s.StartWithWindows) SystemInfo.SetStartWithWindows(true);
            if (s.UseOwnTray) StartTray();

            BuildMonitors();

            Hook = new ShellHook { MinRectProvider = MinRectFor };
            Hook.TaskbarCreated += OnExplorerRestarted;
            // Esperar un poco antes de revisar (el Explorador suele mandar varios avisos seguidos).
            _workAreaDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _workAreaDebounce.Tick += (_, _) => { _workAreaDebounce!.Stop(); WorkArea.Verify(); };
            Hook.WorkAreaChanged += () => { _workAreaDebounce.Stop(); _workAreaDebounce.Start(); };
            if (s.AnimateWindows) ShellHook.EnsureAnimationOn();

            _lastHideTaskbar = s.HideWindowsTaskbar;
            _lastStartWithWindows = s.StartWithWindows;
            _lastAnimate = s.AnimateWindows;
            _lastOwnTray = s.UseOwnTray;
            _lastIslandKey = IslandKey(s);
            SettingsStore.Changed += OnSettingsChanged;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Scale = Win32.SystemScale();
                BuildMonitors();
                // Al enchufar/desenchufar un monitor, Windows a veces termina de acomodarse un poco después: revisar de nuevo.
                var again = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                again.Tick += (_, _) => { again.Stop(); Scale = Win32.SystemScale(); BuildMonitors(); };
                again.Start();
            }));

            StartWeather();
            StartWatchdog();
            ScheduleTrim(6);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SafeCleanup();
            base.OnExit(e);
        }

        private static List<PinnedApp> DefaultPins()
        {
            var list = new List<PinnedApp>
            {
                new PinnedApp { Name = "Launchpad", Target = AppIdentity.Launchpad },
                new PinnedApp { Name = "Explorador", Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") }
            };
            void AddIfExists(string name, params string[] paths)
            {
                foreach (var p in paths)
                    if (File.Exists(p)) { list.Add(new PinnedApp { Name = name, Target = p }); return; }
            }
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            AddIfExists("Microsoft Edge", Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"), Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe"));
            AddIfExists("Google Chrome", Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"), Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"), Path.Combine(local, @"Google\Chrome\Application\chrome.exe"));
            list.Add(new PinnedApp { Name = "Calculadora", Target = AppIdentity.AppsFolderPrefix + "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" });
            list.Add(new PinnedApp { Name = "Configuración", Target = AppIdentity.AppsFolderPrefix + "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel" });
            return list;
        }

        // ================== Monitores ==================

        /// <summary>Crea (o vuelve a crear) una barra de menú y un Dock por monitor, según los ajustes.</summary>
        private static void BuildMonitors()
        {
            var s = SettingsStore.Current;
            var monitors = Monitors.All();
            if (monitors.Count == 0) return;
            string key = string.Join(";", monitors.Select(m => m.Device + "@" + m.Bounds)) +
                         $"|{s.ShowMenuBar}|{s.MenuBarOnAllMonitors}|{s.DockOnAllMonitors}|{s.TopBarMode}|{s.FastWindows}|{Scale}";
            if (key == _lastLayoutKey && Docks.Count > 0) { UpdateWorkAreas(); return; }
            _lastLayoutKey = key;

            foreach (var d in Docks) { try { d.Teardown(); d.Close(); } catch { } }
            foreach (var mb in MenuBars) { try { mb.Teardown(); mb.Close(); } catch { } }
            foreach (var isl in Islands) { try { isl.Teardown(); isl.Close(); } catch { } }
            Docks.Clear();
            MenuBars.Clear();
            Islands.Clear();
            bool island = s.TopBarMode == "island";
            if (island && s.ShowMenuBar) ActivityHub.Start(); else ActivityHub.Stop();
            WorkArea.Reset();

            foreach (var m in monitors)
            {
                bool primary = m.Primary || m == monitors[0];
                if (primary || s.MenuBarOnAllMonitors)
                {
                    if (island)
                    {
                        var isl = new IslandWindow(m);
                        Islands.Add(isl);
                        isl.SetVisible(s.ShowMenuBar);
                    }
                    else
                    {
                        var mb = new MenuBarWindow(m);
                        MenuBars.Add(mb);
                        mb.SetVisible(s.ShowMenuBar);
                    }
                }
                if (primary || s.DockOnAllMonitors)
                {
                    var d = new DockWindow(m);
                    Docks.Add(d);
                    d.Show();
                }
            }
            UpdateWorkAreas();
            ApplyFullscreen();
        }

        /// <summary>Reserva en cada monitor solo lo que ocupan nuestra barra y nuestro Dock.</summary>
        public static void UpdateWorkAreas()
        {
            foreach (var m in Monitors.All())
            {
                var mb = MenuBars.FirstOrDefault(x => x.Monitor.Device == m.Device);
                var isl = Islands.FirstOrDefault(x => x.Monitor.Device == m.Device);
                var d = Docks.FirstOrDefault(x => x.Monitor.Device == m.Device);
                int top = mb?.ReservePx ?? isl?.ReservePx ?? 0;
                WorkArea.Set(m, top, d?.ReservePx ?? 0);
            }
        }

        private static void ApplyFullscreen()
        {
            var fs = Fullscreen?.FullscreenMonitors ?? new HashSet<string>();
            foreach (var d in Docks) d.SetFullscreen(fs.Contains(d.Monitor.Device));
            foreach (var mb in MenuBars) mb.SetFullscreen(fs.Contains(mb.Monitor.Device));
            foreach (var isl in Islands) isl.SetFullscreen(fs.Contains(isl.Monitor.Device));
        }

        /// <summary>Dónde empiezan los paneles (Centro de control, Widgets) debajo de la barra o la isla, en unidades WPF.</summary>
        public static double TopInsetDip(MonitorInfo m)
        {
            var s = SettingsStore.Current;
            double top = m.Bounds.Top / Scale;
            if (!s.ShowMenuBar) return top + 8;
            return top + (s.TopBarMode == "island" ? IslandWindow.TopMargin + IslandWindow.CompactHeightFor(s) + 8 : MenuBarWindow.BarHeightDip + 6);
        }

        /// <summary>Datos frescos del monitor (el área de trabajo puede haber cambiado desde que se guardó).</summary>
        private static MonitorInfo MonitorOrPrimary(MonitorInfo? m)
        {
            if (m != null) return Monitors.Get(m.Handle) ?? m;
            return Monitors.All().FirstOrDefault(x => x.Primary) ?? Monitors.All().First();
        }

        /// <summary>Monitor donde está trabajando el usuario (el de la ventana con foco), para mostrar ahí los avisos de la isla.</summary>
        public static string ActiveMonitorDevice()
        {
            var fg = Win32.GetForegroundWindow();
            var m = fg != IntPtr.Zero ? Monitors.FromWindow(fg) : null;
            return m?.Device ?? Monitors.All().FirstOrDefault(x => x.Primary)?.Device ?? "";
        }

        /// <summary>Hacia dónde se achica una ventana al minimizarla: su ícono en el Dock de su monitor.</summary>
        private static PxRect? MinRectFor(IntPtr hwnd)
        {
            var m = Monitors.FromWindow(hwnd);
            var first = Docks.FirstOrDefault(d => m != null && d.Monitor.Device == m.Device);
            var r = first?.IconRectFor(hwnd);
            if (r != null) return r;
            foreach (var d in Docks)
            {
                r = d.IconRectFor(hwnd);
                if (r != null) return r;
            }
            return null;
        }

        // ================== Bandeja propia ==================

        private static void StartTray()
        {
            var host = TrayHost.Start();
            if (host == null) return;
            host.Changed += () => TrayChanged?.Invoke();
            host.IconRectProvider = ic =>
            {
                foreach (var mb in MenuBars)
                {
                    var r = mb.TrayIconRect(ic);
                    if (r != null) return r;
                }
                foreach (var isl in Islands)
                {
                    var r = isl.TrayIconRect(ic);
                    if (r != null) return r;
                }
                return null;
            };
            TrayChanged?.Invoke();
        }

        private static void StopTray()
        {
            TrayHost.Current?.Dispose();
            TrayChanged?.Invoke();
        }

        // ================== Vigilante ==================

        /// <summary>
        /// Revisión liviana cada 0,7 s (unas pocas consultas a Windows, nada de dibujar):
        /// - la barra de Windows no debe aparecer (algunas apps la hacen asomar al parpadear),
        /// - la bandeja propia tiene que seguir siendo la primera,
        /// - pantalla completa,
        /// - el área de trabajo sigue como la dejamos (cada ~5 s).
        /// </summary>
        private static void StartWatchdog()
        {
            _watchdog = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(700) };
            _watchdog.Tick += (_, _) =>
            {
                try
                {
                    var s = SettingsStore.Current;
                    if (s.HideWindowsTaskbar) Taskbar.Enforce();
                    TrayHost.Current?.EnsureFirst();
                    Fullscreen?.Check();
                    if (++_watchdogTicks % 14 == 0) WorkArea.Verify(); // cada ~10 s
                    if (_watchdogTicks % 4 == 0) KeepBarsOnTop();
                    // Con "apps por monitor": si una ventana se pasó a otro monitor, actualizar los Docks.
                    if (s.DockAppsPerMonitor && Docks.Count > 1 && _watchdogTicks % 3 == 0) foreach (var d in Docks) d.Resync();
                }
                catch (Exception ex) { Log.Write("Vigilante: " + ex.Message); }
            };
            _watchdog.Start();
        }

        /// <summary>Vuelve a poner arriba de todo la barra/isla y el Dock (por si otra ventana "siempre visible" los tapó).</summary>
        private static void KeepBarsOnTop()
        {
            const uint flags = Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE;
            foreach (Window w in MenuBars.Cast<Window>().Concat(Islands).Concat(Docks))
            {
                if (!w.IsVisible) continue;
                var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                if (h != IntPtr.Zero) Win32.SetWindowPos(h, Win32.HWND_TOPMOST, 0, 0, 0, 0, flags);
            }
        }

        // ================== Clima ==================

        private static void StartWeather()
        {
            WeatherService.Updated += w => LastWeather = w;
            _weatherTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(30) };
            _weatherTimer.Tick += async (_, _) => { if (SettingsStore.Current.ShowWeatherInMenuBar) await WeatherService.GetAsync(); };
            _weatherTimer.Start();
            if (SettingsStore.Current.ShowWeatherInMenuBar) _ = WeatherService.GetAsync();
        }

        // ================== Ajustes ==================

        private static void OnSettingsChanged()
        {
            var s = SettingsStore.Current;
            if (s.HideWindowsTaskbar != _lastHideTaskbar)
            {
                if (s.HideWindowsTaskbar) Taskbar.Hide(); else Taskbar.Restore();
                _lastHideTaskbar = s.HideWindowsTaskbar;
            }
            if (s.StartWithWindows != _lastStartWithWindows)
            {
                SystemInfo.SetStartWithWindows(s.StartWithWindows);
                _lastStartWithWindows = s.StartWithWindows;
            }
            if (s.AnimateWindows != _lastAnimate)
            {
                if (s.AnimateWindows) ShellHook.EnsureAnimationOn(); else ShellHook.RestoreAnimation();
                _lastAnimate = s.AnimateWindows;
            }
            if (s.UseOwnTray != _lastOwnTray)
            {
                if (s.UseOwnTray) StartTray(); else StopTray();
                _lastOwnTray = s.UseOwnTray;
            }
            if (s.ShowWeatherInMenuBar && LastWeather == null) _ = WeatherService.GetAsync();
            if (IslandKey(s) != _lastIslandKey)
            {
                _lastIslandKey = IslandKey(s);
                if (s.TopBarMode == "island" && s.ShowMenuBar) ActivityHub.Restart();
            }
            BuildMonitors(); // si cambiaron monitores/visibilidad/modo se rehace; si no, solo recalcula áreas
            foreach (var mb in MenuBars) mb.SetVisible(s.ShowMenuBar);
            foreach (var isl in Islands) isl.SetVisible(s.ShowMenuBar);
            UpdateWorkAreas();
        }

        // ================== Coordinación ==================

        /// <summary>El Explorador de Windows se reinició: volver a esconder su barra y recuperar la bandeja.</summary>
        public static void OnExplorerRestarted()
        {
            var tray = TrayHost.Current;
            if (tray != null && tray.IsOwnBroadcastRecent) return; // fue nuestro propio aviso
            if (SettingsStore.Current.HideWindowsTaskbar) { Taskbar.Hide(); Taskbar.EnforceBurst(5); }
            UpdateWorkAreas();
            if (tray != null)
            {
                tray.Raise();
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                t.Tick += (_, _) => { t.Stop(); tray.Raise(); tray.Broadcast(); };
                t.Start();
            }
        }

        private static bool RecentlyClosed(DateTime closedAt) => (DateTime.Now - closedAt).TotalMilliseconds < 350;

        public static void ToggleLaunchpad() => ToggleLaunchpad(null);

        public static void ToggleLaunchpad(MonitorInfo? monitor)
        {
            if (_launchpad != null && _launchpad.IsVisible) { _launchpad.CloseAnimated(); return; }
            if (_launchpad != null && RecentlyClosed(_launchpad.ClosedAt)) return;
            _launchpad = new LaunchpadWindow(MonitorOrPrimary(monitor));
            _launchpad.ShowAnimated();
        }

        public static void ToggleControlCenter(MonitorInfo? monitor = null)
        {
            // Con la isla dinámica, el Centro de control se despliega como un cajón desde la isla.
            var isl = Islands.FirstOrDefault(i => monitor != null && i.Monitor.Device == monitor.Device) ?? Islands.FirstOrDefault();
            if (isl != null && isl.IsVisible) { isl.ToggleDrawer(); return; }
            if (_controlCenter != null && _controlCenter.IsVisible && !_controlCenter.IsClosing) { _controlCenter.CloseAnimated(); return; }
            if (_controlCenter != null && RecentlyClosed(_controlCenter.ClosedAt)) return;
            _widgets?.CloseAnimated();
            _controlCenter = new ControlCenterWindow();
            _controlCenter.ShowBelowMenuBar(MonitorOrPrimary(monitor));
        }

        public static void ToggleWidgets(MonitorInfo? monitor = null)
        {
            if (_widgets != null && _widgets.IsVisible && !_widgets.IsClosing) { _widgets.CloseAnimated(); return; }
            if (_widgets != null && RecentlyClosed(_widgets.ClosedAt)) return;
            _controlCenter?.CloseAnimated();
            var m = MonitorOrPrimary(monitor);
            _widgets = new WidgetsWindow();
            double top = TopInsetDip(m);
            double bottom = m.Work.Bottom / Scale - 8;
            _widgets.ShowBelowMenuBar(m, Math.Max(300, bottom - top));
        }

        public static void OpenSettings()
        {
            if (_settings != null && _settings.IsVisible) { _settings.Activate(); return; }
            _settings = new SettingsWindow();
            _settings.Show();
            _settings.Activate();
        }

        public static void ShowAbout() => McDialog.ShowAbout();

        /// <summary>Libera memoria que no se usa unos segundos después de cerrar paneles.</summary>
        public static void ScheduleTrim(int seconds = 4)
        {
            if (_trimTimer == null)
            {
                _trimTimer = new DispatcherTimer(DispatcherPriority.Background);
                _trimTimer.Tick += (_, _) =>
                {
                    _trimTimer!.Stop();
                    GC.Collect(2, GCCollectionMode.Optimized, false);
                    Win32.TrimWorkingSet();
                };
            }
            _trimTimer.Stop();
            _trimTimer.Interval = TimeSpan.FromSeconds(seconds);
            _trimTimer.Start();
        }

        public static void Quit()
        {
            SafeCleanup();
            Current.Shutdown();
        }

        private static void SafeCleanup()
        {
            if (_cleanedUp) return;
            _cleanedUp = true;
            _watchdog?.Stop();
            foreach (var d in Docks) { try { d.Teardown(); } catch { } }
            foreach (var mb in MenuBars) { try { mb.Teardown(); } catch { } }
            foreach (var isl in Islands) { try { isl.Teardown(); } catch { } }
            try { ActivityHub.Stop(); } catch { }
            try { TrayHost.Current?.Dispose(); } catch { }
            try { Hook?.Dispose(); } catch { }
            try { Fullscreen?.Dispose(); } catch { }
            try { Tracker?.Dispose(); } catch { }
            try { WorkArea.Reset(); } catch { }
            try { if (Taskbar.IsHiddenByUs || SettingsStore.Current.TaskbarModified) Taskbar.Restore(); } catch { }
            try { ShellHook.RestoreAnimation(); } catch { }
        }
    }
}
