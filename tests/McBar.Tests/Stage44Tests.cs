// McBar — Creado por Andrés Londero
// 1.9.55: aviso de versiones nuevas.
using System;
using McBar.Core;
using McBar.Panels;
using Xunit;

namespace McBar.Tests
{
    public class Stage44Tests
    {
        [Theory]
        [InlineData("v1.9.55", 1, 9, 55)]
        [InlineData("1.9.55", 1, 9, 55)]
        [InlineData(" V2.0.1 ", 2, 0, 1)]
        public void ParseTag_EntiendeLasEtiquetas(string tag, int a, int b, int c)
        {
            var v = UpdateCheck.ParseTag(tag);
            Assert.NotNull(v);
            Assert.Equal(new Version(a, b, c), new Version(v!.Major, v.Minor, v.Build));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("nightly")]
        public void ParseTag_DevuelveNullSiNoEntiende(string? tag) => Assert.Null(UpdateCheck.ParseTag(tag));

        [Fact]
        public void IsNewer_ComparaTresNumeros()
        {
            Assert.True(UpdateCheck.IsNewer(new Version(1, 9, 56), new Version(1, 9, 55, 0)));
            Assert.False(UpdateCheck.IsNewer(new Version(1, 9, 55), new Version(1, 9, 55, 0)));
            Assert.False(UpdateCheck.IsNewer(new Version(1, 9, 54), new Version(1, 9, 55, 0)));
            Assert.False(UpdateCheck.IsNewer(null, new Version(1, 9, 55, 0)));
            Assert.True(UpdateCheck.IsNewer(new Version(1, 10), new Version(1, 9, 99, 0)));
        }

        [Theory]
        [InlineData("Mostrar el porcentaje de batería", "bateria", true)]
        [InlineData("Mostrar el porcentaje de batería", "BATERÍA porcentaje", true)]
        [InlineData("Mostrar el porcentaje de batería", "volumen", false)]
        [InlineData("Volumen y brillo", "", false)]
        public void Busqueda_IgnoraTildesYMayusculas(string text, string query, bool expected) =>
            Assert.Equal(expected, SettingsSearch.Matches(text, query));

        [Fact]
        public void PerfilPorMonitores_ElijeSoloSiHaceFalta()
        {
            var s = new AppSettings();
            s.Profiles.Add(new SettingsProfile { Name = "Notebook", Data = "{}" });
            s.Profiles.Add(new SettingsProfile { Name = "Escritorio", Data = "{}" });
            s.ProfileOneMonitor = "Notebook";
            s.ProfileManyMonitors = "Escritorio";
            Assert.Equal("Notebook", Profiles.ForMonitorCount(s, 1));
            Assert.Equal("Escritorio", Profiles.ForMonitorCount(s, 2));
            s.ActiveProfile = "Notebook";
            Assert.Null(Profiles.ForMonitorCount(s, 1));       // ya está en uso
            s.ProfileOneMonitor = "Inexistente";
            s.ActiveProfile = "";
            Assert.Null(Profiles.ForMonitorCount(s, 1));       // el perfil ya no existe
            s.ProfileManyMonitors = "";
            Assert.Null(Profiles.ForMonitorCount(s, 3));       // sin elegir: no cambia
        }
    }
}
