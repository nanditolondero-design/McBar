// McBar — Creado por Andrés Londero
// 1.8.0 — Atajos estilo Mac (opcional): la tecla Alt izquierda hace de Cmd.
//   Alt+C/V/X/Z/A/S/F/T/W/N/P/R/O → lo mismo que Ctrl+… (copiar, pegar, cortar, deshacer, todo, guardar,
//   buscar, pestaña nueva, cerrar pestaña, nueva ventana, imprimir, recargar, abrir).
//   Alt+Q → cerrar la app · Alt+M → minimizar · Alt+H → ocultar la app · Alt+Espacio → buscador de McBar.
// Alt+Tab, Alt+F4 y el resto siguen como siempre. El gancho (Native/KeyboardHook) llama a ActionFor.
using System;
using System.Collections.Generic;
using McBar.Native;

namespace McBar.Core
{
    public enum MacKeyAction { None, Ctrl, Quit, Minimize, Hide, Search }

    public static class MacKeys
    {
        /// <summary>Qué hace Alt + esa tecla. Pura, se prueba.</summary>
        public static MacKeyAction ActionFor(int vk) => vk switch
        {
            0x41 or 0x43 or 0x46 or 0x4E or 0x4F or 0x50 or 0x52 or 0x53 or 0x54 or 0x56 or 0x57 or 0x58 or 0x5A => MacKeyAction.Ctrl,
            0x51 => MacKeyAction.Quit,
            0x4D => MacKeyAction.Minimize,
            0x48 => MacKeyAction.Hide,
            0x20 => MacKeyAction.Search,
            _ => MacKeyAction.None
        };

        private const ushort VK_CONTROL = 0x11, VK_LMENU = 0xA4, VK_DUMMY = 0xE8;

        /// <summary>
        /// Manda Ctrl+tecla mientras Alt sigue apretada: suelta Alt (falso), Ctrl+tecla, vuelve a apretar Alt y
        /// manda la tecla vacía para que al soltar Alt la app no abra su menú.
        /// </summary>
        internal static void SendAsCtrl(ushort vk)
        {
            WindowOps.SendSequence(new (ushort, bool)[]
            {
                (VK_LMENU, true), (VK_CONTROL, false), (vk, false), (vk, true), (VK_CONTROL, true),
                (VK_LMENU, false), (VK_DUMMY, false), (VK_DUMMY, true)
            });
        }

        /// <summary>Ventanas visibles de la app (mismo proceso) que tiene la ventana.</summary>
        private static List<IntPtr> AppWindows(IntPtr fg)
        {
            var list = new List<IntPtr>();
            if (fg == IntPtr.Zero) return list;
            uint pid = Win32.ProcessOf(fg);
            if (pid == 0 || pid == (uint)Environment.ProcessId) return list;
            Win32.EnumWindows((h, _) =>
            {
                if (Win32.ProcessOf(h) != pid || !Win32.IsWindowVisible(h)) return true;
                if (Win32.GetWindow(h, Win32.GW_OWNER) != IntPtr.Zero) return true;
                if ((Win32.GetExStyle(h) & Win32.WS_EX_TOOLWINDOW) != 0) return true;
                var c = WindowOps.GetClass(h);
                if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return true;
                list.Add(h);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        internal static void Run(MacKeyAction a, IntPtr fg)
        {
            switch (a)
            {
                case MacKeyAction.Quit: foreach (var h in AppWindows(fg)) WindowOps.Close(h); break;
                case MacKeyAction.Minimize: if (AppWindows(fg).Contains(fg)) WindowOps.Minimize(fg); break;
                case MacKeyAction.Hide: foreach (var h in AppWindows(fg)) WindowOps.Minimize(h); break;
                case MacKeyAction.Search: App.ToggleSpotlight(App.ActiveMonitor()); break;
            }
        }
    }
}
