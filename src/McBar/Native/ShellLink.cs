// McBar — Creado por Andrés Londero
// 1.9.18: crea accesos directos (.lnk) con la interfaz nativa IShellLink en vez de WScript.Shell. En las apps
// de un solo archivo (como McBar) el método por COM/WScript a veces fallaba sin aviso y no quedaba el acceso
// directo en el menú Inicio ni en el escritorio.
using System;
using System.Runtime.InteropServices;

namespace McBar.Native
{
    internal static class ShellLink
    {
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")] private class ShellLinkObj { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, uint flags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short wHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int iShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        /// <summary>Crea (o reemplaza) un acceso directo. Devuelve true si quedó el archivo .lnk.</summary>
        public static bool Create(string linkPath, string target, string workingDir, string description, string iconPath, int iconIndex = 0, string? args = null)
        {
            IShellLinkW? link = null;
            try
            {
                link = (IShellLinkW)new ShellLinkObj();
                link.SetPath(target);
                link.SetWorkingDirectory(workingDir);
                link.SetDescription(description);
                if (!string.IsNullOrEmpty(args)) link.SetArguments(args);
                link.SetIconLocation(iconPath, iconIndex);
                ((IPersistFile)link).Save(linkPath, true);
                return System.IO.File.Exists(linkPath);
            }
            catch (Exception ex) { Core.Log.Write("Acceso directo (IShellLink): " + ex.Message); return false; }
            finally { if (link != null) Marshal.ReleaseComObject(link); }
        }
    }
}
