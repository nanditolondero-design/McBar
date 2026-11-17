// McBar — Creado por Andrés Londero
// Operaciones sobre ventanas: estilos "que no roban el foco", desenfoque acrílico, activar ventanas
// de otras apps y enviar atajos de teclado a la app activa (para los menús de la barra superior).
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace McBar.Native
{
    internal static class WindowOps
    {
        /// <summary>La ventana no aparece en Alt+Tab y no se activa al hacerle clic (como el Dock de macOS).</summary>
        public static void MakeToolNoActivate(Window w)
        {
            var h = new WindowInteropHelper(w).Handle;
            var ex = Win32.GetExStyle(h);
            Win32.SetExStyle(h, ex | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW);
        }

        /// <summary>Solo oculta de Alt+Tab (la ventana sí puede recibir el foco, p. ej. para escribir).</summary>
        public static void MakeTool(Window w)
        {
            var h = new WindowInteropHelper(w).Handle;
            Win32.SetExStyle(h, Win32.GetExStyle(h) | Win32.WS_EX_TOOLWINDOW);
        }

        /// <summary>Desenfoque acrílico detrás de la ventana. Si Windows no lo soporta, no pasa nada (queda el fondo semitransparente).</summary>
        public static void EnableBlur(Window w, Color tint)
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                int build = Environment.OSVersion.Version.Build;
                var accent = new Win32.AccentPolicy
                {
                    AccentState = build >= 17134 ? 4 : 3, // 4 = acrílico, 3 = desenfoque simple
                    AccentFlags = 2,
                    GradientColor = ((uint)tint.A << 24) | ((uint)tint.B << 16) | ((uint)tint.G << 8) | tint.R
                };
                int size = Marshal.SizeOf(accent);
                var ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(accent, ptr, false);
                    var data = new Win32.WindowCompositionAttributeData { Attribute = 19, Data = ptr, SizeOfData = size };
                    Win32.SetWindowCompositionAttribute(h, ref data);
                }
                finally { Marshal.FreeHGlobal(ptr); }
            }
            catch { /* sin desenfoque */ }
        }

        public static void SetDarkTitleBar(Window w, bool dark)
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                int v = dark ? 1 : 0;
                Win32.DwmSetWindowAttribute(h, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, 4);
            }
            catch { }
        }

        public static double DpiScale(Visual v)
        {
            var src = PresentationSource.FromVisual(v);
            return src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        }

        /// <summary>Trae una ventana al frente (la restaura si estaba minimizada).</summary>
        public static void Activate(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            if (Win32.IsIconic(hwnd)) Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
            if (Win32.SetForegroundWindow(hwnd)) return;

            // Plan B: unir temporalmente la entrada con el hilo que tiene el foco.
            var fg = Win32.GetForegroundWindow();
            uint fgThread = Win32.GetWindowThreadProcessId(fg, out _);
            uint me = Win32.GetCurrentThreadId();
            if (fgThread != 0 && fgThread != me)
            {
                Win32.AttachThreadInput(me, fgThread, true);
                Win32.BringWindowToTop(hwnd);
                Win32.SetForegroundWindow(hwnd);
                Win32.AttachThreadInput(me, fgThread, false);
            }
        }

        public static void Minimize(IntPtr hwnd) { if (hwnd != IntPtr.Zero) Win32.ShowWindow(hwnd, Win32.SW_MINIMIZE); }
        public static void ToggleZoom(IntPtr hwnd) { if (hwnd != IntPtr.Zero) Win32.ShowWindow(hwnd, Win32.IsZoomed(hwnd) ? Win32.SW_RESTORE : Win32.SW_MAXIMIZE); }
        public static void Close(IntPtr hwnd) { if (hwnd != IntPtr.Zero) Win32.PostMessage(hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); }

        public static string GetTitle(IntPtr hwnd)
        {
            int len = Win32.GetWindowTextLength(hwnd);
            if (len <= 0) return string.Empty;
            var sb = new StringBuilder(len + 1);
            Win32.GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string GetClass(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            Win32.GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        // ---------- Teclado ----------
        public const ushort VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B,
            VK_LEFT = 0x25, VK_RIGHT = 0x27, VK_F1 = 0x70, VK_F11 = 0x7A, VK_OEM_PLUS = 0xBB, VK_OEM_MINUS = 0xBD;

        /// <summary>Activa la ventana destino y le manda la combinación de teclas (p. ej. Ctrl+C).</summary>
        public static async void SendChordTo(IntPtr target, params ushort[] keys)
        {
            if (target != IntPtr.Zero)
            {
                Activate(target);
                await Task.Delay(70);
            }
            SendChord(keys);
        }

        public static void SendChord(params ushort[] keys)
        {
            var inputs = new Win32.INPUT[keys.Length * 2];
            for (int i = 0; i < keys.Length; i++)
                inputs[i] = Key(keys[i], false);
            for (int i = 0; i < keys.Length; i++)
                inputs[keys.Length + i] = Key(keys[keys.Length - 1 - i], true);
            Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
        }

        private static Win32.INPUT Key(ushort vk, bool up)
        {
            uint flags = up ? Win32.KEYEVENTF_KEYUP : 0;
            if (vk == VK_LWIN || vk == VK_LEFT || vk == VK_RIGHT) flags |= Win32.KEYEVENTF_EXTENDEDKEY;
            return new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                U = new Win32.InputUnion { ki = new Win32.KEYBDINPUT { wVk = vk, dwFlags = flags } }
            };
        }
    }
}
