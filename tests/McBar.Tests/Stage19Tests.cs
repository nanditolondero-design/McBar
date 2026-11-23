// McBar — Creado por Andrés Londero
// Pruebas de la 1.6.1: transparencia del vidrio, arrastrar fuera del Launchpad, ajustes nuevos.
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage19Tests
    {
        [Theory]
        [InlineData(100, 50, 100)]  // 50 = como venía
        [InlineData(100, 0, 255)]   // casi opaco (tope 255)
        [InlineData(60, 0, 180)]    // 3 veces
        [InlineData(100, 100, 15)]  // 15 %
        [InlineData(0, 0, 0)]
        public void ScaleAlpha(byte alpha, int transparency, byte expected) =>
            Assert.Equal(expected, ThemeManager.ScaleAlpha(alpha, transparency));

        [Fact]
        public void ScaleAlpha_MasTransparenteEsMenosOpaco() =>
            Assert.True(ThemeManager.ScaleAlpha(120, 80) < ThemeManager.ScaleAlpha(120, 20));

        [Theory]
        [InlineData(960, 1000, true)]  // abajo (Dock)
        [InlineData(960, 50, true)]    // arriba (búsqueda)
        [InlineData(30, 500, true)]    // costado izquierdo
        [InlineData(1900, 500, true)]  // costado derecho
        [InlineData(960, 500, false)]  // en la grilla
        public void LeavesGrid(double x, double y, bool expected) =>
            Assert.Equal(expected, LaunchpadLayout.LeavesGrid(x, y, 1920, 1080));

        [Fact]
        public void Sanitize_SinDesenfoqueYTransparenciaEnRango()
        {
            var s = new AppSettings { BackdropBlur = true, GlassTransparency = 140 };
            s.CustomIcons["C:\\App.exe"] = "x.png";
            SettingsStore.Sanitize(s);
            Assert.False(s.BackdropBlur);
            Assert.Equal(100, s.GlassTransparency);
            Assert.True(s.CustomIcons.ContainsKey("c:\\app.exe")); // sin importar mayúsculas
        }

        [Theory]
        [InlineData("hwnd:1234", @"C:\Windows\System32\ApplicationFrameHost.exe", true)]
        [InlineData("aumid:windows.immersivecontrolpanel", @"C:\Windows\System32\ApplicationFrameHost.exe", false)]
        [InlineData("hwnd:1234", @"C:\Program Files\App\app.exe", false)]
        public void MarcoDeLaTiendaSinReconocer(string key, string exe, bool expected) =>
            Assert.Equal(expected, WindowTracker.IsUnidentifiedUwp(new TrackedWindow { PrimaryKey = key, ExePath = exe }));

        [Fact]
        public void Ajustes_PorDefecto()
        {
            var s = new AppSettings();
            Assert.Equal(50, s.GlassTransparency);
            Assert.Empty(s.CustomIcons);
        }
    }
}
