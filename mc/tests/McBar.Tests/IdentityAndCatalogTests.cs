// McBar — Creado por Andrés Londero
// Pruebas de cómo se identifican las apps (para unir ventanas abiertas con íconos fijados).
using System;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class IdentityAndCatalogTests
    {
        [Fact]
        public void Exe_GeneraClaveEnMinusculas()
        {
            var k = AppIdentity.KeysForTarget(@"C:\Program Files\App\App.EXE");
            Assert.Contains(@"exe:c:\program files\app\app.exe", k);
        }

        [Fact]
        public void AppDeLaStore_GeneraClaveAumid()
        {
            var k = AppIdentity.KeysForTarget(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");
            Assert.Contains("aumid:microsoft.windowscalculator_8wekyb3d8bbwe!app", k);
        }

        [Fact]
        public void CarpetaConocida_SeResuelveAExe()
        {
            var old = AppIdentity.KnownFolderResolver;
            try
            {
                AppIdentity.KnownFolderResolver = _ => @"C:\Windows\System32";
                var k = AppIdentity.KeysForTarget(@"shell:AppsFolder\{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\notepad.exe");
                Assert.Contains(@"exe:c:\windows\system32\notepad.exe", k);
            }
            finally { AppIdentity.KnownFolderResolver = old; }
        }

        [Fact]
        public void AccesoDirecto_UsaElDestino()
        {
            var old = AppIdentity.ShortcutResolver;
            try
            {
                AppIdentity.ShortcutResolver = _ => @"D:\Juegos\juego.exe";
                var k = AppIdentity.KeysForTarget(@"C:\Users\x\Desktop\Juego.lnk");
                Assert.Contains(@"exe:d:\juegos\juego.exe", k);
            }
            finally { AppIdentity.ShortcutResolver = old; }
        }

        [Fact]
        public void ElementosEspeciales_NoTienenClaves()
        {
            Assert.Empty(AppIdentity.KeysForTarget(AppIdentity.Launchpad));
        }

        [Theory]
        [InlineData("Desinstalar Programa", "x!App", false)]
        [InlineData("Uninstall Foo", "foo.exe", false)]
        [InlineData("Sitio web", "https://ejemplo.com", false)]
        [InlineData("Manual", @"{GUID}\manual.pdf", false)]
        [InlineData("Calculadora", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", true)]
        [InlineData("", "algo", false)]
        public void Catalogo_FiltraLoQueNoSonApps(string name, string id, bool expected)
        {
            Assert.Equal(expected, AppCatalog.ShouldInclude(name, id));
        }
    }
}
