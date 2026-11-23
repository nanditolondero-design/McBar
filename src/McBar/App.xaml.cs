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

            // 0.5.0: desinstalar (lo llama Windows desde "Aplicaciones instaladas")
            if (e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                ThemeManager.Apply();
                if (McDialog.Confirm("¿Desinstalar McBar?",
                        "Se va a devolver la barra de Windows tal como estaba y se va a borrar McBar de esta PC. Tus ajustes quedan guardados por si lo volvés a instalar.",
                        "Desinstalar", danger: true))
                {
                    Installer.CloseOtherInstances();
                    try { WorkArea.Reset(); } catch { }
                    try { Taskbar.Restore(); } catch { }
                    try { ShellHook.RestoreAnimation(); } catch { }
                    Installer.Uninstall();
                    McDialog.Info("McBar se desinstaló", "La barra de Windows volvió a la normalidad. ¡Gracias por usar McBar!");
                }
                Shutdown();
                return;
            }

            // 0.5.0: McBar.exe es su propio instalador. Si se abre desde fuera de la carpeta instalada, ofrece
            // instalar (o actualizar). "Probar sin instalar" recuerda esa ruta y no vuelve a preguntar.
            bool portable = e.Args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase))
                            || e.Args.Any(a => a.Equals("--reinicio", StringComparison.OrdinalIgnoreCase))
                            || (string.Equals(SettingsStore.Current.PortablePath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)
                                && SettingsStore.Current.PortableVersion == Installer.CurrentVersion.ToString(3)); // 1.3.1: una versión nueva vuelve a mostrar el instalador
            if (!portable && !Installer.IsRunningInstalled && !System.Diagnostics.Debugger.IsAttached)
            {
                var decision = Installer.Decide(Installer.CurrentVersion, Installer.InstalledVersion());
                if (decision == "launchInstalled") { Installer.LaunchInstalled(); Shutdown(); return; }
                ThemeManager.Apply();
                var setup = new InstallerWindow(decision == "update");
                setup.ShowDialog();
                if (setup.Result == "installed") { Installer.LaunchInstalled(); Shutdown(); return; }
                if (setup.Result != "portable") { Shutdown(); return; }
                SettingsStore.Current.PortablePath = Environment.ProcessPath ?? "";
                SettingsStore.Current.PortableVersion = Installer.CurrentVersion.ToString(3);
                SettingsStore.Save();
            }

            _mutex = new Mutex(true, "McBar.InstanciaUnica", out bool created);
            if (!created && e.Args.Any(a => a.Equals("--reinicio", StringComparison.OrdinalIgnoreCase)))
            {
                // 1.3.1: la copia anterior se trabó y se está cerrando: esperar que suelte el candado
                try { created = _mutex.WaitOne(TimeSpan.FromSeconds(15)); }
                catch (AbandonedMutexException) { created = true; }
            }
            if (!created) { Shutdown(); return; }

            DispatcherUnhandledException += (_, a) =>
            {
                Log.Write("Error: " + a.Exception);
                a.Handled = true; // seguir funcionando ante errores menores
            };
            AppDomain.CurrentDomain.UnhandledException += (_, a) =>
            {
                // 0.9.0: informe + devolver todo a la normalidad; Windows lo vuelve a abrir (Recovery.Register)
                Log.Write("Error fatal: " + a.ExceptionObject);
                Recovery.SaveReport(a.ExceptionObject);
                SafeCleanup();
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, a) =>
            {
                Log.Write("Error en segundo plano: " + a.Exception);
                a.SetObserved();
            };
            Recovery.Register();
            // 0.5.0 (arranque sin parpadeo): al apagar o cerrar sesión la barra de Windows queda oculta si McBar
            // arranca con Windows, así al volver a entrar no aparece antes de que McBar abra.
            Microsoft.Win32.SystemEvents.SessionEnding += (_, _) =>
                SafeCleanup(keepTaskbarHidden: SettingsStore.Current.StartWithWindows && SettingsStore.Current.HideWindowsTaskbar);

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

            if (s.OwnNotifications) NotificationCenter.Start();
            if (s.NeedsKeyboardHook) KeyboardHook.Start();
            StartWeather();
            StartHealth();
            HotCorners.Apply();
            StartDesktopWidgets();
            StartReminders();
            LiveStatus.Start(); // 1.2.0: temporizador, cronómetro, micrófono/cámara y descargas
            StartPersonalization(); // 1.3.0: perfiles, modo juego y fondo según la hora
            StartWatchdog();
            HangWatch.Start(Dispatcher); // 1.3.1: si la interfaz se traba, lo anota y McBar se reinicia solo
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
            bool island = s.IsIslandLike;
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

        /// <summary>
        /// 1.3.1: acomoda una ventana de McBar (Preferencias) para que la isla o la barra no le tapen la parte
        /// de arriba: la baja debajo de la barra y, si no entra, la achica.
        /// </summary>
        public static void KeepBelowTopBar(Window w)
        {
            try
            {
                var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                var m = (h != IntPtr.Zero ? Monitors.FromWindow(h) : null) ?? MonitorOrPrimary(null);
                double top = TopInsetDip(m);
                double bottom = m.Work.Bottom / Scale - 8;
                if (bottom - top < w.MinHeight) return;
                if (w.Top < top) w.Top = top;
                if (w.Top + w.ActualHeight > bottom)
                {
                    w.Height = Math.Max(w.MinHeight, bottom - w.Top);
                    if (w.Top + w.Height > bottom) w.Top = Math.Max(top, bottom - w.Height);
                }
            }
            catch (Exception ex) { Log.Write("KeepBelowTopBar: " + ex.Message); }
        }

        /// <summary>Dónde empiezan los paneles (Centro de control, Widgets) debajo de la barra o la isla, en unidades WPF.</summary>
        public static double TopInsetDip(MonitorInfo m)
        {
            var s = SettingsStore.Current;
            double top = m.Bounds.Top / Scale;
            if (!s.ShowMenuBar) return top + 8;
            return top + (s.IsIslandLike ? IslandWindow.TopGapFor(s) + IslandWindow.CompactHeightFor(s) + 8 : MenuBarWindow.BarHeightDip + 6);
        }

        /// <summary>Datos frescos del monitor (el área de trabajo puede haber cambiado desde que se guardó).</summary>
        internal static MonitorInfo MonitorOrPrimary(MonitorInfo? m)
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
        // ================== Estabilidad (0.9.0) ==================

        private static DispatcherTimer? _health;

        /// <summary>Anota el consumo cada 10 minutos y avisa si McBar se había cerrado por un error.</summary>
        private static void StartHealth()
        {
            Diagnostics.Sample(); // primera medición (para calcular el % de procesador después)
            var report = Recovery.RecentReport();
            if (report != null)
                NotificationCenter.Add(new NotificationInfo
                {
                    App = "McBar",
                    Title = "McBar se cerró por un error y se volvió a abrir",
                    Body = "El detalle quedó en " + report
                });
            _health = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
            _health.Tick += (_, _) => Log.Write("Consumo: " + Diagnostics.Sample().Format());
            _health.Start();
        }

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
            if (PopupGuard.AnyOpen) return; // 1.3.1: con un menú abierto, no subir la isla por encima de él
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
            if (s.IconPackPath != _lastIconPack)
            {
                _lastIconPack = s.IconPackPath;
                IconPack.Reset();
                IconLoader.ClearCache();
                foreach (var d in Docks) d.RefreshIcons();
            }
            if (s.OwnNotifications) NotificationCenter.Start(); else NotificationCenter.Stop();
            if (s.NeedsKeyboardHook) KeyboardHook.Start(); else KeyboardHook.Stop();
            HotCorners.Apply();
            DynamicWallpaper.Apply(); // 1.3.0
            if (IslandKey(s) != _lastIslandKey)
            {
                _lastIslandKey = IslandKey(s);
                if (s.IsIslandLike && s.ShowMenuBar) ActivityHub.Restart();
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
            if (isl != null && isl.IsVisible) { isl.ToggleDrawer(IslandWindow.DrawerKind.ControlCenter); return; }
            if (_controlCenter != null && _controlCenter.IsVisible && !_controlCenter.IsClosing) { _controlCenter.CloseAnimated(); return; }
            if (_controlCenter != null && RecentlyClosed(_controlCenter.ClosedAt)) return;
            _widgets?.CloseAnimated();
            _controlCenter = new ControlCenterWindow();
            _controlCenter.ShowBelowMenuBar(MonitorOrPrimary(monitor));
        }

        /// <summary>
        /// Reloj / Win+N: centro de notificaciones. Con la isla se despliega como cajón desde la píldora;
        /// con la barra de menú es el panel de la derecha (notificaciones + widgets).
        /// </summary>
        public static void ToggleWidgets(MonitorInfo? monitor = null)
        {
            var isl = Islands.FirstOrDefault(i => monitor != null && i.Monitor.Device == monitor.Device) ?? Islands.FirstOrDefault();
            if (isl != null && isl.IsVisible) { isl.ToggleDrawer(IslandWindow.DrawerKind.Notifications); return; }
            OpenWidgetsPanel(monitor);
        }

        private static WeatherWindow? _weather;
        private static string? _lastIconPack;

        /// <summary>Widget del clima propio (barra de menú).</summary>
        public static void ToggleWeather(MonitorInfo? monitor = null)
        {
            if (_weather != null && _weather.IsVisible && !_weather.IsClosing) { _weather.CloseAnimated(); return; }
            if (_weather != null && RecentlyClosed(_weather.ClosedAt)) return;
            _controlCenter?.CloseAnimated();
            _widgets?.CloseAnimated();
            _weather = new WeatherWindow();
            _weather.ShowBelowMenuBar(MonitorOrPrimary(monitor));
        }

        /// <summary>Cajón propio de cada ícono de la isla (Wi-Fi, sonido, batería).</summary>
        internal static void ToggleIslandDrawer(MonitorInfo? monitor, IslandWindow.DrawerKind kind)
        {
            var isl = Islands.FirstOrDefault(i => monitor != null && i.Monitor.Device == monitor.Device) ?? Islands.FirstOrDefault();
            if (isl != null && isl.IsVisible) isl.ToggleDrawer(kind);
            else ToggleControlCenter(monitor);
        }

        public static void OpenWidgetsPanel(MonitorInfo? monitor = null)
        {
            if (_widgets != null && _widgets.IsVisible && !_widgets.IsClosing) { _widgets.CloseAnimated(); return; }
            if (_widgets != null && RecentlyClosed(_widgets.ClosedAt)) return;
            _controlCenter?.CloseAnimated();
            var m = MonitorOrPrimary(monitor);
            _widgets = new WidgetsWindow();
            double top = TopInsetDip(m);
            double bottom = m.Work.Bottom / Scale - 8;
            // 0.4.0: el panel mide lo que su contenido (antes ocupaba todo el alto y quedaba un hueco vacío)
            _widgets.MaxHeight = Math.Max(300, bottom - top);
            _widgets.ShowBelowMenuBar(m);
        }

        /// <summary>Monitor de la ventana con foco (para los atajos de teclado).</summary>
        public static MonitorInfo? ActiveMonitor()
        {
            var fg = Win32.GetForegroundWindow();
            return fg != IntPtr.Zero ? Monitors.FromWindow(fg) : null;
        }

        private static ViewWindow? _view;

        /// <summary>
        /// Wi-Fi, Bluetooth y Sonido propios (0.7.0): con la isla se abren como cajón; con la barra, como panel.
        /// kind: "wifi", "bluetooth" o "sound".
        /// </summary>
        public static void ShowConnectivity(string kind, MonitorInfo? monitor = null)
        {
            var isl = Islands.FirstOrDefault(i => monitor != null && i.Monitor.Device == monitor.Device) ?? Islands.FirstOrDefault();
            if (isl != null && isl.IsVisible)
            {
                isl.ToggleDrawer(kind == "wifi" ? IslandWindow.DrawerKind.Wifi : kind == "bluetooth" ? IslandWindow.DrawerKind.Bluetooth
                    : kind == "timer" ? IslandWindow.DrawerKind.Timer : IslandWindow.DrawerKind.Sound);
                return;
            }
            if (_view != null && _view.IsVisible && !_view.IsClosing)
            {
                bool same = _view.Kind == kind;
                _view.CloseAnimated();
                if (same) return;
            }
            else if (_view != null && _view.Kind == kind && RecentlyClosed(_view.ClosedAt)) return;
            _controlCenter?.CloseAnimated();
            _widgets?.CloseAnimated();
            FrameworkElement view = kind == "wifi" ? new WifiView() : kind == "bluetooth" ? new BluetoothView()
                : kind == "timer" ? new TimerView() : new SoundView(); // 1.2.0: "timer"
            _view = new ViewWindow(kind, view);
            _view.ShowBelowMenuBar(MonitorOrPrimary(monitor));
        }

        // ================== Ventanas estilo Mac (0.8.0) ==================

        private static SwitcherWindow? _switcher;
        private static MissionControlWindow? _mission;
        private static PreviewWindow? _preview;

        /// <summary>Alt+Tab: abre el selector (si no está) o pasa a la siguiente app.</summary>
        public static void SwitcherStep(bool back)
        {
            if (_switcher == null)
            {
                _switcher = new SwitcherWindow(MonitorOrPrimary(ActiveMonitor()));
                if (!_switcher.HasApps) { _switcher = null; return; }
                _switcher.Closed += (_, _) => _switcher = null;
                _switcher.Show();
                if (back) { _switcher.Step(true); _switcher.Step(true); } // Alt+Mayús+Tab: la última app
                return;
            }
            _switcher.Step(back);
        }

        /// <summary>Al soltar Alt: ir a la app elegida.</summary>
        public static void SwitcherCommit() => _switcher?.Commit();

        public static void SwitcherCancel() => _switcher?.Close();

        /// <summary>Win+Tab: todas las ventanas del monitor activo.</summary>
        public static void ToggleMissionControl(MonitorInfo? monitor = null)
        {
            if (_mission != null && _mission.IsVisible && !_mission.IsClosing) { _mission.CloseAnimated(); return; }
            if (_mission != null && RecentlyClosed(_mission.ClosedAt)) return;
            HideDockPreview(now: true);
            _mission = new MissionControlWindow(MonitorOrPrimary(monitor ?? ActiveMonitor()));
            _mission.Show();
        }

        /// <summary>Vista previa de las ventanas de una app del Dock (posición en DIP de pantalla).</summary>
        public static void ShowDockPreview(IReadOnlyList<TrackedWindow> windows, double centerX, double dockTopY, MonitorInfo m)
        {
            if (!SettingsStore.Current.DockPreviews || windows.Count == 0) return;
            if (_preview != null && _preview.IsVisible && _preview.Sources.SequenceEqual(windows.Select(w => w.Hwnd))) { _preview.KeepOpen(); return; }
            HideDockPreview(now: true);
            _preview = new PreviewWindow(windows, centerX, dockTopY, m);
            _preview.Closed += (s, _) => { if (ReferenceEquals(_preview, s)) _preview = null; };
            _preview.Show();
        }

        /// <summary>¿Hay una vista previa del Dock a la vista? (para pasar de un ícono a otro sin esperar).</summary>
        public static bool DockPreviewOpen => _preview != null && _preview.IsVisible;

        public static void HideDockPreview(bool now = false)
        {
            if (_preview == null) return;
            if (now) { _preview.Close(); _preview = null; }
            else _preview.HideSoon();
        }

        // ================== Widgets en el escritorio, calendario y recordatorios (1.1.0) ==================

        private static readonly List<DesktopWidgetWindow> DesktopWidgetWindows = new List<DesktopWidgetWindow>();
        private static DispatcherTimer? _reminderTimer;

        // ================== Personalización (1.3.0) ==================

        private static void StartPersonalization()
        {
            DynamicWallpaper.Apply();
            var s = SettingsStore.Current;
            if (Profiles.Find(s, s.ActiveProfile)?.GameMode == true) GameMode.Set(true);
            GameMode.Changed += () => UpdateWorkAreas(); // el Dock se oculta y deja lugar
            Profiles.ProfileChanged += () => { ThemeManager.Apply(); ReloadDesktopWidgets(); };
        }

        /// <summary>Usar un perfil (desde el menú de McBar, Preferencias o el buscador).</summary>
        public static void UseProfile(string name)
        {
            if (!Profiles.Use(name)) McDialog.Info("No se pudo usar el perfil", $"El perfil «{name}» está vacío o dañado.");
        }

        /// <summary>Al cambiar de perfil o importar ajustes, los widgets del escritorio se vuelven a armar.</summary>
        private static void ReloadDesktopWidgets()
        {
            foreach (var w in DesktopWidgetWindows.ToList()) { try { w.Close(); } catch { } }
            DesktopWidgetWindows.Clear();
            StartDesktopWidgets();
        }

        private static void StartDesktopWidgets()
        {
            foreach (var w in SettingsStore.Current.DesktopWidgets.ToList())
            {
                var win = new DesktopWidgetWindow(w);
                DesktopWidgetWindows.Add(win);
                win.Show();
            }
        }

        /// <summary>Pone un widget nuevo en el escritorio (arriba a la derecha de la pantalla principal, en cascada).</summary>
        public static void AddDesktopWidget(string kind)
        {
            var m = MonitorOrPrimary(null);
            int n = DesktopWidgetWindows.Count;
            var model = new DesktopWidget
            {
                Kind = kind,
                X = m.Work.Right / Scale - 360 - (n / 4) * 330,
                Y = m.Work.Top / Scale + 60 + (n % 4) * 190
            };
            SettingsStore.Current.DesktopWidgets.Add(model);
            SettingsStore.Save();
            var win = new DesktopWidgetWindow(model);
            DesktopWidgetWindows.Add(win);
            win.Show();
        }

        internal static void RemoveDesktopWidget(DesktopWidgetWindow win)
        {
            SettingsStore.Current.DesktopWidgets.Remove(win.Model);
            SettingsStore.Save();
            DesktopWidgetWindows.Remove(win);
            win.Close();
        }

        /// <summary>Cada 30 s: recordatorios vencidos y eventos que empiezan en 10 min → notificación (e isla).</summary>
        private static void StartReminders()
        {
            _ = IcsCalendar.RefreshAsync();
            _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _reminderTimer.Tick += async (_, _) =>
            {
                foreach (var r in Reminders.TakeDue(DateTime.Now))
                    NotificationCenter.Add(new NotificationInfo { App = "Recordatorios", Title = r.Text, Body = r.Due != null ? Reminders.When(r.Due.Value, DateTime.Now) : "" });
                await IcsCalendar.RefreshAsync();
                if (SettingsStore.Current.EventReminders)
                    foreach (var e in IcsCalendar.DueForReminder(10))
                        NotificationCenter.Add(new NotificationInfo
                        {
                            App = "Calendario",
                            Title = e.Title,
                            Body = $"Empieza a las {e.Start:HH:mm}" + (e.Location.Length > 0 ? " · " + e.Location : "")
                        });
            };
            _reminderTimer.Start();
        }

        /// <summary>Quick Look (1.0.0): vista previa del archivo elegido en esa ventana del Explorador.</summary>
        public static void QuickLookFromExplorer(IntPtr explorer)
        {
            var path = QuickLookData.SelectedInExplorer(explorer);
            if (path != null) QuickLookWindow.Toggle(path);
        }

        private static SpotlightWindow? _spotlight;

        /// <summary>Buscador tipo Spotlight (0.6.0): Win+S o la lupa.</summary>
        public static void ToggleSpotlight(MonitorInfo? monitor = null)
        {
            if (_spotlight != null && _spotlight.IsVisible && !_spotlight.IsClosing) { _spotlight.CloseAnimated(); return; }
            if (_spotlight != null && RecentlyClosed(_spotlight.ClosedAt)) return;
            _spotlight = new SpotlightWindow(MonitorOrPrimary(monitor ?? ActiveMonitor()));
            _spotlight.Show();
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

        private static void SafeCleanup(bool keepTaskbarHidden = false)
        {
            if (_cleanedUp) return;
            _cleanedUp = true;
            _watchdog?.Stop();
            HangWatch.Stop();
            _health?.Stop();
            HotCorners.Stop();
            _reminderTimer?.Stop();
            try { LiveStatus.Stop(); } catch { }
            try { DynamicWallpaper.Stop(); } catch { }
            foreach (var w in DesktopWidgetWindows.ToList()) { try { w.Close(); } catch { } }
            foreach (var d in Docks) { try { d.Teardown(); } catch { } }
            foreach (var mb in MenuBars) { try { mb.Teardown(); } catch { } }
            foreach (var isl in Islands) { try { isl.Teardown(); } catch { } }
            try { ActivityHub.Stop(); } catch { }
            try { NotificationCenter.Stop(); } catch { }
            try { KeyboardHook.Stop(); } catch { }
            try { TrayHost.Current?.Dispose(); } catch { }
            try { Hook?.Dispose(); } catch { }
            try { Fullscreen?.Dispose(); } catch { }
            try { Tracker?.Dispose(); } catch { }
            try { WorkArea.Reset(); } catch { }
            if (!keepTaskbarHidden)
            {
                try { if (Taskbar.IsHiddenByUs || SettingsStore.Current.TaskbarModified) Taskbar.Restore(); } catch { }
            }
            try { ShellHook.RestoreAnimation(); } catch { }
        }
    }
}
