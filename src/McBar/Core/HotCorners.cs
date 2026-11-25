// McBar — Creado por Andrés Londero
// Esquinas activas (1.0.0), como en macOS: llevar el mouse a una esquina de la pantalla y dejarlo un
// instante dispara una acción (Launchpad, todas las ventanas, escritorio, buscador, notificaciones,
// bloquear). Se configura en Preferencias → Esquinas. Mira la posición del mouse 10 veces por segundo
// (casi nada de procesador) y solo si hay alguna esquina en uso. No actúa con un juego o video en
// pantalla completa.
using System;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Core
{
    public static class HotCorners
    {
        public static readonly (string key, string name)[] Actions =
        {
            ("none", "Nada"), ("launchpad", "Launchpad"), ("mission", "Todas las ventanas"),
            ("desktop", "Escritorio"), ("spotlight", "Buscador"), ("notifications", "Notificaciones"),
            ("control", "Centro de control"), ("lock", "Bloquear pantalla"), ("screensaver", "Protector de pantalla")
        };

        private static DispatcherTimer? _timer;
        private static int _corner = -1;
        private static DateTime _since;
        private static bool _fired;

        /// <summary>Esquina en la que está el punto: 0 arriba-izq, 1 arriba-der, 2 abajo-izq, 3 abajo-der, -1 ninguna. Pura.</summary>
        public static int CornerAt(int x, int y, PxRect b, int size = 2)
        {
            bool l = x <= b.Left + size, r = x >= b.Right - 1 - size, t = y <= b.Top + size, bo = y >= b.Bottom - 1 - size;
            if (t && l) return 0;
            if (t && r) return 1;
            if (bo && l) return 2;
            if (bo && r) return 3;
            return -1;
        }

        public static string ActionFor(AppSettings s, int corner) => corner switch
        {
            0 => s.CornerTopLeft, 1 => s.CornerTopRight, 2 => s.CornerBottomLeft, 3 => s.CornerBottomRight, _ => "none"
        };

        public static void Apply()
        {
            var s = SettingsStore.Current;
            bool any = ActionFor(s, 0) != "none" || ActionFor(s, 1) != "none" || ActionFor(s, 2) != "none" || ActionFor(s, 3) != "none";
            if (!any) { _timer?.Stop(); return; }
            if (_timer == null)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
                _timer.Tick += (_, _) => Tick();
            }
            _timer.Start();
        }

        public static void Stop() => _timer?.Stop();

        private static void Tick()
        {
            if (!Win32.GetCursorPos(out var p)) return;
            var m = Monitors.FromPoint(p.x, p.y);
            int c = m == null ? -1 : CornerAt(p.x, p.y, m.Bounds);
            if (c != _corner) { _corner = c; _since = DateTime.Now; _fired = false; return; }
            if (c < 0 || _fired || (DateTime.Now - _since).TotalMilliseconds < 250) return;
            if (GameMode.On) return; // 1.3.0: en modo juego las esquinas no hacen nada
            if (m != null && App.Fullscreen != null && App.Fullscreen.FullscreenMonitors.Contains(m.Device)) return;
            // no si se está apretando un botón (por ejemplo arrastrando algo a la esquina)
            if ((Win32.GetAsyncKeyState(0x01) & 0x8000) != 0) return;
            _fired = true;
            Run(ActionFor(SettingsStore.Current, c), m);
        }

        public static void Run(string action, MonitorInfo? m)
        {
            try
            {
                switch (action)
                {
                    case "launchpad": App.ToggleLaunchpad(m); break;
                    case "mission": App.ToggleMissionControl(m); break;
                    case "desktop": WindowOps.SendChord(WindowOps.VK_LWIN, 0x44 /*D*/); break;
                    case "spotlight": App.ToggleSpotlight(m); break;
                    case "notifications": App.ToggleWidgets(m); break;
                    case "control": App.ToggleControlCenter(m); break;
                    case "lock": Launcher.Lock(); break;
                    case "screensaver": McBar.Panels.ScreenSaverWindow.Show(); break;
                }
            }
            catch (Exception ex) { Log.Write("Esquina activa: " + ex.Message); }
        }
    }
}
