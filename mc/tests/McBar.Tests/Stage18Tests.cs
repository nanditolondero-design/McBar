// McBar — Creado por Andrés Londero
// Pruebas de la 1.5.0: medición de fluidez y escala por monitor.
using System.Collections.Generic;
using McBar.Core;
using McBar.Native;
using Xunit;

namespace McBar.Tests
{
    public class Stage18Tests
    {
        [Fact]
        public void Summarize_60CuadrosSinTirones()
        {
            var v = new List<double>();
            for (int i = 0; i < 60; i++) v.Add(1 / 60.0);
            var (fps, slow) = Perf.Summarize(v);
            Assert.InRange(fps, 59.5, 60.5);
            Assert.Equal(0, slow);
        }

        [Fact]
        public void Summarize_CuentaLosCuadrosLentos()
        {
            var v = new List<double> { 0.016, 0.016, 0.040, 0.016 };
            var (_, slow) = Perf.Summarize(v);
            Assert.Equal(25, slow);
        }

        [Fact]
        public void Summarize_IgnoraPausasLargasYVacios()
        {
            Assert.Equal((0.0, 0.0), Perf.Summarize(new List<double>()));
            var (fps, _) = Perf.Summarize(new List<double> { 0.02, 5.0, 0.02 }); // 5 s = estaba quieto
            Assert.InRange(fps, 49.9, 50.1);
        }

        [Fact]
        public void Monitor_EscalaPorDefecto() => Assert.Equal(1.0, new MonitorInfo().Scale);
    }
}
