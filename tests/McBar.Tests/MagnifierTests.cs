// McBar — Creado por Andrés Londero
// Pruebas del efecto de ampliación del Dock.
using System;
using System.Linq;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class MagnifierTests
    {
        private static readonly double[] Base = { 50, 50, 50, 50, 50 };
        private static readonly bool[] All = { true, true, true, true, true };

        [Fact]
        public void SinMouse_TodosQuedanEnTamanoBase()
        {
            var t = Magnifier.TargetSizes(Base, All, 5, null, 100, 135);
            Assert.All(t, s => Assert.Equal(50, s));
        }

        [Fact]
        public void MouseSobreUnIcono_EseLlegaAlMaximo_YLosVecinosCrecenMenos()
        {
            var centers = Magnifier.BaseCenters(Base, 5);
            var t = Magnifier.TargetSizes(Base, All, 5, centers[2], 100, 135);
            Assert.Equal(100, t[2], 3);
            Assert.True(t[1] > 50 && t[1] < 100);
            Assert.Equal(t[1], t[3], 3); // simétrico
            Assert.True(t[0] < t[1]);
        }

        [Fact]
        public void Separadores_NoSeAmplian()
        {
            var b = new double[] { 50, 14, 50 };
            var sc = new[] { true, false, true };
            var t = Magnifier.TargetSizes(b, sc, 5, 0, 100, 135);
            Assert.Equal(14, t[1]);
        }

        [Fact]
        public void LayoutSinAncla_QuedaCentrado()
        {
            var lefts = Magnifier.Layout(Base, Base, 5, null, out var start, out var width);
            Assert.Equal(270, width, 3);
            Assert.Equal(-135, start, 3);
            Assert.Equal(-135, lefts[0], 3);
        }

        [Fact]
        public void LayoutConAncla_ElPuntoBajoElMouseNoSeMueve()
        {
            var centers = Magnifier.BaseCenters(Base, 5);
            double mouse = centers[1];
            var sizes = Magnifier.TargetSizes(Base, All, 5, mouse, 100, 135);
            var lefts = Magnifier.Layout(sizes, Base, 5, mouse, out _, out _);
            double centerOfHovered = lefts[1] + sizes[1] / 2;
            Assert.Equal(mouse, centerOfHovered, 3);
        }

        [Fact]
        public void LayoutConTamanosBase_IgualACentradoAunqueHayaAncla()
        {
            var a = Magnifier.Layout(Base, Base, 5, 37, out var s1, out _);
            var b = Magnifier.Layout(Base, Base, 5, null, out var s2, out _);
            Assert.Equal(s2, s1, 3);
            Assert.True(a.Zip(b).All(p => Math.Abs(p.First - p.Second) < 0.001));
        }

        [Fact]
        public void Approach_SeAcercaYTermina()
        {
            double v = 50;
            for (int i = 0; i < 200; i++) v = Magnifier.Approach(v, 100, 1 / 60.0);
            Assert.Equal(100, v);
        }

        [Fact]
        public void Bounce_EmpiezaYTerminaEnCero()
        {
            Assert.Equal(0, Magnifier.Bounce(0, 20), 3);
            Assert.True(Magnifier.Bounce(0.31, 20) > 15);
            Assert.Equal(0, Magnifier.Bounce(5, 20));
        }
    }
}
