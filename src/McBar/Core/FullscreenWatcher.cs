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
        /// <summary>1.6.1: monitores cuya ventana de arriba está MAXIMIZADA (para esconder la isla, si se eligió).</summary>
        public HashSet<string> MaximizedMonitors { get; } = new HashSet<string>();

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
            var max = new HashSet<string>();
            Win32.EnumWindows((h, _) =>
            {
                if (decided.Count >= monitors.Count) return false; // ya están todos resueltos
                if (!IsCandidate(h)) return true;
                var m = Monitors.FromWindow(h);
                if (m == null || decided.Contains(m.Device)) return true;
                if (!Win32.GetWindowRect(h, out var r)) return true;
                var rect = new PxRect(r.left, r.top, r.right, r.bottom);
                // 0.4.3: una ventanita "siempre visible" (aviso, superposición de un programa, reproductor
                // mini) que está por encima de un video NO decide: se sigue mirando la de abajo.
                if ((Win32.GetExStyle(h) & Win32.WS_EX_TOPMOST) != 0 && !IsLarge(rect, m.Bounds)) return true;
                decided.Add(m.Device);
                bool zoomed = Win32.IsZoomed(h);
                if (IsCovering(rect, m.Bounds) && IsFullscreenStyle(zoomed, Win32.GetStyle(h)))
                    now.Add(m.Device);
                else if (zoomed) max.Add(m.Device); // 1.6.1
                return true;
            }, IntPtr.Zero);

            if (!now.SetEquals(FullscreenMonitors) || !max.SetEquals(MaximizedMonitors))
            {
                FullscreenMonitors.Clear();
                foreach (var d in now) FullscreenMonitors.Add(d);
                MaximizedMonitors.Clear();
                foreach (var d in max) MaximizedMonitors.Add(d);
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

        /// <summary>
        /// Una ventana maximizada común (con barra de título) no es pantalla completa. Pero Chrome, Edge y
        /// otros siguen marcados como "maximizados" cuando ponen un video en pantalla completa: ahí ya no tienen
        /// barra de título ni borde. (pura, se prueba)
        /// </summary>
        public static bool IsFullscreenStyle(bool zoomed, long style) =>
            !zoomed || (style & Win32.WS_CAPTION) != Win32.WS_CAPTION || (style & Win32.WS_THICKFRAME) == 0;

        /// <summary>¿Ocupa más de la mitad del monitor? (pura, se prueba)</summary>
        public static bool IsLarge(PxRect w, PxRect m)
        {
            long ww = Math.Max(0, Math.Min(w.Right, m.Right) - Math.Max(w.Left, m.Left));
            long hh = Math.Max(0, Math.Min(w.Bottom, m.Bottom) - Math.Max(w.Top, m.Top));
            return ww * hh * 2 >= (long)m.Width * m.Height;
        }

        /// <summary>¿La ventana cubre todo el monitor? (pura, se prueba)</summary>
        public static bool IsCovering(PxRect window, PxRect monitor) =>
            window.Left <= monitor.Left && window.Top <= monitor.Top && window.Right >= monitor.Right && window.Bottom >= monitor.Bottom;

        public void Dispose() => _debounce.Stop();
    }
}
