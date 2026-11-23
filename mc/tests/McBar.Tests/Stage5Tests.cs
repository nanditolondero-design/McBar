// McBar — Creado por Andrés Londero
// Pruebas de la etapa 5 (0.5.0): instalador.
using System;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage5Tests
    {
        [Fact]
        public void Instalador_DecideQueHacer()
        {
            var v = new Version(0, 5, 0, 0);
            Assert.Equal("install", Installer.Decide(v, null));
            Assert.Equal("update", Installer.Decide(v, new Version(0, 4, 4, 0)));
            Assert.Equal("launchInstalled", Installer.Decide(v, new Version(0, 5, 0, 0)));
            Assert.Equal("launchInstalled", Installer.Decide(v, new Version(0, 6, 0, 0)));
        }

        [Fact]
        public void Instalador_ReconoceLaCarpetaInstalada()
        {
            const string dir = @"C:\Users\ana\AppData\Local\Programs\McBar";
            Assert.True(Installer.IsInside(@"C:\Users\ana\AppData\Local\Programs\McBar\McBar.exe", dir));
            Assert.True(Installer.IsInside(@"c:\users\ana\appdata\local\programs\mcbar\McBar.exe", dir + "\\"));
            Assert.False(Installer.IsInside(@"C:\Users\ana\Downloads\McBar.exe", dir));
            Assert.False(Installer.IsInside(@"C:\Users\ana\AppData\Local\Programs\McBar2\McBar.exe", dir));
            Assert.False(Installer.IsInside("", dir));
        }
    }
}
