// McBar — Creado por Andrés Londero
// 1.8.0 — Organizador visual (Stage Manager de macOS), opcional:
//   - La app que se está usando queda en el centro; al pasar a otra app, las ventanas de la anterior (en ese
//     monitor) se minimizan y la app aparece como una miniatura en la franja de la izquierda.
//   - Tocar una miniatura trae esa app de vuelta (y la que estaba pasa a la franja).
//   - La franja se ve cuando la ventana activa no la tapa; si la tapa, aparece llevando el mouse al borde
//     izquierdo de la pantalla.
// La miniatura es una foto de la ventana tomada justo antes de minimizarla (las ventanas minimizadas no se
// pueden mostrar "en vivo"). Se guardan hasta 6 apps recientes.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using McBar.Native;
using McBar.Panels;

namespace McBar.Core
{
    public static class StageManager
    {
        public const int MaxRecent = 6;

        internal sealed class Stage
        {
            public string Key = "";
            public List<IntPtr> Windows = new List<IntPtr>();
            public BitmapSource? Snapshot;
            public TrackedWindow? Sample;
            public string Monitor = "";
        }

        private static readonly List<Stage> _recent = new List<Stage>();
        internal static IReadOnlyList<Stage> Recent => _recent;
        private static string? _currentKey;
        private static bool _hooked;
        private static DispatcherTimer? _timer;
        private static StageStrip? _strip;

        /// <summary>Pone la clave adelante de la lista (sin repetir) y corta a max. Pura, se prueba.</summary>
        public static List<string> PushRecent(IEnumerable<string> list, string key, int max)
        {
            var l = list.Where(k => k != key).ToList();
            l.Insert(0, key);
            return l.Take(max).ToList();
        }

        public static void Apply()
        {
            if (!SettingsStore.Current.StageManager) { Stop(); return; }
            if (!_hooked)
            {
                _hooked = true;
                App.Tracker.ForegroundChanged += h => { if (SettingsStore.Current.StageManager) OnForeground(h); };
                App.Tracker.WindowsChanged += () => { if (SettingsStore.Current.StageManager) Prune(); };
            }
            if (_timer == null)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
                _timer.Tick += (_, _) => UpdateStrip();
            }
            _timer.Start();
            var fg = Win32.GetForegroundWindow();
            _currentKey = App.Tracker.Windows.FirstOrDefault(w => w.Hwnd == fg)?.PrimaryKey;
        }

        public static void Stop()
        {
            _timer?.Stop();
            _strip?.Close();
            _strip = null;
            _recent.Clear();
            _currentKey = null;
        }

        private static void OnForeground(IntPtr hwnd)
        {
            var tw = App.Tracker.Windows.FirstOrDefault(w => w.Hwnd == hwnd);
            if (tw == null || string.IsNullOrEmpty(tw.PrimaryKey)) return; // McBar, escritorio, diálogos sin app…
            if (tw.PrimaryKey == _currentKey) return;
            var m = Monitors.FromWindow(hwnd);
            if (_currentKey != null && m != null && !GameMode.On)
            {
                var prev = App.Tracker.Windows
                    .Where(w => w.PrimaryKey == _currentKey && Win32.IsWindow(w.Hwnd) && Win32.IsWindowVisible(w.Hwnd) && !Win32.IsIconic(w.Hwnd))
                    .Where(w => Monitors.FromWindow(w.Hwnd)?.Device == m.Device)
                    .OrderByDescending(w => w.LastActivated)
                    .ToList();
                if (prev.Count > 0)
                {
                    var stage = new Stage { Key = _currentKey, Windows = prev.Select(w => w.Hwnd).ToList(), Sample = prev[0], Monitor = m.Device, Snapshot = Thumb(prev[0].Hwnd) };
                    _recent.RemoveAll(s => s.Key == stage.Key);
                    _recent.Insert(0, stage);
                    while (_recent.Count > MaxRecent) _recent.RemoveAt(_recent.Count - 1);
                    foreach (var w in prev) WindowOps.Minimize(w.Hwnd);
                }
            }
            _recent.RemoveAll(s => s.Key == tw.PrimaryKey);
            _currentKey = tw.PrimaryKey;
            _strip?.Refresh();
        }

        /// <summary>Foto chica de la ventana (máx. 360 px de ancho), con copia propia de los píxeles.</summary>
        private static BitmapSource? Thumb(IntPtr hwnd)
        {
            try
            {
                var img = ScreenCapture.WindowImage(hwnd);
                if (img == null) return null;
                double k = Math.Min(1.0, 360.0 / Math.Max(1, img.PixelWidth));
                var t = new TransformedBitmap(img, new ScaleTransform(k, k));
                var copy = new WriteableBitmap(t);
                copy.Freeze();
                return copy;
            }
            catch (Exception ex) { Log.Write("Organizador: foto: " + ex.Message); return null; }
        }

        private static void Prune()
        {
            bool changed = false;
            foreach (var s in _recent.ToList())
            {
                s.Windows.RemoveAll(h => !Win32.IsWindow(h));
                if (s.Windows.Count == 0) { _recent.Remove(s); changed = true; }
            }
            if (changed) _strip?.Refresh();
        }

        /// <summary>1.9.16: cierra todas las ventanas de esa app (la crucita de la miniatura) y la saca de la franja.</summary>
        internal static void Close(Stage s)
        {
            foreach (var h in s.Windows.ToList()) if (Win32.IsWindow(h)) WindowOps.Close(h);
            _recent.Remove(s);
            _strip?.Refresh();
            if (_recent.Count == 0) _strip?.SetVisible(false);
        }

        /// <summary>Trae de vuelta una app de la franja.</summary>
        internal static void Bring(Stage s)
        {
            foreach (var h in s.Windows.AsEnumerable().Reverse())
                if (Win32.IsWindow(h) && Win32.IsIconic(h)) Win32.ShowWindow(h, Win32.SW_RESTORE);
            var first = s.Windows.FirstOrDefault(Win32.IsWindow);
            if (first != IntPtr.Zero) WindowOps.Activate(first);
        }

        private static void UpdateStrip()
        {
            var fg = Win32.GetForegroundWindow();
            MonitorInfo? m = fg != IntPtr.Zero ? Monitors.FromWindow(fg) : null;
            if (_strip != null && _strip.IsMouseOver) return; // mientras se usa, no se mueve
            m ??= App.ActiveMonitor();
            if (m == null) return;
            var items = _recent.Where(s => s.Monitor == m.Device).ToList();
            bool full = App.Fullscreen != null && App.Fullscreen.FullscreenMonitors.Contains(m.Device);
            if (items.Count == 0 || full || GameMode.On) { _strip?.SetVisible(false); return; }
            if (_strip == null || _strip.Monitor.Device != m.Device)
            {
                _strip?.Close();
                _strip = new StageStrip(m);
                _strip.Show();
            }
            double sc = m.Scale;
            int stripRight = m.Work.Left + (int)Math.Round(StageStrip.WidthDip * sc);
            bool covered = false;
            if (fg != IntPtr.Zero && Win32.ProcessOf(fg) != (uint)Environment.ProcessId && !Win32.IsIconic(fg))
            {
                var r = ScreenCapture.VisibleBounds(fg);
                covered = r.left < stripRight && r.right > m.Work.Left && r.bottom > m.Work.Top && r.top < m.Work.Bottom;
            }
            bool edge = Win32.GetCursorPos(out var p) && p.x <= m.Bounds.Left + 1 && p.y >= m.Bounds.Top && p.y < m.Bounds.Bottom;
            _strip.SetVisible(!covered || edge);
        }
    }
}
