// McBar — Creado por Andrés Londero
// Volumen general de Windows (Core Audio). Interop mínima: solo leer/cambiar volumen y silencio
// del dispositivo de salida predeterminado.
using System;
using System.Runtime.InteropServices;

namespace McBar.Native
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorCoClass { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    internal static class CoreAudio
    {
        private static IAudioEndpointVolume? GetEndpoint()
        {
            try
            {
                var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCoClass();
                if (en.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out var dev) != 0 || dev == null) return null;
                var iid = typeof(IAudioEndpointVolume).GUID;
                if (dev.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out var o) != 0) return null;
                return o as IAudioEndpointVolume;
            }
            catch { return null; }
        }

        /// <summary>Volumen 0..1, o null si no hay dispositivo de audio.</summary>
        public static float? GetVolume()
        {
            var ep = GetEndpoint();
            if (ep == null) return null;
            try { return ep.GetMasterVolumeLevelScalar(out var v) == 0 ? v : null; }
            finally { Marshal.ReleaseComObject(ep); }
        }

        public static bool GetMute()
        {
            var ep = GetEndpoint();
            if (ep == null) return false;
            try { return ep.GetMute(out var m) == 0 && m; }
            finally { Marshal.ReleaseComObject(ep); }
        }

        public static void SetVolume(float value)
        {
            var ep = GetEndpoint();
            if (ep == null) return;
            try
            {
                var g = Guid.Empty;
                ep.SetMasterVolumeLevelScalar(Math.Clamp(value, 0f, 1f), ref g);
                if (value > 0.001f) ep.SetMute(false, ref g);
            }
            finally { Marshal.ReleaseComObject(ep); }
        }

        public static void SetMute(bool mute)
        {
            var ep = GetEndpoint();
            if (ep == null) return;
            try { var g = Guid.Empty; ep.SetMute(mute, ref g); }
            finally { Marshal.ReleaseComObject(ep); }
        }
    }
}
