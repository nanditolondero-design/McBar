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

    /// <summary>Interfaz que Windows llama cuando cambia el volumen (la implementa VolumeWatcher).</summary>
    [ComVisible(true), Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr pNotify);
    }

    /// <summary>
    /// Avisa cuando cambia el volumen o el silencio (con las teclas, desde otra app, etc.), sin sondear.
    /// Los cambios hechos por el propio McBar no se avisan (llevan nuestra "marca").
    /// </summary>
    [ComVisible(true)]
    public sealed class VolumeWatcher : IAudioEndpointVolumeCallback, IDisposable
    {
        private IAudioEndpointVolume? _ep;
        private IntPtr _cb;

        /// <summary>Volumen 0..1 y silencio. Se dispara en un hilo de Windows (no el de la interfaz).</summary>
        public event Action<float, bool>? Changed;

        public bool Start()
        {
            try
            {
                _ep = CoreAudio.GetEndpoint();
                if (_ep == null) return false;
                _cb = Marshal.GetComInterfaceForObject(this, typeof(IAudioEndpointVolumeCallback));
                return _ep.RegisterControlChangeNotify(_cb) == 0;
            }
            catch (Exception ex) { McBar.Core.Log.Write("Volumen: " + ex.Message); return false; }
        }

        public int OnNotify(IntPtr p)
        {
            try
            {
                // AUDIO_VOLUME_NOTIFICATION_DATA { GUID guidEventContext; BOOL bMuted; float fMasterVolume; ... }
                var ctx = Marshal.PtrToStructure<Guid>(p);
                if (ctx == CoreAudio.McBarContext) return 0;
                bool muted = Marshal.ReadInt32(p, 16) != 0;
                float v = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(p, 20));
                Changed?.Invoke(v, muted);
            }
            catch { }
            return 0;
        }

        public void Dispose()
        {
            try
            {
                if (_ep != null && _cb != IntPtr.Zero) _ep.UnregisterControlChangeNotify(_cb);
                if (_cb != IntPtr.Zero) Marshal.Release(_cb);
                if (_ep != null) Marshal.ReleaseComObject(_ep);
            }
            catch { }
            _cb = IntPtr.Zero;
            _ep = null;
        }
    }

    internal static class CoreAudio
    {
        /// <summary>Marca de los cambios de volumen que hace McBar (para no mostrarlos en la isla).</summary>
        public static readonly Guid McBarContext = new Guid("6D63B4A0-7C1E-4E5B-9A2F-4D6342617221");

        /// <summary>1.9.43: se dispara cuando McBar cambia el volumen o el silencio (así el ícono se actualiza al instante).</summary>
        public static event Action? Changed;

        internal static IAudioEndpointVolume? GetEndpoint()
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
                var g = McBarContext;
                ep.SetMasterVolumeLevelScalar(Math.Clamp(value, 0f, 1f), ref g);
                if (value > 0.001f) ep.SetMute(false, ref g);
            }
            finally { Marshal.ReleaseComObject(ep); }
            try { Changed?.Invoke(); } catch { }
        }

        public static void SetMute(bool mute)
        {
            var ep = GetEndpoint();
            if (ep == null) return;
            try { var g = McBarContext; ep.SetMute(mute, ref g); }
            finally { Marshal.ReleaseComObject(ep); }
            try { Changed?.Invoke(); } catch { }
        }
    }
}
