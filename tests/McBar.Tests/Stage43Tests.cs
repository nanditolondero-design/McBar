// McBar — Creado por Andrés Londero
// 1.9.43: resorte suave del Dock y límites de la grilla del Launchpad.
using System;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage43Tests
    {
        [Fact]
        public void SmoothDamp_LlegaAlObjetivoSinPasarseMucho()
        {
            double v = 50, vel = 0, max = 0;
            for (int i = 0; i < 300; i++)
            {
                v = Magnifier.SmoothDamp(v, ref vel, 100, 1 / 60.0, 16);
                max = Math.Max(max, v);
            }
            Assert.Equal(100, v);
            Assert.True(max <= 100.5); // amortiguado: no rebota
        }

        [Fact]
        public void SmoothDamp_ArrancaSuave()
        {
            double vel = 0;
            double first = Magnifier.SmoothDamp(50, ref vel, 100, 1 / 60.0, 16);
            // el primer cuadro avanza poco (el acercamiento exponencial daba un tirón de ~25 %)
            Assert.InRange(first - 50, 0, 8);
        }

        [Fact]
        public void Launchpad_Grilla_LimitesDeLosAjustes()
        {
            var s = new AppSettings { LaunchpadIconSize = 500, LaunchpadLabelSize = 1, LaunchpadColumns = 99, LaunchpadRows = 1 };
            SettingsStore.Sanitize(s);
            Assert.Equal(128, s.LaunchpadIconSize);
            Assert.Equal(9, s.LaunchpadLabelSize);
            Assert.Equal(10, s.LaunchpadColumns);
            Assert.Equal(2, s.LaunchpadRows);
        }
    }
}
