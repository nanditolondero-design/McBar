// McBar — Creado por Andrés Londero
// Pruebas de la etapa 13 (1.3.0): perfiles, exportar/importar, modo juego y fondo según la hora.
using System.Collections.Generic;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage13Tests
    {
        // ---------- Fondo según la hora ----------

        [Theory]
        [InlineData(0, 3)]
        [InlineData(5, 3)]
        [InlineData(6, 0)]
        [InlineData(11, 0)]
        [InlineData(12, 1)]
        [InlineData(17, 1)]
        [InlineData(18, 2)]
        [InlineData(20, 2)]
        [InlineData(21, 3)]
        [InlineData(23, 3)]
        public void SlotFor_MomentoDelDia(int hour, int slot) => Assert.Equal(slot, DynamicWallpaper.SlotFor(hour));

        [Fact]
        public void PathFor_UsaElMomentoAnteriorSiFalta()
        {
            var imgs = new List<string> { "manana.jpg", "", "atardecer.jpg", "" };
            Assert.Equal("manana.jpg", DynamicWallpaper.PathFor(imgs, 8));
            Assert.Equal("manana.jpg", DynamicWallpaper.PathFor(imgs, 14));   // tarde vacía → mañana
            Assert.Equal("atardecer.jpg", DynamicWallpaper.PathFor(imgs, 19));
            Assert.Equal("atardecer.jpg", DynamicWallpaper.PathFor(imgs, 23)); // noche vacía → atardecer
            Assert.Equal("atardecer.jpg", DynamicWallpaper.PathFor(imgs, 3));
        }

        [Fact]
        public void PathFor_SinImagenes() =>
            Assert.Null(DynamicWallpaper.PathFor(new List<string> { "", "", "", "" }, 10));

        // ---------- Perfiles ----------

        [Fact]
        public void SaveAs_GuardaFotoSinPerfilesAdentro()
        {
            var s = new AppSettings { DockIconSize = 60 };
            var p = Profiles.SaveAs(s, "Trabajo");
            Assert.Equal("Trabajo", s.ActiveProfile);
            Assert.Single(s.Profiles);
            Assert.Contains("\"DockIconSize\": 60", p.Data);
            Assert.Contains("\"Profiles\": []", p.Data);
        }

        [Fact]
        public void SaveAs_ReemplazaSiYaExiste()
        {
            var s = new AppSettings();
            Profiles.SaveAs(s, "Juegos", gameMode: true);
            s.DockIconSize = 40;
            Profiles.SaveAs(s, "juegos");
            Assert.Single(s.Profiles);
            Assert.True(s.Profiles[0].GameMode); // no se pierde el modo juego
            Assert.Contains("\"DockIconSize\": 40", s.Profiles[0].Data);
        }

        [Fact]
        public void Switch_CargaElOtroYGuardaElActual()
        {
            var s = new AppSettings { DockIconSize = 52, Theme = "dark" };
            Profiles.SaveAs(s, "Trabajo");
            s.DockIconSize = 80; s.Theme = "light";
            Profiles.SaveAs(s, "Juegos");

            s.DockIconSize = 70; // cambio hecho estando en "Juegos"
            Assert.True(Profiles.Switch(s, "Trabajo"));
            Assert.Equal(52, s.DockIconSize);
            Assert.Equal("dark", s.Theme);
            Assert.Equal("Trabajo", s.ActiveProfile);
            Assert.Equal(2, s.Profiles.Count);

            Assert.True(Profiles.Switch(s, "Juegos"));
            Assert.Equal(70, s.DockIconSize); // el cambio quedó guardado en "Juegos"
        }

        [Fact]
        public void Switch_NoPisaDatosDeEstaPc()
        {
            var s = new AppSettings { TaskbarModified = false, PortablePath = "" };
            Profiles.SaveAs(s, "Trabajo");
            s.TaskbarModified = true; s.OriginalTaskbarState = 3; s.PortablePath = @"D:\McBar";
            Assert.True(Profiles.Switch(s, "Trabajo"));
            Assert.True(s.TaskbarModified);
            Assert.Equal(3, s.OriginalTaskbarState);
            Assert.Equal(@"D:\McBar", s.PortablePath);
        }

        [Fact]
        public void Switch_PerfilInexistenteOVacio()
        {
            var s = new AppSettings();
            Assert.False(Profiles.Switch(s, "Nada"));
            s.Profiles.Add(new SettingsProfile { Name = "Vacío", Data = "" });
            Assert.False(Profiles.Switch(s, "Vacío"));
            s.Profiles.Add(new SettingsProfile { Name = "Roto", Data = "{no es json" });
            Assert.False(Profiles.Switch(s, "Roto"));
        }

        [Fact]
        public void CopyInto_SaltaLosCamposIndicados()
        {
            var a = new AppSettings { DockIconSize = 40, FirstRunDone = true };
            var b = new AppSettings { DockIconSize = 90, FirstRunDone = false };
            Profiles.CopyInto(a, b, Profiles.MachineFields);
            Assert.Equal(90, a.DockIconSize);
            Assert.True(a.FirstRunDone);
        }

        // ---------- Exportar / importar ----------

        [Fact]
        public void Export_ReadExport_IdaYVuelta()
        {
            var s = new AppSettings { DockIconSize = 64, DockStyle = "floating" };
            Profiles.SaveAs(s, "Trabajo");
            var json = Profiles.Export(s);
            Assert.Contains("\"App\": \"McBar\"", json);
            var back = Profiles.ReadExport(json);
            Assert.NotNull(back);
            Assert.Equal(64, back!.DockIconSize);
            Assert.Equal("floating", back.DockStyle);
            Assert.Single(back.Profiles);
        }

        [Fact]
        public void ReadExport_AceptaSettingsJsonSuelto() =>
            Assert.Equal(48, Profiles.ReadExport("{ \"DockIconSize\": 48, \"Pinned\": [] }")!.DockIconSize);

        [Theory]
        [InlineData("")]
        [InlineData("hola")]
        [InlineData("[1,2,3]")]
        [InlineData("{ \"nombre\": \"otra app\" }")]
        [InlineData("{ \"App\": \"Otra\", \"Settings\": {} }")]
        public void ReadExport_RechazaLoQueNoEsDeMcBar(string json) => Assert.Null(Profiles.ReadExport(json));

        [Fact]
        public void ReadExport_CorrigeValoresMalos() =>
            Assert.Equal(96, Profiles.ReadExport("{ \"DockIconSize\": 500 }")!.DockIconSize);

        // ---------- Ajustes y modo juego ----------

        [Fact]
        public void Sanitize_PerfilesYFondos()
        {
            var s = new AppSettings
            {
                Profiles = new List<SettingsProfile> { new SettingsProfile { Name = "" }, new SettingsProfile { Name = "Ok" } },
                ActiveProfile = "NoExiste",
                WallpaperImages = new List<string> { "a.jpg" }
            };
            SettingsStore.Sanitize(s);
            Assert.Single(s.Profiles);
            Assert.Equal("", s.ActiveProfile);
            Assert.Equal(4, s.WallpaperImages.Count);
            Assert.Equal("a.jpg", s.WallpaperImages[0]);
        }

        [Fact]
        public void GameMode_PrendeYApaga()
        {
            int changes = 0;
            void H() => changes++;
            GameMode.Changed += H;
            try
            {
                GameMode.Set(false);
                GameMode.Toggle();
                Assert.True(GameMode.On);
                GameMode.Set(true); // sin cambio: no avisa
                GameMode.Toggle();
                Assert.False(GameMode.On);
                Assert.Equal(2, changes);
            }
            finally { GameMode.Changed -= H; GameMode.Set(false); }
        }
    }
}
