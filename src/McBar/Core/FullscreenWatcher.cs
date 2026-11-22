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

        /// <summary>
        /// Revisa TODOS los monitores (no solo el de la ventana con foco): en cada monitor mira la ventana
        /// "de verdad" que está más arriba; si lo cubre entero y no está maximizada, ese monitor está en
        /// pantalla completa. Así un video en el monitor 2 sigue sin barras aunque trabajes en el monitor 1.
        /// </summary>
        public void Check()
        {
            var monitors = Monitors.All();
            var decided = new HashSet<string>();
            var now = new HashSet<string>();
            Win32.EnumWindows((h, _) =>
            {
                if (decided.Count >= monitors.Count) return false; // ya están todos resueltos
                if (!IsCandidate(h)) return true;
                var m = Monitors.FromWindow(h);
                if (m == null || decided.Contains(m.Device)) return true;
                decided.Add(m.Device);
                if (!Win32.IsZoomed(h) && Win32.GetWindowRect(h, out var r) &&
                    IsCovering(new PxRect(r.left, r.top, r.right, r.bottom), m.Bounds))
                    now.Add(m.Device);
                return true;
            }, IntPtr.Zero);

            if (!now.SetEquals(FullscreenMonitors))
            {
                FullscreenMonitors.Clear();
                foreach (var d in now) FullscreenMonitors.Add(d);
                Changed?.Invoke();
            }
        }

        /// <summary>Ventanas que cuentan: visibles, de apps de verdad (no superposiciones transparentes, ni McBar, ni el escritorio).</summary>
        private bool IsCandidate(IntPtr h)
        {
            if (!Win32.IsWindowVisible(h) || Win32.IsIconic(h)) return false;
            var ex = Win32.GetExStyle(h);
            if ((ex & Win32.WS_EX_TOOLWINDOW) != 0 || (ex & Win32.WS_EX_NOACTIVATE) != 0 || (ex & 0x20 /*WS_EX_TRANSPARENT*/) != 0) return false;
            if (Win32.DwmGetWindowAttribute(h, Win32.DWMWA_CLOAKED, out int cloaked, 4) == 0 && cloaked != 0) return false;
            if (Win32.ProcessOf(h) == _ownPid) return false;
            var cls = WindowOps.GetClass(h);
            if (cls == "Progman" || cls == "WorkerW" || Taskbar.IsShellTrayClass(cls) || cls == "Shell_TrayWnd") return false;
            return true;
        }

        /// <summary>¿La ventana cubre todo el monitor? (pura, se prueba)</summary>
        public static bool IsCovering(PxRect window, PxRect monitor) =>
            window.Left <= monitor.Left && window.Top <= monitor.Top && window.Right >= monitor.Right && window.Bottom >= monitor.Bottom;

        public void Dispose() => _debounce.Stop();
    }
}
