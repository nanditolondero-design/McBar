// McBar — Creado por Andrés Londero
// Pruebas de la 1.7.0: fondos de McBar dentro de la app y opción de la pantalla de bloqueo.
using System.Collections.Generic;
using System.IO;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage20Tests
    {
        [Fact]
        public void FondosDeMcBar_UnoPorMomento() =>
            Assert.Equal(DynamicWallpaper.Slots.Length, DynamicWallpaper.BundledNames.Length);

        [Fact]
        public void FondosDeMcBar_VienenDentroDelExe()
        {
            var asm = typeof(DynamicWallpaper).Assembly;
            foreach (var n in DynamicWallpaper.BundledNames)
            {
                using var st = asm.GetManifestResourceStream("McBar.Fondos." + n);
                Assert.NotNull(st);
                Assert.True(st!.Length > 100_000, n + " parece vacío");
            }
        }

        [Fact]
        public void BundledPaths_EnLaCarpeta()
        {
            var p = DynamicWallpaper.BundledPaths("C:\\x");
            Assert.Equal(4, p.Count);
            Assert.Equal(Path.Combine("C:\\x", "McBar-ondas-manana-4k.jpg"), p[0]);
            Assert.Equal(Path.Combine("C:\\x", "McBar-ondas-noche-4k.jpg"), p[3]);
        }

        [Fact]
        public void UsesBundled()
        {
            var p = DynamicWallpaper.BundledPaths("C:\\x");
            Assert.True(DynamicWallpaper.UsesBundled(p, "C:\\x"));
            Assert.False(DynamicWallpaper.UsesBundled(p, "C:\\y"));
            Assert.False(DynamicWallpaper.UsesBundled(new List<string> { "", "", "", "" }, "C:\\x"));
            Assert.False(DynamicWallpaper.UsesBundled(new List<string>(), "C:\\x"));
        }

        [Fact]
        public void PantallaDeBloqueo_ApagadaPorDefecto() =>
            Assert.False(new AppSettings().WallpaperLockScreen);
    }
}
