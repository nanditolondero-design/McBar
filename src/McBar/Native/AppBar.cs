// McBar — Creado por Andrés Londero
// Registro como "AppBar" de Windows: reserva espacio arriba (barra de menú) o abajo (Dock) para que
// las ventanas maximizadas no queden tapadas, y avisa cuando una app entra en pantalla completa.
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace McBar.Native
{
    internal enum AppBarEdge : uint { Top = 1, Bottom = 3 }

    internal sealed class AppBar : IDisposable
    {
        private const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3;
        private const int ABN_POSCHANGED = 1, ABN_FULLSCREENAPP = 2;

        private readonly Window _window;
        private readonly AppBarEdge _edge;
        private IntPtr _hwnd;
        private uint _callbackMsg;
        private bool _registered;
        private int _thickness, _windowHeight;
        private bool _applying;

        /// <summary>true = una app entró en pantalla completa; false = salió.</summary>
        public event Action<bool>? FullscreenChanged;

        public AppBar(Window window, AppBarEdge edge)
        {
            _window = window;
            _edge = edge;
        }

        /// <summary>Llamar después de SourceInitialized.</summary>
        public void Register()
        {
            if (_registered) return;
            _hwnd = new WindowInteropHelper(_window).Handle;
            if (_callbackMsg == 0)
            {
                _callbackMsg = Win32.RegisterWindowMessage("McBarAppBarMessage");
                HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
            }
            var abd = NewData();
            abd.uCallbackMessage = _callbackMsg;
            Win32.SHAppBarMessage(ABM_NEW, ref abd);
            _registered = true;
        }

        /// <summary>Re-registra (por ejemplo cuando el Explorador de Windows se reinicia).</summary>
        public void ReRegister()
        {
            _registered = false;
            Register();
            if (_thickness > 0) Apply(_thickness, _windowHeight);
        }

        /// <summary>Reserva <paramref name="thicknessPx"/> píxeles y ubica la ventana (alto en píxeles).</summary>
        public void Apply(int thicknessPx, int windowHeightPx)
        {
            EnsureHandle();
            if (_applying) return;
            _applying = true;
            try { ApplyCore(thicknessPx, windowHeightPx); }
            finally { _applying = false; }
        }

        private void ApplyCore(int thicknessPx, int windowHeightPx)
        {
            _thickness = Math.Max(1, thicknessPx);
            _windowHeight = windowHeightPx;
            int sw = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
            int sh = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);

            var abd = NewData();
            abd.uEdge = (uint)_edge;
            abd.rc.left = 0;
            abd.rc.right = sw;
            if (_edge == AppBarEdge.Top) { abd.rc.top = 0; abd.rc.bottom = _thickness; }
            else { abd.rc.bottom = sh; abd.rc.top = sh - _thickness; }

            if (_registered)
            {
                Win32.SHAppBarMessage(ABM_QUERYPOS, ref abd);
                if (_edge == AppBarEdge.Top) abd.rc.bottom = abd.rc.top + _thickness;
                else abd.rc.top = abd.rc.bottom - _thickness;
                Win32.SHAppBarMessage(ABM_SETPOS, ref abd);
            }

            int y = _edge == AppBarEdge.Top ? abd.rc.top : abd.rc.bottom - windowHeightPx;
            Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, abd.rc.left, y, abd.rc.right - abd.rc.left, windowHeightPx, Win32.SWP_NOACTIVATE);
        }

        /// <summary>Ubica la ventana sin reservar espacio (para el Dock con ocultación automática).</summary>
        public void PlaceWithoutReserving(int windowHeightPx)
        {
            EnsureHandle();
            Remove();
            int sw = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
            int sh = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);
            int y = _edge == AppBarEdge.Top ? 0 : sh - windowHeightPx;
            Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, y, sw, windowHeightPx, Win32.SWP_NOACTIVATE);
        }

        public void Remove()
        {
            if (!_registered) return;
            var abd = NewData();
            Win32.SHAppBarMessage(ABM_REMOVE, ref abd);
            _registered = false;
        }

        private void EnsureHandle()
        {
            if (_hwnd == IntPtr.Zero) _hwnd = new WindowInteropHelper(_window).Handle;
        }

        private Win32.APPBARDATA NewData() => new Win32.APPBARDATA { cbSize = Marshal.SizeOf<Win32.APPBARDATA>(), hWnd = _hwnd };

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_callbackMsg != 0 && msg == (int)_callbackMsg)
            {
                switch (wParam.ToInt32())
                {
                    case ABN_POSCHANGED:
                        if (_registered) Apply(_thickness, _windowHeight);
                        break;
                    case ABN_FULLSCREENAPP:
                        FullscreenChanged?.Invoke(lParam != IntPtr.Zero);
                        break;
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose() => Remove();
    }
}
