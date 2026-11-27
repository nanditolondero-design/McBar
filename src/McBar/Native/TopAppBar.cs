// McBar — Creado por Andrés Londero
// 1.9.9: la barra de menú (modo barra) se registra en Windows como "barra de escritorio" (AppBar) arriba de
// todo, igual que la barra de tareas. Así es WINDOWS el que le reserva el lugar: las ventanas maximizadas,
// las acopladas y los íconos del escritorio quedan siempre debajo, y el Explorador no lo vuelve a quitar
// (antes McBar cambiaba el área de trabajo a mano y el Explorador a veces la pisaba y todo quedaba debajo
// de la barra).
using System;
using System.Runtime.InteropServices;
using McBar.Core;

namespace McBar.Native
{
    internal sealed class TopAppBar
    {
        private const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3;
        private const uint ABE_TOP = 1;
        private const int ABN_POSCHANGED = 1;

        private readonly IntPtr _hwnd;
        private readonly uint _callback;
        private bool _registered;
        private Win32.RECT _last;
        private bool _sent;

        public TopAppBar(IntPtr hwnd)
        {
            _hwnd = hwnd;
            _callback = Win32.RegisterWindowMessage("McBarTopAppBar");
        }

        public bool Registered => _registered;

        /// <summary>Registra (si hace falta) y reserva "alto" píxeles arriba del monitor.</summary>
        public void Reserve(PxRect monitor, int heightPx)
        {
            try
            {
                if (!_registered)
                {
                    var n = Data();
                    n.uCallbackMessage = _callback;
                    _registered = Win32.SHAppBarMessage(ABM_NEW, ref n) != UIntPtr.Zero;
                    if (!_registered) { Log.Write("Barra de arriba: Windows no aceptó registrarla como barra de escritorio."); return; }
                }
                var d = Data();
                d.uEdge = ABE_TOP;
                d.rc = new Win32.RECT { left = monitor.Left, top = monitor.Top, right = monitor.Right, bottom = monitor.Top + heightPx };
                Win32.SHAppBarMessage(ABM_QUERYPOS, ref d);
                // si otra barra ya ocupa arriba, Windows la corre: se respeta el alto pedido
                d.rc.bottom = d.rc.top + heightPx;
                // 1.9.56: si ya está reservado el mismo rectángulo no se vuelve a pedir: cada ABM_SETPOS hace que Windows
                // recalcule el área de trabajo (y se pierde el lugar del Dock) y avise a las otras barras, que
                // vuelven a pedir... y las ventanas quedaban tapadas o se sacudían.
                if (_sent && _last.left == d.rc.left && _last.top == d.rc.top && _last.right == d.rc.right && _last.bottom == d.rc.bottom) return;
                Win32.SHAppBarMessage(ABM_SETPOS, ref d);
                _last = d.rc; _sent = true;
            }
            catch (Exception ex) { Log.Write("Barra de arriba (AppBar): " + ex.Message); }
        }

        public void Remove()
        {
            if (!_registered) return;
            try { var d = Data(); Win32.SHAppBarMessage(ABM_REMOVE, ref d); } catch { }
            _registered = false; _sent = false;
        }

        /// <summary>El Explorador se reinició y se olvidó de las barras registradas: hay que volver a registrar.</summary>
        public void Forget() { _registered = false; _sent = false; }

        /// <summary>true si el mensaje es el aviso de Windows de que hay que volver a acomodar la barra.</summary>
        public bool IsReposition(int msg, IntPtr wParam) =>
            _callback != 0 && msg == (int)_callback && wParam.ToInt64() == ABN_POSCHANGED;

        private Win32.APPBARDATA Data() =>
            new Win32.APPBARDATA { cbSize = Marshal.SizeOf<Win32.APPBARDATA>(), hWnd = _hwnd };
    }
}
