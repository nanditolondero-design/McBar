// McBar — Creado por Andrés Londero
// Punto de entrada. Arranca el seguimiento de ventanas, oculta la barra de Windows (si está activado),
// crea la barra de menú y el Dock, y coordina los paneles. Garantiza que la barra de tareas de Windows
// se restaure al salir, al cerrar sesión o si McBar se cierra por un error.
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
        private static bool _cleanedUp;

        public static WindowTracker Tracker { get; private set; } = null!;
        public static DockWindow? Dock { get; private set; }
        public static MenuBarWindow? MenuBarWin { get; private set; }

        private static LaunchpadWindow? _launchpad;
        private static ControlCenterWindow? _controlCenter;
        private static WidgetsWindow? _widgets;
        private static SettingsWindow? _settings;
        private static bool _lastHideTaskbar, _lastShowMenuBar, _lastStartWithWindows;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            SettingsStore.Load();

            // "McBar.exe --restaurar": devuelve la barra de Windows a como estaba y sale.
            if (e.Args.Any(a => a.Equals("--restaurar", StringComparison.OrdinalIgnoreCase)))
            {
                Taskbar.Restore();
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

            var s = SettingsStore.Current;
            if (!s.FirstRunDone)
            {
                s.Pinned = DefaultPins();
                s.FirstRunDone = true;
                SettingsStore.Save();
            }

            ThemeManager.Start();
            Tracker = new WindowTracker();
            Tracker.ShellWindowShown += _ => Taskbar.Enforce();
            Tracker.ForegroundChanged += hwnd =>
            {
                if (Taskbar.TemporarilyShown && !Taskbar.IsShellTrayClass(WindowOps.GetClass(hwnd))) Taskbar.EndTemporary();
            };
            Tracker.Start();

            if (s.HideWindowsTaskbar) Taskbar.Hide();
            else if (s.TaskbarModified) Taskbar.Restore(); // quedó modificada de una sesión anterior
            if (s.StartWithWindows) SystemInfo.SetStartWithWindows(true);

            MenuBarWin = new MenuBarWindow();
            MenuBarWin.SetVisible(s.ShowMenuBar);
            Dock = new DockWindow();
            Dock.Show();

            _lastHideTaskbar = s.HideWindowsTaskbar;
            _lastShowMenuBar = s.ShowMenuBar;
            _lastStartWithWindows = s.StartWithWindows;
            SettingsStore.Changed += OnSettingsChanged;

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

        private static void OnSettingsChanged()
        {
            var s = SettingsStore.Current;
            if (s.HideWindowsTaskbar != _lastHideTaskbar)
            {
                if (s.HideWindowsTaskbar) Taskbar.Hide(); else Taskbar.Restore();
                _lastHideTaskbar = s.HideWindowsTaskbar;
                Dock?.ApplyGeometry();
            }
            if (s.ShowMenuBar != _lastShowMenuBar)
            {
                MenuBarWin?.SetVisible(s.ShowMenuBar);
                _lastShowMenuBar = s.ShowMenuBar;
            }
            if (s.StartWithWindows != _lastStartWithWindows)
            {
                SystemInfo.SetStartWithWindows(s.StartWithWindows);
                _lastStartWithWindows = s.StartWithWindows;
            }
        }

        // ================== Coordinación ==================

        /// <summary>Una app entró o salió de pantalla completa (video, juego, presentación).</summary>
        public static void OnFullscreenChanged(bool fullscreen)
        {
            if (fullscreen)
            {
                // Windows a veces avisa "pantalla completa" cuando se hace clic en el escritorio: se ignora.
                var fg = Win32.GetForegroundWindow();
                var cls = WindowOps.GetClass(fg);
                if (fg == IntPtr.Zero || cls == "Progman" || cls == "WorkerW" || Taskbar.IsShellTrayClass(cls)) return;
            }
            Dock?.SetFullscreen(fullscreen);
            MenuBarWin?.SetFullscreen(fullscreen);
        }

        /// <summary>El Explorador de Windows se reinició: volver a esconder su barra y re-registrar las nuestras.</summary>
        public static void OnExplorerRestarted()
        {
            if (SettingsStore.Current.HideWindowsTaskbar) Taskbar.Hide();
            MenuBarWin?.ReRegisterAppBar();
            Dock?.ReRegisterAppBar();
        }

        private static bool RecentlyClosed(DateTime closedAt) => (DateTime.Now - closedAt).TotalMilliseconds < 350;

        public static void ToggleLaunchpad()
        {
            if (_launchpad != null && _launchpad.IsVisible) { _launchpad.CloseAnimated(); return; }
            if (_launchpad != null && RecentlyClosed(_launchpad.ClosedAt)) return;
            _launchpad = new LaunchpadWindow();
            _launchpad.ShowAnimated();
        }

        public static void ToggleControlCenter()
        {
            if (_controlCenter != null && _controlCenter.IsVisible && !_controlCenter.IsClosing) { _controlCenter.CloseAnimated(); return; }
            if (_controlCenter != null && RecentlyClosed(_controlCenter.ClosedAt)) return;
            _widgets?.CloseAnimated();
            _controlCenter = new ControlCenterWindow();
            _controlCenter.ShowBelowMenuBar();
        }

        public static void ToggleWidgets()
        {
            if (_widgets != null && _widgets.IsVisible && !_widgets.IsClosing) { _widgets.CloseAnimated(); return; }
            if (_widgets != null && RecentlyClosed(_widgets.ClosedAt)) return;
            _controlCenter?.CloseAnimated();
            _widgets = new WidgetsWindow();
            double top = SettingsStore.Current.ShowMenuBar ? MenuBarWindow.BarHeightDip - 4 : 0;
            double height = Math.Max(300, SystemParameters.WorkArea.Bottom - top + 6);
            _widgets.ShowBelowMenuBar(height);
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
            try { Dock?.Teardown(); } catch { }
            try { MenuBarWin?.Teardown(); } catch { }
            try { Tracker?.Dispose(); } catch { }
            try { if (Taskbar.IsHiddenByUs || SettingsStore.Current.TaskbarModified) Taskbar.Restore(); } catch { }
        }
    }
}
