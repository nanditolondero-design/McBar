// McBar — Creado por Andrés Londero
// Detecta cuando una app ocupa todo un monitor (video, juego, presentación) para esconder la barra
// y el Dock SOLO en ese monitor. Reemplaza el aviso de Windows para AppBars, que a veces avisaba
// "pantalla completa" al minimizar o al tocar el escritorio y dejaba las barras escondidas.
//
// Bajo consumo: solo escucha los cambios de tamaño de la ventana que tiene el foco (se re-engancha
// cada vez que cambia el foco), no los de todas las ventanas.
using System;
using System.Collections.Generic;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Core
{
    internal sealed class FullscreenWatcher : IDisposable
    {
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private readonly Win32.WinEventDelegate _proc;
        private readonly DispatcherTimer _debounce;
        private readonly uint _ownPid = (uint)Environment.ProcessId;
        private IntPtr _hook;
        private IntPtr _watched;

        /// <summary>Monitores (por nombre de dispositivo) que tienen una app en pantalla completa.</summary>
        public HashSet<string> FullscreenMonitors { get; } = new HashSet<string>();

        public event Action? Changed;

        public FullscreenWatcher()
        {
            _proc = OnEvent;
            _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(180) };
            _debounce.Tick += (_, _) => { _debounce.Stop(); Check(); };
        }

        /// <summary>Llamar cuando cambia la ventana con foco.</summary>
        public void OnForeground(IntPtr hwnd)
        {
            if (hwnd != _watched)
            {
                if (_hook != IntPtr.Zero) { Win32.UnhookWinEvent(_hook); _hook = IntPtr.Zero; }
                _watched = hwnd;
                uint thread = Win32.GetWindowThreadProcessId(hwnd, out var pid);
                if (hwnd != IntPtr.Zero && pid != _ownPid && thread != 0)
                    _hook = Win32.SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _proc, pid, thread, Win32.WINEVENT_OUTOFCONTEXT);
            }
            _debounce.Stop();
            _debounce.Start();
        }

        private void OnEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (hwnd != _watched || idObject != 0) return;
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

        public void Dispose()
        {
            if (_hook != IntPtr.Zero) Win32.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
