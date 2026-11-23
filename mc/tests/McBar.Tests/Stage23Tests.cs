// McBar — Creado por Andrés Londero
// Pruebas de la 1.8.3: carpetas de apps en el Dock (como Android) y Preferencias de vidrio.
using System.Collections.Generic;
using System.Linq;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage23Tests
    {
        private static List<PinnedApp> Pins(params string[] t) => t.Select(x => new PinnedApp { Name = x, Target = x }).ToList();
        private static string[] T(List<PinnedApp> l) => l.Select(p => p.Target).ToArray();
        private static string Name(string t) => t.ToUpperInvariant();

        [Fact]
        public void SoltarUnaAppSobreOtra_ArmaCarpetaEnSuLugar()
        {
            var folders = new List<DockAppFolder>();
            var r = DockFolders.Drop(Pins("a", "b", "c"), folders, "c", "a", () => "x1");
            Assert.Equal(new[] { "mcbar:appfolder:x1", "b" }, T(r));
            Assert.Single(folders);
            Assert.Equal(new[] { "a", "c" }, folders[0].Targets.ToArray());
        }

        [Fact]
        public void SoltarSobreUnaCarpeta_LaAgrega()
        {
            var folders = new List<DockAppFolder> { new DockAppFolder { Id = "f", Targets = new List<string> { "a", "b" } } };
            var r = DockFolders.Drop(Pins("mcbar:appfolder:f", "c"), folders, "c", "mcbar:appfolder:f", () => "no");
            Assert.Equal(new[] { "mcbar:appfolder:f" }, T(r));
            Assert.Equal(new[] { "a", "b", "c" }, folders[0].Targets.ToArray());
        }

        [Fact]
        public void AppAbiertaSinFijar_TambienEntra()
        {
            var folders = new List<DockAppFolder>();
            var r = DockFolders.Drop(Pins("a", "b"), folders, "z", "b", () => "k");
            Assert.Equal(new[] { "a", "mcbar:appfolder:k" }, T(r));
            Assert.Equal(new[] { "b", "z" }, folders[0].Targets.ToArray());
        }

        [Fact]
        public void SacarDeLaCarpeta_YSiQuedaUna_SeDeshace()
        {
            var folders = new List<DockAppFolder> { new DockAppFolder { Id = "f", Targets = new List<string> { "a", "b", "c" } } };
            var r = DockFolders.TakeOut(Pins("x", "mcbar:appfolder:f", "y"), folders, "mcbar:appfolder:f", "b", Name);
            Assert.Equal(new[] { "x", "mcbar:appfolder:f", "b", "y" }, T(r));
            r = DockFolders.TakeOut(r, folders, "mcbar:appfolder:f", "c", Name);
            Assert.Equal(new[] { "x", "a", "c", "b", "y" }, T(r));
            Assert.Empty(folders);
        }

        [Fact]
        public void Deshacer_DevuelveLasAppsEnSuLugar()
        {
            var folders = new List<DockAppFolder> { new DockAppFolder { Id = "f", Targets = new List<string> { "a", "b" } } };
            var r = DockFolders.Dissolve(Pins("x", "mcbar:appfolder:f", "y"), folders, "mcbar:appfolder:f", Name);
            Assert.Equal(new[] { "x", "a", "b", "y" }, T(r));
            Assert.Equal("A", r[1].Name);
            Assert.Empty(folders);
        }

        [Fact]
        public void CarpetaNueva_DesdeElMenu()
        {
            var folders = new List<DockAppFolder>();
            var r = DockFolders.NewWith(Pins("a", "b"), folders, "b", "Juegos", () => "j");
            Assert.Equal(new[] { "a", "mcbar:appfolder:j" }, T(r));
            Assert.Equal("Juegos", folders[0].Name);
        }

        [Fact]
        public void PreferenciasDeVidrio_UsanLaTransparencia() =>
            Assert.Contains("Mc.SettingsBg", ThemeManager.GlassBackgrounds);

        [Fact]
        public void Sanitize_Carpetas()
        {
            var s = new AppSettings { DockFolders = null! };
            SettingsStore.Sanitize(s);
            Assert.NotNull(s.DockFolders);
            s.DockFolders.Add(new DockAppFolder { Id = "", Targets = new List<string> { "a" } });
            s.DockFolders.Add(new DockAppFolder { Id = "ok", Name = " ", Targets = null! });
            SettingsStore.Sanitize(s);
            Assert.Single(s.DockFolders);
            Assert.Equal("Carpeta", s.DockFolders[0].Name);
        }
    }
}
