// McBar — Creado por Andrés Londero
// Pruebas de la etapa 8 (0.8.0): ventanas estilo Mac.
using System;
using System.Linq;
using McBar.Core;
using McBar.Native;
using Xunit;

namespace McBar.Tests
{
    public class Stage8Tests
    {
        private static TrackedWindow W(int h, string app, long last) =>
            new TrackedWindow { Hwnd = new IntPtr(h), PrimaryKey = app, LastActivated = last };

        [Fact]
        public void AltTab_AppsAgrupadasPorUsoReciente()
        {
            var apps = WindowSwitch.AppsByRecency(new[]
            {
                W(1, "chrome", 5), W(2, "word", 9), W(3, "chrome", 12), W(4, "explorer", 1)
            });
            Assert.Equal(new[] { "chrome", "word", "explorer" }, apps.Select(a => a[0].PrimaryKey));
            Assert.Equal(new IntPtr(3), apps[0][0].Hwnd); // la ventana más reciente de Chrome
            Assert.Equal(2, apps[0].Count);
        }

        [Fact]
        public void AltTab_DaLaVuelta()
        {
            Assert.Equal(0, WindowSwitch.Step(2, 3, false));
            Assert.Equal(2, WindowSwitch.Step(0, 3, true));
            Assert.Equal(1, WindowSwitch.Step(0, 3, false));
        }

        [Fact]
        public void MissionControl_GrillaSinSalirseYCentrada()
        {
            foreach (int n in new[] { 1, 2, 3, 5, 9, 14 })
            {
                var cells = WindowSwitch.GridLayout(n, 1800, 900, 30);
                Assert.Equal(n, cells.Count);
                Assert.All(cells, c =>
                {
                    Assert.True(c.X >= -0.01 && c.X + c.W <= 1800.01);
                    Assert.True(c.Y >= -0.01 && c.Y + c.H <= 900.01);
                });
            }
            var one = WindowSwitch.GridLayout(1, 1800, 900, 30)[0];
            Assert.Equal(1800, one.W, 3);
        }

        [Fact]
        public void Miniatura_MantieneLaProporcion()
        {
            var (x, y, w, h) = DwmThumb.Fit(1920, 1080, 200, 200);
            Assert.Equal(200, w);
            Assert.Equal(112, h);
            Assert.Equal(0, x);
            Assert.Equal(44, y);
        }
    }
}
