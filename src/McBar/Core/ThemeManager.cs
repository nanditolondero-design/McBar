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
            if (glass) md.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/Glass.xaml", UriKind.Absolute) });
            ThemeChanged?.Invoke();
        }

        public static Color GetColor(string key) =>
            Application.Current.TryFindResource(key) is Color c ? c : Colors.Transparent;
    }
}
