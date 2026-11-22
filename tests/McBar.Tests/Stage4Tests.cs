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
    }
}
