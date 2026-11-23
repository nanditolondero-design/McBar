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

        /// <summary>
        /// Ventana COMÚN con fondo transparente (el "vidrio" de Windows extendido a toda la ventana).
        /// La dibuja la tarjeta de video directamente: mucho más rápida que una ventana "en capas"
        /// (AllowsTransparency), que Windows copia entera en cada cuadro. La forma (dónde recibe clics) se
        /// define con SetShape. Requiere AllowsTransparency = false y Background = Transparent.
        /// </summary>
        public static void MakeGlass(Window w)
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                var src = HwndSource.FromHwnd(h);
                if (src?.CompositionTarget != null) src.CompositionTarget.BackgroundColor = Colors.Transparent;
                var m = new Win32.MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
                Win32.DwmExtendFrameIntoClientArea(h, ref m);
            }
            catch (Exception ex) { McBar.Core.Log.Write("Vidrio: " + ex.Message); }
        }

        /// <summary>
        /// 1.8.3: ventana normal (con barra de título) de vidrio, como el vidrio del iPhone: el fondo de Windows se
        /// ve difuminado a través. En Windows 11 22H2 o más nuevo usa el acrílico del sistema (DWMWA_SYSTEMBACKDROP_TYPE);
        /// en Windows 10 el desenfoque de siempre. La transparencia la da el fondo de WPF (Mc.SettingsBg).
        /// </summary>
        public static void SetGlassBackdrop(Window w, bool on)
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;
                var src = HwndSource.FromHwnd(h);
                int build = Environment.OSVersion.Version.Build;
                var m = on ? new Win32.MARGINS { left = -1, right = -1, top = -1, bottom = -1 } : new Win32.MARGINS();
                if (src?.CompositionTarget != null) src.CompositionTarget.BackgroundColor = on ? Colors.Transparent : SystemColors.WindowColor;
                Win32.DwmExtendFrameIntoClientArea(h, ref m);
                if (build >= 22621)
                {
                    int type = on ? 3 /*acrílico*/ : 1 /*nada*/;
                    Win32.DwmSetWindowAttribute(h, 38 /*DWMWA_SYSTEMBACKDROP_TYPE*/, ref type, 4);
                }
                else if (on) EnableBlur(w, Color.FromArgb(0x30, 0x18, 0x18, 0x1E));
                else DisableBlur(w);
            }
            catch (Exception ex) { McBar.Core.Log.Write("Vidrio de la ventana: " + ex.Message); }
        }

        /// <summary>Forma de la ventana (en píxeles de la ventana): rectángulo redondeado + rectángulos extra.</summary>
        public static void SetShape(IntPtr hwnd, int x, int y, int w, int h, int radius, params (int x, int y, int w, int h)[] extra)
        {
            if (hwnd == IntPtr.Zero) return;
            var rgn = Win32.CreateRoundRectRgn(x, y, x + Math.Max(1, w) + 1, y + Math.Max(1, h) + 1, radius * 2, radius * 2);
            foreach (var r in extra)
            {
                if (r.w <= 0 || r.h <= 0) continue;
                var add = Win32.CreateRectRgn(r.x, r.y, r.x + r.w, r.y + r.h);
                Win32.CombineRgn(rgn, rgn, add, Win32.RGN_OR);
                Win32.DeleteObject(add);
            }
            Win32.SetWindowRgn(hwnd, rgn, true); // Windows se queda con la región
        }

        /// <summary>Quita la forma (toda la ventana recibe clics).</summary>
        public static void ClearShape(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero) Win32.SetWindowRgn(hwnd, IntPtr.Zero, true);
        }

        /// <summary>Saca el desenfoque (vuelve a un fondo normal).</summary>
        public static void DisableBlur(Window w)
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                var accent = new Win32.AccentPolicy { AccentState = 0 };
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
            catch { }
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

        /// <summary>
        /// 1.5.0: pone una ventana de McBar en un monitor, en píxeles reales de ESE monitor. Con monitores de
        /// escalas distintas, Left/Top de WPF (en unidades de la escala del principal) la dejaban corrida en el otro.
        /// <paramref name="leftDip"/>/<paramref name="topDip"/> son distancias desde la esquina del monitor, en
        /// unidades de su escala; ancho y alto (opcionales) también.
        /// </summary>
        public static void PlaceOnMonitor(Window w, MonitorInfo m, double leftDip, double topDip, double? widthDip = null, double? heightDip = null)
        {
            double sc = m.Scale;
            int x = m.Bounds.Left + (int)Math.Round(leftDip * sc), y = m.Bounds.Top + (int)Math.Round(topDip * sc);
            if (widthDip != null) w.Width = widthDip.Value;
            if (heightDip != null) w.Height = heightDip.Value;
            // si la ventana todavía no existe, primero en unidades de WPF (aproximado)…
            if (new WindowInteropHelper(w).Handle == IntPtr.Zero)
            {
                double sys = Win32.SystemScale();
                w.Left = x / sys; w.Top = y / sys;
            }
            void Exact()
            {
                var h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;
                uint flags = 0x0004 | 0x0010; // NOZORDER | NOACTIVATE
                int pw = 0, ph = 0;
                if (widthDip != null && heightDip != null) { pw = (int)Math.Round(widthDip.Value * sc); ph = (int)Math.Round(heightDip.Value * sc); }
                else flags |= 0x0001; // NOSIZE
                Win32.SetWindowPos(h, IntPtr.Zero, x, y, pw, ph, flags);
            }
            // …y después en píxeles exactos, ya con la ventana creada
            if (new WindowInteropHelper(w).Handle != IntPtr.Zero) Exact();
            else w.SourceInitialized += (_, _) => Exact();
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

        /// <summary>1.8.0: manda una secuencia de teclas tal cual (tecla, ¿soltar?).</summary>
        public static void SendSequence((ushort vk, bool up)[] keys)
        {
            var inputs = new Win32.INPUT[keys.Length];
            for (int i = 0; i < keys.Length; i++) inputs[i] = Key(keys[i].vk, keys[i].up);
            Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
        }

        private static Win32.INPUT Key(ushort vk, bool up)
        {
            uint flags = up ? Win32.KEYEVENTF_KEYUP : 0;
            // teclas "extendidas" (1.8.1: también Windows derecha, flechas, Inicio/Fin, RePág/AvPág, Insert y Supr)
            if (vk == VK_LWIN || vk == 0x5C || (vk >= 0x21 && vk <= 0x28) || vk == 0x2D || vk == 0x2E) flags |= Win32.KEYEVENTF_EXTENDEDKEY;
            return new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                U = new Win32.InputUnion { ki = new Win32.KEYBDINPUT { wVk = vk, dwFlags = flags } }
            };
        }
    }
}
