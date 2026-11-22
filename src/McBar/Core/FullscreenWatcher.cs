// McBar — Creado por Andrés Londero
// Detecta cuando una app ocupa todo un monitor (video, juego, presentación) para esconder la barra
// y el Dock SOLO en ese monitor. Reemplaza el aviso de Windows para AppBars, que a veces avisaba
// "pantalla completa" al minimizar o al tocar el escritorio y dejaba las barras escondidas.
//
// Se revisa al cambiar el foco y una vez por segundo desde el vigilante de App (una consulta mínima).
// 0.3.1: antes escuchaba cada movimiento de la ventana con foco (incluido el mouse sobre ella) y eso
// despertaba a McBar cientos de veces por segundo. Además las ventanas MAXIMIZADAS ya no cuentan como
// pantalla completa (eso escondía la barra).
using System;
using System.Collections.Generic;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Core
{
    internal sealed class FullscreenWatcher : IDisposable
    {
        private readonly DispatcherTimer _debounce;
        private readonly uint _ownPid = (uint)Environment.ProcessId;

        /// <summary>Monitores (por nombre de dispositivo) que tienen una app en pantalla completa.</summary>
        public HashSet<string> FullscreenMonitors { get; } = new HashSet<string>();

        public event Action? Changed;

        public FullscreenWatcher()
        {
            _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(180) };
            _debounce.Tick += (_, _) => { _debounce.Stop(); Check(); };
        }

        /// <summary>Llamar cuando cambia la ventana con foco.</summary>
        public void OnForeground(IntPtr hwnd)
        {
            _debounce.Stop();
            _debounce.Start();
        }

        public void Check()
        {
            var fg = Win32.GetForegroundWindow();
            var now = new HashSet<string>();
            if (IsFullscreen(fg, out var device)) now.Add(device);
            if (!now.SetEquals(FullscreenMonitors))
            {
                FullscreenMonitors.Clear();
                foreach (var d in now) FullscreenMonitors.Add(d);
                Changed?.Invoke();
            }
        }

        private bool IsFullscreen(IntPtr hwnd, out string device)
        {
            device = "";
            if (hwnd == IntPtr.Zero || !Win32.IsWindowVisible(hwnd) || Win32.IsIconic(hwnd)) return false;
            if (Win32.ProcessOf(hwnd) == _ownPid) return false;
            if (Win32.IsZoomed(hwnd)) return false; // maximizada no es pantalla completa
            var cls = WindowOps.GetClass(hwnd);
            if (cls == "Progman" || cls == "WorkerW" || Taskbar.IsShellTrayClass(cls)) return false;
            if (!Win32.GetWindowRect(hwnd, out var r)) return false;
            var m = Monitors.FromWindow(hwnd);
            if (m == null) return false;
            device = m.Device;
            return IsCovering(new PxRect(r.left, r.top, r.right, r.bottom), m.Bounds);
        }

        /// <summary>¿La ventana cubre todo el monitor? (pura, se prueba)</summary>
        public static bool IsCovering(PxRect window, PxRect monitor) =>
            window.Left <= monitor.Left && window.Top <= monitor.Top && window.Right >= monitor.Right && window.Bottom >= monitor.Bottom;

        public void Dispose() => _debounce.Stop();
    }
}
