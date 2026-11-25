// McBar — Creado por Andrés Londero
// 1.9.32: lee los íconos que YA están en la bandeja real de Windows (la del Explorador, que McBar esconde).
// Hace falta porque muchas apps (NVIDIA, Seguridad de Windows/Defender y otras que ya estaban abiertas antes de
// McBar) no se re-registran cuando McBar avisa "se creó una barra nueva" (TaskbarCreated). Esas apps nunca mandan
// su ícono a la ventana Shell_TrayWnd de McBar, así que no aparecían ni se podían abrir. Acá se leen directamente
// de la barra de herramientas de la bandeja del Explorador, leyendo la memoria de ese proceso (es lo mismo que
// hacen las herramientas que reemplazan la barra de Windows). Por eso McBar tiene que correr como la app normal
// (integridad media, igual que el Explorador); si corriera como administrador, no podría leerla.
using System;
using System.Collections.Generic;

namespace McBar.Native
{
    internal static class TrayImport
    {
        public struct Entry { public IntPtr Hwnd; public uint Uid; public uint Callback; public uint HIcon; }

        /// <summary>Lee la bandeja visible y la de íconos ocultos. Nunca tira (devuelve lo que pudo leer).</summary>
        public static List<Entry> Read()
        {
            var list = new List<Entry>();
            foreach (var tb in Toolbars())
            {
                try { ReadToolbar(tb, list); } catch (Exception ex) { Log.Write("Importar bandeja: " + ex.Message); }
            }
            return list;
        }

        private static IEnumerable<IntPtr> Toolbars()
        {
            // Bandeja visible: Shell_TrayWnd > TrayNotifyWnd > SysPager > ToolbarWindow32
            var tray = Win32.FindWindow("Shell_TrayWnd", null); // la REAL del Explorador (la nuestra no tiene estos hijos)
            var notify = tray != IntPtr.Zero ? Win32.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null) : IntPtr.Zero;
            if (notify != IntPtr.Zero)
            {
                var pager = Win32.FindWindowEx(notify, IntPtr.Zero, "SysPager", null);
                var tb = pager != IntPtr.Zero
                    ? Win32.FindWindowEx(pager, IntPtr.Zero, "ToolbarWindow32", null)
                    : Win32.FindWindowEx(notify, IntPtr.Zero, "ToolbarWindow32", null);
                if (tb != IntPtr.Zero) yield return tb;
            }
            // Íconos ocultos (el "^" que despliega los que no entran): NotifyIconOverflowWindow > ToolbarWindow32
            var overflow = Win32.FindWindow("NotifyIconOverflowWindow", null);
            var otb = overflow != IntPtr.Zero ? Win32.FindWindowEx(overflow, IntPtr.Zero, "ToolbarWindow32", null) : IntPtr.Zero;
            if (otb != IntPtr.Zero) yield return otb;
        }

        private static void ReadToolbar(IntPtr tb, List<Entry> list)
        {
            Win32.GetWindowThreadProcessId(tb, out uint pid);
            if (pid == 0) return;
            var hProc = Win32.OpenProcess(Win32.PROCESS_VM_OPERATION | Win32.PROCESS_VM_READ | Win32.PROCESS_VM_WRITE | Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero) return;
            IntPtr remote = IntPtr.Zero;
            try
            {
                int count = (int)Win32.SendMessage(tb, Win32.TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt64();
                if (count <= 0 || count > 1000) return;
                remote = Win32.VirtualAllocEx(hProc, IntPtr.Zero, (IntPtr)256, Win32.MEM_COMMIT | Win32.MEM_RESERVE, Win32.PAGE_READWRITE);
                if (remote == IntPtr.Zero) return;
                var tbbtn = new byte[32];   // TBBUTTON en 64 bits: dwData en offset 16, iString en 24
                var tray = new byte[32];    // TRAYDATA: hwnd@0 uID@8 uCallback@12 (reservado@16) hIcon@24
                for (int i = 0; i < count; i++)
                {
                    if (Win32.SendMessage(tb, Win32.TB_GETBUTTON, (IntPtr)i, remote) == IntPtr.Zero) continue;
                    if (!Win32.ReadProcessMemory(hProc, remote, tbbtn, (IntPtr)tbbtn.Length, out _)) continue;
                    long dwData = BitConverter.ToInt64(tbbtn, 16);
                    if (dwData == 0) continue;
                    if (!Win32.ReadProcessMemory(hProc, (IntPtr)dwData, tray, (IntPtr)tray.Length, out _)) continue;
                    long hwnd = BitConverter.ToInt64(tray, 0);
                    if (hwnd == 0) continue;
                    list.Add(new Entry
                    {
                        Hwnd = (IntPtr)hwnd,
                        Uid = BitConverter.ToUInt32(tray, 8),
                        Callback = BitConverter.ToUInt32(tray, 12),
                        HIcon = (uint)BitConverter.ToInt64(tray, 24)
                    });
                }
            }
            finally
            {
                if (remote != IntPtr.Zero) Win32.VirtualFreeEx(hProc, remote, IntPtr.Zero, Win32.MEM_RELEASE);
                Win32.CloseHandle(hProc);
            }
        }
    }
}
