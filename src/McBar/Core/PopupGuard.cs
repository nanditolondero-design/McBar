// McBar — Creado por Andrés Londero
// Cierra los menús y ventanitas emergentes de McBar cuando se hace clic en otra app.
// Hace falta porque el Dock y la barra no toman el foco (para no quitárselo a la app activa) y en ese
// caso WPF a veces no se entera del clic afuera: el menú quedaba "pegado".
// Solo revisa mientras hay un menú abierto (cada 100 ms); en reposo no hace nada.
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Core
{
    internal static class PopupGuard
    {
        private static readonly List<Action> OpenClosers = new List<Action>();
        private static readonly uint OwnPid = (uint)Environment.ProcessId;
        private static DispatcherTimer? _timer;
        private static bool _wasDown;

        public static void Attach(ContextMenu cm)
        {
            Action close = () => cm.IsOpen = false;
            cm.Opened += (_, _) => { Add(close); RaiseSoon(cm); };
            cm.Closed += (_, _) => Remove(close);
        }

        public static void Attach(Popup p)
        {
            Action close = () => p.IsOpen = false;
            p.Opened += (_, _) => { Add(close); if (p.Child != null) RaiseSoon(p.Child); };
            p.Closed += (_, _) => Remove(close);
        }

        /// <summary>Otra app tomó el foco: cerrar todo.</summary>
        public static void OnForeground(IntPtr hwnd)
        {
            if (OpenClosers.Count == 0 || hwnd == IntPtr.Zero) return;
            if (Win32.ProcessOf(hwnd) != OwnPid) CloseAll();
        }

        public static bool AnyOpen => OpenClosers.Count > 0;

        /// <summary>
        /// 1.3.1: pone el menú por ENCIMA de la isla y la barra (que también están siempre arriba): si no,
        /// la píldora expandida tapaba el menú que se abría desde ella.
        /// </summary>
        private static void RaiseSoon(System.Windows.Media.Visual v)
        {
            void Raise()
            {
                if (System.Windows.PresentationSource.FromVisual(v) is System.Windows.Interop.HwndSource src && src.Handle != IntPtr.Zero)
                    Win32.SetWindowPos(src.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            }
            Raise();
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(Raise), DispatcherPriority.Loaded);
        }

        private static void Add(Action close)
        {
            OpenClosers.Add(close);
            _wasDown = Win32.IsMouseButtonDown();
            if (_timer == null)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(100) };
                _timer.Tick += (_, _) => Tick();
            }
            _timer.Start();
        }

        private static void Remove(Action close)
        {
            OpenClosers.Remove(close);
            if (OpenClosers.Count == 0) _timer?.Stop();
        }

        private static void Tick()
        {
            bool down = Win32.IsMouseButtonDown();
            if (down && !_wasDown && Win32.GetCursorPos(out var p))
            {
                var under = Win32.WindowFromPoint(p);
                if (under == IntPtr.Zero || Win32.ProcessOf(under) != OwnPid) CloseAll();
            }
            _wasDown = down;
        }

        public static void CloseAll()
        {
            foreach (var c in OpenClosers.ToArray()) { try { c(); } catch { } }
            OpenClosers.Clear();
            _timer?.Stop();
        }
    }
}
