// McBar — Creado por Andrés Londero
// Pruebas de la etapa 7 (0.7.0): Wi-Fi y Bluetooth propios.
using System.Linq;
using McBar.Core;
using McBar.Native;
using Xunit;

namespace McBar.Tests
{
    public class Stage7Tests
    {
        [Fact]
        public void WiFi_ConectadaPrimeroSinRepetidos()
        {
            var nets = new[]
            {
                new WifiNet { Ssid = "Vecino", Bars = 2 },
                new WifiNet { Ssid = "Casa", Bars = 3, Connected = true },
                new WifiNet { Ssid = "Fibertel", Bars = 4 },
                new WifiNet { Ssid = "Fibertel", Bars = 1 },
            };
            var r = WifiService.Arrange(nets);
            Assert.Equal(new[] { "Casa", "Fibertel", "Vecino" }, r.Select(n => n.Ssid));
            Assert.Equal(4, r[1].Bars);
        }

        [Theory]
        [InlineData(0x240404u, BtKind.Audio)]     // auriculares
        [InlineData(0x002580u, BtKind.Mouse)]     // mouse
        [InlineData(0x002540u, BtKind.Keyboard)]  // teclado
        [InlineData(0x5A020Cu, BtKind.Phone)]     // celular
        [InlineData(0x1F00u, BtKind.Other)]
        public void Bluetooth_TipoDeDispositivo(uint cod, BtKind kind)
        {
            Assert.Equal(kind, BluetoothDevices.KindOf(cod));
        }
    }
}
