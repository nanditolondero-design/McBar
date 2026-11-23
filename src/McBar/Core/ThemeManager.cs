// McBar — Creado por Andrés Londero
// Cambia entre tema claro y oscuro en vivo (todos los colores usan DynamicResource).
using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace McBar.Core
{
    public static class ThemeManager
    {
        public static bool IsDark { get; private set; } = true;
        public static bool IsGlass => SettingsStore.Current.Theme == "glass";

        /// <summary>Se dispara después de cambiar de tema (para renovar desenfoques, etc.).</summary>
        public static event Action? ThemeChanged;

        public static void Start()
        {
            Apply();
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General && SettingsStore.Current.Theme == "auto")
                    Application.Current.Dispatcher.BeginInvoke(new Action(Apply));
            };
        }

        public static void Apply()
        {
            var pref = SettingsStore.Current.Theme;
            bool glass = pref == "glass";
            bool dark = glass || pref == "dark" || (pref == "auto" && SystemInfo.WindowsIsDark());
            IsDark = dark;
            var dict = new ResourceDictionary { Source = new Uri(dark ? "pack://application:,,,/Themes/Dark.xaml" : "pack://application:,,,/Themes/Light.xaml", UriKind.Absolute) };
            var md = Application.Current.Resources.MergedDictionaries;
            if (md.Count > 0) md[0] = dict; else md.Insert(0, dict);
            // 0.4.3: tema Vidrio = oscuro + capa de vidrio encima (al final, para que gane)
            for (int i = md.Count - 1; i >= 1; i--)
                if (md[i].Source != null && md[i].Source.OriginalString.EndsWith("Glass.xaml", StringComparison.OrdinalIgnoreCase)) md.RemoveAt(i);
            if (glass)
            {
                var g = new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/Glass.xaml", UriKind.Absolute) };
                ApplyGlassTransparency(g, SettingsStore.Current.GlassTransparency); // 1.6.1
                md.Add(g);
            }
            ThemeChanged?.Invoke();
        }

        /// <summary>Fondos del tema Vidrio a los que se les cambia la transparencia (los bordes y brillos no).</summary>
        public static readonly string[] GlassBackgrounds =
            { "Mc.GlassBg", "Mc.PanelBg", "Mc.TileBg", "Mc.DockGlass", "Mc.DockBg", "Mc.MenuBarBg", "Mc.LabelBg" };

        /// <summary>
        /// Opacidad nueva de un fondo según la transparencia elegida (0..100). 50 deja el valor original;
        /// 0 lo hace casi opaco y 100 casi invisible. Pura, se prueba.
        /// </summary>
        public static byte ScaleAlpha(byte alpha, int transparency)
        {
            transparency = Math.Clamp(transparency, 0, 100);
            double f = transparency <= 50
                ? 1 + (50 - transparency) / 50.0 * 2.0   // hasta 3 veces más opaco
                : 1 - (transparency - 50) / 50.0 * 0.85;  // hasta 15 % de lo original
            return (byte)Math.Clamp(Math.Round(alpha * f), 0, 255);
        }

        private static void ApplyGlassTransparency(ResourceDictionary dict, int transparency)
        {
            if (transparency == 50) return;
            foreach (var key in GlassBackgrounds)
            {
                switch (dict[key])
                {
                    case SolidColorBrush sb:
                        var c = sb.Color;
                        dict[key] = Frozen(new SolidColorBrush(Color.FromArgb(ScaleAlpha(c.A, transparency), c.R, c.G, c.B)));
                        break;
                    case LinearGradientBrush lg:
                        var copy = lg.Clone();
                        foreach (var st in copy.GradientStops) st.Color = Color.FromArgb(ScaleAlpha(st.Color.A, transparency), st.Color.R, st.Color.G, st.Color.B);
                        dict[key] = Frozen(copy);
                        break;
                }
            }
        }

        private static Brush Frozen(Brush b) { b.Freeze(); return b; }

        public static Color GetColor(string key) =>
            Application.Current.TryFindResource(key) is Color c ? c : Colors.Transparent;
    }
}
