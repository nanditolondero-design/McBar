// McBar — Creado por Andrés Londero
// Pruebas de la 1.3.1: vigilante de trabadas e instalador al bajar una versión nueva.
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage14Tests
    {
        [Theory]
        [InlineData(0.5, true, "ok")]
        [InlineData(4.9, true, "ok")]
        [InlineData(5, true, "warn")]
        [InlineData(19, true, "warn")]
        [InlineData(20, true, "restart")]
        [InlineData(60, false, "warn")]
        public void HangWatch_Decide(double seconds, bool restart, string expected) =>
            Assert.Equal(expected, HangWatch.Decide(seconds, restart));

        [Fact]
        public void Ajustes_ReinicioActivadoPorDefecto()
        {
            var s = new AppSettings();
            Assert.True(s.RestartIfHung);
            Assert.Equal("", s.PortableVersion);
        }

        [Fact]
        public void Migracion_LaIslaDejaDeReservarLaFranjaUnaSolaVez()
        {
            var s = new AppSettings { IslandReserveSpace = true, LookVersion = 1 };
            SettingsStore.Migrate(s, save: false);
            Assert.False(s.IslandReserveSpace);
            Assert.Equal(SettingsStore.CurrentLook, s.LookVersion);
            s.IslandReserveSpace = true; // si después la vuelve a prender, no se pisa
            SettingsStore.Migrate(s, save: false);
            Assert.True(s.IslandReserveSpace);
        }

        [Fact]
        public void Perfiles_NoCopianLaVersionPortatil() =>
            Assert.Contains(nameof(AppSettings.PortableVersion), Profiles.MachineFields);
    }
}
