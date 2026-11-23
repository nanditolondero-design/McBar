// McBar — Creado por Andrés Londero
// Pruebas de la 1.9.6: acomodar las ventanas maximizadas que no se enteran del cambio de área de trabajo.
using McBar.Core;
using McBar.Native;
using Xunit;

namespace McBar.Tests
{
    public class Stage25Tests
    {
        private static readonly PxRect ConDock = new PxRect(0, 32, 1920, 990);
        private static readonly PxRect SinDock = new PxRect(0, 32, 1920, 1080);

        [Fact]
        public void Explorador_QuedoConElHueco_SeEstira()
        {
            // maximizada con el borde invisible de 8 px, pegada al área vieja (con el Dock)
            var win = new PxRect(-8, 24, 1928, 998);
            var fit = MaximizedFit.Fit(win, ConDock, SinDock);
            Assert.NotNull(fit);
            Assert.Equal(1088, fit!.Value.Bottom);
            Assert.Equal(24, fit.Value.Top);
            Assert.Equal(-8, fit.Value.Left);
        }

        [Fact]
        public void YaAcomodada_NoSeToca()
        {
            var win = new PxRect(-8, 24, 1928, 1088);
            Assert.Null(MaximizedFit.Fit(win, ConDock, SinDock));
        }

        [Fact]
        public void VuelveElDock_SeAchica()
        {
            var win = new PxRect(-8, 24, 1928, 1088);
            var fit = MaximizedFit.Fit(win, SinDock, ConDock);
            Assert.Equal(998, fit!.Value.Bottom);
        }

        [Fact]
        public void CambioChico_NoSeToca()
        {
            var win = new PxRect(-8, 24, 1928, 998);
            Assert.Null(MaximizedFit.Fit(win, ConDock, new PxRect(0, 32, 1920, 1000)));
        }

        [Fact]
        public void Captura_SeLlamaComoEnMac() =>
            Assert.Equal("Captura de pantalla 2026-11-23 a las 20.10.05.png", Screenshots.FileName(new System.DateTime(2026, 11, 23, 20, 10, 5)));
    }
}
