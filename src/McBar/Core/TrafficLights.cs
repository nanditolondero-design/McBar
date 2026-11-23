// McBar — Creado por Andrés Londero
// 1.8.0 — Botones rojo / amarillo / verde sobre la ventana activa (EXPERIMENTAL, opcional).
// No se puede cambiar de verdad la barra de título de otras apps sin tocar el sistema, así que McBar dibuja
// una ventanita propia con los tres botones arriba a la izquierda de la ventana activa y la sigue (20 veces
// por segundo, solo con la opción prendida). Rojo = cerrar, amarillo = minimizar, verde = maximizar/volver.
// No aparece en: ventanas de McBar, pantalla completa, ventanas sin barra de título, y las apps de la lista
// "No mostrar en" (por ejemplo los navegadores, que tienen pestañas en ese lugar).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using McBar.Native;
using McBar.Panels;

namespace McBar.Core
{
    public static class TrafficLights
    {
        private static DispatcherTimer? _timer;
        private static TrafficLightsWindow? _win;
        private static IntPtr _target;
        private static readonly Dictionary<uint, string> _exeByPid = new Dictionary<uint, string>();

        /// <summary>Lista "chrome, msedge.exe , Firefox" → nombres sin .exe en minúscula. Pura, se prueba.</summary>
        public static HashSet<string> ParseExclude(string? text) =>
            new HashSet<string>((text ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToLowerInvariant())
                .Select(x => x.EndsWith(".exe") ? x.Substring(0, x.Length - 4) : x)
                .Where(x => x.Length > 0));

        public static void Apply()
        {
            if (!SettingsStore.Current.TrafficLights) { Stop(); return; }
            if (_timer == null)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(50) };
                _timer.Tick += (_, _) => Tick();
            }
            _timer.Start();
        }

        public static void Stop()
        {
            _timer?.Stop();
            _win?.Close();
            _win = null;
            _target = IntPtr.Zero;
        }

        private static string ExeName(uint pid)
        {
            if (_exeByPid.TryGetValue(pid, out var n)) return n;
            try { n = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); } catch { n = ""; }
            if (_exeByPid.Count > 300) _exeByPid.Clear();
            _exeByPid[pid] = n;
            return n;
        }

        private static string _excludeText = "\u0000";
        private static HashSet<string> _exclude = new HashSet<string>();

        private static bool Eligible(IntPtr h, out Win32.RECT r)
        {
            r = default;
            if (h == IntPtr.Zero || !Win32.IsWindowVisible(h) || Win32.IsIconic(h)) return false;
            uint pid = Win32.ProcessOf(h);
            if (pid == 0 || pid == (uint)Environment.ProcessId) return false;
            long style = Win32.GetStyle(h);
            if ((style & Win32.WS_CAPTION) != Win32.WS_CAPTION && (style & Win32.WS_THICKFRAME) == 0) return false;
            if ((Win32.GetExStyle(h) & Win32.WS_EX_TOOLWINDOW) != 0) return false;
            if (Win32.GetWindow(h, Win32.GW_OWNER) != IntPtr.Zero) return false; // diálogos
            var cls = WindowOps.GetClass(h);
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return false;
            var s = SettingsStore.Current;
            if (s.TrafficLightsExclude != _excludeText) { _excludeText = s.TrafficLightsExclude; _exclude = ParseExclude(_excludeText); }
            if (_exclude.Contains(ExeName(pid))) return false;
            r = ScreenCapture.VisibleBounds(h);
            if (r.right - r.left < 200 || r.bottom - r.top < 80) return false;
            var m = Monitors.FromWindow(h);
            if (m != null && r.left <= m.Bounds.Left && r.top <= m.Bounds.Top && r.right >= m.Bounds.Right && r.bottom >= m.Bounds.Bottom) return false; // pantalla completa
            return true;
        }

        private static void Tick()
        {
            if (GameMode.On) { _win?.Hide(); return; }
            var fg = Win32.GetForegroundWindow();
            if (_win != null && fg == _win.Handle) return;
            if (!Eligible(fg, out var r)) { _win?.Hide(); _target = IntPtr.Zero; return; }
            _target = fg;
            if (_win == null) { _win = new TrafficLightsWindow(); }
            var m = Monitors.FromWindow(fg);
            double sc = m?.Scale ?? 1;
            _win.Target = fg;
            _win.PlaceAt(r.left + (int)Math.Round(10 * sc), r.top + (int)Math.Round(7 * sc));
        }
    }
}
