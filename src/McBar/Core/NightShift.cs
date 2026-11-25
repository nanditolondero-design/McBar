// McBar — Creado por Andrés Londero
// 1.9.23: Modo noche (como Night Shift de macOS). De noche (o siempre, si se elige) entibia los colores de la
// pantalla para descansar la vista. Usa la rampa de gamma del sistema (SetDeviceGammaRamp): baja el azul y un
// poco el verde según la intensidad. Al apagarlo o salir, se devuelve la rampa normal.
using System;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Core
{
    public static class NightShift
    {
        private static DispatcherTimer? _timer;
        private static bool _warm; // true si ahora está entibiada

        public static void Start()
        {
            SettingsStore.Changed += Apply;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _timer.Tick += (_, _) => Apply();
            _timer.Start();
            Apply();
        }

        /// <summary>¿Corresponde entibiar ahora, según los ajustes y la hora? Pura, se prueba.</summary>
        public static bool ShouldWarm(bool enabled, bool allDay, DateTime now)
        {
            if (!enabled) return false;
            if (allDay) return true;
            int h = now.Hour;
            return h >= 20 || h < 7; // de 20:00 a 07:00
        }

        public static void Apply()
        {
            var s = SettingsStore.Current;
            bool want = ShouldWarm(s.NightShift, s.NightShiftAllDay, DateTime.Now);
            if (want) SetRamp(Math.Clamp(s.NightShiftStrength, 0, 100) / 100.0);
            else if (_warm) Reset();
        }

        /// <summary>Arma la rampa (256 por canal) para una intensidad 0..1. Pura, se prueba.</summary>
        public static ushort[] BuildRamp(double strength)
        {
            double g = 1.0 - 0.28 * strength; // verde un poco más bajo
            double b = 1.0 - 0.58 * strength; // azul bastante más bajo
            var ramp = new ushort[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                int baseVal = i * 256;
                ramp[i] = (ushort)baseVal;                                   // rojo
                ramp[256 + i] = (ushort)Math.Clamp(baseVal * g, 0, 65535);    // verde
                ramp[512 + i] = (ushort)Math.Clamp(baseVal * b, 0, 65535);    // azul
            }
            return ramp;
        }

        private static void SetRamp(double strength)
        {
            try
            {
                var hdc = Win32.GetDC(IntPtr.Zero);
                try { if (Win32.SetDeviceGammaRamp(hdc, BuildRamp(strength))) _warm = true; }
                finally { Win32.ReleaseDC(IntPtr.Zero, hdc); }
            }
            catch (Exception ex) { Log.Write("Modo noche: " + ex.Message); }
        }

        /// <summary>Devuelve la pantalla a los colores normales.</summary>
        public static void Reset()
        {
            try
            {
                var ramp = new ushort[256 * 3];
                for (int i = 0; i < 256; i++) { ushort v = (ushort)(i * 256); ramp[i] = v; ramp[256 + i] = v; ramp[512 + i] = v; }
                var hdc = Win32.GetDC(IntPtr.Zero);
                try { Win32.SetDeviceGammaRamp(hdc, ramp); }
                finally { Win32.ReleaseDC(IntPtr.Zero, hdc); }
            }
            catch { }
            _warm = false;
        }

        public static void Stop()
        {
            if (_timer != null) { _timer.Stop(); _timer = null; }
            SettingsStore.Changed -= Apply;
            Reset();
        }
    }
}
