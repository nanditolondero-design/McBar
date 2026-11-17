// McBar — Creado por Andrés Londero
// Pruebas de ajustes (guardar/leer/corregir) y del clima (interpretación de la respuesta).
using System;
using System.IO;
using System.Text.Json;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class SettingsAndWeatherTests
    {
        [Fact]
        public void Ajustes_SeGuardanYSeLeen()
        {
            var dir = Path.Combine(Path.GetTempPath(), "mcbar-test-" + Guid.NewGuid().ToString("N"));
            SettingsStore.UseFolder(dir);
            try
            {
                var s = SettingsStore.Load();
                s.DockIconSize = 60;
                s.Pinned.Add(new PinnedApp { Name = "Bloc", Target = @"C:\x\bloc.exe" });
                SettingsStore.Save();
                var r = SettingsStore.Load();
                Assert.Equal(60, r.DockIconSize);
                Assert.Single(r.Pinned);
                Assert.Equal("Bloc", r.Pinned[0].Name);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        [Fact]
        public void Ajustes_ValoresFueraDeRangoSeCorrigen()
        {
            var s = SettingsStore.Sanitize(new AppSettings { DockIconSize = 500, MagnifiedSize = 10, Theme = "rosa" });
            Assert.Equal(96, s.DockIconSize);
            Assert.True(s.MagnifiedSize >= s.DockIconSize);
            Assert.Equal("auto", s.Theme);
        }

        [Theory]
        [InlineData(0, "Despejado")]
        [InlineData(3, "Nublado")]
        [InlineData(63, "Lluvia")]
        [InlineData(95, "Tormenta")]
        [InlineData(12345, "—")]
        public void CodigosDeClima(int code, string text)
        {
            Assert.Equal(text, WeatherCodes.Describe(code).text);
        }

        [Fact]
        public void Clima_InterpretaRespuestaDeOpenMeteo()
        {
            const string json = @"{
              ""current"": { ""temperature_2m"": 21.4, ""weather_code"": 2 },
              ""daily"": { ""temperature_2m_max"": [25.1], ""temperature_2m_min"": [14.0] },
              ""hourly"": {
                ""time"": [""2026-11-17T15:00"", ""2026-11-17T16:00"", ""2026-11-17T17:00"", ""2026-11-17T18:00""],
                ""temperature_2m"": [20, 21, 22, 23],
                ""weather_code"": [0, 1, 2, 3]
              }
            }";
            using var doc = JsonDocument.Parse(json);
            var w = WeatherService.Parse(doc.RootElement, "Buenos Aires", new DateTime(2026, 11, 17, 16, 10, 0));
            Assert.Equal(21.4, w.Temp, 3);
            Assert.Equal(25.1, w.Max, 3);
            Assert.Equal(3, w.Hours.Count); // descarta las horas ya pasadas
            Assert.Equal(16, w.Hours[0].Time.Hour);
        }
    }
}
