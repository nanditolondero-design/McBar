// McBar — Creado por Andrés Londero
// Pruebas de la 1.8.6: vidrio de la píldora hecho con el fondo de pantalla.
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage24Tests
    {
        [Fact]
        public void ParteVisible_Rellenar()
        {
            // imagen 16:9 en monitor 16:10 → se recorta a los costados
            var r = WallpaperGlass.VisiblePart(160, 90, 1680, 1050, false);
            Assert.Equal(90, r.Height, 3);
            Assert.Equal(144, r.Width, 3);
            Assert.Equal(8, r.X, 3);
            Assert.Equal(0, r.Y, 3);
        }

        [Fact]
        public void Paneles_TienenMinimoDeOpacidad()
        {
            // 1.9.0: los paneles de vidrio no quedan tan transparentes que no se lea el texto
            Assert.Contains("Mc.PanelCardBg", ThemeManager.GlassBackgrounds);
            Assert.True(ThemeManager.MinAlpha("Mc.PanelCardBg") >= 0x50);
            Assert.Equal(0, ThemeManager.MinAlpha("Mc.DockBg"));
        }

        [Fact]
        public void BotonDeApagado_EsEspecial()
        {
            Assert.True(AppIdentity.IsSpecial(AppIdentity.Power));
            Assert.Empty(AppIdentity.KeysForTarget(AppIdentity.Power));
            Assert.DoesNotContain(new AppSettings().Stacks, p => p.Target == AppIdentity.Power); // viene apagado
        }

        [Fact]
        public void InicioRapido_PrendidoDeFabrica() => Assert.True(new AppSettings().FastStartup);

        [Fact]
        public void ParteVisible_Expandir_UsaTodo()
        {
            var r = WallpaperGlass.VisiblePart(160, 90, 1680, 1050, true);
            Assert.Equal(160, r.Width, 3);
            Assert.Equal(90, r.Height, 3);
        }
    }
}
