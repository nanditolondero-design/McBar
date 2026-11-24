// McBar — Creado por Andrés Londero
// Pruebas de la 1.9.12: tamaños automáticos según la pantalla.
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage26Tests
    {
        [Fact]
        public void Dock_1080_ElDeSiempre() { Assert.Equal(52, AutoSize.DockIcon(1080)); Assert.Equal(96, AutoSize.Magnified(52)); }

        [Fact]
        public void Dock_Notebook_MasChico() => Assert.True(AutoSize.DockIcon(768) < 52 && AutoSize.DockIcon(768) >= 38);

        [Fact]
        public void Dock_PantallaGrande_TieneTope() => Assert.Equal(64, AutoSize.DockIcon(2160));

        [Fact]
        public void Automatico_DeFabrica() => Assert.True(AutoSize.DockAuto(new AppSettings()));

        [Fact]
        public void AjustesViejos_RespetanElTamañoElegido()
        {
            var s = new AppSettings { DockSizeAuto = null, DockIconSize = 64 };
            Assert.False(AutoSize.DockAuto(s));
            Assert.False(SettingsStore.Sanitize(s).DockSizeAuto);
            var t = new AppSettings { DockSizeAuto = null };
            Assert.True(AutoSize.DockAuto(t));
        }
    }
}
