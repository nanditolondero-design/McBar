// McBar — Creado por Andrés Londero
// 1.9.34: bandeja del sistema de Windows 11 leída por UI Automation.
//
// Por qué: desde Windows 11 22H2 la bandeja del Explorador ya no es una barra clásica (ToolbarWindow32) sino
// controles XAML. El método viejo (Native/TrayImport.cs, que lee la memoria del Explorador) solo ve el área de
// íconos ocultos; los íconos "siempre visibles" (NVIDIA, etc.) quedan fuera. UI Automation es la vía que Windows
// ofrece para recorrer esos controles: cada ícono es un botón con nombre, y se puede "invocar" por la misma vía.
//
// Limitaciones propias de UI Automation (no tiene hwnd, uID ni mensaje de retorno de la app):
//  - el clic izquierdo se hace con InvokePattern; el derecho (menú) hay que simularlo con el mouse sobre el
//    botón real, por eso se muestra la barra de Windows un momento (igual que Win+B);
//  - no entrega el dibujo del ícono: se busca el programa dueño por el nombre y se usa su ícono.
// Todas las llamadas de UI Automation corren en hilos del ThreadPool (MTA), nunca en el hilo de la interfaz.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using McBar.Core;

namespace McBar.Native
{
    internal static class TrayUia
    {
        public sealed class Entry
        {
            public string Name = "";
            public string AutomationId = "";
            public string ClassName = "";
            public bool Overflow;                 // salió de la ventana de íconos ocultos (solo existe mientras está abierta)
            public AutomationElement Element = null!;
        }

        public sealed class Result
        {
            public List<Entry> Entries = new List<Entry>();
            public bool TaskbarReadable;          // se pudo recorrer el árbol de la barra de Windows
        }

        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);

        private static bool _dumped;
        private static int _busy;

        /// <summary>Lee los íconos de la bandeja de Windows 11. Nunca tira. Llamar desde un hilo del ThreadPool.</summary>
        public static Result Read()
        {
            var res = new Result();
            if (Interlocked.Exchange(ref _busy, 1) == 1) return res; // ya hay una lectura en curso
            try
            {
                var roots = new List<(IntPtr h, bool overflow)>();
                var tray = Taskbar.ExplorerTray();
                if (tray != IntPtr.Zero) roots.Add((tray, false));
                var ov = Win32.FindWindow("TopLevelWindowForOverflowXamlIsland", null);
                if (ov != IntPtr.Zero) roots.Add((ov, true));

                var cond = new OrCondition(
                    new PropertyCondition(AutomationElement.ClassNameProperty, TrayUiaRules.IconClass),
                    new PropertyCondition(AutomationElement.AutomationIdProperty, TrayUiaRules.IconAutomationId));

                foreach (var (h, overflow) in roots)
                {
                    try
                    {
                        var root = AutomationElement.FromHandle(h);
                        var found = root.FindAll(TreeScope.Descendants, cond);
                        if (!overflow) res.TaskbarReadable = true;
                        foreach (AutomationElement el in found)
                        {
                            try
                            {
                                var cur = el.Current;
                                if (!TrayUiaRules.IsAppIcon(cur.Name, cur.AutomationId, cur.ClassName)) continue;
                                res.Entries.Add(new Entry
                                {
                                    Name = TrayUiaRules.CleanName(cur.Name),
                                    AutomationId = cur.AutomationId ?? "",
                                    ClassName = cur.ClassName ?? "",
                                    Overflow = overflow,
                                    Element = el
                                });
                            }
                            catch (ElementNotAvailableException) { }
                        }
                    }
                    catch (Exception ex) { Log.Write("Bandeja UIA: " + ex.GetType().Name + " " + ex.Message); }
                }

                // Diagnóstico (una sola vez): si en esta versión de Windows no se encontró ningún botón, se deja en el
                // registro cómo se ve el árbol de la barra, para poder ajustar los nombres de clase.
                if (res.Entries.Count == 0 && !_dumped && tray != IntPtr.Zero)
                {
                    _dumped = true;
                    DumpTree(tray);
                }
            }
            catch (Exception ex) { Log.Write("Bandeja UIA: " + ex.Message); }
            finally { Interlocked.Exchange(ref _busy, 0); }
            return res;
        }

