// McBar — Creado por Andrés Londero
// 1.8.0 — Menús reales de la app en la barra (opcional). Muchas apps clásicas de Windows (Bloc de notas
// clásico, Paint.NET, Notepad++, 7-Zip, programas viejos…) tienen un menú de Windows (HMENU) debajo del
// título. McBar lo lee (los menús son del sistema, se pueden leer desde otro programa) y lo muestra en la
// barra como en macOS; al elegir una opción le manda a la app el mismo mensaje (WM_COMMAND) que si se la
// hubiera elegido en su propio menú. Las apps modernas (Chrome, Office, las de la Tienda) no tienen ese
// menú: con ellas se siguen viendo los menús de siempre (Archivo, Edición, Ver…).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using McBar.Native;

namespace McBar.Core
{
    internal static class AppMenus
    {
        [DllImport("user32.dll")] private static extern IntPtr GetMenu(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int GetMenuItemCount(IntPtr menu);
        [DllImport("user32.dll")] private static extern IntPtr GetSubMenu(IntPtr menu, int pos);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMenuItemInfo(IntPtr menu, uint item, bool byPos, ref MENUITEMINFO mii);
        [DllImport("user32.dll")] private static extern bool IsMenu(IntPtr menu);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MENUITEMINFO
        {
            public uint cbSize, fMask, fType, fState, wID;
            public IntPtr hSubMenu, hbmpChecked, hbmpUnchecked, dwItemData;
            public IntPtr dwTypeData;
            public uint cch;
            public IntPtr hbmpItem;
        }

        private const uint MIIM_STATE = 0x1, MIIM_ID = 0x2, MIIM_SUBMENU = 0x4, MIIM_STRING = 0x40, MIIM_FTYPE = 0x100;
        private const uint MFT_SEPARATOR = 0x800, MFT_OWNERDRAW = 0x100, MFS_DISABLED = 0x3, MFS_CHECKED = 0x8;
        private const uint WM_COMMAND = 0x0111, WM_INITMENUPOPUP = 0x0117;

        internal sealed class Entry
        {
            public string Text = "";
            public string Shortcut = "";
            public uint Id;
            public IntPtr Sub;
            public bool Separator, Disabled, Checked;
        }

        /// <summary>Separa "&amp;Guardar\tCtrl+S" en ("Guardar", "Ctrl+S"); "&amp;&amp;" queda "&amp;". Pura, se prueba.</summary>
        public static (string text, string shortcut) Clean(string raw)
        {
            raw ??= "";
            string shortcut = "";
            int tab = raw.IndexOf('\t');
            if (tab >= 0) { shortcut = raw.Substring(tab + 1).Trim(); raw = raw.Substring(0, tab); }
            var sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == '&')
                {
                    if (i + 1 < raw.Length && raw[i + 1] == '&') { sb.Append('&'); i++; }
                    continue;
                }
                sb.Append(raw[i]);
            }
            return (sb.ToString().Trim(), shortcut);
        }

        /// <summary>Menú principal de la ventana (o IntPtr.Zero si no tiene).</summary>
        public static IntPtr MainMenu(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return IntPtr.Zero;
            var m = GetMenu(hwnd);
            return m != IntPtr.Zero && IsMenu(m) && GetMenuItemCount(m) > 0 ? m : IntPtr.Zero;
        }

        public static List<Entry> Read(IntPtr menu)
        {
            var list = new List<Entry>();
            int n = GetMenuItemCount(menu);
            for (int i = 0; i < n && i < 80; i++)
            {
                var buf = Marshal.AllocHGlobal(512 * 2);
                try
                {
                    var mii = new MENUITEMINFO { cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(), fMask = MIIM_STATE | MIIM_ID | MIIM_SUBMENU | MIIM_STRING | MIIM_FTYPE, dwTypeData = buf, cch = 511 };
                    if (!GetMenuItemInfo(menu, (uint)i, true, ref mii)) continue;
                    var e = new Entry
                    {
                        Id = mii.wID, Sub = mii.hSubMenu,
                        Separator = (mii.fType & MFT_SEPARATOR) != 0,
                        Disabled = (mii.fState & MFS_DISABLED) != 0,
                        Checked = (mii.fState & MFS_CHECKED) != 0,
                    };
                    if (!e.Separator)
                    {
                        var raw = (mii.fType & MFT_OWNERDRAW) != 0 ? "" : Marshal.PtrToStringUni(buf) ?? "";
                        (e.Text, e.Shortcut) = Clean(raw);
                        if (e.Text.Length == 0) continue; // dibujadas por la app: no se pueden mostrar
                    }
                    list.Add(e);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            // sin separadores repetidos ni en las puntas
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].Separator && (i == 0 || i == list.Count - 1 || list[i - 1].Separator)) list.RemoveAt(i);
            return list;
        }

        /// <summary>Le pide a la app que actualice su submenú (qué está habilitado, tildado…) antes de leerlo.</summary>
        private static void Init(IntPtr hwnd, IntPtr sub, int pos)
        {
            Win32.SendMessageTimeout(hwnd, WM_INITMENUPOPUP, sub, (IntPtr)(pos & 0xFFFF), Win32.SMTO_ABORTIFHUNG, 150, out _);
        }

        /// <summary>Arma el menú desplegable de WPF para el submenú "pos" del menú principal.</summary>
        public static ContextMenu Build(IntPtr hwnd, IntPtr sub, int pos)
        {
            var cm = new ContextMenu();
            Fill(cm.Items, hwnd, sub, pos, 0);
            if (cm.Items.Count == 0) cm.Items.Add(new MenuItem { Header = "(vacío)", IsEnabled = false });
            return cm;
        }

        private static void Fill(ItemCollection items, IntPtr hwnd, IntPtr menu, int pos, int depth)
        {
            Init(hwnd, menu, pos);
            var entries = Read(menu);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Separator) { items.Add(new Separator()); continue; }
                var mi = new MenuItem { Header = e.Text.Replace("_", "__"), IsEnabled = !e.Disabled, IsChecked = e.Checked };
                if (e.Shortcut.Length > 0) mi.InputGestureText = e.Shortcut;
                if (e.Sub != IntPtr.Zero)
                {
                    if (depth < 4) Fill(mi.Items, hwnd, e.Sub, i, depth + 1);
                }
                else
                {
                    uint id = e.Id;
                    mi.Click += (_, _) =>
                    {
                        WindowOps.Activate(hwnd);
                        Win32.PostMessage(hwnd, WM_COMMAND, (IntPtr)(id & 0xFFFF), IntPtr.Zero);
                    };
                }
                items.Add(mi);
            }
        }
    }
}
