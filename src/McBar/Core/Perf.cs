// McBar — Creado por Andrés Londero
// 1.5.0 — Medición de fluidez, para comprobar que las mejoras sirven (Preferencias → Diagnóstico):
//  - Dock: cuadros por segundo de la última animación (ampliación, rebote, ocultar…) y cuántos cuadros
//    tardaron de más (más de 25 ms = se nota un tironcito).
//  - Launchpad: cuánto tarda en aparecer desde que se pide.
// No gasta nada: solo anota tiempos que el Dock ya calcula en cada cuadro.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace McBar.Core
{
    public static class Perf
    {
        private static readonly List<double> _frames = new List<double>();
        public static double LastDockFps { get; private set; }
        public static double LastDockSlowPercent { get; private set; }
        public static double LastLaunchpadMs { get; private set; } = -1;

        /// <summary>Cuadros por segundo y % de cuadros lentos (más de 25 ms) a partir de los intervalos (s). Pura, se prueba.</summary>
        public static (double fps, double slowPercent) Summarize(IReadOnlyList<double> intervals)
        {
            var v = intervals.Where(x => x > 0 && x < 0.5).ToList();
            if (v.Count == 0) return (0, 0);
            double fps = v.Count / v.Sum();
            double slow = 100.0 * v.Count(x => x > 0.025) / v.Count;
            return (fps, slow);
        }

        /// <summary>Un cuadro del Dock (segundos desde el anterior).</summary>
        public static void DockFrame(double seconds)
        {
            if (_frames.Count < 2000) _frames.Add(seconds);
        }

        /// <summary>Terminó una animación del Dock: se guarda el resumen (si duró algo).</summary>
        public static void DockAnimationEnded()
        {
            if (_frames.Count >= 10)
            {
                var (fps, slow) = Summarize(_frames);
                LastDockFps = fps;
                LastDockSlowPercent = slow;
            }
            _frames.Clear();
        }

        public static void LaunchpadOpened(double ms) => LastLaunchpadMs = ms;

        public static string Format()
        {
            var es = new CultureInfo("es-AR");
            string dock = LastDockFps > 0
                ? $"Dock: {LastDockFps.ToString("0", es)} cuadros por segundo en la última animación ({LastDockSlowPercent.ToString("0", es)} % con tironcitos)"
                : "Dock: mové el mouse sobre el Dock para medir";
            string lp = LastLaunchpadMs >= 0 ? $"Launchpad: aparece en {LastLaunchpadMs.ToString("0", es)} ms" : "Launchpad: abrilo una vez para medir";
            return dock + "\n" + lp;
        }
    }
}
