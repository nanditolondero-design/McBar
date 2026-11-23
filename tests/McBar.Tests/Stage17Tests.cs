// McBar — Creado por Andrés Londero
// Pruebas de la 1.4.1: quitar del Dock arrastrando a cualquier lado y avisos de ajustes a prueba de fallas.
using System;
using System.IO;
using McBar.Core;
using McBar.Dock;
using Xunit;

namespace McBar.Tests
{
    public class Stage17Tests
    {
        [Theory]
        [InlineData(60, 500, true)]    // hacia arriba
        [InlineData(10, 500, false)]   // apenas movido, sobre la barra
        [InlineData(0, 150, true)]     // a la izquierda de la barra
        [InlineData(0, 950, true)]     // a la derecha de la barra
        [InlineData(0, 230, false)]    // en el borde (margen de 40)
        public void OutsideDock_ArribaOALosCostados(double up, double x, bool expected) =>
            Assert.Equal(expected, DockWindow.OutsideDock(up, x, barLeft: 200, barW: 600));

        [Fact]
        public void NotifyChanged_SiUnaParteFallaLasOtrasIgualSeEnteran()
        {
            SettingsStore.UseFolder(Path.Combine(Path.GetTempPath(), "mcbar-test-" + Guid.NewGuid().ToString("N")));
            bool second = false;
            Action bad = () => throw new InvalidOperationException("falla a propósito");
            Action good = () => second = true;
            SettingsStore.Changed += bad;
            SettingsStore.Changed += good;
            try
            {
                SettingsStore.NotifyChanged();
                Assert.True(second);
            }
            finally
            {
                SettingsStore.Changed -= bad;
                SettingsStore.Changed -= good;
            }
        }
    }
}
