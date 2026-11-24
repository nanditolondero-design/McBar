// McBar — Creado por Andrés Londero
// Pruebas de la 1.9.6 (ventanas maximizadas que no se enteran del cambio de área de trabajo) y 1.9.7 (carpetas del Dock que se cierran solas, cartel de la Papelera).
using McBar.Core;
using McBar.Dock;
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
    

        [Fact]
        public void Carpetas_SeCierranSolas_DeFabrica() => Assert.True(new AppSettings().DockPopupsAutoClose);

        [Fact]
        public void Cerca_ConMargen()
        {
            var r = new System.Windows.Rect(100, 100, 200, 100);
            Assert.True(DockWindow.IsNear(r, new System.Windows.Point(150, 150), 0));
            Assert.True(DockWindow.IsNear(r, new System.Windows.Point(310, 150), 24));
            Assert.False(DockWindow.IsNear(r, new System.Windows.Point(340, 150), 24));
            Assert.False(DockWindow.IsNear(r, new System.Windows.Point(150, 40), 24));
        }

        [Fact]
        public void CartelDeLaPapelera() => Assert.Equal("Mover a la Papelera", DockWindow.TrashDropLabel);
    

        // 1.9.8: barra de arriba en modo barra (28 px)
        private static readonly PxRect Monitor = new PxRect(0, 0, 1920, 1080);
        private static readonly PxRect ConBarra = new PxRect(0, 28, 1920, 990);

        [Fact]
        public void Maximizada_ConElMonitorEntero_BajaDeLaBarra()
        {
            var win = new PxRect(-8, -8, 1928, 1088);
            var fit = MaximizedFit.Fit(win, Monitor, ConBarra);
            Assert.Equal(20, fit!.Value.Top);
            Assert.Equal(998, fit.Value.Bottom);
        }

        [Fact]
        public void VentanaComun_DebajoDeLaBarra_SeBaja()
        {
            var fit = MaximizedFit.FitTop(new PxRect(0, 0, 960, 990), ConBarra);
            Assert.Equal(28, fit!.Value.Top);
            Assert.Equal(990, fit.Value.Bottom);
        }

        [Fact]
        public void VentanaComun_Chica_SeCorreEntera()
        {
            var fit = MaximizedFit.FitTop(new PxRect(100, 10, 500, 150), ConBarra);
            Assert.Equal(28, fit!.Value.Top);
            Assert.Equal(168, fit.Value.Bottom);
        }

        [Fact]
        public void VentanaComun_YaAbajo_NoSeToca() => Assert.Null(MaximizedFit.FitTop(new PxRect(100, 200, 500, 600), ConBarra));

        [Fact]
        public void AreaDeTrabajo_ConBarraYDock() =>
            Assert.Equal(ConBarra.Top, WorkArea.Compute(Monitor, 28, 90).Top);
    

        // 1.9.10: Launchpad según el tamaño de la pantalla
        [Fact]
        public void Launchpad_PantallaGrande_7x5De80()
        {
            var f = McBar.Panels.LaunchpadWindow.Fit(1920, 1080, 80);
            Assert.Equal((7, 5), (f.cols, f.rows));
            Assert.Equal(80, f.icon);
        }

        [Fact]
        public void Launchpad_Notebook1366_ÍconosMasChicos()
        {
            var f = McBar.Panels.LaunchpadWindow.Fit(1366, 768, 80);
            Assert.True(f.icon >= 52 && f.icon < 80);
            Assert.True(f.top < 124);
            // entra todo: filas × (ícono + nombre) en el lugar que queda
            Assert.True(f.rows * (f.icon + 52) <= 768 - f.top - f.bottom - 80);
        }

        [Fact]
        public void Launchpad_PantallaMuyChica_MenosFilas()
        {
            var f = McBar.Panels.LaunchpadWindow.Fit(1024, 600, 70);
            Assert.True(f.rows < 5 || f.cols < 7);
            Assert.True(f.icon >= 40);
        }
    }
}
