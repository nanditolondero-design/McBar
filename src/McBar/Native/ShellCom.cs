// McBar — Creado por Andrés Londero
// Interfaces COM del Shell de Windows: íconos en alta resolución (IShellItemImageFactory),
// recorrer la carpeta de aplicaciones (shell:AppsFolder), resolver accesos directos (.lnk)
// y leer el AppUserModelID de una ventana (para agrupar ventanas de la misma app).
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace McBar.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE { public int cx, cy; public SIZE(int x, int y) { cx = x; cy = y; } }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnumShellItems
    {
        [PreserveSig] int Next(uint celt, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IShellItem[] rgelt, out uint pceltFetched);
        [PreserveSig] int Skip(uint celt);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumShellItems ppenum);
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr p;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        [PreserveSig] int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPersistFile
    {
        [PreserveSig] int GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        [PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLinkCoClass { }

    internal static class ShellCom
    {
        public static Guid IID_IShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
        public static Guid IID_IShellItemImageFactory = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
        public static Guid IID_IEnumShellItems = new Guid("70629033-e363-4a28-a567-0db78006e6d7");
        public static Guid IID_IPropertyStore = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        public static Guid BHID_EnumItems = new Guid("94f60519-2850-4924-aa5a-d15e84868039");

        public const uint SIGDN_NORMALDISPLAY = 0;
        public const uint SIGDN_PARENTRELATIVEPARSING = 0x80018001;
        public const int SIIGBF_BIGGERSIZEOK = 0x1;
        public const int SIIGBF_ICONONLY = 0x4;
        public const ushort VT_LPWSTR = 31;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object? ppv);

        [DllImport("shell32.dll", PreserveSig = true)]
        public static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object? ppv);

        [DllImport("ole32.dll")]
        public static extern int PropVariantClear(ref PROPVARIANT pvar);

        private static Guid BHID_DataObject = new Guid("B8C0BD9F-ED24-455c-83E6-D5390C4FE8C4");
        private static Guid IID_IDataObject = new Guid("0000010e-0000-0000-C000-000000000046");

        /// <summary>
        /// Datos de arrastre del Shell para un elemento (por ejemplo "shell:AppsFolder\..."): lo mismo que
        /// arrastra el menú Inicio. Al soltarlo en el escritorio, Windows crea un acceso directo. 0.4.3.
        /// </summary>
        public static System.Windows.IDataObject? ShellDataObject(string parsingName)
        {
            try
            {
                var riid = IID_IShellItem;
                if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref riid, out var obj) != 0 || obj is not IShellItem item) return null;
                var bhid = BHID_DataObject;
                var iid = IID_IDataObject;
                item.BindToHandler(IntPtr.Zero, ref bhid, ref iid, out var dobj);
                return dobj == null ? null : new System.Windows.DataObject(dobj);
            }
            catch { return null; }
        }

        public static string? GetDisplayName(IShellItem item, uint sigdn)
        {
            try
            {
                item.GetDisplayName(sigdn, out var p);
                try { return Marshal.PtrToStringUni(p); } finally { Marshal.FreeCoTaskMem(p); }
            }
            catch { return null; }
        }

        /// <summary>AppUserModelID que la ventana declara (Chrome, Edge, Office, etc. lo usan para agrupar).</summary>
        public static string? GetWindowAppId(IntPtr hwnd)
        {
            object? o = null;
            try
            {
                if (SHGetPropertyStoreForWindow(hwnd, ref IID_IPropertyStore, out o) != 0 || o is not IPropertyStore store) return null;
                var key = new PROPERTYKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
                if (store.GetValue(ref key, out var pv) != 0) return null;
                try { return pv.vt == VT_LPWSTR && pv.p != IntPtr.Zero ? Marshal.PtrToStringUni(pv.p) : null; }
                finally { PropVariantClear(ref pv); }
            }
            catch { return null; }
            finally { if (o != null) Marshal.ReleaseComObject(o); }
        }

        /// <summary>Destino real (.exe) de un acceso directo .lnk, o null.</summary>
        public static string? ResolveShortcut(string lnkPath)
        {
            object? link = null;
            try
            {
                link = new ShellLinkCoClass();
                ((IPersistFile)link).Load(lnkPath, 0);
                var sb = new StringBuilder(1024);
                ((IShellLinkW)link).GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                var s = sb.ToString();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            catch { return null; }
            finally { if (link != null) Marshal.ReleaseComObject(link); }
        }
    }
}
