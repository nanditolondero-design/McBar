// McBar — Creado por Andrés Londero
// Bandeja del sistema propia de McBar.
//
// Cómo funciona (igual que Cairo Desktop / RetroBar):
//  1. McBar crea una ventana invisible con el nombre de clase "Shell_TrayWnd" (el mismo que la barra de
//     Windows) y la pone primera en el orden. Cuando una app llama a Shell_NotifyIcon, Windows busca esa
//     clase y nos manda el ícono a nosotros.
//  2. Se avisa a todas las apps "se creó una barra de tareas nueva" (mensaje TaskbarCreated) para que
//     vuelvan a mandar sus íconos.
//  3. Todo lo que recibimos se REENVÍA también al Explorador, así Windows sigue funcionando igual.
//  4. Al hacer clic en un ícono, le mandamos a la app el mismo mensaje que le mandaría Windows.
//
// IMPORTANTE (0.3.1): la ventana vive en un HILO PROPIO. Antes estaba en el hilo de la interfaz y cada
// mensaje de cualquier app (más el reenvío al Explorador) frenaba el Dock y la barra.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using McBar.Core;

namespace McBar.Native
{
    internal sealed class TrayHost : IDisposable
    {
        public static TrayHost? Current { get; private set; }

        private const uint WS_POPUP = 0x80000000, WS_CHILD = 0x40000000, WS_CLIPCHILDREN = 0x02000000, WS_CLIPSIBLINGS = 0x04000000;
        private const uint WS_EX_TOOLWINDOW = 0x80, WS_EX_TOPMOST = 0x8;
        private const uint ASFW_ANY = 0xFFFFFFFF; // 1.9.17: permitir que cualquier proceso pase al frente
        private const int WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_LBUTTONDBLCLK = 0x203,
                          WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205, WM_CONTEXTMENU = 0x7B, NIN_SELECT = 0x400;

        private readonly Win32.WndProcDelegate _wndProc; // referencia viva para el GC
        private readonly Dispatcher _ui;
        private Dispatcher? _trayThread;
        private readonly List<TrayIcon> _icons = new List<TrayIcon>(); // solo se toca en el hilo de la bandeja
        private volatile TrayIcon[] _snapshot = Array.Empty<TrayIcon>(); // copia para leer desde la interfaz
        private readonly Dictionary<uint, string> _exeByPid = new Dictionary<uint, string>();
        private IntPtr _hwnd;
        private uint _taskbarCreated;
        private int _changePending;

        /// <summary>Íconos actuales (copia segura para la interfaz).</summary>
        public IReadOnlyList<TrayIcon> Icons => _snapshot;
        public IntPtr Hwnd => _hwnd;
        public DateTime LastBroadcast { get; private set; } = DateTime.MinValue;

        /// <summary>Cambió la lista de íconos (se avisa agrupado, en el hilo de la interfaz).</summary>
        public event Action? Changed;

        /// <summary>Dónde está dibujado un ícono (en píxeles). Se consulta en el hilo de la interfaz.</summary>
        public Func<TrayIcon, PxRect?>? IconRectProvider { get; set; }

        private TrayHost()
        {
            _wndProc = WndProc;
            _ui = Dispatcher.CurrentDispatcher;
        }

