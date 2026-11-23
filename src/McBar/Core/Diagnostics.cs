// McBar — Creado por Andrés Londero
// Consumo de McBar (0.9.0): memoria, procesador, identificadores y objetos gráficos. Se anota en el
// registro cada 10 minutos (para encontrar pérdidas de memoria) y se ve en vivo en Preferencias → General.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace McBar.Core
{
    public readonly struct UsageSample
    {
        public double MemoryMb { get; init; }
        public double PrivateMb { get; init; }
        public double CpuPercent { get; init; }
        public int Handles { get; init; }
        public int GdiObjects { get; init; }
        public int UserObjects { get; init; }

        public string Format() => string.Format(new CultureInfo("es-AR"),
            "{0:0} MB en uso ({1:0} MB propios) · {2:0.0} % de procesador · {3} identificadores · {4} GDI · {5} USER",
            MemoryMb, PrivateMb, CpuPercent, Handles, GdiObjects, UserObjects);
    }

    public static class Diagnostics
    {
        [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);

        private static TimeSpan _lastCpu;
        private static DateTime _lastAt = DateTime.MinValue;

        /// <summary>Porcentaje de procesador entre dos mediciones (pura, se prueba).</summary>
        public static double CpuPercent(TimeSpan cpuBefore, TimeSpan cpuNow, double seconds, int cores) =>
            seconds <= 0 || cores <= 0 ? 0 : Math.Clamp((cpuNow - cpuBefore).TotalSeconds / seconds / cores * 100, 0, 100);

        public static UsageSample Sample()
        {
            try
            {
                using var p = Process.GetCurrentProcess();
                var now = DateTime.Now;
                var cpu = p.TotalProcessorTime;
                double pct = _lastAt == DateTime.MinValue ? 0 : CpuPercent(_lastCpu, cpu, (now - _lastAt).TotalSeconds, Environment.ProcessorCount);
                _lastCpu = cpu;
                _lastAt = now;
                return new UsageSample
                {
                    MemoryMb = p.WorkingSet64 / 1048576.0,
                    PrivateMb = p.PrivateMemorySize64 / 1048576.0,
                    CpuPercent = pct,
                    Handles = p.HandleCount,
                    GdiObjects = GetGuiResources(p.Handle, 0),
                    UserObjects = GetGuiResources(p.Handle, 1)
                };
            }
            catch { return new UsageSample(); }
        }
    }
}
