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
//  - Se activa solo con Preferencias → Notificaciones → "Usar Win+N y Win+A de McBar".
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

        private static IntPtr Proc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
                {
                    var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    if ((k.flags & LLKHF_INJECTED) == 0 && (k.vkCode == VK_N || k.vkCode == VK_A)
                        && (Down(VK_LWIN) || Down(VK_RWIN))
                        && !Down(VK_SHIFT) && !Down(VK_CONTROL) && !Down(VK_MENU))
                    {
                        // Tecla "vacía" para que al soltar Windows no se abra el menú Inicio.
                        WindowOps.SendChord(VK_DUMMY);
                        bool notif = k.vkCode == VK_N;
                        _ui?.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                var m = App.ActiveMonitor();
                                if (notif) App.ToggleWidgets(m); else App.ToggleControlCenter(m);
                            }
                            catch (Exception ex) { Log.Write("KeyboardHook acción: " + ex.Message); }
                        }));
                        return (IntPtr)1; // nos comemos la tecla: Windows no abre su panel
                    }
                }
            }
            catch { }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }
}