        /// <summary>Llamar desde el hilo de la interfaz.</summary>
        public static TrayHost? Start()
        {
            if (Current != null) return Current;
            var host = new TrayHost();
            var ready = new ManualResetEventSlim();
            bool ok = false;
            var t = new Thread(() =>
            {
                host._trayThread = Dispatcher.CurrentDispatcher;
                ok = host.Create();
                ready.Set();
                if (ok) Dispatcher.Run(); // bucle de mensajes propio de la bandeja
            })
            { IsBackground = true, Name = "McBar.Bandeja" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            ready.Wait(5000);
            if (!ok) return null;
            Current = host;
            host.Raise();
            host.Broadcast();
            host.ImportExisting(); // 1.9.32: traer los íconos que ya estaban en la bandeja real de Windows
            host.AnnounceMissing();
            // 1.9.41: segunda, tercera y cuarta ola (apps ocupadas que no atendieron el primer aviso, o que arrancan junto
            // con McBar). Los temporizadores se crean dentro del hilo de la bandeja.
            host._trayThread?.BeginInvoke(new Action(() =>
            {
                foreach (var ms in new[] { 1500, 3500, 8000 })
                {
                    var delay = ms;
                    var tm = new DispatcherTimer(DispatcherPriority.Background, host._trayThread!) { Interval = TimeSpan.FromMilliseconds(delay) };
                    tm.Tick += (_, _) => { tm.Stop(); if (delay == 3500) host.Broadcast(); host.AnnounceMissing(); };
                    tm.Start();
                }
            }));
            return host;
        }

        private bool Create()
        {
            try
            {
                _taskbarCreated = Win32.RegisterWindowMessage("TaskbarCreated");
                var hInst = Win32.GetModuleHandle(null);
                // 1.9.41: igual que ManagedShell (Cairo/RetroBar): clase con estilo 0x8 y ventana con WS_CLIPCHILDREN/SIBLINGS,
                // más un hijo "TrayNotifyWnd" (algunas apps lo buscan dentro de Shell_TrayWnd).
                var wc = new Win32.WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEX>(),
                    style = 0x8,
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                    hInstance = hInst,
                    lpszClassName = "Shell_TrayWnd"
                };
                Win32.RegisterClassEx(ref wc);
                _hwnd = Win32.CreateWindowEx(WS_EX_TOOLWINDOW | WS_EX_TOPMOST, "Shell_TrayWnd", "", WS_POPUP | WS_CLIPCHILDREN | WS_CLIPSIBLINGS, 0, 0, 0, 0,
                                             IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);
                if (_hwnd == IntPtr.Zero) { Log.Write("Bandeja: no se pudo crear la ventana"); return false; }
                var wn = wc;
                wn.lpszClassName = "TrayNotifyWnd";
                Win32.RegisterClassEx(ref wn);
                Win32.CreateWindowEx(0, "TrayNotifyWnd", "", WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS, 0, 0, 0, 0,
                                     _hwnd, IntPtr.Zero, hInst, IntPtr.Zero);
                // 1.9.41: vigilante propio, cada 100 ms y en el hilo de la bandeja (ManagedShell hace lo mismo). Antes se
                // revisaba cada 700 ms desde la interfaz, y todo ícono que se registraba en ese hueco iba al Explorador
                // en vez de a McBar (y McBar nunca se enteraba).
                _guard = new DispatcherTimer(DispatcherPriority.Background, _trayThread!) { Interval = TimeSpan.FromMilliseconds(100) };
                _guard.Tick += (_, _) => Guard();
                _guard.Start();
                return true;
            }
            catch (Exception ex) { Log.Write("Bandeja: " + ex.Message); return false; }
        }

        private DispatcherTimer? _guard;

