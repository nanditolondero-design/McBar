// McBar — Creado por Andrés Londero
// Pruebas de la 1.3.2: íconos con forma de Mac.
using System;
using System.Linq;
using System.Windows.Media;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage15Tests
    {
        [Fact]
        public void Squircle_EstaDentroDelCuadroYTocaLosLados()
        {
            var pts = IconShaper.SquirclePoints(50, 50, 40);
            Assert.All(pts, p => { Assert.InRange(p.X, 9.99, 90.01); Assert.InRange(p.Y, 9.99, 90.01); });
            Assert.Contains(pts, p => Math.Abs(p.X - 90) < 0.01); // toca el lado derecho
            // las "esquinas" quedan redondeadas: ningún punto llega a la esquina exacta
            Assert.DoesNotContain(pts, p => Math.Abs(p.X - 90) < 1 && Math.Abs(p.Y - 90) < 1);
        }

        private static byte[] Alpha(int w, int h, Func<int, int, bool> on)
        {
            var a = new byte[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) a[y * w + x] = on(x, y) ? (byte)255 : (byte)0;
            return a;
        }

        [Fact]
        public void IsFullBleed_IconoQueLlenaElCuadro() =>
            Assert.True(IconShaper.IsFullBleed(Alpha(32, 32, (x, y) => true), 32, 32));

        [Fact]
        public void IsFullBleed_IconoSueltoConTransparencia() =>
            Assert.False(IconShaper.IsFullBleed(Alpha(32, 32, (x, y) => x > 6 && x < 25 && y > 6 && y < 25), 32, 32));

        [Fact]
        public void IsFullBleed_CirculoNoCuenta() =>
            Assert.False(IconShaper.IsFullBleed(Alpha(32, 32, (x, y) => (x - 15.5) * (x - 15.5) + (y - 15.5) * (y - 15.5) < 15 * 15), 32, 32));

        [Fact]
        public void IsFullBleed_Vacio() => Assert.False(IconShaper.IsFullBleed(new byte[16], 4, 4));

        [Fact]
        public void Dominant_PrefiereLoColorido()
        {
            // 3 píxeles grises y 1 rojo intenso: el resultado tira a rojo
            var px = new byte[] { 128, 128, 128, 255, 128, 128, 128, 255, 128, 128, 128, 255, 0, 0, 255, 255 };
            var c = IconShaper.Dominant(px);
            Assert.True(c.R > c.G + 40 && c.R > c.B + 40);
        }

        [Fact]
        public void Dominant_TransparenteDaGris() => Assert.Equal(Color.FromRgb(0x8E, 0x8E, 0x93), IconShaper.Dominant(new byte[8]));

        [Fact]
        public void Mix_ConBlanco()
        {
            Assert.Equal(Color.FromRgb(255, 255, 255), IconShaper.Mix(Color.FromRgb(0, 100, 200), 1));
            Assert.Equal(Color.FromRgb(0, 100, 200), IconShaper.Mix(Color.FromRgb(0, 100, 200), 0));
        }

        [Fact]
        public void Ajustes_FormaDeIconos()
        {
            Assert.Equal("none", new AppSettings().IconShape);
            var s = new AppSettings { IconShape = "redonda" };
            SettingsStore.Sanitize(s);
            Assert.Equal("none", s.IconShape);
        }
    }
}
