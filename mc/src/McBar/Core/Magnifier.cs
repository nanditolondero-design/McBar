// McBar — Creado por Andrés Londero
// Matemática del efecto de ampliación del Dock (sin nada de interfaz, así se puede probar).
//
// Idea: se calcula el tamaño de cada ícono según la distancia del mouse a la posición que el ícono
// tendría SIN ampliar (así no hay "temblores"). Después se arma la fila ampliada de modo que el punto
// que está bajo el mouse siga bajo el mouse, igual que en macOS: los vecinos se corren hacia los costados.
using System;

namespace McBar.Core
{
    public static class Magnifier
    {
        /// <summary>Curva suave: 1 en el centro, 0 a partir de <paramref name="range"/>.</summary>
        public static double Falloff(double distance, double range)
        {
            if (range <= 0) return 0;
            double x = Math.Abs(distance) / range;
            if (x >= 1) return 0;
            return (Math.Cos(x * Math.PI) + 1) / 2;
        }

        /// <summary>
        /// Tamaños deseados. <paramref name="baseSizes"/>: tamaño sin ampliar de cada elemento
        /// (0 = elemento que se está quitando). <paramref name="scalable"/>: false para separadores.
        /// <paramref name="mouse"/>: posición X del mouse relativa al centro del Dock, o null si no está encima.
        /// </summary>
        public static double[] TargetSizes(double[] baseSizes, bool[] scalable, double gap, double? mouse, double maxSize, double range)
        {
            int n = baseSizes.Length;
            var result = new double[n];
            var centers = BaseCenters(baseSizes, gap);
            for (int i = 0; i < n; i++)
            {
                double b = baseSizes[i];
                if (mouse == null || !scalable[i] || b <= 0 || maxSize <= b) { result[i] = b; continue; }
                result[i] = b + (maxSize - b) * Falloff(mouse.Value - centers[i], range);
            }
            return result;
        }

        /// <summary>Centros de cada elemento en la fila sin ampliar, relativos al centro del Dock.</summary>
        public static double[] BaseCenters(double[] baseSizes, double gap)
        {
            int n = baseSizes.Length;
            var c = new double[n];
            double total = TotalWidth(baseSizes, gap);
            double x = -total / 2;
            for (int i = 0; i < n; i++)
            {
                c[i] = x + baseSizes[i] / 2;
                x += baseSizes[i] + (baseSizes[i] > 0 ? gap : 0);
            }
            return c;
        }

        public static double TotalWidth(double[] sizes, double gap)
        {
            double t = 0; int visible = 0;
            foreach (var s in sizes) { t += s; if (s > 0.01) visible++; }
            return t + gap * Math.Max(0, visible - 1);
        }

        /// <summary>
        /// Posición izquierda de cada elemento (relativa al centro del Dock) para los tamaños actuales,
        /// manteniendo fijo bajo <paramref name="anchor"/> el mismo punto de la fila sin ampliar.
        /// Si anchor es null, la fila queda centrada.
        /// </summary>
        public static double[] Layout(double[] currentSizes, double[] baseSizes, double gap, double? anchor, out double start, out double width)
        {
            int n = currentSizes.Length;
            var lefts = new double[n];
            width = TotalWidth(currentSizes, gap);
            double baseWidth = TotalWidth(baseSizes, gap);

            // posiciones acumuladas en la fila actual (desde 0)
            var magLeft = new double[n];
            double x = 0;
            for (int i = 0; i < n; i++)
            {
                magLeft[i] = x;
                x += currentSizes[i] + (currentSizes[i] > 0.01 ? gap : 0);
            }

            if (anchor == null || n == 0 || baseWidth <= 0)
            {
                start = -width / 2;
            }
            else
            {
                double L0 = -baseWidth / 2;
                double m = Math.Clamp(anchor.Value, L0, L0 + baseWidth - 0.001);
                // buscar el elemento de la fila base que contiene a m
                double bx = L0;
                int k = n - 1; double local = 0;
                for (int i = 0; i < n; i++)
                {
                    double slot = baseSizes[i] + (baseSizes[i] > 0 ? gap : 0);
                    if (m < bx + slot || i == n - 1)
                    {
                        k = i;
                        double into = m - bx;
                        double b = baseSizes[i];
                        if (b <= 0) local = 0;
                        else if (into <= b) local = currentSizes[i] * (into / b); // dentro del ícono: misma proporción
                        else local = currentSizes[i] + (into - b);                  // en el espacio entre íconos
                        break;
                    }
                    bx += slot;
                }
                start = m - (magLeft[k] + local);
            }

            for (int i = 0; i < n; i++) lefts[i] = start + magLeft[i];
            return lefts;
        }

        /// <summary>Acerca <paramref name="current"/> a <paramref name="target"/> suavemente según el tiempo transcurrido.</summary>
        public static double Approach(double current, double target, double dtSeconds, double speed = 16)
        {
            double k = 1 - Math.Exp(-dtSeconds * speed);
            double v = current + (target - current) * k;
            return Math.Abs(target - v) < 0.25 ? target : v;
        }

        /// <summary>Altura del rebote al abrir una app (en px), o 0 cuando terminó.</summary>
        public static double Bounce(double elapsedSeconds, double height, int bounces = 3, double period = 0.62)
        {
            if (elapsedSeconds < 0 || elapsedSeconds >= bounces * period) return 0;
            double phase = elapsedSeconds % period / period;  // 0..1 dentro de cada salto
            int which = (int)(elapsedSeconds / period);
            double decay = 1.0 - which * 0.18;
            return Math.Sin(phase * Math.PI) * height * decay;
        }
    }
}
