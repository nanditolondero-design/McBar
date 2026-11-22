// McBar — Creado por Andrés Londero
// Barra de menú superior clásica (una por monitor): ventana con fondo desenfocado que aloja MenuBarContent.
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

        public MonitorInfo Monitor => _monitor;
        public int ReservePx => _wantVisible ? (int)Math.Ceiling(BarHeightDip * App.Scale) : 0;

        internal MenuBarWindow(MonitorInfo monitor)
        {
            InitializeComponent();
            _monitor = monitor;
            _content = new MenuBarContent(monitor);
            _content.SetTwoRows(false);
            Root.Child = _content;

            SourceInitialized += (_, _) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                WindowOps.MakeToolNoActivate(this);
                ApplyBlur();
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
            else Hide();
        }

        public void ApplyGeometry()
        {
            if (_hwnd == IntPtr.Zero || !_wantVisible) return;
            var b = _monitor.Bounds;
            int h = (int)Math.Ceiling(BarHeightDip * App.Scale);
            Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, b.Left, b.Top, b.Width, h, Win32.SWP_NOACTIVATE);
        }

        public void SetFullscreen(bool fullscreen)
        {
            _fullscreen = fullscreen;
            if (!_wantVisible) return;
            if (fullscreen) Hide(); else { Show(); ApplyGeometry(); }
        }

        public void Teardown()
        {
            ThemeManager.ThemeChanged -= _onTheme;
            _content.Teardown();
        }

        public PxRect? TrayIconRect(TrayIcon ic) => _content.TrayIconRect(ic);

        private void ApplyBlur()
        {
            if (_hwnd == IntPtr.Zero) return;
            WindowOps.EnableBlur(this, ThemeManager.GetColor("Mc.BlurTint"));
        }
    }
}
