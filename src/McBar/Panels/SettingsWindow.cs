// McBar — Creado por Andrés Londero
// Preferencias de McBar. Cada cambio se aplica al instante y se guarda en settings.json.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class SettingsWindow : Window
    {
        private readonly DispatcherTimer _apply;

        public SettingsWindow()
        {
            Title = "Preferencias de McBar";
            Width = 540;
            Height = 700;
            MinWidth = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SetResourceReference(BackgroundProperty, "Mc.WindowBg");
            SetResourceReference(FontFamilyProperty, "Mc.Font");
            Icon = McDialog.Logo();

            _apply = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _apply.Tick += (_, _) => { _apply.Stop(); SettingsStore.NotifyChanged(); };

            SourceInitialized += (_, _) => WindowOps.SetDarkTitleBar(this, ThemeManager.IsDark);
            ThemeManager.ThemeChanged += OnTheme;
            Closed += (_, _) => { ThemeManager.ThemeChanged -= OnTheme; if (_apply.IsEnabled) { _apply.Stop(); SettingsStore.NotifyChanged(); } App.ScheduleTrim(); };

            Content = new ScrollViewer { Content = Build(), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(26, 18, 26, 22) };
        }

        private void OnTheme()
        {
            if (new WindowInteropHelper(this).Handle != IntPtr.Zero) WindowOps.SetDarkTitleBar(this, ThemeManager.IsDark);
        }

        private void Changed() { _apply.Stop(); _apply.Start(); }

        private UIElement Build()
        {
            var s = SettingsStore.Current;
            var root = new StackPanel();

            // ---------- Dock ----------
            root.Children.Add(Header("Dock"));
            var dock = new StackPanel();
            dock.Children.Add(SliderSetting("Tamaño", 32, 80, s.DockIconSize, v => { s.DockIconSize = (int)v; if (s.MagnifiedSize < s.DockIconSize) s.MagnifiedSize = s.DockIconSize; }));
            dock.Children.Add(Switch("Ampliación", s.Magnification, v => s.Magnification = v));
            dock.Children.Add(SliderSetting("Tamaño ampliado", 48, 160, s.MagnifiedSize, v => s.MagnifiedSize = Math.Max((int)v, s.DockIconSize)));
            dock.Children.Add(Switch("Ocultar y mostrar el Dock automáticamente", s.DockAutoHide, v => s.DockAutoHide = v));
            dock.Children.Add(Switch("Animar al abrir aplicaciones", s.BounceOnLaunch, v => s.BounceOnLaunch = v));
            dock.Children.Add(Switch("Mostrar indicadores de apps abiertas", s.ShowIndicators, v => s.ShowIndicators = v));
            dock.Children.Add(Switch("Clic en la app activa la minimiza (como Windows)", s.ClickMinimizes, v => s.ClickMinimizes = v));
            dock.Children.Add(Segment("Estilo", new[] { ("classic", "Clásico"), ("floating", "Flotante"), ("full", "Ancho completo") },
                s.DockStyle, v => { s.DockStyle = v; Changed(); }));
            dock.Children.Add(Switch("El fondo se estira al ampliar (como macOS)", s.DockBackgroundStretch, v => s.DockBackgroundStretch = v));
            dock.Children.Add(Switch("Mostrar el Dock en todos los monitores", s.DockOnAllMonitors, v => s.DockOnAllMonitors = v));
            dock.Children.Add(Switch("Animar ventanas al minimizar y restaurar (hacia el Dock)", s.AnimateWindows, v => s.AnimateWindows = v));
            root.Children.Add(Group(dock));

            // ---------- Barra de menú ----------
            root.Children.Add(Header("Barra superior"));
            var bar = new StackPanel();
            bar.Children.Add(Switch("Mostrar la barra superior", s.ShowMenuBar, v => s.ShowMenuBar = v));
            bar.Children.Add(Segment("Tipo", new[] { ("menubar", "Barra de menú"), ("island", "Isla dinámica") },
                s.TopBarMode, v => { s.TopBarMode = v; Changed(); }));
            bar.Children.Add(Switch("Mostrar el porcentaje de batería", s.ShowBatteryPercent, v => s.ShowBatteryPercent = v));
            bar.Children.Add(Switch("Mostrar la fecha en el reloj", s.ShowDateInClock, v => s.ShowDateInClock = v));
            bar.Children.Add(Switch("Mostrar el clima", s.ShowWeatherInMenuBar, v => s.ShowWeatherInMenuBar = v));
            bar.Children.Add(Switch("Mostrar la barra en todos los monitores", s.MenuBarOnAllMonitors, v => s.MenuBarOnAllMonitors = v));
            root.Children.Add(Group(bar));

            // ---------- Isla dinámica ----------
            root.Children.Add(Header("Isla dinámica"));
            var isl = new StackPanel();
            isl.Children.Add(Switch("Dejar lugar arriba para la isla (las ventanas no quedan debajo)", s.IslandReserveSpace, v => s.IslandReserveSpace = v));
            isl.Children.Add(Switch("Música y videos (con controles)", s.IslandMedia, v => s.IslandMedia = v));
            isl.Children.Add(Switch("Volumen y brillo", s.IslandVolume, v => s.IslandVolume = v));
            isl.Children.Add(Switch("Batería y carga", s.IslandBattery, v => s.IslandBattery = v));
            isl.Children.Add(Switch("Notificaciones", s.IslandNotifications, v => s.IslandNotifications = v));
            var mode = new TextBlock { Text = "Notificaciones: " + McBar.Core.ActivityHub.NotificationMode, FontSize = 11.5, Margin = new Thickness(0, 4, 0, 2), TextWrapping = TextWrapping.Wrap };
            mode.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            isl.Children.Add(mode);
            root.Children.Add(Group(isl));

            // ---------- Bandeja ----------
            root.Children.Add(Header("Bandeja del sistema"));
            var tray = new StackPanel();
            tray.Children.Add(Switch("Mostrar los íconos de las apps en la barra de McBar", s.UseOwnTray, v => s.UseOwnTray = v));
            tray.Children.Add(Switch("Íconos en blanco y negro (estilo Mac)", s.TrayMonochrome, v => s.TrayMonochrome = v));
            tray.Children.Add(TrayList(s));
            root.Children.Add(Group(tray));

            // ---------- General ----------
            root.Children.Add(Header("General"));
            var gen = new StackPanel();
            gen.Children.Add(Switch("Ocultar la barra de tareas de Windows", s.HideWindowsTaskbar, v => s.HideWindowsTaskbar = v));
            gen.Children.Add(Switch("Abrir McBar al iniciar Windows", s.StartWithWindows, v => s.StartWithWindows = v));
            gen.Children.Add(ThemeSelector(s));
            root.Children.Add(Group(gen));

            // ---------- Widgets ----------
            root.Children.Add(Header("Widgets"));
            var wid = new StackPanel();
            wid.Children.Add(CityEditor(s));
            wid.Children.Add(Switch("Calendario", s.WidgetCalendar, v => s.WidgetCalendar = v));
            wid.Children.Add(Switch("Clima", s.WidgetWeather, v => s.WidgetWeather = v));
            wid.Children.Add(Switch("Batería", s.WidgetBattery, v => s.WidgetBattery = v));
            wid.Children.Add(Switch("Sistema (procesador y memoria)", s.WidgetSystem, v => s.WidgetSystem = v));
            wid.Children.Add(Switch("Notas", s.WidgetNotes, v => s.WidgetNotes = v));
            root.Children.Add(Group(wid));

            // ---------- Pie ----------
            var foot = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            var about = new Button { Content = "Acerca de McBar", HorizontalAlignment = HorizontalAlignment.Left };
            about.SetResourceReference(StyleProperty, "McButton");
            about.Click += (_, _) => McDialog.ShowAbout();
            var quit = new Button { Content = "Salir de McBar", HorizontalAlignment = HorizontalAlignment.Right };
            quit.SetResourceReference(StyleProperty, "McButton");
            quit.Click += (_, _) =>
            {
                if (McDialog.Confirm("¿Salir de McBar?", "Se va a volver a mostrar la barra de tareas de Windows.", "Salir")) App.Quit();
            };
            foot.Children.Add(about);
            foot.Children.Add(quit);
            root.Children.Add(foot);

            var credit = new TextBlock { Text = "Creado por Andrés Londero", FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };
            credit.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            root.Children.Add(credit);
            return root;
        }

        private static TextBlock Header(string text)
        {
            var t = new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 16, 0, 6) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            return t;
        }

        private static Border Group(UIElement child)
        {
            var b = new Border { Child = child, Padding = new Thickness(14, 8, 14, 8), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) };
            b.SetResourceReference(Border.BackgroundProperty, "Mc.TileBg");
            b.SetResourceReference(Border.BorderBrushProperty, "Mc.TileBorder");
            return b;
        }

        private CheckBox Switch(string text, bool value, Action<bool> set)
        {
            var c = new CheckBox { Content = text, IsChecked = value };
            c.SetResourceReference(StyleProperty, "McSwitch");
            c.Checked += (_, _) => { set(true); Changed(); };
            c.Unchecked += (_, _) => { set(false); Changed(); };
            return c;
        }

        private UIElement SliderSetting(string text, double min, double max, double value, Action<double> set)
        {
            var g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var val = new TextBlock { Text = ((int)value).ToString(), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            val.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            var s = new Slider { Minimum = min, Maximum = max, Value = value, IsSnapToTickEnabled = true, TickFrequency = 2 };
            s.SetResourceReference(StyleProperty, "McSlider");
            s.ValueChanged += (_, e) => { val.Text = ((int)e.NewValue).ToString(); set(e.NewValue); Changed(); };
            Grid.SetColumn(s, 1);
            Grid.SetColumn(val, 2);
            g.Children.Add(label);
            g.Children.Add(s);
            g.Children.Add(val);
            return g;
        }

        /// <summary>Lista de los íconos de bandeja actuales: los apagados van al menú ⌃ de la barra.</summary>
        private UIElement TrayList(AppSettings s)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            var host = McBar.Native.TrayHost.Current;
            if (host == null || host.Icons.Count == 0)
            {
                var t = new TextBlock { Text = "Los íconos aparecen acá cuando las apps los agregan.", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
                t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
                sp.Children.Add(t);
                return sp;
            }
            var hint = new TextBlock { Text = "Visibles en la barra (los apagados quedan en el menú ⌃):", FontSize = 12, Margin = new Thickness(0, 0, 0, 4) };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            sp.Children.Add(hint);
            foreach (var ic in host.Icons.Where(i => i.Icon != null).GroupBy(i => i.StableId).Select(g => g.First()))
            {
                string name = !string.IsNullOrWhiteSpace(ic.Tooltip) ? ic.Tooltip.Split('\n')[0] : System.IO.Path.GetFileNameWithoutExtension(ic.ExePath);
                if (name.Length > 48) name = name.Substring(0, 47) + "…";
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new Image { Source = ic.Icon, Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
                var label = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(label);
                var id = ic.StableId;
                var c = new CheckBox { Content = row, IsChecked = !s.TrayHiddenIds.Contains(id) };
                c.SetResourceReference(StyleProperty, "McSwitch");
                c.Checked += (_, _) => { s.TrayHiddenIds.Remove(id); Changed(); };
                c.Unchecked += (_, _) => { if (!s.TrayHiddenIds.Contains(id)) s.TrayHiddenIds.Add(id); Changed(); };
                sp.Children.Add(c);
            }
            return sp;
        }

        private UIElement Segment(string text, (string key, string name)[] options, string current, Action<string> set)
        {
            var g = new Grid { Margin = new Thickness(0, 6, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            g.Children.Add(label);
            var seg = new StackPanel { Orientation = Orientation.Horizontal };
            var segBorder = new Border { Child = seg, CornerRadius = new CornerRadius(8), Padding = new Thickness(1) };
            segBorder.SetResourceReference(Border.BackgroundProperty, "Mc.Hover");
            string group = "Seg" + text;
            foreach (var (key, name) in options)
            {
                var rb = new RadioButton { Content = name, GroupName = group, IsChecked = current == key };
                rb.SetResourceReference(StyleProperty, "McSegment");
                var k = key;
                rb.Checked += (_, _) => set(k);
                seg.Children.Add(rb);
            }
            Grid.SetColumn(segBorder, 1);
            g.Children.Add(segBorder);
            return g;
        }

        private UIElement ThemeSelector(AppSettings s)
        {
            var g = new Grid { Margin = new Thickness(0, 6, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock { Text = "Apariencia", VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            g.Children.Add(label);

            var seg = new StackPanel { Orientation = Orientation.Horizontal };
            var segBorder = new Border { Child = seg, CornerRadius = new CornerRadius(8), Padding = new Thickness(1) };
            segBorder.SetResourceReference(Border.BackgroundProperty, "Mc.Hover");
            foreach (var (key, name) in new[] { ("auto", "Automática"), ("light", "Clara"), ("dark", "Oscura") })
            {
                var rb = new RadioButton { Content = name, GroupName = "McTheme", IsChecked = s.Theme == key };
                rb.SetResourceReference(StyleProperty, "McSegment");
                var k = key;
                rb.Checked += (_, _) => { s.Theme = k; SettingsStore.Save(); ThemeManager.Apply(); };
                seg.Children.Add(rb);
            }
            Grid.SetColumn(segBorder, 1);
            g.Children.Add(segBorder);
            return g;
        }

        private UIElement CityEditor(AppSettings s)
        {
            var g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock { Text = "Ciudad del clima", VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var box = new TextBox { Text = s.WeatherCity };
            box.SetResourceReference(StyleProperty, "McTextBox");
            var save = new Button { Content = "Aplicar", Margin = new Thickness(8, 0, 0, 0) };
            save.SetResourceReference(StyleProperty, "McButton");
            save.Click += (_, _) => { s.WeatherCity = box.Text.Trim(); s.WeatherResolvedFor = null; SettingsStore.Save(); };
            Grid.SetColumn(box, 1);
            Grid.SetColumn(save, 2);
            g.Children.Add(label);
            g.Children.Add(box);
            g.Children.Add(save);
            return g;
        }
    }
}
