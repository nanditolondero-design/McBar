// McBar — Creado por Andrés Londero
// Pruebas de la etapa 4 (0.4.0): carpetas del Launchpad y centro de notificaciones propio.
using System;
using System.Collections.Generic;
using System.Linq;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage4Tests
    {
        private sealed record A(string Name, string Target);

        private static List<A> Apps(params string[] names) => names.Select(n => new A(n, "t:" + n)).ToList();

        // ---------- Carpetas ----------

        [Fact]
        public void Carpeta_SoltarAppSobreOtra_CreaCarpetaConLasDos()
        {
            var folders = new List<LaunchpadFolder>();
            var f = LaunchpadLayout.CreateFolder(folders, "t:Word", "t:Excel");
            Assert.Single(folders);
            Assert.Equal(new[] { "t:Word", "t:Excel" }, f.Targets);
            Assert.Equal("Carpeta", f.Name);
        }

        [Fact]
        public void Carpeta_SeMuestraPrimeroYSusAppsNoSeRepiten()
        {
            var apps = Apps("Chrome", "Excel", "Word", "Zoom");
            var folders = new List<LaunchpadFolder>();
            LaunchpadLayout.CreateFolder(folders, "t:Word", "t:Excel", "Office");
            var grid = LaunchpadLayout.Arrange(apps, folders, a => a.Target);
            Assert.Equal(3, grid.Count);
            Assert.True(grid[0].IsFolder);
            Assert.Equal("Office", grid[0].Folder!.Name);
            Assert.Equal(new[] { "Word", "Excel" }, grid[0].Apps.Select(a => a.Name));
            Assert.Equal(new[] { "Chrome", "Zoom" }, grid.Skip(1).Select(e => e.App!.Name));
        }

        [Fact]
        public void Carpeta_UnaAppEstaEnUnaSolaCarpeta()
        {
            var folders = new List<LaunchpadFolder>();
            var a = LaunchpadLayout.CreateFolder(folders, "t:1", "t:2");
            var b = LaunchpadLayout.CreateFolder(folders, "t:3", "t:4");
            LaunchpadLayout.AddToFolder(folders, b.Id, "t:1");
            Assert.Equal(new[] { "t:2" }, a.Targets);
            Assert.Equal(new[] { "t:3", "t:4", "t:1" }, b.Targets);
        }

        [Fact]
        public void Carpeta_AlQuedarVaciaDesaparece()
        {
            var folders = new List<LaunchpadFolder>();
            LaunchpadLayout.CreateFolder(folders, "t:1", "t:2");
            LaunchpadLayout.RemoveFromFolder(folders, "t:1");
            Assert.Single(folders);
            LaunchpadLayout.RemoveFromFolder(folders, "t:2");
            Assert.Empty(folders);
        }

        [Fact]
        public void Carpeta_RenombrarYDeshacer()
        {
            var folders = new List<LaunchpadFolder>();
            var f = LaunchpadLayout.CreateFolder(folders, "t:1", "t:2");
            LaunchpadLayout.Rename(folders, f.Id, "  Juegos ");
            Assert.Equal("Juegos", f.Name);
            LaunchpadLayout.Rename(folders, f.Id, "   ");
            Assert.Equal("Carpeta", f.Name);
            Assert.True(LaunchpadLayout.Dissolve(folders, f.Id));
            Assert.Empty(folders);
        }

        [Fact]
        public void Carpeta_LimpiaAppsDesinstaladas()
        {
            var folders = new List<LaunchpadFolder>();
            LaunchpadLayout.CreateFolder(folders, "t:1", "t:2");
            LaunchpadLayout.CreateFolder(folders, "t:3", "t:4");
            LaunchpadLayout.Clean(folders, new[] { "t:1", "t:2" });
            Assert.Single(folders);
        }

        [Fact]
        public void Buscar_EncuentraAppsDentroDeCarpetas()
        {
            var apps = Apps("Chrome", "Excel", "Word");
            var folders = new List<LaunchpadFolder>();
            LaunchpadLayout.CreateFolder(folders, "t:Word", "t:Excel", "Office");
            var r = LaunchpadLayout.Search(apps, folders, a => a.Target, a => a.Name, "exc");
            Assert.Single(r);
            Assert.Equal("Excel", r[0].App!.Name);
            var byFolder = LaunchpadLayout.Search(apps, folders, a => a.Target, a => a.Name, "offi");
            Assert.True(byFolder[0].IsFolder);
        }

        [Fact]
        public void Ajustes_TransparenciaDelLaunchpadSeLimita()
        {
            var s = new AppSettings { LaunchpadTransparency = 300, LaunchpadFolders = null! };
            SettingsStore.Sanitize(s);
            Assert.Equal(100, s.LaunchpadTransparency);
            Assert.NotNull(s.LaunchpadFolders);
        }

        // ---------- Notificaciones ----------

        [Fact]
        public void Notificaciones_TiempoRelativo()
        {
            var now = new DateTime(2026, 11, 22, 15, 0, 0);
            Assert.Equal("ahora", NotificationCenter.Ago(now.AddSeconds(-20), now));
            Assert.Equal("hace 5 min", NotificationCenter.Ago(now.AddMinutes(-5), now));
            Assert.Equal("hace 2 h", NotificationCenter.Ago(now.AddHours(-2), now));
            Assert.Equal("ayer", NotificationCenter.Ago(now.AddDays(-1), now));
            Assert.Equal("10/11", NotificationCenter.Ago(new DateTime(2026, 11, 10, 9, 0, 0), now));
        }

        [Fact]
        public void Notificaciones_AgrupadasPorAppLaMasNuevaPrimero()
        {
            var t = new DateTime(2026, 11, 22, 12, 0, 0);
            var items = new[]
            {
                new NotificationEntry { Id = 1, App = "WhatsApp", Time = t },
                new NotificationEntry { Id = 2, App = "Correo", Time = t.AddMinutes(5) },
                new NotificationEntry { Id = 3, App = "WhatsApp", Time = t.AddMinutes(10) },
                new NotificationEntry { Id = 4, App = "", Time = t.AddMinutes(1) }
            };
            var g = NotificationCenter.Grouped(items);
            Assert.Equal(new[] { "WhatsApp", "Correo", "Notificaciones" }, g.Select(x => x.Key));
            Assert.Equal(new long[] { 3, 1 }, g[0].Select(x => x.Id));
        }
            // ---------- 0.4.1 ----------

        [Fact]
        public void Migracion_PasaAlNuevoAspectoUnaSolaVez_YDevuelveLaPapelera()
        {
            var s = new AppSettings { DockBackgroundStretch = false, BackdropBlur = true, Stacks = new List<PinnedApp>(), LookVersion = 0 };
            SettingsStore.Migrate(s, save: false);
            Assert.True(s.DockBackgroundStretch);
            Assert.False(s.BackdropBlur);
            Assert.Contains(s.Stacks, x => x.Target == "mcbar:trash");
            // si después el usuario lo cambia, no se vuelve a pisar
            s.BackdropBlur = true;
            s.Stacks.Clear();
            SettingsStore.Migrate(s, save: false);
            Assert.True(s.BackdropBlur);
            Assert.Empty(s.Stacks);
        }

        [Fact]
        public void Clima_LeeLosProximosDias()
        {
            const string json = @"{
              ""current"": { ""temperature_2m"": 21.4, ""weather_code"": 2, ""apparent_temperature"": 23.0, ""relative_humidity_2m"": 61, ""wind_speed_10m"": 12.5 },
              ""daily"": { ""time"": [""2026-11-22"", ""2026-11-23""], ""temperature_2m_max"": [25.1, 28], ""temperature_2m_min"": [14.0, 16], ""weather_code"": [1, 61], ""precipitation_probability_max"": [5, 80] },
              ""hourly"": { ""time"": [""2026-11-22T15:00"", ""2026-11-22T16:00""], ""temperature_2m"": [20, 21], ""weather_code"": [0, 1] }
            }";
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var w = WeatherService.Parse(doc.RootElement, "Rosario", new DateTime(2026, 11, 22, 15, 10, 0));
            Assert.Equal(2, w.Days.Count);
            Assert.Equal(61, w.Days[1].Code);
            Assert.Equal(80, w.Days[1].RainChance);
            Assert.Equal(61, w.Humidity);
            Assert.Equal(23.0, w.FeelsLike, 3);
            Assert.Equal(2, w.Hours24.Count);
        }
            // ---------- 0.4.2 ----------

        [Fact]
        public void Launchpad_OcultarYMostrarApps()
        {
            var apps = Apps("Chrome", "Excel", "Word");
            var hidden = new List<string>();
            Assert.True(LaunchpadLayout.ToggleHidden(hidden, "t:Excel"));
            Assert.Equal(new[] { "Chrome", "Word" }, LaunchpadLayout.Visible(apps, hidden, a => a.Target, false).Select(a => a.Name));
            Assert.Equal(3, LaunchpadLayout.Visible(apps, hidden, a => a.Target, true).Count);
            Assert.False(LaunchpadLayout.ToggleHidden(hidden, "T:EXCEL")); // sin importar mayúsculas
            Assert.Empty(hidden);
        }

        [Fact]
        public void Ajustes_ModoGotaEsValido()
        {
            var s = SettingsStore.Sanitize(new AppSettings { TopBarMode = "drop", LaunchpadHidden = null! });
            Assert.Equal("drop", s.TopBarMode);
            Assert.True(s.IsIslandLike);
            Assert.NotNull(s.LaunchpadHidden);
            Assert.Equal("menubar", SettingsStore.Sanitize(new AppSettings { TopBarMode = "raro" }).TopBarMode);
        }
    }
}
