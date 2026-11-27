// McBar — Creado por Andrés Londero
// Barra de menú superior clásica (una por monitor): ventana con fondo desenfocado que aloja MenuBarContent.
// 1.9.9: se registra como barra de escritorio (AppBar) arriba: Windows le reserva el lugar.
using System;
using System.Windows;
using System.Windows.Interop;
using McBar.Core;
using McBar.Native;

namespace McBar.MenuBar
{
    public partial class MenuBarWindow : Window
    {
        public const double BarHeightDip = 28;

        private readonly MonitorInfo _monitor;
        private readonly MenuBarContent _content;
        private readonly Action _onTheme;
        private IntPtr _hwnd;
        private bool _fullscreen;
        private bool _wantVisible;
        private TopAppBar? _appBar; // 1.9.9

        public MonitorInfo Monitor => _monitor;
        public int ReservePx => _wantVisible ? (int)Math.Ceiling(BarHeightDip * _monitor.Scale) : 0;

        internal MenuBarWindow(MonitorInfo monitor)
        {
            InitializeComponent();
            _monitor = monitor;
            _content = new MenuBarContent(monitor);
            _content.SetTwoRows(false);
            Root.Child = _content;

            // 1.5.0: al pasar a un monitor con otra escala, Windows le cambia el tamaño: se vuelve a acomodar.
            DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(ApplyGeometry), System.Windows.Threading.DispatcherPriority.Loaded);
            SourceInitialized += (_, _) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                WindowOps.MakeToolNoActivate(this);
                ApplyBlur();
                // 1.9.9: Windows reserva el lugar de la barra (ver TopAppBar)
                _appBar = new TopAppBar(_hwnd);
                HwndSource.FromHwnd(_hwnd)?.AddHook((IntPtr h, int msg, IntPtr wp, IntPtr lp, ref bool handled) =>
                {
                    if (_appBar != null && _appBar.IsReposition(msg, wp))
                        Dispatcher.BeginInvoke(new Action(ApplyGeometry), System.Windows.Threading.DispatcherPriority.Background);
                    return IntPtr.Zero;
                });
                if (_wantVisible) ApplyGeometry();
            };
            _onTheme = ApplyBlur;
            ThemeManager.ThemeChanged += _onTheme;
        }

        public void SetVisible(bool visible)
        {
            _wantVisible = visible;
            if (visible)
            {
                if (!_fullscreen) Show();
                ApplyGeometry();
            }
            else { _appBar?.Remove(); Hide(); }
        }

        public void ApplyGeometry()
        {
            if (_hwnd == IntPtr.Zero || !_wantVisible) return;
            var b = _monitor.Bounds;
            int h = (int)Math.Ceiling(BarHeightDip * _monitor.Scale);
            _appBar?.Reserve(b, h);
            Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, b.Left, b.Top, b.Width, h, Win32.SWP_NOACTIVATE);
        }

        public void SetFullscreen(bool fullscreen)
        {
            _fullscreen = fullscreen;
            if (!_wantVisible) return;
            if (fullscreen) Hide(); else { Show(); ApplyGeometry(); }
        }

        /// <summary>1.9.9: el Explorador se reinició: volver a registrar la barra para que reserve el lugar.</summary>
        public void OnShellRestarted()
        {
            _appBar?.Forget();
            ApplyGeometry();
        }

        public void Teardown()
        {
            ThemeManager.ThemeChanged -= _onTheme;
            _appBar?.Remove(); // 1.9.9: devolver el lugar
            _content.Teardown();
        }

        public PxRect? TrayIconRect(TrayIcon ic) => _content.TrayIconRect(ic);

        private void ApplyBlur()
        {
            if (_hwnd == IntPtr.Zero) return;
            // 1.9.56: con el tema Vidrio el tinte blanco del Dock dejaba la barra casi blanca (y el texto blanco no se leía)
            var tint = ThemeManager.IsGlass ? System.Windows.Media.Color.FromArgb(0x66, 0x1E, 0x1E, 0x26) : ThemeManager.GetColor("Mc.BlurTint");
            WindowOps.EnableBlur(this, tint);
        }
    }
}