        /// <summary>Mantiene nuestra Shell_TrayWnd primera (la que Windows encuentra) y la del Explorador al fondo.</summary>
        private void Guard()
        {
            try
            {
                if (_hwnd == IntPtr.Zero) return;
                if (Win32.FindWindow("Shell_TrayWnd", null) == _hwnd) return;
                Raise();
                // Con la barra de Windows escondida por McBar se la manda al fondo (como ManagedShell) para que no
                // vuelva a quedar primera cuando el Explorador se la vuelve a poner "siempre visible".
                if (Taskbar.IsHiddenByUs)
                {
                    var ex = Taskbar.ExplorerTray();
                    if (ex != IntPtr.Zero)
                        Win32.SetWindowPos(ex, new IntPtr(1) /*HWND_BOTTOM*/, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                }
            }
            catch { }
        }

        /// <summary>Pone nuestra ventana primera entre las "Shell_TrayWnd".</summary>
        public void Raise()
        {
            if (_hwnd != IntPtr.Zero)
                Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }

        /// <summary>Si el Explorador quedó primero, volver a ponernos primeros (lo llama el vigilante cada tanto).</summary>
        public void EnsureFirst()
        {
            if (_hwnd != IntPtr.Zero && Win32.FindWindow("Shell_TrayWnd", null) != _hwnd) Raise();
        }

        public void Broadcast()
        {
            LastBroadcast = DateTime.Now;
            Win32.PostMessage(Win32.HWND_BROADCAST, _taskbarCreated, IntPtr.Zero, IntPtr.Zero);
        }

        public bool IsOwnBroadcastRecent => (DateTime.Now - LastBroadcast).TotalSeconds < 5;

        // ================== Hilo de la bandeja ==================

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == Win32.WM_COPYDATA && lParam != IntPtr.Zero)
            {
                try
                {
                    var cds = Marshal.PtrToStructure<Win32.COPYDATASTRUCT>(lParam);
                    long kind = cds.dwData.ToInt64();
                    if (kind == 1 && cds.lpData != IntPtr.Zero && cds.cbData > 0)
                    {
                        var data = new byte[cds.cbData];
                        Marshal.Copy(cds.lpData, data, 0, cds.cbData);
                        var m = TrayData.Parse(data);
                        bool ok = m != null && Handle(m);
                        Forward(wParam, lParam, 300);
                        return new IntPtr(ok ? 1 : 0);
                    }
                    if (kind == 3 && cds.lpData != IntPtr.Zero && cds.cbData >= 20)
                    {
                        var data = new byte[cds.cbData];
                        Marshal.Copy(cds.lpData, data, 0, cds.cbData);
                        var r = Identify(data);
                        if (r != IntPtr.Zero) return r;
                    }
                }
                catch (Exception ex) { Log.Write("Bandeja: " + ex.Message); }
                return Forward(wParam, lParam, 3000);
            }
            return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private static IntPtr Forward(IntPtr wParam, IntPtr lParam, uint timeout)
        {
            var explorer = Taskbar.ExplorerTray();
            if (explorer == IntPtr.Zero) return IntPtr.Zero;
            Win32.SendMessageTimeout(explorer, Win32.WM_COPYDATA, wParam, lParam, Win32.SMTO_ABORTIFHUNG, timeout, out var result);
            return result;
        }

        private bool Handle(TrayMessage m)
        {
            var id = TrayData.IdOf(m);
            var icon = _icons.FirstOrDefault(i => i.Id == id);
            switch (m.Message)
            {
                case TrayData.NIM_ADD:
                case TrayData.NIM_MODIFY:
                    if (icon == null)
                    {
                        if (m.Message == TrayData.NIM_MODIFY && (m.Flags & TrayData.NIF_ICON) == 0) return false;
                        // 1.9.32: si lo habíamos importado de la bandeja real (otro Id, por ej. con GUID), sacar el duplicado
                        _icons.RemoveAll(i => i.HWnd == new IntPtr(unchecked((int)m.HWnd)) && (i.Uid == m.Uid || i.Imported));
                        icon = new TrayIcon
                        {
                            Id = id,
                            HWnd = new IntPtr(unchecked((int)m.HWnd)),
                            Uid = m.Uid,
                            Guid = (m.Flags & TrayData.NIF_GUID) != 0 ? m.Guid : Guid.Empty,
                            ExePath = ExeOf(new IntPtr(unchecked((int)m.HWnd)))
                        };
                        _icons.Add(icon);
                    }
                    if (m.HWnd != 0) icon.HWnd = new IntPtr(unchecked((int)m.HWnd));
                    if ((m.Flags & TrayData.NIF_MESSAGE) != 0) icon.CallbackMessage = m.CallbackMessage;
                    if ((m.Flags & TrayData.NIF_TIP) != 0 && m.Tip != null) icon.Tooltip = m.Tip;
                    if ((m.Flags & TrayData.NIF_ICON) != 0) icon.Icon = ConvertIcon(m.HIcon) ?? icon.Icon;
                    if ((m.Flags & TrayData.NIF_STATE) != 0 && (m.StateMask & TrayData.NIS_HIDDEN) != 0)
                        icon.Hidden = (m.State & TrayData.NIS_HIDDEN) != 0;
                    NotifyChanged();
                    return true;
                case TrayData.NIM_DELETE:
                    if (icon != null) { _icons.Remove(icon); NotifyChanged(); }
                    return true;
                case TrayData.NIM_SETVERSION:
                    if (icon != null) icon.Version = m.Version;
                    return true;
                default:
                    return true;
            }
        }

