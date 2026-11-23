// McBar — Creado por Andrés Londero
// Pruebas de la 1.8.0 (Estilo Mac): todo opcional y apagado de fábrica, y las partes "puras" de cada opción.
using System;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage21Tests
    {
        [Fact]
        public void TodoApagadoDeFabrica()
        {
            var s = new AppSettings();
            Assert.False(s.AutoDarkMode);
            Assert.False(s.MacCursors);
            Assert.False(s.ShakeToFind);
            Assert.False(s.MacSounds);
            Assert.False(s.ClickWallpaperShowsDesktop);
            Assert.False(s.MacScreenshots);
            Assert.False(s.MacShortcuts);
            Assert.False(s.RealAppMenus);
            Assert.False(s.StageManager);
            Assert.False(s.MissionDesktopTiles);
            Assert.Equal("grid", s.StackStyle);
            Assert.False(s.NeedsMouseHook);
        }

        [Theory]
        [InlineData(20, 19, 7, true)]
        [InlineData(3, 19, 7, true)]
        [InlineData(7, 19, 7, false)]
        [InlineData(12, 19, 7, false)]
        [InlineData(10, 9, 17, true)]   // oscuro de día (raro, pero vale)
        [InlineData(18, 9, 17, false)]
        [InlineData(5, 5, 5, false)]    // mismo horario: nunca oscuro
        public void ModoOscuroPorHora(int hour, int dark, int light, bool expected) =>
            Assert.Equal(expected, AutoAppearance.IsDarkAt(hour, dark, light));

        [Fact]
        public void CopiaDelRegistro_IdaYVuelta()
        {
            Assert.Equal((false, false, "", 0), RegBackup.Decode(RegBackup.Encode(null)));
            Assert.Equal((true, true, "", 1), RegBackup.Decode(RegBackup.Encode(1)));
            Assert.Equal((true, false, @"%SystemRoot%\cursors\aero_arrow.cur", 0), RegBackup.Decode(RegBackup.Encode(@"%SystemRoot%\cursors\aero_arrow.cur")));
            Assert.Equal((true, false, "", 0), RegBackup.Decode(""));
        }

        [Fact]
        public void Sacudida_SeDetecta()
        {
            var d = new ShakeDetector();
            bool hit = false;
            long t = 0; int x = 500;
            for (int i = 0; i < 6 && !hit; i++)
            {
                int dir = i % 2 == 0 ? 1 : -1;
                for (int k = 0; k < 6; k++) { t += 15; x += dir * 20; hit |= d.Add(t, x); }
            }
            Assert.True(hit);
        }

        [Fact]
        public void Sacudida_MovimientoNormalNo()
        {
            var d = new ShakeDetector();
            long t = 0; int x = 0;
            bool hit = false;
            for (int i = 0; i < 200; i++) { t += 15; x += 5; hit |= d.Add(t, x); }      // ir hacia un lado
            for (int i = 0; i < 20; i++) { t += 15; x += i % 2 == 0 ? 3 : -3; hit |= d.Add(t, x); } // temblor chico
            Assert.False(hit);
        }

        [Theory]
        [InlineData("SysListView32", "Progman", true)]
        [InlineData("SHELLDLL_DefView", "WorkerW", true)]
        [InlineData("SysListView32", "CabinetWClass", false)] // lista de una carpeta del Explorador
        [InlineData("Chrome_WidgetWin_1", "Chrome_WidgetWin_1", false)]
        public void ClicEnElFondo_SoloEscritorio(string cls, string root, bool expected) =>
            Assert.Equal(expected, WallpaperClick.IsDesktopClass(cls, root));

        [Fact]
        public void Abanico_SubeYSeCurva()
        {
            var (dx0, a0) = StackFan.Position(0);
            var (dx5, a5) = StackFan.Position(5);
            Assert.Equal(0, dx0); Assert.Equal(0, a0);
            Assert.True(dx5 > 0 && a5 > 0);
        }

        [Theory]
        [InlineData(0x43, MacKeyAction.Ctrl)]     // C
        [InlineData(0x56, MacKeyAction.Ctrl)]     // V
        [InlineData(0x51, MacKeyAction.Quit)]     // Q
        [InlineData(0x4D, MacKeyAction.Minimize)] // M
        [InlineData(0x48, MacKeyAction.Hide)]     // H
        [InlineData(0x20, MacKeyAction.Search)]   // Espacio
        [InlineData(0x09, MacKeyAction.None)]     // Tab (Alt+Tab sigue igual)
        [InlineData(0x73, MacKeyAction.None)]     // F4 (Alt+F4 sigue igual)
        public void AltComoCmd(int vk, MacKeyAction expected) => Assert.Equal(expected, MacKeys.ActionFor(vk));

        [Fact]
        public void NombreDeLaCaptura() =>
            Assert.Equal("Captura de pantalla 2026-11-23 a las 14.05.09.png", Screenshots.FileName(new DateTime(2026, 11, 23, 14, 5, 9)));

        [Theory]
        [InlineData("&Guardar\tCtrl+S", "Guardar", "Ctrl+S")]
        [InlineData("Tom && Jerry", "Tom & Jerry", "")]
        [InlineData("&Archivo", "Archivo", "")]
        public void MenusReales_Texto(string raw, string text, string shortcut) =>
            Assert.Equal((text, shortcut), AppMenus.Clean(raw));

        [Fact]
        public void Organizador_Recientes()
        {
            var l = StageManager.PushRecent(new[] { "a", "b", "c" }, "c", 3);
            Assert.Equal(new[] { "c", "a", "b" }, l);
            l = StageManager.PushRecent(l, "d", 3);
            Assert.Equal(new[] { "d", "c", "a" }, l);
        }

        [Theory]
        [InlineData(null, "grid", "grid")]
        [InlineData(null, "fan", "fan")]
        [InlineData("list", "fan", "list")]   // 1.8.1: la pila con su propia forma
        [InlineData("rara", "fan", "fan")]
        [InlineData(null, "rara", "grid")]
        public void Pilas_FormaDeCadaUna(string? own, string general, string expected) =>
            Assert.Equal(expected, StackFan.Effective(own, general));

        [Fact]
        public void CursoresYSonidos_VienenDentroDelExe()
        {
            var asm = typeof(MacCursors).Assembly;
            foreach (var (_, file) in MacCursors.Map)
                using (var st = asm.GetManifestResourceStream("McBar.Cursores." + file)) Assert.True(st != null, file);
            foreach (var (_, file) in MacSounds.Map)
                using (var st = asm.GetManifestResourceStream("McBar.Sonidos." + file)) Assert.True(st != null, file);
            using var shutter = asm.GetManifestResourceStream("McBar.Sonidos.captura.wav");
            Assert.NotNull(shutter);
        }

        [Fact]
        public void PlacaDeIcono_UnColorPorApp()
        {
            // 1.8.1: un ícono rojo y uno azul dan placas distintas, del mismo tono que el ícono
            var (rt, _) = IconShaper.PlateColors(System.Windows.Media.Color.FromRgb(220, 30, 30));
            var (bt, _) = IconShaper.PlateColors(System.Windows.Media.Color.FromRgb(20, 90, 230));
            Assert.True(rt.R > rt.B);
            Assert.True(bt.B > bt.R);
            // un ícono gris queda en gris claro
            var (gt, gb) = IconShaper.PlateColors(System.Windows.Media.Color.FromRgb(128, 128, 130));
            Assert.True(Math.Abs(gt.R - gt.B) < 6 && gb.R < gt.R);
        }

        [Fact]
        public void Hsl_IdaYVuelta()
        {
            var c = System.Windows.Media.Color.FromRgb(200, 120, 40);
            IconShaper.ToHsl(c, out var h, out var s2, out var l);
            var back = IconShaper.FromHsl(h, s2, l);
            Assert.True(Math.Abs(back.R - c.R) <= 1 && Math.Abs(back.G - c.G) <= 1 && Math.Abs(back.B - c.B) <= 1);
        }

        [Fact]
        public void Sanitize_PilasYHoras()
        {
            var s = new AppSettings { StackStyle = "rara", AutoDarkFrom = 40, AutoLightFrom = -3, ScreenshotFolder = null!, StackStyles = null! };
            SettingsStore.Sanitize(s);
            Assert.Equal("grid", s.StackStyle);
            Assert.Equal(23, s.AutoDarkFrom);
            Assert.Equal(0, s.AutoLightFrom);
            Assert.Equal("", s.ScreenshotFolder);
            Assert.NotNull(s.StackStyles);
        }
    }
}
