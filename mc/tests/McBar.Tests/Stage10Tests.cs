// McBar — Creado por Andrés Londero
// Pruebas de la etapa 10 (1.0.0): mosaico, esquinas activas, escritorios y Quick Look.
using System;
using McBar.Core;
using McBar.Native;
using Xunit;

namespace McBar.Tests
{
    public class Stage10Tests
    {
        [Fact]
        public void Mosaico_MitadesYCuartosConMargen()
        {
            var l = Tiling.Compute(TileLayout.Left, 0, 0, 1920, 1040, 6);
            Assert.Equal(6, l.X);
            Assert.Equal(6, l.Y);
            Assert.Equal(960 - 6 - 3, l.W);
            Assert.Equal(1040 - 12, l.H);
            var r = Tiling.Compute(TileLayout.Right, 0, 0, 1920, 1040, 6);
            Assert.Equal(960 + 3, r.X);
            Assert.Equal(1920 - 6, r.X + r.W);
            var q = Tiling.Compute(TileLayout.BottomRight, 100, 50, 1000, 800, 0);
            Assert.Equal((600, 450, 500, 400), (q.X, q.Y, q.W, q.H));
            var fill = Tiling.Compute(TileLayout.Fill, 0, 0, 1000, 800, 0);
            Assert.Equal((0, 0, 1000, 800), (fill.X, fill.Y, fill.W, fill.H));
        }

        [Fact]
        public void EsquinasActivas_DetectaLaEsquina()
        {
            var b = new PxRect(0, 0, 1920, 1080);
            Assert.Equal(0, HotCorners.CornerAt(0, 0, b));
            Assert.Equal(1, HotCorners.CornerAt(1919, 1, b));
            Assert.Equal(2, HotCorners.CornerAt(1, 1079, b));
            Assert.Equal(3, HotCorners.CornerAt(1919, 1079, b));
            Assert.Equal(-1, HotCorners.CornerAt(960, 0, b));
            var second = new PxRect(1920, 0, 3840, 1080); // segundo monitor
            Assert.Equal(0, HotCorners.CornerAt(1920, 0, second));
        }

        [Fact]
        public void Escritorios_LeeLosIdsDelRegistro()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var data = new byte[32];
            a.ToByteArray().CopyTo(data, 0);
            b.ToByteArray().CopyTo(data, 16);
            Assert.Equal(new[] { a, b }, VirtualDesktops.ParseIds(data));
            Assert.Empty(VirtualDesktops.ParseIds(null));
        }

        [Theory]
        [InlineData(@"C:\a\foto.JPG", "image")]
        [InlineData(@"C:\a\peli.mkv", "video")]
        [InlineData(@"C:\a\tema.mp3", "audio")]
        [InlineData(@"C:\a\factura.pdf", "pdf")]
        [InlineData(@"C:\a\notas.txt", "text")]
        [InlineData(@"C:\a\planilla.xlsx", "other")]
        public void QuickLook_TipoDeArchivo(string path, string kind)
        {
            Assert.Equal(kind, QuickLookData.KindOf(path));
        }

        [Fact]
        public void QuickLook_Tamaño()
        {
            Assert.Equal("512 bytes", QuickLookData.Size(512));
            Assert.Equal("1,5 KB", QuickLookData.Size(1536));
            Assert.Equal("2 MB", QuickLookData.Size(2 * 1048576));
        }

        [Fact]
        public void Mosaico_AtajosDeTeclado()
        {
            Assert.Equal(TileLayout.Left, KeyboardHook.TileFor(0x25));
            Assert.Equal(TileLayout.TopRight, KeyboardHook.TileFor(0x49));
            Assert.Equal(TileLayout.Fill, KeyboardHook.TileFor(0x0D));
            Assert.Null(KeyboardHook.TileFor(0x41));
        }
    }
}
