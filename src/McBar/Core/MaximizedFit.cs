// McBar — Creado por Andrés Londero
// 1.9.6: cuando el Dock se esconde (o vuelve) por una ventana maximizada, cambia el área de trabajo y Windows
// avisa a las ventanas maximizadas para que se reacomoden. Algunas (el Explorador, sobre todo) no se enteran y
// quedan con el hueco abajo hasta que se achican y se vuelven a maximizar. Acá se las acomoda a mano: solo las
// maximizadas de ese monitor cuyo borde todavía está pegado al área de trabajo VIEJA.
// 1.9.8: también las ventanas comunes que quedaron con la barra de título debajo de la barra de arriba.
using System;
using System.Collections.Generic;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Core
{
    internal static class MaximizedFit
    {
        /// <summary>
        /// Nuevo rectángulo para una ventana maximizada que quedó con el área vieja, o null si no hace falta tocarla.
        /// Se mueven solo los bordes de arriba y de abajo (los que cambian por la barra y el Dock). Pura, se prueba.
        /// </summary>
        public static PxRect? Fit(PxRect win, PxRect oldWork, PxRect newWork)
        {
            int top = win.Top, bottom = win.Bottom;
            bool moved = false;
            if (Math.Abs(newWork.Bottom - oldWork.Bottom) >= 20 && Near(oldWork.Bottom - win.Bottom))
            { bottom = win.Bottom + (newWork.Bottom - oldWork.Bottom); moved = true; }
            if (Math.Abs(newWork.Top - oldWork.Top) >= 20 && Near(win.Top - oldWork.Top))
            { top = win.Top + (newWork.Top - oldWork.Top); moved = true; }
            if (!moved || bottom - top < 100) return null;
            return new PxRect(win.Left, top, win.Right, bottom);
        }

        /// <summary>
        /// 1.9.8: ventana común (no maximizada; por ejemplo acoplada a media pantalla) que quedó con la barra de
        /// título debajo de la barra de arriba: se baja su borde de arriba hasta el área de trabajo (si queda
        /// muy chica, se corre entera). null = no hace falta. Pura, se prueba.
        /// </summary>
        public static PxRect? FitTop(PxRect win, PxRect work)
        {
            if (win.Top >= work.Top || win.Bottom <= work.Top) return null;
            if (win.Bottom - work.Top >= 200) return new PxRect(win.Left, work.Top, win.Right, win.Bottom);
            int dy = work.Top - win.Top;
            return new PxRect(win.Left, win.Top + dy, win.Right, win.Bottom + dy);
        }

        // el borde de una maximizada sobresale unos píxeles (el borde invisible de Windows) del área de trabajo
        private static bool Near(int inside) => inside >= -16 && inside <= 2;

        /// <summary>Se llama después de cambiar el área de trabajo, con las áreas de antes de cada monitor.</summary>
        public static void Schedule(Dictionary<string, PxRect> before)
        {
            if (before.Count == 0) return;
            // dos pasadas: primero se deja que Windows haga lo suyo; la segunda agarra a las que tardan
            foreach (var ms in new[] { 300, 1000 })
            {
                var t = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(ms) };
                t.Tick += (_, _) => { t.Stop(); Run(before); };
                t.Start();
            }
        }

        private static void Run(Dictionary<string, PxRect> before)
        {
            try
            {
                var monitors = new Dictionary<string, MonitorInfo>();
                foreach (var m in Monitors.All()) monitors[m.Device] = m;
                uint own = (uint)Environment.ProcessId;
                Win32.EnumWindows((h, _) =>
                {
                    if (!Win32.IsWindowVisible(h) || Win32.IsIconic(h)) return true;
                    if (Win32.ProcessOf(h) == own) return true;
                    var m = Monitors.FromWindow(h);
                    if (m == null || !before.TryGetValue(m.Device, out var old) || !monitors.TryGetValue(m.Device, out var now)) return true;
                    if (!Win32.GetWindowRect(h, out var r)) return true;
                    var rect = new PxRect(r.left, r.top, r.right, r.bottom);
                    PxRect? fit;
                    if (Win32.IsZoomed(h)) fit = Fit(rect, old, now.Work);
                    else
                    {
                        // 1.9.8: solo ventanas de apps con barra de título, y solo si la barra de arriba creció
                        var st = Win32.GetStyle(h);
                        var ex = Win32.GetExStyle(h);
                        bool appWin = (st & Win32.WS_CAPTION) == Win32.WS_CAPTION && (ex & Win32.WS_EX_TOOLWINDOW) == 0;
                        fit = appWin && now.Work.Top > old.Top ? FitTop(rect, now.Work) : null;
                    }
                    if (fit is PxRect f)
                        Win32.SetWindowPos(h, IntPtr.Zero, f.Left, f.Top, f.Width, f.Height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex) { Log.Write("Ajuste de maximizadas: " + ex.Message); }
        }
    }
}
