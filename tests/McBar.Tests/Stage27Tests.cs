// McBar — Creado por Andrés Londero
// 1.9.34: reglas de la bandeja leída por UI Automation.
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage27Tests
    {
        [Fact]
        public void AppButtonsAreIcons()
        {
            Assert.True(TrayUiaRules.IsAppIcon("NVIDIA Settings", "SystemTrayIcon", "SystemTray.NormalButton"));
            Assert.True(TrayUiaRules.IsAppIcon("Windows Security notification icon", "SystemTrayIcon", "SystemTray.NormalButton"));
        }

        [Fact]
        public void WindowsControlsAreNotIcons()
        {
            Assert.False(TrayUiaRules.IsAppIcon("Wi-Fi, Volume, Battery", "ControlCenterButton", "SystemTray.OmniButton"));
            Assert.False(TrayUiaRules.IsAppIcon("Show Hidden Icons", "NotificationChevron", "SystemTray.Button"));
            Assert.False(TrayUiaRules.IsAppIcon("Volumen: 40%", "SystemTrayIcon", "SystemTray.NormalButton"));
            Assert.False(TrayUiaRules.IsAppIcon("", "SystemTrayIcon", "SystemTray.NormalButton"));
            Assert.False(TrayUiaRules.IsAppIcon("Algo", "OtroId", "Windows.UI.Xaml.Button"));
        }

        [Fact]
        public void NamesAreCleanedAndCompared()
        {
            Assert.Equal("NVIDIA Settings", TrayUiaRules.CleanName("NVIDIA Settings\nRunning"));
            Assert.True(TrayUiaRules.SameIcon("Windows Security notification icon", "Windows Security"));
            Assert.True(TrayUiaRules.SameIcon("WhatsApp", "whatsapp"));
            Assert.False(TrayUiaRules.SameIcon("NVIDIA Settings", "MagnAir"));
            Assert.False(TrayUiaRules.SameIcon("", "WhatsApp"));
        }

        [Fact]
        public void OwnerScoreUsesMeaningfulWords()
        {
            Assert.True(TrayUiaRules.Score("NVIDIA Settings", "nvcontainer", "NVIDIA Container", "NVIDIA Container", "NVIDIA Corporation") >= 1);
            Assert.Equal(0, TrayUiaRules.Score("NVIDIA Settings", "chrome", "Google Chrome", "Google Chrome", "Google LLC"));
            Assert.Equal(0, TrayUiaRules.Score("Windows notification icon", "chrome"));
        }

        [Fact]
        public void UiaIconHasStableIdByName()
        {
            var a = new TrayIcon { FromUia = true, UiaName = "NVIDIA Settings" };
            var b = new TrayIcon { FromUia = true, UiaName = "nvidia settings\nrunning" };
            Assert.Equal(a.StableId, b.StableId);
        }
    }
}
