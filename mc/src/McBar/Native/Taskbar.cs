// McBar — Creado por Andrés Londero
// Oculta y restaura la barra de tareas de Windows.
// Para que las ventanas maximizadas usen todo el espacio, la barra se pone en "ocultar automáticamente"
// y además se esconde su ventana. El estado original se guarda en los ajustes y se restaura al salir,
// si la app se cierra por un error, o al ejecutar "McBar.exe --restaurar".
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using McBar.Core;

namespace McBar.Native
{
    internal static class Taskbar
    {
        private const uint ABM_GETSTATE = 4, ABM_SETSTATE = 10;
        private const int ABS_AUTOHIDE = 1;

        public static bool IsHiddenByUs { get; private set; }
        public static bool TemporarilyShown { get; private set; }

        private static readonly uint OwnPid = (uint)System.Environment.ProcessId;

        /// <summary>
        /// La barra real del Explorador. McBar también crea una ventana "Shell_TrayWnd" (su bandeja propia),
        /// así que hay que saltear las de nuestro propio proceso.
        /// </summary>
        public static IntPtr ExplorerTray()
        {
            IntPtr after = IntPtr.Zero;
            while (true)
            {
                after = Win32.FindWindowEx(IntPtr.Zero, after, "Shell_TrayWnd", null);
                if (after == IntPtr.Zero) return IntPtr.Zero;
                if (Win32.ProcessOf(after) != OwnPid) return after;
            }
        }

        private static IEnumerable<IntPtr> TrayWindows()
        {
            var main = ExplorerTray();
            if (main != IntPtr.Zero) yield return main;
            IntPtr after = IntPtr.Zero;
            while (true)
            {
                after = Win32.FindWindowEx(IntPtr.Zero, after, "Shell_SecondaryTrayWnd", null);
                if (after == IntPtr.Zero) break;
                if (Win32.ProcessOf(after) != OwnPid) yield return after;
            }
        }

        private static int GetState()
        {
            var abd = new Win32.APPBARDATA { cbSize = Marshal.SizeOf<Win32.APPBARDATA>(), hWnd = ExplorerTray() };
            return (int)Win32.SHAppBarMessage(ABM_GETSTATE, ref abd).ToUInt32();
        }

        private static void SetState(int state)
        {
            var abd = new Win32.APPBARDATA { cbSize = Marshal.SizeOf<Win32.APPBARDATA>(), hWnd = ExplorerTray(), lParam = new IntPtr(state) };
            Win32.SHAppBarMessage(ABM_SETSTATE, ref abd);
        }

        public static void Hide()
        {
            var s = SettingsStore.Current;
            if (!s.TaskbarModified)
            {
                s.OriginalTaskbarState = GetState();
                s.TaskbarModified = true;
                SettingsStore.Save();
            }
            SetState(ABS_AUTOHIDE);
            if ((GetState() & ABS_AUTOHIDE) == 0)
                Log.Write("Windows no aceptó poner su barra en 'ocultar automáticamente' (McBar igual fija el área de trabajo).");
            foreach (var h in TrayWindows()) Win32.ShowWindow(h, Win32.SW_HIDE);
            IsHiddenByUs = true;
            TemporarilyShown = false;
        }

        /// <summary>Vuelve a esconderla si Windows la mostró (por ejemplo al abrir Inicio o reiniciar el Explorador).</summary>
        public static void Enforce()
        {
            if (!IsHiddenByUs || TemporarilyShown) return;
            foreach (var h in TrayWindows())
                if (Win32.IsWindowVisible(h)) Win32.ShowWindow(h, Win32.SW_HIDE);
        }

        public static void Restore()
        {
            var s = SettingsStore.Current;
            if (s.TaskbarModified)
            {
                SetState(s.OriginalTaskbarState);
                s.TaskbarModified = false;
                SettingsStore.Save();
            }
            foreach (var h in TrayWindows()) Win32.ShowWindow(h, Win32.SW_SHOW);
            IsHiddenByUs = false;
            TemporarilyShown = false;
        }

        private static System.Windows.Threading.DispatcherTimer? _burst;
        private static DateTime _burstUntil;

        /// <summary>
        /// Vigilancia rápida (cada 100 ms) durante unos segundos. Se usa después de acciones que hacen que el
        /// Explorador vuelva a mostrar su barra en algún monitor (vaciar la papelera, borrar archivos, etc.)
        /// y cada vez que aparece, para que casi no se llegue a ver.
        /// </summary>
        public static void EnforceBurst(double seconds = 2.5)
        {
            if (!IsHiddenByUs) return;
            Enforce();
            var until = DateTime.Now.AddSeconds(seconds);
            if (until > _burstUntil) _burstUntil = until;
            if (_burst == null)
            {
                _burst = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(100) };
                _burst.Tick += (_, _) =>
                {
                    Enforce();
                    if (DateTime.Now > _burstUntil) _burst!.Stop();
                };
            }
            _burst.Start();
        }

        /// <summary>Muestra la barra de Windows un momento para usar los íconos de la bandeja (Win+B).</summary>
        public static void ShowTemporarily()
        {
            if (!IsHiddenByUs) return;
            TemporarilyShown = true;
            foreach (var h in TrayWindows()) Win32.ShowWindow(h, Win32.SW_SHOWNOACTIVATE);
            WindowOps.SendChord(WindowOps.VK_LWIN, 0x42 /*B*/);
        }

        public static void EndTemporary()
        {
            if (!TemporarilyShown) return;
            TemporarilyShown = false;
            Enforce();
        }

        /// <summary>¿La ventana pertenece a la barra de Windows o a sus menús de bandeja?</summary>
        public static bool IsShellTrayClass(string cls) =>
            cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" || cls == "NotifyIconOverflowWindow" ||
            cls == "TopLevelWindowForOverflowXamlIsland" || cls == "Xaml_WindowedPopupClass";
    }
}
