// McBar — Creado por Andrés Londero
// Pruebas de la etapa 9 (0.9.0): estabilidad.
using System;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage9Tests
    {
        [Fact]
        public void Recuperacion_NoReiniciaSiSeCaeMuchasVeces()
        {
            var now = new DateTime(2026, 11, 22, 21, 0, 0);
            Assert.False(Recovery.TooManyRecentCrashes(new[] { now.AddMinutes(-1), now.AddMinutes(-30) }, now));
            Assert.True(Recovery.TooManyRecentCrashes(new[] { now.AddMinutes(-1), now.AddMinutes(-4), now.AddMinutes(-9) }, now));
        }

        [Fact]
        public void Consumo_PorcentajeDeProcesador()
        {
            Assert.Equal(25, Diagnostics.CpuPercent(TimeSpan.Zero, TimeSpan.FromSeconds(1), 1, 4), 3);
            Assert.Equal(0, Diagnostics.CpuPercent(TimeSpan.Zero, TimeSpan.FromSeconds(1), 0, 4));
        }
    }
}
