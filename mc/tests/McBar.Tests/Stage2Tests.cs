// McBar — Creado por Andrés Londero
// Pruebas de la etapa 2: área de trabajo, pantalla completa, bandeja propia, íconos de clima y ajustes nuevos.
using System;
using System.Text;
using McBar.Core;
using McBar.Native;
using Xunit;

namespace McBar.Tests
{
    public class Stage2Tests
    {
        [Fact]
        public void AreaDeTrabajo_SacaSoloLoReservado()
        {
            var r = WorkArea.Compute(new PxRect(0, 0, 1920, 1080), 28, 70);
            Assert.Equal(28, r.Top);
            Assert.Equal(1010, r.Bottom);
            Assert.Equal(1920, r.Width);
        }

        [Fact]
        public void AreaDeTrabajo_SinReservas_EsTodoElMonitor()
        {
            var r = WorkArea.Compute(new PxRect(1920, 0, 3840, 1080), 0, 0);
            Assert.Equal(new PxRect(1920, 0, 3840, 1080).ToString(), r.ToString());
        }

        [Fact]
        public void PantallaCompleta_SoloSiCubreTodoElMonitor()
        {
            var monitor = new PxRect(0, 0, 1920, 1080);
            Assert.True(FullscreenWatcher.IsCovering(new PxRect(0, 0, 1920, 1080), monitor));
            Assert.True(FullscreenWatcher.IsCovering(new PxRect(-8, -8, 1928, 1088), monitor));
            Assert.False(FullscreenWatcher.IsCovering(new PxRect(0, 28, 1920, 1010), monitor)); // maximizada normal
        }

        private static byte[] TrayBlock(uint message, uint hwnd, uint uid, uint flags, string tip, uint version = 0)
        {
            var d = new byte[964];
            BitConverter.GetBytes(TrayData.Signature).CopyTo(d, 0);
            BitConverter.GetBytes(message).CopyTo(d, 4);
            BitConverter.GetBytes(956u).CopyTo(d, 8);
            BitConverter.GetBytes(hwnd).CopyTo(d, 12);
            BitConverter.GetBytes(uid).CopyTo(d, 16);
            BitConverter.GetBytes(flags).CopyTo(d, 20);
            BitConverter.GetBytes(0x8001u).CopyTo(d, 24);
            Encoding.Unicode.GetBytes(tip).CopyTo(d, 32);
            BitConverter.GetBytes(version).CopyTo(d, 808);
            return d;
        }

        [Fact]
        public void Bandeja_LeeElMensajeDeUnaApp()
        {
            var m = TrayData.Parse(TrayBlock(TrayData.NIM_ADD, 0x1234, 7, TrayData.NIF_MESSAGE | TrayData.NIF_TIP, "WhatsApp"));
            Assert.NotNull(m);
            Assert.Equal(TrayData.NIM_ADD, m!.Message);
            Assert.Equal(0x1234u, m.HWnd);
            Assert.Equal(7u, m.Uid);
            Assert.Equal(0x8001u, m.CallbackMessage);
            Assert.Equal("WhatsApp", m.Tip);
            Assert.Equal("h:1234:7", TrayData.IdOf(m));
        }

        [Fact]
        public void Bandeja_RechazaDatosQueNoSonDeBandeja()
        {
            Assert.Null(TrayData.Parse(new byte[40]));
            Assert.Null(TrayData.Parse(new byte[4]));
        }

        [Fact]
        public void Bandeja_LeeLaVersion()
        {
            var m = TrayData.Parse(TrayBlock(TrayData.NIM_SETVERSION, 1, 1, 0, "", 4));
            Assert.Equal(4u, m!.Version);
        }

        [Theory]
        [InlineData(0, WeatherKind.Clear)]
        [InlineData(2, WeatherKind.PartlyCloudy)]
        [InlineData(45, WeatherKind.Fog)]
        [InlineData(53, WeatherKind.Drizzle)]
        [InlineData(81, WeatherKind.Rain)]
        [InlineData(73, WeatherKind.Snow)]
        [InlineData(96, WeatherKind.Storm)]
        public void Clima_TipoDeIcono(int code, WeatherKind kind)
        {
            Assert.Equal(kind, WeatherCodes.Kind(code));
        }

        [Fact]
        public void Ajustes_LadoDerechoPorDefectoYEstiloValido()
        {
            var s = SettingsStore.Sanitize(new AppSettings { Stacks = null!, DockStyle = "raro", TrayHiddenIds = null! });
            Assert.Equal(2, s.Stacks.Count);
            Assert.Equal("mcbar:downloads", s.Stacks[0].Target);
            Assert.Equal("classic", s.DockStyle);
            Assert.NotNull(s.TrayHiddenIds);
        }

        [Fact]
        public void Ajustes_SePuedenSacarPapeleraYDescargas()
        {
            var s = SettingsStore.Sanitize(new AppSettings { Stacks = new System.Collections.Generic.List<PinnedApp>() });
            Assert.Empty(s.Stacks); // lista vacía a propósito: no se vuelven a agregar solas
        }
    }
}
