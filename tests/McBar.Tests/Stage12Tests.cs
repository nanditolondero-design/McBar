// McBar — Creado por Andrés Londero
// Pruebas de la etapa 12 (1.2.0): isla más viva — temporizador, cronómetro, descargas y privacidad.
using System;
using System.Collections.Generic;
using McBar.Core;
using McBar.Panels;
using Xunit;

namespace McBar.Tests
{
    public class Stage12Tests
    {
        [Theory]
        [InlineData(0, 0, 5, false, "0:05")]
        [InlineData(0, 4, 5, false, "4:05")]
        [InlineData(1, 2, 3, false, "1:02:03")]
        [InlineData(0, 0, 0, false, "0:00")]
        public void Format_MinutosYHoras(int h, int m, int s, bool tenths, string expected) =>
            Assert.Equal(expected, LiveStatus.Format(new TimeSpan(h, m, s), tenths));

        [Fact]
        public void Format_ConDecimas() =>
            Assert.Equal("1:05,3", LiveStatus.Format(new TimeSpan(0, 0, 1, 5, 380), tenths: true));

        [Fact]
        public void Format_NegativoQuedaEnCero() => Assert.Equal("0:00", LiveStatus.Format(TimeSpan.FromSeconds(-4)));

        [Theory]
        [InlineData("foto.jpg.crdownload", "foto.jpg")]
        [InlineData("video.mp4.part", "video.mp4")]
        [InlineData("setup.exe.opdownload", "setup.exe")]
        [InlineData("Unconfirmed 123456.crdownload", "archivo")]
        [InlineData("documento.pdf", null)]
        [InlineData(".part", null)]
        public void FinalName_ReconoceDescargasAMedias(string file, string? expected) =>
            Assert.Equal(expected, LiveStatus.FinalName(file));

        [Theory]
        [InlineData("C:#Program Files#Zoom#bin#Zoom.exe", "Zoom")]
        [InlineData("Microsoft.WindowsCamera_8wekyb3d8bbwe", "WindowsCamera")]
        [InlineData("MSTeams_8wekyb3d8bbwe", "MSTeams")]
        public void AppName_NombreLegible(string key, string expected) =>
            Assert.Equal(expected, LiveStatus.AppName(key));

        [Fact]
        public void PrivacyText_JuntaMicrofonoYCamara()
        {
            Assert.Equal("Micrófono: Zoom · Cámara: Teams", LiveStatus.PrivacyText(new List<string> { "Zoom" }, new List<string> { "Teams" }));
            Assert.Equal("Cámara: Teams", LiveStatus.PrivacyText(new List<string>(), new List<string> { "Teams" }));
            Assert.Equal("", LiveStatus.PrivacyText(new List<string>(), new List<string>()));
        }

        [Theory]
        [InlineData("temporizador 10", 600)]
        [InlineData("Temporizador 5 min", 300)]
        [InlineData("timer 1:30", 90)]
        [InlineData("temporizador 1 hora", 3600)]
        [InlineData("temporizador 1,5", 90)]
        [InlineData("timer 25m", 1500)]
        public void ParseTimerCommand_Valido(string q, int seconds) =>
            Assert.Equal(TimeSpan.FromSeconds(seconds), LiveStatus.ParseTimerCommand(q));

        [Theory]
        [InlineData("temporizador")]
        [InlineData("temporizador hola")]
        [InlineData("temporizador 0")]
        [InlineData("temporizador 30 horas")]
        [InlineData("chrome")]
        public void ParseTimerCommand_Invalido(string q) => Assert.Null(LiveStatus.ParseTimerCommand(q));

        [Theory]
        [InlineData("10", 600)]
        [InlineData("1:30", 90)]
        [InlineData("0,5", 30)]
        public void TimerView_ParseMinutes(string text, int seconds) =>
            Assert.Equal(TimeSpan.FromSeconds(seconds), TimerView.ParseMinutes(text));

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("-3")]
        [InlineData("0:75")]
        public void TimerView_ParseMinutesInvalido(string text) => Assert.Null(TimerView.ParseMinutes(text));

        [Fact]
        public void SizeText_Unidades()
        {
            Assert.Equal("500 KB", LiveStatus.SizeText(512000));
            Assert.Equal("1,5 MB", LiveStatus.SizeText(1572864));
            Assert.Equal("2 GB", LiveStatus.SizeText(2147483648));
        }

        [Fact]
        public void Ajustes_NuevosValoresPorDefecto()
        {
            var s = new AppSettings();
            Assert.True(s.LiveActivities);
            Assert.True(s.PrivacyDots);
            Assert.True(s.LiveDownloads);
            Assert.Equal(new List<int> { 1, 5, 10, 25 }, s.TimerPresets);
        }

        [Fact]
        public void Sanitize_AtajosDelTemporizador()
        {
            var s = new AppSettings { TimerPresets = new List<int> { 0, -2, 3, 3, 5000 } };
            SettingsStore.Sanitize(s);
            Assert.Equal(new List<int> { 3 }, s.TimerPresets);
            s.TimerPresets = new List<int>();
            SettingsStore.Sanitize(s);
            Assert.Equal(4, s.TimerPresets.Count);
        }
    }
}
