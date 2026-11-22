// McBar — Creado por Andrés Londero
// Pruebas de la etapa 3 (isla dinámica): avisos de batería, ajustes y posición estimada de la música.
using System;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage3Tests
    {
        private static BatteryState B(int pct, bool charging) => new BatteryState { HasBattery = true, Percent = pct, Charging = charging };

        [Fact]
        public void Bateria_AlEnchufar_MuestraCargando()
        {
            Assert.Equal(ActivityKind.Charging, ActivityHub.BatteryActivity(B(50, false), B(50, true)));
        }

        [Fact]
        public void Bateria_AlDesenchufar_MuestraUsandoBateria()
        {
            Assert.Equal(ActivityKind.Unplugged, ActivityHub.BatteryActivity(B(80, true), B(80, false)));
        }

        [Fact]
        public void Bateria_Baja_SoloAlCruzar20y10()
        {
            Assert.Equal(ActivityKind.LowBattery, ActivityHub.BatteryActivity(B(21, false), B(20, false)));
            Assert.Null(ActivityHub.BatteryActivity(B(19, false), B(18, false)));
            Assert.Equal(ActivityKind.LowBattery, ActivityHub.BatteryActivity(B(11, false), B(10, false)));
            Assert.Null(ActivityHub.BatteryActivity(B(21, true), B(20, true))); // cargando: no avisar
        }

        [Fact]
        public void Bateria_SinBateria_NoAvisa()
        {
            Assert.Null(ActivityHub.BatteryActivity(new BatteryState(), new BatteryState()));
        }

        [Fact]
        public void Ajustes_ModoDeBarraValido()
        {
            Assert.Equal("menubar", SettingsStore.Sanitize(new AppSettings { TopBarMode = "otra" }).TopBarMode);
            Assert.Equal("island", SettingsStore.Sanitize(new AppSettings { TopBarMode = "island" }).TopBarMode);
        }

        [Fact]
        public void Musica_PosicionEnPausaNoAvanza()
        {
            var m = new MediaInfo { Playing = false, Position = TimeSpan.FromSeconds(30), Duration = TimeSpan.FromMinutes(3), PositionUpdated = DateTimeOffset.Now.AddSeconds(-20) };
            Assert.Equal(30, m.LivePosition.TotalSeconds, 1);
        }

        [Fact]
        public void Musica_PosicionReproduciendoAvanzaYNoPasaElFinal()
        {
            var m = new MediaInfo { Playing = true, Position = TimeSpan.FromSeconds(30), Duration = TimeSpan.FromSeconds(40), PositionUpdated = DateTimeOffset.Now.AddSeconds(-20) };
            Assert.Equal(40, m.LivePosition.TotalSeconds, 1);
        }
    }
}
