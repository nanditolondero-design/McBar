// McBar — Creado por Andrés Londero
// Dispositivos de audio (0.7.0): listar parlantes/auriculares y micrófonos, y elegir el predeterminado
// sin abrir Configuración. Listar usa Core Audio (IMMDeviceEnumerator); cambiar el predeterminado usa
// IPolicyConfig, la interfaz que usa el propio panel de Sonido de Windows (no está documentada pero es
// estable desde Windows 7; es la que usan EarTrumpet, SoundSwitch, etc.).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using McBar.Core;

namespace McBar.Native
{
    public sealed class AudioDevice
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public bool IsDefault { get; init; }
        public bool IsInput { get; init; }
    }

    internal static class AudioDevices
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorClass { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDevEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IDevice endpoint);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
            [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out int state);
        }

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey { public Guid fmtid; public int pid; }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PropVariant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pointer;
        }

        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant pvar);

        // IPolicyConfig (Windows 10/11): solo importa el orden de los métodos hasta SetDefaultEndpoint.
        [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
        private class PolicyConfigClient { }

        [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
            [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
            [PreserveSig] int ResetDeviceFormat(IntPtr a);
            [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
            [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
            [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
            [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
            [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
            [PreserveSig] int GetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
            [PreserveSig] int SetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        }

        private static PropertyKey FriendlyName = new PropertyKey { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };

        /// <summary>Salidas (input=false) o entradas (input=true) activas, con la predeterminada marcada.</summary>
        public static List<AudioDevice> List(bool input)
        {
            var list = new List<AudioDevice>();
            try
            {
                var en = (IDevEnumerator)new MMDeviceEnumeratorClass();
                int flow = input ? 1 : 0;
                string? def = null;
                if (en.GetDefaultAudioEndpoint(flow, 0 /*eConsole*/, out var d) == 0 && d != null)
                {
                    d.GetId(out def);
                    Marshal.ReleaseComObject(d);
                }
                if (en.EnumAudioEndpoints(flow, 1 /*DEVICE_STATE_ACTIVE*/, out var col) != 0 || col == null) return list;
                col.GetCount(out var n);
                for (uint i = 0; i < n; i++)
                {
                    if (col.Item(i, out var dev) != 0 || dev == null) continue;
                    try
                    {
                        dev.GetId(out var id);
                        string name = id;
                        if (dev.OpenPropertyStore(0 /*STGM_READ*/, out var store) == 0 && store != null)
                        {
                            var key = FriendlyName;
                            if (store.GetValue(ref key, out var pv) == 0)
                            {
                                if (pv.vt == 31 /*VT_LPWSTR*/ && pv.pointer != IntPtr.Zero) name = Marshal.PtrToStringUni(pv.pointer) ?? id;
                                PropVariantClear(ref pv);
                            }
                            Marshal.ReleaseComObject(store);
                        }
                        list.Add(new AudioDevice { Id = id, Name = name, IsDefault = id == def, IsInput = input });
                    }
                    finally { Marshal.ReleaseComObject(dev); }
                }
                Marshal.ReleaseComObject(col);
                Marshal.ReleaseComObject(en);
            }
            catch (Exception ex) { Log.Write("Dispositivos de audio: " + ex.Message); }
            return list;
        }

        /// <summary>Hace predeterminado el dispositivo para todo (música, llamadas y sistema).</summary>
        public static bool SetDefault(string id)
        {
            try
            {
                var pc = (IPolicyConfig)new PolicyConfigClient();
                int r = 0;
                for (int role = 0; role < 3; role++) r |= pc.SetDefaultEndpoint(id, role);
                Marshal.ReleaseComObject(pc);
                return r == 0;
            }
            catch (Exception ex) { Log.Write("Cambiar salida de audio: " + ex.Message); return false; }
        }
    }
}