        private IntPtr Identify(byte[] d)
        {
            if (TrayData.U32(d, 0) != TrayData.Signature) return IntPtr.Zero;
            uint which = TrayData.U32(d, 4);
            uint hwnd = TrayData.U32(d, 12), uid = TrayData.U32(d, 16);
            Guid g = d.Length >= 36 ? new Guid(new ReadOnlySpan<byte>(d, 20, 16)) : Guid.Empty;
            var icon = _icons.FirstOrDefault(i => (g != Guid.Empty && i.Guid == g) || ((uint)i.HWnd.ToInt64() == hwnd && i.Uid == uid));
            var provider = IconRectProvider;
            if (icon == null || provider == null) return IntPtr.Zero;
            PxRect? r = null;
            try
            {
                // Se pregunta a la interfaz con tiempo límite (nunca bloquear la bandeja).
                var op = _ui.BeginInvoke(new Func<PxRect?>(() => provider(icon)));
                if (op.Wait(TimeSpan.FromMilliseconds(200)) == DispatcherOperationStatus.Completed) r = op.Result as PxRect?;
            }
            catch { }
            if (r == null) return IntPtr.Zero;
            int x = which == 1 ? r.Value.Left : r.Value.Right;
            int y = which == 1 ? r.Value.Top : r.Value.Bottom;
            return new IntPtr((x & 0xFFFF) | ((y & 0xFFFF) << 16));
        }

        private static System.Windows.Media.ImageSource? ConvertIcon(uint handle)
        {
            if (handle == 0) return null;
            var copy = Win32.CopyIcon(new IntPtr(unchecked((int)handle)));
            if (copy == IntPtr.Zero) return null;
            try
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(copy, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze(); // congelado: se puede usar desde el hilo de la interfaz
                return src;
            }
            catch { return null; }
            finally { Win32.DestroyIcon(copy); }
        }

