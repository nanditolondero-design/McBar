// McBar — Creado por Andrés Londero
// Animación al minimizar y restaurar: Windows le pregunta al "shell" hacia dónde tiene que achicar la
// ventana (mensaje HSHELL_GETMINRECT). McBar contesta con la posición del ícono de esa app en el Dock,
// así la ventana se va volando al Dock y vuelve desde ahí. También se asegura de que la opción de
// Windows "Animar las ventanas al minimizar y maximizar" esté activada (y la deja como estaba al salir).
using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using McBar.Core;

namespace McBar.Native
{
    internal sealed class ShellHook : IDisposable
    {
        private const int HSHELL_GETMINRECT = 5;
        private readonly HwndSource _source;
        private readonly uint _shellMsg;

        /// <summary>Devuelve el rectángulo (en píxeles) del ícono de la app de esa ventana, o null.</summary>
        public Func<IntPtr, PxRect?>? MinRectProvider { get; set; }

        public ShellHook()
        {
            var p = new HwndSourceParameters("McBarShellHook") { Width = 0, Height = 0, WindowStyle = 0 };
            _source = new HwndSource(p);
            _source.AddHook(WndProc);
            _shellMsg = Win32.RegisterWindowMessage("SHELLHOOK");
            Win32.RegisterShellHookWindow(_source.Handle);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)_shellMsg && (wParam.ToInt64() & 0x7FFF) == HSHELL_GETMINRECT && lParam != IntPtr.Zero && MinRectProvider != null)
            {
                try
                {
                    // SHELLHOOKINFO { HWND hwnd; RECT rc; }
                    var target = Marshal.ReadIntPtr(lParam);
                    var r = MinRectProvider(target);
                    if (r != null)
                    {
                        int off = IntPtr.Size;
                        Marshal.WriteInt32(lParam, off, r.Value.Left);
                        Marshal.WriteInt32(lParam, off + 4, r.Value.Top);
                        Marshal.WriteInt32(lParam, off + 8, r.Value.Right);
                        Marshal.WriteInt32(lParam, off + 12, r.Value.Bottom);
                        handled = true;
                        return new IntPtr(1);
                    }
                }
                catch (Exception ex) { Log.Write("ShellHook: " + ex.Message); }
            }
            return IntPtr.Zero;
        }

        // ---------- Opción de Windows "animar ventanas" ----------

        public static bool GetWindowAnimation()
        {
            var ai = new Win32.ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<Win32.ANIMATIONINFO>() };
            return Win32.SystemParametersInfoAnim(Win32.SPI_GETANIMATION, ai.cbSize, ref ai, 0) && ai.iMinAnimate != 0;
        }

        public static void SetWindowAnimation(bool on)
        {
            var ai = new Win32.ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<Win32.ANIMATIONINFO>(), iMinAnimate = on ? 1 : 0 };
            Win32.SystemParametersInfoAnim(Win32.SPI_SETANIMATION, ai.cbSize, ref ai, Win32.SPIF_SENDCHANGE | Win32.SPIF_UPDATEINIFILE);
        }

        /// <summary>Activa la animación de Windows guardando cómo estaba.</summary>
        public static void EnsureAnimationOn()
        {
            var s = SettingsStore.Current;
            bool current = GetWindowAnimation();
            if (s.OriginalMinAnimate == null) { s.OriginalMinAnimate = current ? 1 : 0; SettingsStore.Save(); }
            if (!current) SetWindowAnimation(true);
        }

        /// <summary>Deja la opción de Windows como estaba antes de McBar.</summary>
        public static void RestoreAnimation()
        {
            var s = SettingsStore.Current;
            if (s.OriginalMinAnimate == null) return;
            SetWindowAnimation(s.OriginalMinAnimate.Value != 0);
            s.OriginalMinAnimate = null;
            SettingsStore.Save();
        }

        public void Dispose()
        {
            try { Win32.DeregisterShellHookWindow(_source.Handle); } catch { }
            _source.Dispose();
        }
    }
}
