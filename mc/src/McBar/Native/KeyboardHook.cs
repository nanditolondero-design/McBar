// McBar — Creado por Andrés Londero
// Atajos de teclado propios (0.4.0): Win+N abre el centro de notificaciones de McBar y Win+A el Centro de
// control, en lugar de los paneles laterales de Windows.
//
// Cómo funciona:
//  - Gancho de teclado de bajo nivel (WH_KEYBOARD_LL) en un HILO PROPIO con su propio bucle de mensajes,
//    para que nunca frene la interfaz (Windows desconecta los ganchos lentos).
//  - Si se aprieta N o A con la tecla Windows apretada (y sin Ctrl/Alt/Shift) nos "comemos" la tecla y le
//    avisamos al hilo de la interfaz.
//  - Para que al soltar la tecla Windows NO se abra el menú Inicio, mandamos una tecla "vacía" (VK 0xE8,
//    sin asignar) — el mismo truco que usa PowerToys.
//  - 0.6.0: también la tecla Windows sola (Launchpad), Win+S (buscador) y las teclas de volumen.
//  - Cada atajo se activa o desactiva en Preferencias → Teclado.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using McBar.Core;

namespace McBar.Native
{
    internal static class KeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x100, WM_SYSKEYDOWN = 0x104;
        private const uint LLKHF_INJECTED = 0x10;
        private const int VK_N = 0x4E, VK_A = 0x41, VK_LWIN = 0x5B, VK_RWIN = 0x5C,
                          VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
        private const ushort VK_DUMMY = 0xE8;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

        private static LowLevelProc? _proc;   // referencia viva para el GC
        private static IntPtr _hook = IntPtr.Zero;
        private static Dispatcher? _thread;
        private static Dispatcher? _ui;

        public static bool IsRunning => _hook != IntPtr.Zero;

