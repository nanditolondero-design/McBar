// McBar — Creado por Andrés Londero
// Pruebas de la 1.4.0: íconos de macOSicons para las apps instaladas.
using System;
using System.Collections.Generic;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage16Tests
    {
        private static MacIconHit H(string name, long dl = 0) => new MacIconHit { AppName = name, PngUrl = "https://x/" + name + ".png", Downloads = dl };

        [Fact]
        public void Pick_PrefiereElMismoNombreYElMasBajado()
        {
            var hits = new[] { H("Google Chrome Canary", 900), H("Google Chrome", 10), H("google-chrome", 500) };
            Assert.Equal("google-chrome", MacIcons.Pick(hits, "Google Chrome")!.AppName);
        }

        [Fact]
        public void Pick_AceptaNombreParecido() =>
            Assert.Equal("Spotify", MacIcons.Pick(new[] { H("Spotify") }, "Spotify Music")!.AppName);

        [Fact]
        public void Pick_NoPoneElIconoDeOtraApp() =>
            Assert.Null(MacIcons.Pick(new[] { H("Safari"), H("Mail") }, "WhatsApp"));

        [Fact]
        public void Pick_IgnoraResultadosSinImagen() =>
            Assert.Null(MacIcons.Pick(new[] { new MacIconHit { AppName = "Discord", PngUrl = "" } }, "Discord"));

        [Theory]
        [InlineData("Desinstalar Zoom", true)]
        [InlineData("Uninstall Steam", true)]
        [InlineData("Léame", true)]
        [InlineData("Visual Studio Code", false)]
        [InlineData("Discord", false)]
        public void ShouldSkip_SacaDesinstaladoresYAyudas(string name, bool skip) => Assert.Equal(skip, MacIcons.ShouldSkip(name));

        [Fact]
        public void Remaining_VuelveACeroAlCambiarDeMes()
        {
            var now = new DateTime(2026, 11, 22);
            Assert.Equal(38, MacIcons.Remaining("2026-11", 12, 50, now));
            Assert.Equal(50, MacIcons.Remaining("2026-10", 50, 50, now));
            Assert.Equal(0, MacIcons.Remaining("2026-11", 80, 50, now));
        }

        [Fact]
        public void SafeFileName_SacaCaracteresProhibidos()
        {
            Assert.Equal("AC  DC", MacIcons.SafeFileName("AC/\\DC"));
            Assert.Equal("app", MacIcons.SafeFileName("??"));
        }

        [Fact]
        public void NeedsLookup_ReintentaNoEncontradasALos30Dias()
        {
            var now = new DateTime(2026, 11, 22);
            Assert.True(MacIcons.NeedsLookup(null, now, "."));
            Assert.False(MacIcons.NeedsLookup(new MacIconEntry { NotFound = true, Date = now.AddDays(-5) }, now, "."));
            Assert.True(MacIcons.NeedsLookup(new MacIconEntry { NotFound = true, Date = now.AddDays(-31) }, now, "."));
            Assert.True(MacIcons.NeedsLookup(new MacIconEntry { File = "no-existe-123.png", Date = now }, now, ".")); // se borró el archivo
        }

        [Fact]
        public void ParseHits_LeeLaRespuestaDeLaApi()
        {
            const string json = "{\"hits\":[{\"appName\":\"Safari\",\"lowResPngUrl\":\"https://a/s.png\",\"icnsUrl\":\"x\",\"credit\":\"Elías\",\"creditUrl\":\"https://c\",\"downloads\":42}],\"query\":\"Safari\",\"totalHits\":1}";
            var h = Assert.Single(MacIcons.ParseHits(json));
            Assert.Equal("Safari", h.AppName);
            Assert.Equal("https://a/s.png", h.PngUrl);
            Assert.Equal("Elías", h.Credit);
            Assert.Equal(42, h.Downloads);
            Assert.Empty(MacIcons.ParseHits("{\"message\":\"Unauthorized\"}"));
        }

        [Fact]
        public void Exportar_NoIncluyeLaClave()
        {
            var s = new AppSettings { MacIconsKey = "SECRETO-CIFRADO" };
            Assert.DoesNotContain("SECRETO-CIFRADO", Profiles.Export(s));
            Assert.DoesNotContain("SECRETO-CIFRADO", Profiles.Capture(s));
            Assert.Contains(nameof(AppSettings.MacIconsKey), Profiles.MachineFields);
        }
    }
}