        private string ExeOf(IntPtr hwnd)
        {
            uint pid = Win32.ProcessOf(hwnd);
            if (_exeByPid.TryGetValue(pid, out var p)) return p;
            p = "";
            var hp = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hp != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    uint size = (uint)sb.Capacity;
                    if (Win32.QueryFullProcessImageName(hp, 0, sb, ref size)) p = sb.ToString();
                }
                finally { Win32.CloseHandle(hp); }
            }
            _exeByPid[pid] = p;
            return p;
        }

        private void NotifyChanged()
        {
            _snapshot = _icons.ToArray();
            if (Interlocked.Exchange(ref _changePending, 1) == 1) return;
            // agrupar: muchas apps actualizan su ícono seguido (algunas cada segundo)
            _ui.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                Interlocked.Exchange(ref _changePending, 0);
                Changed?.Invoke();
            }));
        }

        /// <summary>
        /// 1.9.32: importa los íconos que YA están en la bandeja real de Windows (NVIDIA, Seguridad de Windows y
        /// toda app que estaba abierta antes de McBar o que no se re-registra). Corre en el hilo de la bandeja.
        /// </summary>
        public void ImportExisting()
        {
            _trayThread?.BeginInvoke(new Action(() =>
            {
                List<TrayImport.Entry> found;
                try { found = TrayImport.Read(); } catch (Exception ex) { Log.Write("Importar bandeja: " + ex.Message); return; }
                bool changed = false;
                foreach (var e in found)
                {
                    try
                    {
                        if (e.Hwnd == IntPtr.Zero || !Win32.IsWindow(e.Hwnd)) continue;
                        // ya lo tenemos (lo mandó la app o lo importamos antes)
                        if (_icons.Any(i => i.HWnd == e.Hwnd && i.Uid == e.Uid)) continue;
                        var exe = ExeOf(e.Hwnd);
                        // los íconos del propio Explorador (reloj, volumen, red) no se importan: McBar tiene los suyos
                        if (exe.EndsWith("\\explorer.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        var icon = new TrayIcon
                        {
                            Id = $"h:{(uint)e.Hwnd.ToInt64():X}:{e.Uid}", // mismo formato que un ícono registrado, para no duplicar
                            HWnd = e.Hwnd,
                            Uid = e.Uid,
                            CallbackMessage = e.Callback,
                            Version = 0,
                            Imported = true, // 1.9.33: el clic se manda en los dos formatos (no sabemos su versión)
                            ExePath = exe,
                            Icon = ConvertIcon(e.HIcon) ?? McBar.Core.IconLoader.FromWindow(e.Hwnd)
                        };
                        _icons.Add(icon);
                        changed = true;
                        // 1.9.39: el aviso "se creó una barra nueva" de difusión no llega a las ventanas internas (message-only)
                        // que usan muchos programas de bandeja. Se lo mandamos directo a la ventana de este ícono: si la app
                        // se vuelve a registrar, reemplaza al importado y McBar pasa a tener sus datos reales (versión,
                        // mensaje de retorno, GUID). Si corre como administrador Windows lo rechaza y no pasa nada.
                        Win32.PostMessage(e.Hwnd, _taskbarCreated, IntPtr.Zero, IntPtr.Zero);
                        Log.Write($"Bandeja: importado de Windows 0x{e.Hwnd.ToInt64():X} uid={e.Uid} cb=0x{e.Callback:X} ({System.IO.Path.GetFileName(exe)})");
                    }
                    catch (Exception ex) { Log.Write("Importar ícono: " + ex.Message); }
                }
                if (changed) NotifyChanged();
            }));
        }

        // ================== 1.9.41: re-avisar a las apps que no se registraron ==================
        //
        // En Windows 11 22H2 o posterior la bandeja del Explorador es XAML (ya no tiene SysPager/ToolbarWindow32),
        // así que ManagedShell/RetroBar tampoco pueden "leer" lo que ya está puesto: dependen de que cada app se
        // vuelva a registrar al recibir TaskbarCreated. Pero el aviso de difusión (HWND_BROADCAST) NO llega a las
        // ventanas "solo de mensajes" (HWND_MESSAGE), que son justo las que usan muchos programas de bandeja
        // (Seguridad de Windows, NVIDIA...). Por eso acá se manda TaskbarCreated directo a TODAS las ventanas de las
        // apps que Windows recuerda como apps de bandeja (HKCU\Control Panel\NotifyIconSettings) y que están
        // corriendo pero todavía no tienen ícono en McBar. Con tope de intentos por proceso para no insistir de más.

        private readonly Dictionary<uint, int> _announceTries = new Dictionary<uint, int>();
        private const int MaxAnnounceTries = 8;

        private static HashSet<string> KnownTrayApps()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
                if (key == null) return set;
                foreach (var name in key.GetSubKeyNames())
                {
                    try
                    {
                        using var k = key.OpenSubKey(name);
                        // la ruta suele empezar con el GUID de una carpeta conocida ({...}\NVIDIA...\nvtray.exe): alcanza con el nombre del .exe
                        if (k?.GetValue("ExecutablePath") is string path && path.Length > 0)
                        {
                            var file = path.Substring(path.LastIndexOfAny(new[] { '\\', '/' }) + 1);
                            if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) set.Add(file);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log.Write("Bandeja: no se pudo leer NotifyIconSettings: " + ex.Message); }
            return set;
        }

        /// <summary>Todas las ventanas (de primer nivel y "solo de mensajes") agrupadas por proceso.</summary>
        private static Dictionary<uint, List<IntPtr>> WindowsByProcess()
        {
            var map = new Dictionary<uint, List<IntPtr>>();
            void Add(IntPtr h)
            {
                uint pid = Win32.ProcessOf(h);
                if (pid == 0) return;
                if (!map.TryGetValue(pid, out var l)) map[pid] = l = new List<IntPtr>();
                l.Add(h);
            }
            Win32.EnumWindowsProc cb = (h, _) => { Add(h); return true; };
            Win32.EnumWindows(cb, IntPtr.Zero);
            GC.KeepAlive(cb);
            IntPtr m = IntPtr.Zero;
            while ((m = Win32.FindWindowEx(Win32.HWND_MESSAGE, m, null, null)) != IntPtr.Zero) Add(m);
            return map;
        }

        public void AnnounceMissing()
        {
            _trayThread?.BeginInvoke(new Action(() =>
            {
                try
                {
                    var known = KnownTrayApps();
                    if (known.Count == 0) return;
                    uint own = (uint)Environment.ProcessId;
                    var have = new HashSet<string>(_icons.Select(i => System.IO.Path.GetFileName(i.ExePath)), StringComparer.OrdinalIgnoreCase);
                    var byPid = WindowsByProcess();
                    foreach (var dead in _announceTries.Keys.Where(k => !byPid.ContainsKey(k)).ToList()) _announceTries.Remove(dead);
                    foreach (var kv in byPid)
                    {
                        if (kv.Key == own) continue;
                        var exe = ExeOf(kv.Value[0]);
                        if (exe.Length == 0) continue;
                        var file = System.IO.Path.GetFileName(exe);
                        if (!known.Contains(file) || have.Contains(file)) continue;
                        if (file.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        _announceTries.TryGetValue(kv.Key, out int tries);
                        if (tries >= MaxAnnounceTries) continue;
                        _announceTries[kv.Key] = tries + 1;
                        foreach (var h in kv.Value) Win32.PostMessage(h, _taskbarCreated, IntPtr.Zero, IntPtr.Zero);
                        Log.Write($"Bandeja: aviso TaskbarCreated directo a {file} (pid {kv.Key}, {kv.Value.Count} ventanas, intento {tries + 1}/{MaxAnnounceTries})");
                    }
                }
                catch (Exception ex) { Log.Write("Bandeja (aviso directo): " + ex.Message); }
            }));
        }

        /// <summary>Saca los íconos de apps que se cerraron sin avisar.</summary>
        public void Prune()
        {
            _trayThread?.BeginInvoke(new Action(() =>
            {
                if (_icons.RemoveAll(i => !Win32.IsWindow(i.HWnd)) > 0) NotifyChanged();
            }));
        }

        // ================== Clics (desde la interfaz) ==================

        public enum Click { LeftDown, LeftUp, LeftDouble, RightDown, RightUp }

        public void Send(TrayIcon icon, Click click, PxRect anchor)
        {
            if (icon.IsWindowsSystem) return; // nunca tocar los íconos propios de Windows
            // 1.9.38: Seguridad de Windows (Defender) ignora el clic que le reenviamos. Su clic izquierdo equivale a abrir
            // la app con su protocolo oficial, así que se hace directo. El clic derecho sigue por el camino normal.
            if (icon.ExePath.EndsWith("\\SecurityHealthSystray.exe", StringComparison.OrdinalIgnoreCase))
            {
                if (click == Click.LeftUp)
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("windowsdefender:") { UseShellExecute = true }); Log.Write("Tray: Seguridad de Windows abierta con windowsdefender:"); }
                    catch (Exception ex) { Log.Write("Tray: no se pudo abrir Seguridad de Windows: " + ex.Message); }
                }
                if (click == Click.LeftDown || click == Click.LeftUp || click == Click.LeftDouble) return;
            }
            if (icon.CallbackMessage == 0 || !Win32.IsWindow(icon.HWnd)) { Prune(); return; }
            // 1.9.19: la barra de McBar nunca toma el foco, así que Windows le prohíbe al programa traer su menú
            // al frente (por eso no abría ni mostraba menú). Se baja a cero el "tiempo de bloqueo de foco" para que
            // cualquier programa pueda pasar al frente; esto sí dura hasta que el programa procesa el clic (el
            // AttachThreadInput de la 1.9.17 se desenganchaba antes, por eso no servía).
            RelaxForegroundLock();
            Win32.AllowSetForegroundWindow(ASFW_ANY);
            Win32.AllowSetForegroundWindow(Win32.ProcessOf(icon.HWnd)); // para que pueda mostrar su menú al frente
            // 1.9.15: la interfaz ya manda "abajo" al apretar y "arriba" al soltar (dos llamadas), así que acá va
            // un solo mensaje por llamada. Para los íconos nuevos (versión 4) el menú se pide con WM_CONTEXTMENU
            // y la selección con NIN_SELECT; para los viejos, con el "arriba" común.
            // 1.9.33: los importados (versión desconocida) usan el set "nuevo" y además se mandan en los dos formatos.
            bool modern = icon.Version >= 4 || icon.Imported;
            int[] msgs = click switch
            {
                Click.LeftDown => new[] { WM_LBUTTONDOWN },
                Click.LeftUp => modern ? new[] { WM_LBUTTONUP, NIN_SELECT } : new[] { WM_LBUTTONUP },
                Click.LeftDouble => new[] { WM_LBUTTONDBLCLK },
                Click.RightDown => new[] { WM_RBUTTONDOWN },
                Click.RightUp => modern ? new[] { WM_RBUTTONUP, WM_CONTEXTMENU } : new[] { WM_RBUTTONUP },
                _ => Array.Empty<int>()
            };
            // 1.9.15: registro para diagnóstico (queda en mcbar.log). Dice qué se reenvía y a quién.
            Log.Write($"Tray {click} -> hwnd=0x{icon.HWnd.ToInt64():X} uid={icon.Uid} ver={icon.Version} imp={icon.Imported} cb=0x{icon.CallbackMessage:X} sys={icon.IsWindowsSystem} msgs=[{string.Join(",", msgs.Select(m => "0x" + m.ToString("X")))}]");
            foreach (var mm in msgs)
            {
                // formato nuevo (v4): wParam = posición del ícono, lParam = mensaje + uid. formato clásico: wParam = uid, lParam = mensaje.
                int x = anchor.Left, y = anchor.Bottom;
                var v4w = new IntPtr((x & 0xFFFF) | ((y & 0xFFFF) << 16));
                var v4l = new IntPtr((mm & 0xFFFF) | (int)((icon.Uid & 0xFFFF) << 16));
                var oldw = new IntPtr(icon.Uid);
                var oldl = new IntPtr(mm);
                // para un ícono importado mandamos los dos; para uno conocido, el que corresponde a su versión
                var pairs = icon.Imported
                    ? new[] { (v4w, v4l), (oldw, oldl) }
                    : (icon.Version >= 4 ? new[] { (v4w, v4l) } : new[] { (oldw, oldl) });
                foreach (var (wParam, lParam) in pairs)
                {
                    bool ok = Win32.PostMessage(icon.HWnd, icon.CallbackMessage, wParam, lParam);
                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        // 1.9.20: err=5 (acceso denegado) = el programa corre como administrador y Windows no deja
                        // que McBar (normal) le mande el clic. Es un límite de seguridad de Windows (UIPI).
                        Log.Write($"Tray: Windows rechazó el envío a 0x{icon.HWnd.ToInt64():X} (error {err}{(err == 5 ? " = el programa corre como administrador" : "")})");
                        // 1.9.29: si está activado el ayudante, se lo pedimos a la copia que corre como administrador.
                        if (err == 5 && SettingsStore.Current.TrayAdminHelper)
                            TrayHelper.Forward(icon.HWnd, icon.CallbackMessage, wParam, lParam);
                    }
                }
            }
        }

        // 1.9.19: baja el bloqueo de foco (una vez) para que los programas del área de notificaciones puedan
        // mostrar su menú o abrir su ventana. Se guarda el valor anterior y se restaura al cerrar.
        private bool _fgLockSaved;
        private uint _fgLockOld;
        private void RelaxForegroundLock()
        {
            if (_fgLockSaved) return;
            try
            {
                Win32.SystemParametersInfoGetUint(Win32.SPI_GETFOREGROUNDLOCKTIMEOUT, 0, out _fgLockOld, 0);
                Win32.SystemParametersInfoSetPtr(Win32.SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, 0);
                _fgLockSaved = true;
            }
            catch (Exception ex) { Log.Write("Bloqueo de foco: " + ex.Message); }
        }
        private void RestoreForegroundLock()
        {
            if (!_fgLockSaved) return;
            try { Win32.SystemParametersInfoSetPtr(Win32.SPI_SETFOREGROUNDLOCKTIMEOUT, 0, (IntPtr)_fgLockOld, 0); } catch { }
            _fgLockSaved = false;
        }

        public void Dispose()
        {
            RestoreForegroundLock(); // 1.9.19: devolver el bloqueo de foco como estaba
            var t = _trayThread;
            if (t != null)
            {
                try
                {
                    t.Invoke(new Action(() =>
                    {
                        if (_hwnd != IntPtr.Zero) { Win32.DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
                    }), TimeSpan.FromSeconds(2));
                    t.BeginInvokeShutdown(DispatcherPriority.Normal);
                }
                catch { }
            }
            Current = null;
            _snapshot = Array.Empty<TrayIcon>();
            // Las apps vuelven a mandar sus íconos a la barra de Windows (por las dudas, ya se le reenviaban).
            Win32.PostMessage(Win32.HWND_BROADCAST, _taskbarCreated, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
