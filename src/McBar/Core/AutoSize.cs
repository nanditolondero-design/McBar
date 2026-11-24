// McBar — Creado por Andrés Londero
// 1.9.12: tamaños que se adaptan solos a la pantalla. De fábrica todo arranca acomodado a la pantalla donde
// está (una notebook de 1366×768 y un monitor 4K quedan bien sin tocar nada); si el usuario mueve el tamaño
// del Dock en Preferencias, desde ahí manda lo que eligió.
using System;
using McBar.Native;

namespace McBar.Core
{
    public static class AutoSize
    {
        /// <summary>Tamaño de los íconos del Dock para una pantalla de "alto" DIP. 1080 → 52 (el de siempre). Pura, se prueba.</summary>
        public static int DockIcon(double heightDip) => (int)Math.Clamp(Math.Round(heightDip * 0.048), 38, 64);

        /// <summary>Tamaño ampliado (lupa) que acompaña: 52 → 96, como venía. Pura, se prueba.</summary>
        public static int Magnified(int icon) => Math.Clamp((int)Math.Round(icon * 1.85), icon, 128);

        /// <summary>¿Se usa el tamaño automático? (null en ajustes viejos = automático solo si no se había cambiado).</summary>
        public static bool DockAuto(AppSettings s) => s.DockSizeAuto ?? (s.DockIconSize == 52 && s.MagnifiedSize == 96);

        public static double HeightDip(MonitorInfo m) => m.Bounds.Height / Math.Max(0.5, m.Scale);
    }
}
