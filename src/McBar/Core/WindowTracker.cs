// McBar — Creado por Andrés Londero
// Sigue las ventanas abiertas (las mismas que muestra Alt+Tab) SIN sondear en un bucle:
// Windows avisa por "WinEvents" cuando una ventana aparece, se oculta o se destruye, y recién ahí
// se vuelve a listar (agrupando varios avisos seguidos en una sola actualización).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Core
{
    public sealed class TrackedWindow
    {
        public IntPtr Hwnd { get; init; }
        public uint ProcessId { get; init; }
        public string ExePath { get; init; } = "";
        public string ClassName { get; init; } = "";
        /// <summary>Clave principal para agrupar (AUMID si hay, si no la ruta del exe).</summary>
        public string PrimaryKey { get; init; } = "";
        public HashSet<string> Keys { get; init; } = new HashSet<string>();
        /// <summary>Para íconos: ruta del exe o "shell:AppsFolder\&lt;aumid&gt;".</summary>
        public string IconSource { get; init; } = "";
        public long LastActivated { get; set; }
    }

    public sealed class WindowTracker : IDisposable
    {
        private readonly Dictionary<IntPtr, TrackedWindow> _cache = new Dictionary<IntPtr, TrackedWindow>();
        private readonly Dictionary<uint, string> _exeByPid = new Dictionary<uint, string>();
        private readonly List<IntPtr> _hooks = new List<IntPtr>();
        private readonly Win32.WinEventDelegate _hookProc; // referencia viva para que el GC no la libere
        private readonly DispatcherTimer _debounce;
        private readonly uint _ownPid = (uint)Environment.ProcessId;
        private long _activationCounter;

        public IReadOnlyList<TrackedWindow> Windows { get; private set; } = Array.Empty<TrackedWindow>();

        /// <summary>Última ventana de otra app que tuvo el foco (para mandarle los atajos del menú).</summary>
        public IntPtr LastExternalForeground { get; private set; }

        public event Action? WindowsChanged;
        public event Action<IntPtr>? ForegroundChanged;
        /// <summary>Se dispara cuando Windows muestra su barra de tareas (para volver a esconderla).</summary>
        public event Action<string>? ShellWindowShown;

        public WindowTracker()
        {
            _hookProc = OnWinEvent;
            _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(220) };
            _debounce.Tick += (_, _) => { _debounce.Stop(); Refresh(); };
        }

        public void Start()
        {
            uint flags = Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS;
            _hooks.Add(Win32.SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _hookProc, 0, 0, flags));
            _hooks.Add(Win32.SetWinEventHook(Win32.EVENT_SYSTEM_MINIMIZEEND, Win32.EVENT_SYSTEM_MINIMIZEEND, IntPtr.Zero, _hookProc, 0, 0, flags));
            _hooks.Add(Win32.SetWinEventHook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZESTART, IntPtr.Zero, _hookProc, 0, 0, flags)); // 1.6.2
            _hooks.Add(Win32.SetWinEventHook(Win32.EVENT_OBJECT_DESTROY, Win32.EVENT_OBJECT_HIDE, IntPtr.Zero, _hookProc, 0, 0, flags));
            _hooks.Add(Win32.SetWinEventHook(Win32.EVENT_OBJECT_NAMECHANGE, Win32.EVENT_OBJECT_NAMECHANGE, IntPtr.Zero, _hookProc, 0, 0, flags));
            _hooks.Add(Win32.SetWinEventHook(Win32.EVENT_OBJECT_CLOAKED, Win32.EVENT_OBJECT_UNCLOAKED, IntPtr.Zero, _hookProc, 0, 0, flags));
            Refresh();
            var fg = Win32.GetForegroundWindow();
            if (fg != IntPtr.Zero) NoteForeground(fg);
        }

        private void OnWinEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (hwnd == IntPtr.Zero || idObject != 0 || idChild != 0) return; // solo la ventana en sí

            // 1.6.2: se minimizó una ventana: la barra/isla tiene que dejar de mostrar sus menús
            if (ev == EVENT_SYSTEM_MINIMIZESTART) { Minimized?.Invoke(hwnd); return; }

            if (ev == Win32.EVENT_SYSTEM_FOREGROUND || ev == Win32.EVENT_SYSTEM_MINIMIZEEND)
            {
                NoteForeground(hwnd);
                if (!_cache.TryGetValue(hwnd, out var fg) || IsUnidentifiedUwp(fg)) _debounce.Start(); // 1.6.1
                return;
            }

            // Cambio de título: solo importa si la ventana todavía no está en la lista
            // (algunas apps se muestran sin título y se lo ponen después).
            if (ev == Win32.EVENT_OBJECT_NAMECHANGE)
            {
                // 1.6.1: salvo que sea un marco de la Tienda sin reconocer (se vuelve a probar)
                if ((_cache.TryGetValue(hwnd, out var known) && !IsUnidentifiedUwp(known)) || !Win32.IsWindowVisible(hwnd)) return;
            }

            // Solo nos interesan ventanas de primer nivel
            if (Win32.GetAncestor(hwnd, Win32.GA_PARENT) != Win32.GetDesktopWindow()) return;

            if (ev == Win32.EVENT_OBJECT_SHOW)
            {
                var cls = WindowOps.GetClass(hwnd);
                if (Taskbar.IsShellTrayClass(cls)) ShellWindowShown?.Invoke(cls);
                NotificationCenter.OnWindowShown(hwnd, cls); // cartelitos de notificación de Windows
            }
            else if (ev == Win32.EVENT_OBJECT_UNCLOAKED)
            {
                // Las ventanas modernas (como el cartelito de notificación) se "descubren" en vez de mostrarse.
                NotificationCenter.OnWindowShown(hwnd, WindowOps.GetClass(hwnd));
            }

            if (ev == Win32.EVENT_OBJECT_DESTROY && !_cache.ContainsKey(hwnd)) return;
            _debounce.Stop();
            _debounce.Start();
        }

        private void NoteForeground(IntPtr hwnd)
        {
            Win32.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != _ownPid)
            {
                LastExternalForeground = hwnd;
                if (_cache.TryGetValue(hwnd, out var tw)) tw.LastActivated = ++_activationCounter;
            }
            ForegroundChanged?.Invoke(hwnd);
        }

        /// <summary>Marco de una app de la Tienda que todavía no se sabe de qué app es.</summary>
        public static bool IsUnidentifiedUwp(TrackedWindow w) =>
            w.PrimaryKey.StartsWith("hwnd:", StringComparison.Ordinal) && w.ExePath.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);

        private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;

        /// <summary>1.6.2: una ventana se empezó a minimizar.</summary>
        public event Action<IntPtr>? Minimized;

        public void Refresh()
        {
            bool identified = false;
            var list = new List<TrackedWindow>();
            var seen = new HashSet<IntPtr>();
            Win32.EnumWindows((h, _) =>
            {
                if (IsAltTabWindow(h))
                {
                    seen.Add(h);
                    if (!_cache.TryGetValue(h, out var tw))
                    {
                        tw = Describe(h);
                        if (tw != null) _cache[h] = tw;
                    }
                    else if (IsUnidentifiedUwp(tw))
                    {
                        // 1.6.1: apps de la Tienda (como Configuración) que al principio no se pudieron reconocer:
                        // se vuelve a probar. Antes quedaban como una app desconocida sin ícono y el ícono fijo
                        // "desaparecía" (quedaba un lugar vacío en el Dock).
                        var again = Describe(h);
                        if (again != null && !IsUnidentifiedUwp(again)) { again.LastActivated = tw.LastActivated; _cache[h] = tw = again; identified = true; }
                    }
                    if (tw != null) list.Add(tw);
                }
                return true;
            }, IntPtr.Zero);

            // limpiar ventanas que ya no existen
            var dead = new List<IntPtr>();
            foreach (var k in _cache.Keys) if (!seen.Contains(k)) dead.Add(k);
            foreach (var k in dead) _cache.Remove(k);
            if (_exeByPid.Count > 400) _exeByPid.Clear();

            if (identified || !SameWindows(list, Windows))
            {
                Windows = list;
                WindowsChanged?.Invoke();
            }
        }

        private static bool SameWindows(List<TrackedWindow> a, IReadOnlyList<TrackedWindow> b)
        {
            if (a.Count != b.Count) return false;
            var set = new HashSet<IntPtr>();
            foreach (var w in b) set.Add(w.Hwnd);
            foreach (var w in a) if (!set.Contains(w.Hwnd)) return false;
            return true;
        }

        private bool IsAltTabWindow(IntPtr h)
        {
            if (!Win32.IsWindowVisible(h)) return false;
            var ex = Win32.GetExStyle(h);
            bool appWindow = (ex & Win32.WS_EX_APPWINDOW) != 0;
            if (Win32.GetWindow(h, Win32.GW_OWNER) != IntPtr.Zero && !appWindow) return false;
            if ((ex & Win32.WS_EX_TOOLWINDOW) != 0) return false;
            if ((ex & Win32.WS_EX_NOACTIVATE) != 0 && !appWindow) return false;
            if (Win32.DwmGetWindowAttribute(h, Win32.DWMWA_CLOAKED, out int cloaked, 4) == 0 && cloaked != 0) return false;
            if (Win32.GetWindowTextLength(h) == 0) return false;
            Win32.GetWindowThreadProcessId(h, out var pid);
            if (pid == _ownPid) return false;
            return true;
        }

        private TrackedWindow? Describe(IntPtr h)
        {
            var cls = WindowOps.GetClass(h);
            if (cls == "Progman" || cls == "WorkerW" || Taskbar.IsShellTrayClass(cls) || cls == "Windows.UI.Core.CoreWindow") return null;
            Win32.GetWindowThreadProcessId(h, out var pid);
            var exe = ExeOf(pid);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            string primary = "", iconSource = exe;

            if (!string.IsNullOrEmpty(exe)) { primary = AppIdentity.ExeKey(exe); keys.Add(primary); }

            // Apps de la Store: la ventana real es de ApplicationFrameHost; la app está en una ventana hija.
            if (exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
            {
                // 1.6.1: si la app todavía no "entró" en su marco (recién abierta o minimizada), se usa el
                // identificador que Windows le pone al marco (el mismo que usa la barra de tareas).
                var aumid = UwpAumid(h, pid) ?? ShellCom.GetWindowAppId(h);
                if (!string.IsNullOrEmpty(aumid))
                {
                    primary = AppIdentity.AumidKey(aumid);
                    keys.Add(primary);
                    iconSource = AppIdentity.AppsFolderPrefix + aumid;
                }
                else
                {
                    primary = "hwnd:" + h.ToInt64(); // no se pudo identificar: cada ventana por separado
                    keys.Clear();
                    keys.Add(primary);
                    iconSource = "";
                }
            }
            else
            {
                var winAumid = ShellCom.GetWindowAppId(h);
                if (!string.IsNullOrEmpty(winAumid))
                {
                    var k = AppIdentity.AumidKey(winAumid);
                    keys.Add(k);
                    primary = k;
                }
            }

            if (string.IsNullOrEmpty(primary)) { primary = "hwnd:" + h.ToInt64(); keys.Add(primary); }

            return new TrackedWindow
            {
                Hwnd = h,
                ProcessId = pid,
                ExePath = exe,
                ClassName = cls,
                PrimaryKey = primary,
                Keys = keys,
                IconSource = iconSource,
                LastActivated = 0
            };
        }

        private string ExeOf(uint pid)
        {
            if (_exeByPid.TryGetValue(pid, out var cached)) return cached;
            string path = "";
            var hp = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hp != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    uint size = (uint)sb.Capacity;
                    if (Win32.QueryFullProcessImageName(hp, 0, sb, ref size)) path = sb.ToString();
                }
                finally { Win32.CloseHandle(hp); }
            }
            _exeByPid[pid] = path;
            return path;
        }

        private static string? UwpAumid(IntPtr frame, uint framePid)
        {
            string? result = null;
            Win32.EnumChildWindows(frame, (child, _) =>
            {
                Win32.GetWindowThreadProcessId(child, out var cpid);
                if (cpid != framePid)
                {
                    result = AumidOfProcess(cpid);
                    if (result != null) return false;
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        private static string? AumidOfProcess(uint pid)
        {
            var hp = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hp == IntPtr.Zero) return null;
            try
            {
                uint len = 512;
                var sb = new StringBuilder((int)len);
                return Win32.GetApplicationUserModelId(hp, ref len, sb) == 0 ? sb.ToString() : null;
            }
            finally { Win32.CloseHandle(hp); }
        }

        /// <summary>Nombre visible de la app de una ventana (para la barra de menú).</summary>
        public string AppNameFor(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "Explorador";
            var cls = WindowOps.GetClass(hwnd);
            if (cls == "Progman" || cls == "WorkerW" || Taskbar.IsShellTrayClass(cls)) return "Explorador";
            if (_cache.TryGetValue(hwnd, out var tw))
            {
                if (tw.Keys.Count > 0 && tw.IconSource.StartsWith(AppIdentity.AppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
                    return Shorten(WindowOps.GetTitle(hwnd));
                if (!string.IsNullOrEmpty(tw.ExePath)) return Shorten(DescriptionOf(tw.ExePath));
            }
            Win32.GetWindowThreadProcessId(hwnd, out var pid);
            var exe = ExeOf(pid);
            return string.IsNullOrEmpty(exe) ? Shorten(WindowOps.GetTitle(hwnd)) : Shorten(DescriptionOf(exe));
        }

        private readonly Dictionary<string, string> _descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private string DescriptionOf(string exe)
        {
            if (_descriptions.TryGetValue(exe, out var d)) return d;
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exe);
                d = !string.IsNullOrWhiteSpace(info.FileDescription) ? info.FileDescription.Trim()
                  : !string.IsNullOrWhiteSpace(info.ProductName) ? info.ProductName.Trim()
                  : Path.GetFileNameWithoutExtension(exe);
            }
            catch { d = Path.GetFileNameWithoutExtension(exe); }
            if (exe.EndsWith("\\explorer.exe", StringComparison.OrdinalIgnoreCase)) d = "Explorador";
            _descriptions[exe] = d;
            return d;
        }

        private static string Shorten(string s) => s.Length > 40 ? s.Substring(0, 39) + "…" : s;

        public void Dispose()
        {
            foreach (var h in _hooks) if (h != IntPtr.Zero) Win32.UnhookWinEvent(h);
            _hooks.Clear();
        }
    }
}
