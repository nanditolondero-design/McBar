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
    

        // 1.9.23: Modo noche
        [Fact]
        public void ModoNoche_DeNoche_SiPrendido()
        {
            Assert.False(NightShift.ShouldWarm(false, false, new System.DateTime(2026,1,1,23,0,0)));
            Assert.True(NightShift.ShouldWarm(true, false, new System.DateTime(2026,1,1,23,0,0)));
            Assert.False(NightShift.ShouldWarm(true, false, new System.DateTime(2026,1,1,13,0,0)));
            Assert.True(NightShift.ShouldWarm(true, true, new System.DateTime(2026,1,1,13,0,0)));
        }

        [Fact]
        public void ModoNoche_Rampa_BajaElAzul()
        {
            var r = NightShift.BuildRamp(1.0);
            Assert.Equal(255*256, r[255]);          // rojo full
            Assert.True(r[512+255] < r[255]);        // azul más bajo que rojo
            Assert.True(r[256+255] < r[255]);        // verde algo más bajo
            Assert.True(r[512+255] < r[256+255]);    // azul más bajo que verde
            var off = NightShift.BuildRamp(0.0);
            Assert.Equal(255*256, off[512+255]);     // sin intensidad: azul normal
        }
    }
}
