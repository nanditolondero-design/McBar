// McBar — Creado por Andrés Londero
// 1.8.0 — Estilo Mac con el mouse (opcional, apagado de fábrica):
//   - Sacudir el mouse para encontrarlo: si se mueve rápido de un lado a otro, aparece un puntero grande
//     un momento (como en macOS).
//   - Clic en el fondo de pantalla: aparta las ventanas y muestra el escritorio; otro clic las devuelve
//     (como en macOS Sonoma). Solo si el clic cae en el fondo, no en un ícono del escritorio.
// Un único gancho de mouse de bajo nivel (WH_MOUSE_LL) en un HILO PROPIO, como KeyboardHook: el gancho solo
// anota y avisa, nunca hace trabajo largo. Solo corre si alguna de las dos opciones está prendida.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Core
{
    /// <summary>Detecta una "sacudida": varios cambios de dirección rápidos y largos en horizontal. Pura, se prueba.</summary>
    public sealed class ShakeDetector
    {
        public int MinTravel { get; init; } = 70;     // px entre cambios de dirección
        public int WindowMs { get; init; } = 900;     // en cuánto tiempo
        public int Reversals { get; init; } = 4;      // cuántos cambios de dirección

        private int _lastX = int.MinValue, _dir, _travel;
        private readonly List<long> _rev = new List<long>();

        public bool Add(long ms, int x)
        {
            if (_lastX == int.MinValue) { _lastX = x; return false; }
            int dx = x - _lastX;
            _lastX = x;
            if (dx == 0) return false;
            int d = Math.Sign(dx);
            if (_dir == 0 || d == _dir) { _dir = d; _travel += Math.Abs(dx); return false; }
            bool big = _travel >= MinTravel;
            _dir = d;
            _travel = Math.Abs(dx);
            if (!big) { _rev.Clear(); return false; }
            _rev.Add(ms);
            _rev.RemoveAll(t => ms - t > WindowMs);
            if (_rev.Count >= Reversals) { _rev.Clear(); return true; }
            return false;
        }
    }

    internal static class MouseHook
    {
        private const int WH_MOUSE_LL = 14, WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202;
        private const uint LLMHF_INJECTED = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT { public Win32.POINT pt; public uint mouseData, flags, time; public IntPtr extra; }
        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, LowLevelProc fn, IntPtr mod, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

        private static LowLevelProc? _proc;
        private static IntPtr _hook;
        private static Dispatcher? _thread, _ui;
        private static readonly ShakeDetector Shake = new ShakeDetector();
        private static long _shakeCooldown;
        private static Win32.POINT _downPt;
        private static long _downAt;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        public static void Apply()
        {
            if (SettingsStore.Current.NeedsMouseHook) Start(); else Stop();
        }

        private static void Start()
        {
            if (_thread != null) return;
            _ui = Dispatcher.CurrentDispatcher;
            var ready = new ManualResetEventSlim();
            var t = new Thread(() =>
            {
                _thread = Dispatcher.CurrentDispatcher;
                try
                {
                    _proc = Proc;
                    _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
                    if (_hook == IntPtr.Zero) Log.Write("MouseHook: no se pudo instalar (" + Marshal.GetLastWin32Error() + ")");
                }
                catch (Exception ex) { Log.Write("MouseHook: " + ex.Message); }
                ready.Set();
                Dispatcher.Run();
            }) { IsBackground = true, Name = "McBar.Mouse" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            ready.Wait(2000);
        }

        public static void Stop()
        {
            var d = _thread;
            if (d == null) return;
            try
            {
                d.Invoke(() => { if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; });
                d.InvokeShutdown();
            }
            catch { }
            _thread = null;
        }

        private static IntPtr Proc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    var m = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if ((m.flags & LLMHF_INJECTED) == 0)
                    {
                        var s = SettingsStore.Current;
                        long now = Clock.ElapsedMilliseconds;
                        int msg = (int)wParam;
                        if (msg == WM_MOUSEMOVE && s.ShakeToFind && now > _shakeCooldown && Shake.Add(now, m.pt.x))
                        {
                            _shakeCooldown = now + 1600;
                            if (!GameMode.On) _ui?.BeginInvoke(new Action(ShakeOverlay.Pop));
                        }
                        else if (msg == WM_LBUTTONDOWN) { _downPt = m.pt; _downAt = now; }
                        else if (msg == WM_LBUTTONUP && s.ClickWallpaperShowsDesktop && !GameMode.On)
                        {
                            var p = m.pt;
                            if (Math.Abs(p.x - _downPt.x) <= 4 && Math.Abs(p.y - _downPt.y) <= 4 && now - _downAt < 500)
                                Task.Run(() => WallpaperClick.Check(p));
                        }
                    }
                }
            }
            catch { }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        internal static void Ui(Action a) => _ui?.BeginInvoke(new Action(() =>
        {
            try { a(); } catch (Exception ex) { Log.Write("Mouse: " + ex.Message); }
        }));
    }

    /// <summary>Clic en el fondo de pantalla → mostrar el escritorio (Win+D); otro clic → volver.</summary>
    internal static class WallpaperClick
    {
        private static long _last;

        /// <summary>¿Esa clase de ventana es parte del escritorio de Windows? Pura, se prueba.</summary>
        public static bool IsDesktopClass(string cls, string rootCls) =>
            (rootCls == "Progman" || rootCls == "WorkerW") &&
            (cls == "SysListView32" || cls == "SHELLDLL_DefView" || cls == "Progman" || cls == "WorkerW");

        public static void Check(Win32.POINT p)
        {
            try
            {
                var h = Win32.WindowFromPoint(p);
                if (h == IntPtr.Zero) return;
                var root = Win32.GetAncestor(h, 2 /*GA_ROOT*/);
                if (!IsDesktopClass(WindowOps.GetClass(h), WindowOps.GetClass(root))) return;
                // ¿cayó en un ícono? (entonces no hace nada, como en macOS)
                if (WindowOps.GetClass(h) == "SysListView32")
                {
                    var el = System.Windows.Automation.AutomationElement.FromPoint(new Point(p.x, p.y));
                    if (el == null) return;
                    var ct = el.Current.ControlType;
                    if (ct != System.Windows.Automation.ControlType.List && ct != System.Windows.Automation.ControlType.Pane) return;
                }
                long now = Environment.TickCount64;
                if (now - _last < 600) return; // doble clic: una sola vez
                _last = now;
                MouseHook.Ui(() => WindowOps.SendChord(WindowOps.VK_LWIN, 0x44 /*D*/));
            }
            catch (Exception ex) { Log.Write("Clic en el fondo: " + ex.Message); }
        }
    }

    /// <summary>Puntero grande un momento, siguiendo al mouse (al sacudirlo).</summary>
    internal sealed class ShakeOverlay : Window
    {
        private static ShakeOverlay? _current;
        private readonly DispatcherTimer _follow;
        private readonly ScaleTransform _scale = new ScaleTransform(0.3, 0.3, 8.4, 8.4);
        private DateTime _until;

        public static void Pop()
        {
            if (_current != null) { _current.Extend(); return; }
            _current = new ShakeOverlay();
            _current.Closed += (_, _) => _current = null;
            _current.Show();
        }

        private ShakeOverlay()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Width = 140; Height = 160;
            // la misma flecha que los cursores de McBar (grilla de 32), grande
            var geo = Geometry.Parse("M2,2 L2,24.5 L7.2,19.6 L10.6,27.4 L13.8,26 L10.5,18.6 L17.4,18.6 Z");
            var arrow = new Path
            {
                Data = geo, Fill = Brushes.Black, Stroke = Brushes.White, StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 2, Opacity = 0.4 },
                RenderTransform = new TransformGroup { Children = { new ScaleTransform(4.2, 4.2), _scale } },
            };
            var c = new Canvas { IsHitTestVisible = false };
            c.Children.Add(arrow);
            Content = c;
            SourceInitialized += (_, _) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                Win32.SetExStyle(h, Win32.GetExStyle(h) | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW | 0x20 /*TRANSPARENT*/);
                Move();
            };
            _until = DateTime.Now.AddMilliseconds(1300);
            _follow = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
            _follow.Tick += (_, _) =>
            {
                Move();
                if (DateTime.Now > _until) Hide2();
            };
            Loaded += (_, _) =>
            {
                _follow.Start();
                var grow = new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut } };
                _scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                _scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            };
        }

        private void Extend() => _until = DateTime.Now.AddMilliseconds(1000);

        private bool _hiding;
        private void Hide2()
        {
            if (_hiding) return;
            _hiding = true;
            var shrink = new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(180));
            shrink.Completed += (_, _) => { _follow.Stop(); Close(); };
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink.Clone());
        }

        private void Move()
        {
            var h = new WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero || !Win32.GetCursorPos(out var p)) return;
            var m = Monitors.FromPoint(p.x, p.y);
            double sc = m?.Scale ?? 1;
            // la punta de la flecha (2,2 en la grilla × 4,2) queda justo en el puntero
            int off = (int)Math.Round(2 * 4.2 * sc);
            Win32.SetWindowPos(h, (IntPtr)(-1) /*TOPMOST*/, p.x - off, p.y - off, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
    }
}
