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
        public void ParteVisible_Expandir_UsaTodo()
        {
            var r = WallpaperGlass.VisiblePart(160, 90, 1680, 1050, true);
            Assert.Equal(160, r.Width, 3);
            Assert.Equal(90, r.Height, 3);
        }
    }
}