        private static void DumpTree(IntPtr hwnd)
        {
            try
            {
                var sb = new StringBuilder("Bandeja UIA: no se encontró ningún botón de ícono. Árbol de la barra de Windows:\n");
                int count = 0;
                void Walk(AutomationElement e, int depth)
                {
                    if (count >= 80 || depth > 7) return;
                    var c = e.Current;
                    count++;
                    sb.Append(new string(' ', depth * 2)).Append($"[{c.ControlType.ProgrammaticName}] class='{c.ClassName}' id='{c.AutomationId}' name='{c.Name}'\n");
                    var walker = TreeWalker.RawViewWalker;
                    for (var ch = walker.GetFirstChild(e); ch != null; ch = walker.GetNextSibling(ch)) Walk(ch, depth + 1);
                }
                Walk(AutomationElement.FromHandle(hwnd), 0);
                Log.Write(sb.ToString());
            }
            catch (Exception ex) { Log.Write("Bandeja UIA (árbol): " + ex.Message); }
        }

        // ================== Programa dueño del ícono (para su dibujo y para no duplicar) ==================

        private sealed class ProcInfo { public string Path = ""; public string Name = ""; public string Product = ""; public string Desc = ""; public string Company = ""; }
        private static readonly Dictionary<string, string?> OwnerCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ProcInfo> InfoCache = new Dictionary<string, ProcInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Busca el programa que tiene este ícono, por parecido de nombre. Devuelve la ruta del .exe o null.</summary>
        public static string? ResolveExe(string name)
        {
            lock (OwnerCache)
            {
                if (OwnerCache.TryGetValue(name, out var cached)) return cached;
            }
            string? best = null; int bestScore = 0;
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        string? path = null;
                        var hp = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)p.Id);
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
                        if (string.IsNullOrEmpty(path)) continue;
                        if (path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase)
                            && !path.EndsWith("SecurityHealthSystray.exe", StringComparison.OrdinalIgnoreCase)) continue; // procesos del sistema no
                        ProcInfo info;
                        lock (InfoCache)
                        {
                            if (!InfoCache.TryGetValue(path, out info!))
                            {
                                info = new ProcInfo { Path = path, Name = System.IO.Path.GetFileNameWithoutExtension(path) };
                                try
                                {
                                    var v = FileVersionInfo.GetVersionInfo(path);
                                    info.Product = v.ProductName ?? ""; info.Desc = v.FileDescription ?? ""; info.Company = v.CompanyName ?? "";
                                }
                                catch { }
                                InfoCache[path] = info;
                            }
                        }
                        int score = TrayUiaRules.Score(name, info.Name, info.Product, info.Desc, info.Company);
                        if (score > bestScore) { bestScore = score; best = path; }
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch (Exception ex) { Log.Write("Bandeja UIA (dueño): " + ex.Message); }
            lock (OwnerCache) { OwnerCache[name] = best; }
            return best;
        }

        // ================== Clics ==================

        /// <summary>Vuelve a buscar el botón por nombre (por si el Explorador se reinició y el elemento viejo ya no sirve).</summary>
        private static AutomationElement? Refind(string name)
        {
            var r = Read();
            return r.Entries.FirstOrDefault(e => TrayUiaRules.SameIcon(e.Name, name))?.Element;
        }

        /// <summary>Clic izquierdo (Invoke) o derecho (mouse real sobre el botón). Corre en un hilo aparte; nunca bloquea la interfaz.</summary>
        public static void Click(TrayIcon icon, bool right, Action<TrayIcon> onGone)
        {
            Task.Run(() =>
            {
                try
                {
                    var el = icon.UiaElement as AutomationElement;
                    if (!right)
                    {
                        if (TryInvoke(el)) { Log.Write($"Tray UIA invoke OK: {icon.UiaName}"); return; }
                        el = Refind(icon.UiaName);
                        if (el == null) { Log.Write($"Tray UIA: el botón '{icon.UiaName}' ya no está"); onGone(icon); return; }
                        icon.UiaElement = el;
                        if (TryInvoke(el)) { Log.Write($"Tray UIA invoke OK (reubicado): {icon.UiaName}"); return; }
                        Log.Write($"Tray UIA: Invoke falló para '{icon.UiaName}', pruebo con el mouse");
                    }
                    el ??= Refind(icon.UiaName);
                    if (el == null) { onGone(icon); return; }
                    MouseClick(el, right, icon.UiaName);
                }
                catch (Exception ex) { Log.Write("Tray UIA clic: " + ex.GetType().Name + " " + ex.Message); }
            });
        }

        private static bool TryInvoke(AutomationElement? el)
        {
            if (el == null) return false;
            try
            {
                if (el.TryGetCurrentPattern(InvokePattern.Pattern, out var p)) { ((InvokePattern)p).Invoke(); return true; }
            }
            catch (ElementNotAvailableException) { }
            catch (InvalidOperationException) { }
            catch (Exception ex) { Log.Write("Tray UIA invoke: " + ex.Message); }
            return false;
        }

        /// <summary>
        /// Clic con el mouse real sobre el botón. UI Automation no tiene "clic derecho", así que se muestra la barra de
        /// Windows un momento (Win+B la trae aunque esté en auto-ocultar), se mueve el mouse al botón, se hace clic y se
        /// devuelve el mouse a donde estaba.
        /// </summary>
        private static void MouseClick(AutomationElement el, bool right, string name)
        {
            Taskbar.ShowTemporarily();
            Thread.Sleep(450); // dejar que la barra termine de aparecer
            System.Windows.Rect r;
            try { r = el.Current.BoundingRectangle; }
            catch { Log.Write("Tray UIA: no se pudo leer la posición de " + name); return; }
            int vx = Win32.GetSystemMetrics(76), vy = Win32.GetSystemMetrics(77), vw = Win32.GetSystemMetrics(78), vh = Win32.GetSystemMetrics(79);
            int cx = (int)(r.Left + r.Width / 2), cy = (int)(r.Top + r.Height / 2);
            if (r.IsEmpty || r.Width < 2 || r.Height < 2 || cx < vx || cy < vy || cx >= vx + vw || cy >= vy + vh)
            {
                Log.Write($"Tray UIA: el botón '{name}' no está a la vista (rect={r}); no se hace clic");
                return;
            }
            Win32.GetCursorPos(out var old);
            SetCursorPos(cx, cy);
            uint down = right ? 0x0008u : 0x0002u, up = right ? 0x0010u : 0x0004u; // MOUSEEVENTF_RIGHTDOWN/UP, LEFTDOWN/UP
            var inputs = new Win32.INPUT[2];
            inputs[0].type = 0; inputs[0].U.mi = new Win32.MOUSEINPUT { dwFlags = down };
            inputs[1].type = 0; inputs[1].U.mi = new Win32.MOUSEINPUT { dwFlags = up };
            Win32.SendInput(2, inputs, Marshal.SizeOf<Win32.INPUT>());
            Log.Write($"Tray UIA mouse {(right ? "derecho" : "izquierdo")} en '{name}' ({cx},{cy})");
            Thread.Sleep(120);
            SetCursorPos(old.x, old.y);
            // La barra volvió a esconderse sola cuando se abra otra ventana (App.xaml.cs); por las dudas, red de seguridad.
            _ = Task.Run(() =>
            {
                for (int i = 0; i < 4 && Taskbar.TemporarilyShown; i++)
                {
                    Thread.Sleep(3000);
                    if (Win32.FindWindow("Xaml_WindowedPopupClass", null) != IntPtr.Zero) continue; // menú todavía abierto
                    break;
                }
                if (Taskbar.TemporarilyShown && Win32.FindWindow("Xaml_WindowedPopupClass", null) == IntPtr.Zero) Taskbar.EndTemporary();
            });
        }
    }
}