        public static void Start()
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
                    using var mod = Process.GetCurrentProcess().MainModule;
                    _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(mod?.ModuleName), 0);
                    if (_hook == IntPtr.Zero) Log.Write("KeyboardHook: no se pudo instalar el gancho (" + Marshal.GetLastWin32Error() + ")");
                    else Log.Write("KeyboardHook: Win+N / Win+A activos");
                }
                catch (Exception ex) { Log.Write("KeyboardHook: " + ex.Message); }
                ready.Set();
                Dispatcher.Run();
            }) { IsBackground = true, Name = "McBar.Keyboard" };
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
                d.Invoke(() =>
                {
                    if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
                    _hook = IntPtr.Zero;
                });
                d.InvokeShutdown();
            }
            catch { }
            _thread = null;
        }

        private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        private const int WM_KEYUP = 0x101, WM_SYSKEYUP = 0x105;
        private const int VK_S = 0x53, VK_VOLUME_MUTE = 0xAD, VK_VOLUME_DOWN = 0xAE, VK_VOLUME_UP = 0xAF;
        private const int VK_TAB = 0x09, VK_ESCAPE = 0x1B, VK_LEFT = 0x25, VK_RIGHT = 0x27, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
        private static bool _winDown, _winOther, _switching;
        private static bool _winSwallowed, _winInjected; // 1.8.1
        private static int _winVk = VK_LWIN;
        private static readonly System.Collections.Generic.HashSet<int> _eaten = new System.Collections.Generic.HashSet<int>(); // 1.8.0
        private const int VK_SPACE = 0x20, VK_UP = 0x26, VK_DOWN = 0x28, VK_RETURN = 0x0D, VK_BACK = 0x08,
                          VK_C = 0x43, VK_U = 0x55, VK_I = 0x49, VK_J = 0x4A, VK_K = 0x4B;

        [StructLayout(LayoutKind.Sequential)]
        private struct GuiThreadInfo
        {
            public int cbSize, flags;
            public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
            public Win32.RECT rcCaret;
        }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, System.Text.StringBuilder sb, int max);

        private static string ClassOf(IntPtr h)
        {
            var sb = new System.Text.StringBuilder(64);
            return h != IntPtr.Zero && GetClassName(h, sb, sb.Capacity) > 0 ? sb.ToString() : "";
        }

        /// <summary>¿El foco está en la lista de archivos de una ventana del Explorador? (para Quick Look)</summary>
        private static IntPtr ExplorerWithFileFocus()
        {
            var fg = GetForegroundWindow();
            if (ClassOf(fg) != "CabinetWClass") return IntPtr.Zero;
            var gi = new GuiThreadInfo { cbSize = Marshal.SizeOf<GuiThreadInfo>() };
            if (!GetGUIThreadInfo(GetWindowThreadProcessId(fg, out _), ref gi)) return IntPtr.Zero;
            return ClassOf(gi.hwndFocus) == "DirectUIHWND" ? fg : IntPtr.Zero;
        }

        /// <summary>Atajos de mosaico (Ctrl+Alt izquierdo + tecla), como Rectangle en macOS. Pura.</summary>
        public static McBar.Core.TileLayout? TileFor(int vk) => vk switch
        {
            0x25 => McBar.Core.TileLayout.Left,
            0x27 => McBar.Core.TileLayout.Right,
            0x26 => McBar.Core.TileLayout.Top,
            0x28 => McBar.Core.TileLayout.Bottom,
            0x55 => McBar.Core.TileLayout.TopLeft,
            0x49 => McBar.Core.TileLayout.TopRight,
            0x4A => McBar.Core.TileLayout.BottomLeft,
            0x4B => McBar.Core.TileLayout.BottomRight,
            0x0D => McBar.Core.TileLayout.Fill,
            0x43 => McBar.Core.TileLayout.Center,
            _ => null
        };

        private static void Ui(Action a)
        {
            _ui?.BeginInvoke(new Action(() =>
            {
                try { a(); } catch (Exception ex) { Log.Write("Atajo: " + ex.Message); }
            }));
        }

        /// <summary>
        /// 0.6.0:
        ///  - Tecla Windows SOLA → Launchpad. Al apretarla se manda enseguida la tecla vacía (0xE8): así
        ///    Windows nunca abre Inicio al soltarla, pero Win+E, Win+D, etc. siguen funcionando.
        ///  - Win+S → buscador de McBar. Win+N / Win+A como antes.
        ///  - Teclas de volumen: McBar cambia el volumen y muestra su cartel (o la isla), no el de Windows.
        /// </summary>
        private static IntPtr Proc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    bool down = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                    bool up = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;
                    if ((k.flags & LLKHF_INJECTED) == 0)
                    {
                        var s = SettingsStore.Current;
                        int vk = (int)k.vkCode;

                        // 1.8.1: la tecla Windows se "traga" entera (Windows no la ve), así Inicio nunca se abre. Antes
                        // se mandaba la tecla vacía 0xE8, pero en algunas versiones de Windows 11 Inicio se abría igual
                        // junto con el Launchpad. Si se aprieta otra tecla con Windows (Win+E, Win+D, Win+flechas…),
                        // recién ahí se le manda a Windows "Windows apretada" + esa tecla (ver el final de Proc).
                        if (vk == VK_LWIN || vk == VK_RWIN)
                        {
                            if (down)
                            {
                                if (!_winDown)
                                {
                                    _winDown = true;
                                    _winOther = false;
                                    _winInjected = false;
                                    _winVk = vk;
                                    _winSwallowed = s.WinKeyLaunchpad || GameMode.On; // 1.3.0: en modo juego tampoco abre Inicio
                                }
                                if (_winSwallowed) return (IntPtr)1;
                            }
                            else if (up)
                            {
                                bool alone = _winDown && !_winOther;
                                bool swallowed = _winSwallowed, injected = _winInjected;
                                _winDown = false; _winSwallowed = false; _winInjected = false;
                                if (alone && s.WinKeyLaunchpad && !GameMode.On && !Down(VK_SHIFT) && !Down(VK_CONTROL) && !Down(VK_MENU))
                                    Ui(() => App.ToggleLaunchpad(App.ActiveMonitor()));
                                if (swallowed)
                                {
                                    // si Windows llegó a ver "Windows apretada", soltarla (con la tecla vacía antes: sin Inicio)
                                    if (injected) WindowOps.SendSequence(new (ushort, bool)[] { (VK_DUMMY, false), (VK_DUMMY, true), ((ushort)_winVk, true) });
                                    return (IntPtr)1;
                                }
                            }
                        }
                        else if (down && _winDown) _winOther = true;

                        // ---- Alt+Tab propio (0.8.0) ----
                        if (_switching)
                        {
                            if (up && (vk == VK_MENU || vk == VK_LMENU || vk == VK_RMENU))
                            {
                                _switching = false;
                                Ui(App.SwitcherCommit);
                            }
                            else if (vk == VK_TAB || vk == VK_LEFT || vk == VK_RIGHT)
                            {
                                if (down) { bool back = vk == VK_LEFT || (vk == VK_TAB && Down(VK_SHIFT)); Ui(() => App.SwitcherStep(back)); }
                                return (IntPtr)1;
                            }
                            else if (vk == VK_ESCAPE)
                            {
                                if (down) { _switching = false; Ui(App.SwitcherCancel); }
                                return (IntPtr)1;
                            }
                        }
                        else if (s.OwnAltTab && down && vk == VK_TAB && Down(VK_MENU) && !Down(VK_CONTROL) && !_winDown)
                        {
                            _switching = true;
                            WindowOps.SendChord(VK_DUMMY); // que al soltar Alt no se active el menú de la app
                            bool back = Down(VK_SHIFT);
                            Ui(() => App.SwitcherStep(back));
                            return (IntPtr)1; // Windows no abre su Alt+Tab
                        }

                        // ---- Capturas estilo Mac (1.8.0): Win+Mayús+3 / 4 / 5 ----
                        if (s.MacScreenshots && down && _winDown && Down(VK_SHIFT) && !Down(VK_CONTROL) && !Down(VK_MENU) && vk >= 0x33 && vk <= 0x35)
                        {
                            if (!_winSwallowed) WindowOps.SendChord(VK_DUMMY); // que no se abra Inicio al soltar
                            int which = vk - 0x33;
                            Ui(() => Screenshots.Start(which));
                            return (IntPtr)1;
                        }

                        // ---- Atajos estilo Mac (1.8.0): Alt izquierdo hace de Cmd ----
                        if (s.MacShortcuts && !_switching)
                        {
                            if (up && _eaten.Remove(vk)) return (IntPtr)1; // la tecla que ya se usó como atajo
                            if (down && !_winDown && !GameMode.On && Down(VK_LMENU) && !Down(VK_RMENU) && !Down(VK_CONTROL))
                            {
                                var mk = MacKeys.ActionFor(vk);
                                if (mk != MacKeyAction.None)
                                {
                                    _eaten.Add(vk);
                                    if (mk == MacKeyAction.Ctrl) MacKeys.SendAsCtrl((ushort)vk);
                                    else
                                    {
                                        WindowOps.SendChord(VK_DUMMY); // que al soltar Alt la app no abra su menú
                                        var fgw = GetForegroundWindow();
                                        Ui(() => MacKeys.Run(mk, fgw));
                                    }
                                    return (IntPtr)1;
                                }
                            }
                        }

                        // ---- Quick Look (1.0.0): Espacio en la lista de archivos del Explorador ----
                        if (s.QuickLook && down && vk == VK_SPACE && !_winDown && !Down(VK_SHIFT) && !Down(VK_CONTROL) && !Down(VK_MENU))
                        {
                            var ex = ExplorerWithFileFocus();
                            if (ex != IntPtr.Zero) { Ui(() => App.QuickLookFromExplorer(ex)); return (IntPtr)1; }
                        }

                        // ---- Mosaico (1.0.0): Ctrl + Alt izquierdo + flecha/U/I/J/K/Enter/C; Retroceso = como estaba ----
                        if (s.TileShortcuts && down && Down(VK_CONTROL) && Down(VK_LMENU) && !Down(VK_RMENU) && !_winDown)
                        {
                            if (vk == VK_BACK) { Ui(() => WindowTiler.Restore(GetForegroundWindow())); return (IntPtr)1; }
                            var tile = TileFor(vk);
                            if (tile != null)
                            {
                                var t = tile.Value;
                                Ui(() => WindowTiler.Apply(GetForegroundWindow(), t));
                                return (IntPtr)1;
                            }
                        }

                        if (s.OwnVolumeKeys && (vk == VK_VOLUME_MUTE || vk == VK_VOLUME_DOWN || vk == VK_VOLUME_UP))
                        {
                            if (down) Ui(() => VolumeKeys.Handle(vk));
                            return (IntPtr)1; // Windows no ve la tecla: no muestra su cartel
                        }

                        if (down && _winDown && !Down(VK_SHIFT) && !Down(VK_CONTROL) && !Down(VK_MENU))
                        {
                            Action? act = null;
                            if (s.OwnShortcuts && vk == VK_N) act = () => App.ToggleWidgets(App.ActiveMonitor());
                            else if (s.OwnShortcuts && vk == VK_A) act = () => App.ToggleControlCenter(App.ActiveMonitor());
                            else if (s.OwnSearch && vk == VK_S) act = () => App.ToggleSpotlight(App.ActiveMonitor());
                            else if (s.OwnWinTab && vk == VK_TAB) act = () => App.ToggleMissionControl(App.ActiveMonitor());
                            if (act != null)
                            {
                                if (!_winSwallowed) WindowOps.SendChord(VK_DUMMY); // que no se abra Inicio al soltar
                                Ui(act);
                                return (IntPtr)1; // Windows no abre su panel
                            }
                        }

                        // 1.8.1: Windows + otra tecla que McBar no usa → se la pasamos a Windows con "Windows apretada"
                        if (down && _winDown && _winSwallowed && !_winInjected && !GameMode.On)
                        {
                            if (vk == 0x4C /*L*/ && !Down(VK_SHIFT) && !Down(VK_CONTROL) && !Down(VK_MENU))
                            {
                                Ui(Launcher.Lock); // Win+L: bloquear (Windows no acepta "Windows" simulada para esto)
                                return (IntPtr)1;
                            }
                            _winInjected = true;
                            WindowOps.SendSequence(new (ushort, bool)[] { ((ushort)_winVk, false), ((ushort)vk, false) });
                            return (IntPtr)1;
                        }
                    }
                }
            }
            catch { }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }
}
