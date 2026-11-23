// McBar — Creado por Andrés Londero
// Teclas de volumen (0.6.0): McBar cambia el volumen (de a 2, como Windows) y muestra su propio cartel:
// en la isla si está activa, o el cartel estilo macOS (Panels/OsdWindow) en modo barra.
using System;
using McBar.Native;

namespace McBar.Core
{
    public static class VolumeKeys
    {
        public const double Step = 0.02;

        /// <summary>Volumen nuevo al apretar subir/bajar (pura, se prueba). Redondea a pasos de 2.</summary>
        public static float Next(float current, bool up)
        {
            double v = Math.Round(current / Step) * Step + (up ? Step : -Step);
            return (float)Math.Clamp(v, 0, 1);
        }

        public static void Handle(int vk)
        {
            var cur = CoreAudio.GetVolume();
            if (cur == null) return;
            float v = cur.Value;
            bool muted = CoreAudio.GetMute();
            if (vk == 0xAD) { muted = !muted; CoreAudio.SetMute(muted); }
            else
            {
                v = Next(v, vk == 0xAF);
                CoreAudio.SetVolume(v);
                muted = v <= 0.001f;
            }
            var a = new Activity { Kind = ActivityKind.Volume, Value = v, Muted = muted };
            if (!ActivityHub.Show(a)) McBar.Panels.OsdWindow.ShowVolume(v, muted);
        }
    }
}
