// McBar — Creado por Andrés Londero
// Preferencias de McBar. Cada cambio se aplica al instante y se guarda en settings.json.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class SettingsWindow : Window
    {
        private readonly DispatcherTimer _apply;
        private int _segCounter;

        public SettingsWindow()
        {
            Title = "Preferencias de McBar";
            Width = 780;
            Height = 640;
            MinWidth = 640;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SetResourceReference(BackgroundProperty, "Mc.SettingsBg"); // 1.8.3: de vidrio con el tema Vidrio
            SetResourceReference(FontFamilyProperty, "Mc.Font");
            Icon = McDialog.Logo();

            _apply = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _apply.Tick += (_, _) => { _apply.Stop(); SettingsStore.NotifyChanged(); };

            SourceInitialized += (_, _) => { WindowOps.SetDarkTitleBar(this, ThemeManager.IsDark); WindowOps.SetGlassBackdrop(this, ThemeManager.IsGlass); };
            ThemeManager.ThemeChanged += OnTheme;
            Closed += (_, _) => { ThemeManager.ThemeChanged -= OnTheme; if (_apply.IsEnabled) { _apply.Stop(); SettingsStore.NotifyChanged(); } App.ScheduleTrim(); };

            Content = Tabbed(Build());
            Loaded += (_, _) => App.KeepBelowTopBar(this); // 1.3.1: que la isla no tape la parte de arriba

            // 1.3.0: al cambiar de perfil o importar, se vuelve a armar con los ajustes nuevos (en la misma pestaña)
            Action reload = () => Dispatcher.BeginInvoke(new Action(() => { if (IsLoaded) Content = Tabbed(Build()); }));
            Profiles.ProfileChanged += reload;
            Closed += (_, _) => Profiles.ProfileChanged -= reload;
        }

        private void OnTheme()
        {
            if (new WindowInteropHelper(this).Handle == IntPtr.Zero) return;
            WindowOps.SetDarkTitleBar(this, ThemeManager.IsDark);
            WindowOps.SetGlassBackdrop(this, ThemeManager.IsGlass); // 1.8.3
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
            // 1.8.9: como la píldora
            dock.Children.Add(Switch("Esconder el Dock con ventanas maximizadas", s.DockHideOnMaximized, v => s.DockHideOnMaximized = v));
            // 1.9.1: botón de apagado/reinicio al lado de la Papelera (es una "pila" más del Dock)
            var power = new CheckBox { Content = "Botón de apagado en el Dock (apagar, reiniciar, suspender…)", IsChecked = s.Stacks.Any(x => x.Target == AppIdentity.Power) };
            power.SetResourceReference(StyleProperty, "McSwitch");
            power.Click += (_, _) =>
            {
                bool on = power.IsChecked == true;
                s.Stacks.RemoveAll(x => x.Target == AppIdentity.Power);
                if (on)
                {
                    int trash = s.Stacks.FindIndex(x => x.Target == AppIdentity.Trash);
                    var pin = new PinnedApp { Name = "Apagar", Target = AppIdentity.Power };
                    if (trash >= 0) s.Stacks.Insert(trash + 1, pin); else s.Stacks.Add(pin);
                }
                SettingsStore.NotifyPinsChanged(null); // guarda y rearma los Docks
            };
            dock.Children.Add(power);
            dock.Children.Add(Switch("Animar al abrir aplicaciones", s.BounceOnLaunch, v => s.BounceOnLaunch = v));
            dock.Children.Add(Switch("Mostrar indicadores de apps abiertas", s.ShowIndicators, v => s.ShowIndicators = v));
            dock.Children.Add(Switch("Clic en la app activa la minimiza (como Windows)", s.ClickMinimizes, v => s.ClickMinimizes = v));
            dock.Children.Add(Segment("Estilo", new[] { ("classic", "Clásico"), ("floating", "Flotante"), ("full", "Ancho completo"), ("taskbar", "Barra"), ("icons", "Solo íconos") },
                s.DockStyle, v => { s.DockStyle = v; Changed(); }));
            dock.Children.Add(Switch("El fondo se estira al ampliar (como macOS)", s.DockBackgroundStretch, v => s.DockBackgroundStretch = v));
            // 1.6.1: se quitó "Desenfoque detrás del Dock" (rompía el Dock); queda siempre apagado
            dock.Children.Add(Switch("Animaciones rápidas (apagar solo si el Dock o la isla se ven con fondo negro)", s.FastWindows, v => s.FastWindows = v));
            dock.Children.Add(Switch("Cada Dock muestra solo las apps abiertas en su monitor", s.DockAppsPerMonitor, v => s.DockAppsPerMonitor = v));
            dock.Children.Add(Switch("Mostrar el Dock en todos los monitores", s.DockOnAllMonitors, v => s.DockOnAllMonitors = v));
            dock.Children.Add(Switch("Animar ventanas al minimizar y restaurar (hacia el Dock)", s.AnimateWindows, v => s.AnimateWindows = v));
            dock.Children.Add(Switch("Vista previa de las ventanas al dejar el mouse sobre una app", s.DockPreviews, v => s.DockPreviews = v));
            // 1.3.2: todos los íconos de apps con la forma de macOS
            dock.Children.Add(Segment("Forma de los íconos", new[] { ("none", "Original"), ("mac", "Como en Mac") },
                s.IconShape, v => { s.IconShape = v; Changed(); }));
            dock.Children.Add(IconPackRow(s));
            dock.Children.Add(MacIconsBox(s)); // 1.4.0
            var restore = new Button { Content = "Volver a poner la Papelera y Descargas", Margin = new Thickness(0, 10, 0, 4), HorizontalAlignment = HorizontalAlignment.Left };
            restore.SetResourceReference(StyleProperty, "McButton");
            restore.Click += (_, _) => App.Dock?.RestoreDefaultStacks();
            dock.Children.Add(restore);
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
            isl.Children.Add(Segment("Estilo", new[] { ("black", "Negro"), ("graphite", "Grafito"), ("glass", "Vidrio"), ("accent", "Color") },
                s.IslandStyle, v => { s.IslandStyle = v; Changed(); }));
            isl.Children.Add(Segment("Tamaño", new[] { ("small", "Chica"), ("normal", "Normal"), ("large", "Grande") },
                s.IslandSize, v => { s.IslandSize = v; Changed(); }));
            isl.Children.Add(Switch("Mostrar el día junto a la hora", s.IslandShowDate, v => s.IslandShowDate = v));
            isl.Children.Add(Switch("Borde brillante", s.IslandGlow, v => s.IslandGlow = v));
            isl.Children.Add(Switch("Dejar lugar arriba para la isla (las ventanas no quedan debajo)", s.IslandReserveSpace, v => s.IslandReserveSpace = v));
            // 1.6.1: como en pantalla completa: se esconde y queda la rayita arriba (al pasar el mouse vuelve)
            isl.Children.Add(Switch("Esconder la isla con ventanas maximizadas", s.IslandHideOnMaximized, v => s.IslandHideOnMaximized = v));
            isl.Children.Add(Switch("Música y videos (con controles)", s.IslandMedia, v => s.IslandMedia = v));
            isl.Children.Add(Switch("Volumen y brillo", s.IslandVolume, v => s.IslandVolume = v));
            isl.Children.Add(Switch("Batería y carga", s.IslandBattery, v => s.IslandBattery = v));
            isl.Children.Add(Switch("Notificaciones", s.IslandNotifications, v => s.IslandNotifications = v));
            // 1.2.0: actividades en vivo (también se ven en la barra de menú)
            isl.Children.Add(Switch("Temporizador, cronómetro y descargas en vivo", s.LiveActivities, v => s.LiveActivities = v));
            isl.Children.Add(Switch("Progreso de las descargas", s.LiveDownloads, v => s.LiveDownloads = v));
            isl.Children.Add(Switch("Avisar cuando una app usa el micrófono o la cámara", s.PrivacyDots, v => s.PrivacyDots = v));
            var mode = new TextBlock { Text = "Notificaciones: " + McBar.Core.ActivityHub.NotificationMode, FontSize = 11.5, Margin = new Thickness(0, 4, 0, 2), TextWrapping = TextWrapping.Wrap };
            mode.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            isl.Children.Add(mode);
            root.Children.Add(Group(isl));

            // ---------- Notificaciones (0.4.0) ----------
            root.Children.Add(Header("Notificaciones"));
            var nt = new StackPanel();
            nt.Children.Add(Switch("Centro de notificaciones propio (en la píldora y en la barra)", s.OwnNotifications, v => s.OwnNotifications = v));
            nt.Children.Add(Switch("Usar Win+N y Win+A de McBar (en lugar de los paneles de Windows)", s.OwnShortcuts, v => s.OwnShortcuts = v));
            nt.Children.Add(Switch("Ocultar los avisos de Windows (experimental)", s.HideWindowsToasts, v => s.HideWindowsToasts = v));
            root.Children.Add(Group(nt));

            // ---------- Teclado (0.6.0) ----------
            root.Children.Add(Header("Teclado"));
            var kb = new StackPanel();
            kb.Children.Add(Switch("Tecla Windows sola: abrir el Launchpad (en vez de Inicio)", s.WinKeyLaunchpad, v => s.WinKeyLaunchpad = v));
            kb.Children.Add(Switch("Win+S: buscador de McBar (apps, archivos y cuentas)", s.OwnSearch, v => s.OwnSearch = v));
            kb.Children.Add(Switch("Teclas de volumen con el cartel de McBar", s.OwnVolumeKeys, v => s.OwnVolumeKeys = v));
            kb.Children.Add(Switch("Alt+Tab estilo Mac (íconos de las apps)", s.OwnAltTab, v => s.OwnAltTab = v));
            kb.Children.Add(Switch("Win+Tab: ver todas las ventanas (Mission Control)", s.OwnWinTab, v => s.OwnWinTab = v));
            kb.Children.Add(Switch("Espacio en el Explorador: vista previa rápida (Quick Look)", s.QuickLook, v => s.QuickLook = v));
            kb.Children.Add(Switch("Ctrl+Alt+flechas: acomodar ventanas en mosaico", s.TileShortcuts, v => s.TileShortcuts = v));
            var kbTip = new TextBlock { Text = "Win+N abre las notificaciones y Win+A el Centro de control (se cambian en Notificaciones).", FontSize = 11.5, Margin = new Thickness(0, 4, 0, 2), TextWrapping = TextWrapping.Wrap };
            kbTip.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            kb.Children.Add(kbTip);
            root.Children.Add(Group(kb));

            // ---------- Esquinas activas (1.0.0) ----------
            root.Children.Add(Header("Esquinas activas"));
            var hc = new StackPanel();
            hc.Children.Add(CornerRow("Arriba a la izquierda", s.CornerTopLeft, v => s.CornerTopLeft = v));
            hc.Children.Add(CornerRow("Arriba a la derecha", s.CornerTopRight, v => s.CornerTopRight = v));
            hc.Children.Add(CornerRow("Abajo a la izquierda", s.CornerBottomLeft, v => s.CornerBottomLeft = v));
            hc.Children.Add(CornerRow("Abajo a la derecha", s.CornerBottomRight, v => s.CornerBottomRight = v));
            var hcTip = new TextBlock { Text = "Llevá el mouse a esa esquina de cualquier pantalla y dejalo un instante.", FontSize = 11.5, Margin = new Thickness(0, 4, 0, 2), TextWrapping = TextWrapping.Wrap };
            hcTip.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            hc.Children.Add(hcTip);
            root.Children.Add(Group(hc));

            // ---------- Launchpad (0.4.0) ----------
            root.Children.Add(Header("Launchpad"));
            var lp = new StackPanel();
            lp.Children.Add(SliderSetting("Transparencia del fondo", 0, 100, s.LaunchpadTransparency, v => s.LaunchpadTransparency = (int)v));
            lp.Children.Add(Switch("Desenfoque del fondo", s.LaunchpadBlur, v => s.LaunchpadBlur = v));
            var tip = new TextBlock { Text = "Carpetas: arrastrá una app sobre otra. Clic derecho para más opciones.", FontSize = 11.5, Margin = new Thickness(0, 4, 0, 2), TextWrapping = TextWrapping.Wrap };
            tip.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            lp.Children.Add(tip);
            root.Children.Add(Group(lp));

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
            // 1.9.1: que McBar abra antes y no se vea la barra de Windows al iniciar
            gen.Children.Add(Switch("Abrir sin la espera de Windows al iniciar (también los otros programas de inicio)", s.FastStartup, v => s.FastStartup = v));
            gen.Children.Add(ThemeSelector(s));
            // 1.6.1: cuánto se transparenta el tema Vidrio (paneles, Dock, barra)
            gen.Children.Add(SliderSetting("Transparencia del vidrio", 0, 100, s.GlassTransparency, v => s.GlassTransparency = (int)v));
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
            wid.Children.Add(Switch("Recordatorios", s.WidgetReminders, v => s.WidgetReminders = v));
            root.Children.Add(Group(wid));

            // ---------- 1.1.0: widgets en el escritorio y calendario ----------
            root.Children.Add(Group(DesktopWidgetsBox()));
            root.Children.Add(Group(CalendarBox(s)));

            // ---------- Estilo Mac (1.8.0): todo opcional, todo apagado de fábrica ----------
            root.Children.Add(Header("Estilo Mac"));
            root.Children.Add(Spaced(Group(MacLookAppearance(s))));
            root.Children.Add(Spaced(Group(MacLookWindows(s))));
            root.Children.Add(Group(MacLookKeyboard(s)));

            // ---------- Personalización (1.3.0) ----------
            root.Children.Add(Header("Personalización"));
            root.Children.Add(Spaced(Group(ProfilesBox(s))));
            root.Children.Add(Spaced(Group(GameModeBox())));
            root.Children.Add(Spaced(Group(WallpaperBox(s))));
            root.Children.Add(Group(BackupBox()));

            // ---------- Diagnóstico (0.9.0) ----------
            root.Children.Add(Header("Diagnóstico"));
            root.Children.Add(Group(DiagnosticsBox()));

            // ---------- Pie ----------
            var foot = new Grid { Margin = new Thickness(0, 10, 0, 0), Tag = "tail" };
            var about = new Button { Content = "Acerca de McBar", HorizontalAlignment = HorizontalAlignment.Left };
            about.SetResourceReference(StyleProperty, "McButton");
            about.Click += (_, _) => McDialog.ShowAbout();
            // 1.8.8: si se está probando sin instalar, botón para instalar (al medio, abajo)
            if (App.IsTrying)
            {
                var inst = new Button { Content = "Instalar McBar…", HorizontalAlignment = HorizontalAlignment.Center };
                inst.SetResourceReference(StyleProperty, "McAccentButton");
                inst.Click += (_, _) => App.InstallNow();
                foot.Children.Add(inst);
            }
            var quit = new Button { Content = "Salir de McBar", HorizontalAlignment = HorizontalAlignment.Right };
            quit.SetResourceReference(StyleProperty, "McButton");
            quit.Click += (_, _) =>
            {
                if (McDialog.Confirm("¿Salir de McBar?", "Se va a volver a mostrar la barra de tareas de Windows.", "Salir")) App.Quit();
            };
            foot.Children.Add(about);
            foot.Children.Add(quit);
            root.Children.Add(foot);

            var credit = new TextBlock { Text = "Creado por Andrés Londero", FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), Tag = "tail" };
            credit.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            root.Children.Add(credit);
            return root;
        }

        // ================== Pestañas (0.4.1) ==================

        private static string _lastTab = "";

        /// <summary>
        /// Ordena las Preferencias en pestañas, como en macOS: lista de secciones a la izquierda y el
        /// contenido de la elegida a la derecha. Cada sección empieza en un título (Header) y los botones de
        /// abajo (Acerca de / Salir) quedan siempre visibles.
        /// </summary>
        private UIElement Tabbed(UIElement builtElement)
        {
            if (builtElement is not StackPanel built) return builtElement; // Build() siempre devuelve un StackPanel
            var sections = new List<(string title, StackPanel body)>();
            var tail = new StackPanel { Margin = new Thickness(26, 0, 26, 14) };
            var items = built.Children.Cast<UIElement>().ToList();
            built.Children.Clear();
            StackPanel? cur = null;
            foreach (var el in items)
            {
                if (el is FrameworkElement fe && Equals(fe.Tag, "tail")) { tail.Children.Add(el); continue; }
                if (el is TextBlock tb && Equals(tb.Tag, "header"))
                {
                    cur = new StackPanel();
                    sections.Add((tb.Text, cur));
                    tb.Margin = new Thickness(4, 0, 0, 12);
                    tb.FontSize = 20;
                    tb.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
                    cur.Children.Add(tb);
                    continue;
                }
                if (cur == null) { cur = new StackPanel(); sections.Add(("General", cur)); }
                cur.Children.Add(el);
            }

            var content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(26, 20, 26, 16) };
            var side = new StackPanel { Margin = new Thickness(10, 16, 10, 10) };
            var buttons = new List<Border>();
            void Select(int i)
            {
                _lastTab = sections[i].title;
                content.Content = sections[i].body;
                content.ScrollToTop();
                for (int k = 0; k < buttons.Count; k++)
                {
                    if (k == i) buttons[k].SetResourceReference(Border.BackgroundProperty, "Mc.Accent");
                    else buttons[k].Background = Brushes.Transparent;
                    if (buttons[k].Child is TextBlock t)
                    {
                        if (k == i) t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.AccentText");
                        else t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
                    }
                }
            }
            for (int i = 0; i < sections.Count; i++)
            {
                int idx = i;
                var t = new TextBlock { Text = sections[i].title, FontSize = 13 };
                var b = new Border { Child = t, Padding = new Thickness(12, 7, 12, 7), CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 1, 0, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent };
                b.MouseLeftButtonUp += (_, _) => Select(idx);
                buttons.Add(b);
                side.Children.Add(b);
            }

            var sideHost = new Border { Child = side, BorderThickness = new Thickness(0, 0, 1, 0) };
            sideHost.SetResourceReference(Border.BackgroundProperty, "Mc.TileBg");
            sideHost.SetResourceReference(Border.BorderBrushProperty, "Mc.TileBorder");

            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition());
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.Children.Add(content);
            Grid.SetRow(tail, 1);
            right.Children.Add(tail);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(sideHost);
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            int start = Math.Max(0, sections.FindIndex(x => x.title == _lastTab));
            if (sections.Count > 0) Select(start);
            return grid;
        }

        private static TextBlock Header(string text)
        {
            var t = new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 16, 0, 6), Tag = "header" };
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
            // 1.8.1: el texto baja de renglón si no entra (antes se cortaba)
            var c = new CheckBox { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, IsChecked = value };
            c.SetResourceReference(StyleProperty, "McSwitch");
            c.Checked += (_, _) => { set(true); Changed(); };
            c.Unchecked += (_, _) => { set(false); Changed(); };
            return c;
        }

        // ================== Personalización (1.3.0) ==================

        private static TextBlock BoxTitle(string text)
        {
            var t = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 2) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            return t;
        }

        private static TextBlock Tip(string text)
        {
            var t = new TextBlock { Text = text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            return t;
        }

        private static Button Btn(string text, Action click, Thickness? margin = null)
        {
            var b = new Button { Content = text, Margin = margin ?? new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            b.SetResourceReference(StyleProperty, "McButton");
            b.Click += (_, _) => click();
            return b;
        }

        private static Border Spaced(Border group)
        {
            group.Margin = new Thickness(0, 0, 0, 10);
            return group;
        }

        /// <summary>Aplica ya los cambios pendientes de esta ventana (antes de cambiar de perfil o importar).</summary>
        private void FlushPending()
        {
            if (_apply.IsEnabled) { _apply.Stop(); SettingsStore.NotifyChanged(); }
        }

        /// <summary>Perfiles: lista (Usar, modo juego, Borrar) y guardar los ajustes actuales como perfil.</summary>
        private UIElement ProfilesBox(AppSettings s)
        {
            var sp = new StackPanel();
            void Render()
            {
                sp.Children.Clear();
                sp.Children.Add(BoxTitle("Perfiles"));
                sp.Children.Add(Tip("Un perfil guarda todos los ajustes: Dock y apps fijadas, barra o isla, tema, widgets… Por ejemplo «Trabajo» y «Juegos». Al pasar a otro perfil, lo que cambiaste en el actual queda guardado en él."));
                foreach (var p in s.Profiles.ToList())
                {
                    var prof = p;
                    var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    bool active = prof.Name == s.ActiveProfile;
                    var name = new TextBlock { Text = active ? prof.Name + "  (en uso)" : prof.Name, VerticalAlignment = VerticalAlignment.Center, FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal };
                    name.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
                    g.Children.Add(name);
                    var game = new CheckBox { Content = "Modo juego", IsChecked = prof.GameMode, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                    game.SetResourceReference(StyleProperty, "McSwitch");
                    game.Click += (_, _) =>
                    {
                        prof.GameMode = game.IsChecked == true;
                        SettingsStore.Save();
                        if (prof.Name == s.ActiveProfile) GameMode.Set(prof.GameMode);
                    };
                    Grid.SetColumn(game, 1);
                    g.Children.Add(game);
                    var btns = new StackPanel { Orientation = Orientation.Horizontal };
                    btns.Children.Add(Btn(active ? "Actualizar" : "Usar", () =>
                    {
                        FlushPending();
                        if (prof.Name == s.ActiveProfile) { Profiles.SaveAs(s, prof.Name); SettingsStore.Save(); Render(); }
                        else App.UseProfile(prof.Name); // Preferencias se vuelve a armar sola (ProfileChanged)
                    }));
                    btns.Children.Add(Btn("Borrar", () =>
                    {
                        if (!McDialog.Confirm($"¿Borrar el perfil «{prof.Name}»?", "Los ajustes actuales no cambian.", "Borrar")) return;
                        s.Profiles.Remove(prof);
                        if (s.ActiveProfile == prof.Name) s.ActiveProfile = "";
                        SettingsStore.Save();
                        Render();
                    }, new Thickness(0)));
                    Grid.SetColumn(btns, 2);
                    g.Children.Add(btns);
                    sp.Children.Add(g);
                }

                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 2) };
                var box = new TextBox { Width = 180, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), ToolTip = "Nombre del perfil" };
                box.SetResourceReference(StyleProperty, "McTextBox");
                void SaveNew(string n, bool? game)
                {
                    n = n.Trim();
                    if (n.Length == 0) return;
                    if (Profiles.Find(s, n) != null && !McDialog.Confirm($"Ya hay un perfil «{n}»", "¿Reemplazarlo con los ajustes actuales?", "Reemplazar")) return;
                    FlushPending();
                    Profiles.SaveAs(s, n, game);
                    SettingsStore.Save();
                    Render();
                }
                row.Children.Add(box);
                row.Children.Add(Btn("Guardar los ajustes actuales como perfil", () => SaveNew(box.Text, null)));
                sp.Children.Add(row);
                if (s.Profiles.Count == 0)
                {
                    var quick = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 2) };
                    quick.Children.Add(Btn("+ Trabajo", () => SaveNew("Trabajo", false)));
                    quick.Children.Add(Btn("+ Juegos (con modo juego)", () => SaveNew("Juegos", true)));
                    sp.Children.Add(quick);
                }
            }
            Render();
            return sp;
        }

        /// <summary>Modo juego: interruptor y qué hace.</summary>
        private UIElement GameModeBox()
        {
            var sp = new StackPanel();
            sp.Children.Add(BoxTitle("Modo juego"));
            sp.Children.Add(Tip("Mientras está prendido: la tecla Windows no abre el Launchpad ni el Inicio, las esquinas activas no hacen nada, las notificaciones no aparecen en la isla (quedan guardadas) y el Dock se oculta solo. También se prende desde el Centro de control, el menú de McBar o el buscador («modo juego»)."));
            var c = new CheckBox { Content = "Modo juego", IsChecked = GameMode.On };
            c.SetResourceReference(StyleProperty, "McSwitch");
            c.Click += (_, _) => GameMode.Set(c.IsChecked == true);
            Action sync = () => Dispatcher.BeginInvoke(new Action(() => c.IsChecked = GameMode.On));
            GameMode.Changed += sync;
            Closed += (_, _) => GameMode.Changed -= sync;
            sp.Children.Add(c);
            return sp;
        }

        /// <summary>Fondo de pantalla según la hora: una imagen por momento del día.</summary>
        private UIElement WallpaperBox(AppSettings s)
        {
            var sp = new StackPanel();
            sp.Children.Add(BoxTitle("Fondo de pantalla según la hora"));
            sp.Children.Add(Tip("Elegí una imagen para cada momento del día (como los fondos dinámicos de macOS). Si un momento no tiene imagen, sigue la del anterior."));
            var byTime = Switch("Cambiar el fondo de pantalla según la hora", s.WallpaperByTime, v => s.WallpaperByTime = v);
            sp.Children.Add(byTime);
            var refresh = new List<Action>(); // para mostrar los nombres nuevos al usar los fondos de McBar
            for (int i = 0; i < DynamicWallpaper.Slots.Length; i++)
            {
                int idx = i;
                var (_, slotName, start) = DynamicWallpaper.Slots[i];
                var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new TextBlock { Text = $"{slotName} (desde las {start})", VerticalAlignment = VerticalAlignment.Center };
                label.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
                var file = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12, Margin = new Thickness(0, 0, 8, 0) };
                file.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
                void Show()
                {
                    var path = s.WallpaperImages[idx];
                    file.Text = string.IsNullOrWhiteSpace(path) ? "Sin imagen" : System.IO.Path.GetFileName(path);
                    file.ToolTip = string.IsNullOrWhiteSpace(path) ? null : path;
                }
                Show();
                refresh.Add(Show);
                var btns = new StackPanel { Orientation = Orientation.Horizontal };
                btns.Children.Add(Btn("Elegir…", () =>
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Imagen para: " + slotName, Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|Todos los archivos|*.*" };
                    if (dlg.ShowDialog(this) == true) { s.WallpaperImages[idx] = dlg.FileName; Show(); Changed(); }
                }));
                btns.Children.Add(Btn("Quitar", () => { s.WallpaperImages[idx] = ""; Show(); Changed(); }, new Thickness(0)));
                Grid.SetColumn(file, 1);
                Grid.SetColumn(btns, 2);
                g.Children.Add(label);
                g.Children.Add(file);
                g.Children.Add(btns);
                sp.Children.Add(g);
            }

            // 1.7.0: fondos propios de McBar (vienen dentro de la app) y pantalla de bloqueo
            var status = new TextBlock { FontSize = 11.5, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
            status.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            row.Children.Add(Btn("Usar los fondos de McBar", () =>
            {
                if (DynamicWallpaper.UseBundled())
                {
                    foreach (var r in refresh) r();
                    byTime.IsChecked = true;
                    status.Text = "Listo: fondos «Ondas» de McBar para la mañana, la tarde, el atardecer y la noche.";
                    Changed();
                }
                else status.Text = "No se pudieron copiar los fondos de McBar (mirá el registro en Diagnóstico).";
            }, new Thickness(0)));
            sp.Children.Add(row);
            sp.Children.Add(status);
            sp.Children.Add(Switch("Usar también en la pantalla de bloqueo", s.WallpaperLockScreen, v => s.WallpaperLockScreen = v));
            sp.Children.Add(Tip("Windows la muestra también al iniciar sesión si en Configuración → Personalización → Pantalla de bloqueo está prendido «Mostrar la imagen de fondo de la pantalla de bloqueo en la pantalla de inicio de sesión»."));
            return sp;
        }

        // ================== Estilo Mac (1.8.0) ==================

        private UIElement MacLookAppearance(AppSettings s)
        {
            var sp = new StackPanel();
            sp.Children.Add(BoxTitle("Apariencia"));
            sp.Children.Add(Tip("Todo es opcional. Cursores y sonidos se pueden volver a los de antes apagando la opción."));
            sp.Children.Add(Switch("Modo oscuro automático según la hora (Windows y las apps)", s.AutoDarkMode, v => s.AutoDarkMode = v));
            sp.Children.Add(SliderSetting("Oscuro desde las", 0, 23, s.AutoDarkFrom, v => s.AutoDarkFrom = (int)v, 1, h => h + ":00")); // 1.8.5: "19:00"
            sp.Children.Add(SliderSetting("Claro desde las", 0, 23, s.AutoLightFrom, v => s.AutoLightFrom = (int)v, 1, h => h + ":00"));
            sp.Children.Add(Switch("Cursores estilo Mac", s.MacCursors, v => s.MacCursors = v));
            sp.Children.Add(Switch("Sacudir el mouse para encontrarlo (el puntero se agranda)", s.ShakeToFind, v => s.ShakeToFind = v));
            sp.Children.Add(Switch("Sonidos del sistema estilo Mac", s.MacSounds, v => s.MacSounds = v));
            return sp;
        }

        private UIElement MacLookWindows(AppSettings s)
        {
            var sp = new StackPanel();
            sp.Children.Add(BoxTitle("Ventanas y escritorio"));
            sp.Children.Add(Switch("Clic en el fondo de pantalla para ver el escritorio (otro clic las devuelve)", s.ClickWallpaperShowsDesktop, v => s.ClickWallpaperShowsDesktop = v));
            sp.Children.Add(Switch("Organizador visual (Stage Manager)", s.StageManager, v => s.StageManager = v));
            sp.Children.Add(Tip("La app que usás queda al centro y las demás pasan a una franja a la izquierda; tocá una para traerla."));
            sp.Children.Add(Switch("Escritorios con la imagen del fondo en «Ver todas las ventanas»", s.MissionDesktopTiles, v => s.MissionDesktopTiles = v));
            return sp;
        }

        private UIElement MacLookKeyboard(AppSettings s)
        {
            var sp = new StackPanel();
            sp.Children.Add(BoxTitle("Teclado, capturas y Dock"));
            sp.Children.Add(Switch("Capturas con Win+Mayús+3 (pantalla), 4 (zona) y 5 (opciones)", s.MacScreenshots, v => s.MacScreenshots = v));
            var folderText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12, Margin = new Thickness(0, 0, 8, 0) };
            folderText.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            void ShowFolder() => folderText.Text = "Se guardan en: " + (string.IsNullOrWhiteSpace(s.ScreenshotFolder) ? "Escritorio" : s.ScreenshotFolder);
            ShowFolder();
            var fg = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            fg.ColumnDefinitions.Add(new ColumnDefinition());
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var fbtns = new StackPanel { Orientation = Orientation.Horizontal };
            fbtns.Children.Add(Btn("Elegir carpeta…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Carpeta para las capturas" };
                if (dlg.ShowDialog(this) == true) { s.ScreenshotFolder = dlg.FolderName; ShowFolder(); Changed(); }
            }));
            fbtns.Children.Add(Btn("Escritorio", () => { s.ScreenshotFolder = ""; ShowFolder(); Changed(); }, new Thickness(0)));
            Grid.SetColumn(fbtns, 1);
            fg.Children.Add(folderText);
            fg.Children.Add(fbtns);
            sp.Children.Add(fg);
            sp.Children.Add(Switch("Alt como Cmd (Alt+C copia, Alt+V pega, Alt+Q cierra la app…)", s.MacShortcuts, v => s.MacShortcuts = v));
            sp.Children.Add(Tip("Alt+C/V/X/Z/A/S/F/T/W/N/P/R/O hacen lo mismo que con Ctrl. Alt+Q cierra la app, Alt+M minimiza, Alt+H oculta la app y Alt+Espacio abre el buscador. Alt+Tab y Alt+F4 siguen igual."));
            sp.Children.Add(Switch("Menús reales de la app en la barra (si la app los tiene)", s.RealAppMenus, v => s.RealAppMenus = v));
            sp.Children.Add(Segment("Pilas del Dock", new[] { ("grid", "Cuadrícula"), ("fan", "Abanico"), ("list", "Lista") },
                s.StackStyle, v => { s.StackStyle = v; Changed(); }));
            // 1.8.1: explicar cómo se usan las pilas
            sp.Children.Add(Tip("Las pilas son las carpetas del lado derecho del Dock (Descargas, las que agregues). Al tocarlas se abren así. Cada una puede tener su propia forma: clic derecho en la carpeta del Dock → Mostrar como. Para agregar una: arrastrá una carpeta al Dock, o con el botón de acá abajo."));
            var addRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 2) };
            addRow.Children.Add(Btn("Agregar una carpeta al Dock…", () => App.Dock?.AddFolderFromDialog(), new Thickness(0)));
            sp.Children.Add(addRow);
            return sp;
        }

        /// <summary>Copia de seguridad: exportar / importar todos los ajustes (y los perfiles) en un .json.</summary>
        private UIElement BackupBox()
        {
            var sp = new StackPanel();
            sp.Children.Add(BoxTitle("Copia de seguridad"));
            sp.Children.Add(Tip("Guardá todos los ajustes (con los perfiles) en un archivo para pasarlos a otra PC o tenerlos de respaldo."));
            var status = new TextBlock { FontSize = 11.5, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
            status.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Btn("Exportar ajustes…", () =>
            {
                FlushPending();
                var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Exportar ajustes de McBar", Filter = "Ajustes de McBar (*.json)|*.json", FileName = $"McBar-ajustes-{DateTime.Now:yyyy-MM-dd}.json" };
                if (dlg.ShowDialog(this) != true) return;
                try { Profiles.ExportTo(dlg.FileName); status.Text = "Guardado en " + dlg.FileName; }
                catch (Exception ex) { status.Text = "No se pudo guardar: " + ex.Message; }
            }));
            row.Children.Add(Btn("Importar ajustes…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Importar ajustes de McBar", Filter = "Ajustes de McBar (*.json)|*.json|Todos los archivos|*.*" };
                if (dlg.ShowDialog(this) != true) return;
                if (!McDialog.Confirm("¿Importar estos ajustes?", "Se reemplazan los ajustes actuales y los perfiles por los del archivo.", "Importar")) return;
                FlushPending();
                bool ok;
                try { ok = Profiles.ImportFrom(dlg.FileName); }
                catch (Exception ex) { Log.Write("Importar ajustes: " + ex.Message); ok = false; }
                if (!ok) McDialog.Info("No se pudo importar", "El archivo no tiene ajustes de McBar.");
            }, new Thickness(0)));
            sp.Children.Add(row);
            sp.Children.Add(status);
            return sp;
        }

        /// <summary>1.4.0: íconos de macOSicons.com bajados solos para las apps instaladas.</summary>
        private UIElement MacIconsBox(AppSettings s)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 10, 0, 4) };
            sp.Children.Add(BoxTitle("Íconos de macOSicons.com"));
            sp.Children.Add(Tip("McBar busca cada app instalada en macOSicons.com y baja su ícono estilo Mac (las del Dock primero). Necesita la clave gratis de tu cuenta: creala en macosicons.com → tu cuenta → API. Los íconos son de sus autores (ver créditos)."));
            var status = new TextBlock { FontSize = 11.5, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
            status.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            void ShowState() => status.Text = (MacIcons.HasKey ? "Clave guardada. " : "Sin clave. ") +
                $"{MacIcons.DownloadedCount()} ícono(s) bajado(s). Este mes quedan {MacIcons.RemainingNow()} de {s.MacIconsMonthlyLimit} búsquedas.";

            var keyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var key = new PasswordBox { Width = 240, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), ToolTip = "Clave de la API de macOSicons" };
            key.SetResourceReference(StyleProperty, "McPasswordBox");
            keyRow.Children.Add(key);
            keyRow.Children.Add(Btn("Guardar clave", () => { MacIcons.SetKey(key.Password); key.Password = ""; ShowState(); }));
            sp.Children.Add(keyRow);

            sp.Children.Add(Switch("Bajar solos los íconos de las apps nuevas", s.MacIconsAuto, v => s.MacIconsAuto = v));

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            Button? go = null;
            go = Btn("Bajar íconos ahora", async () =>
            {
                if (!MacIcons.HasKey) { status.Text = "Primero guardá la clave de macOSicons."; return; }
                go!.IsEnabled = false;
                Action<string> prog = t => status.Text = t;
                MacIcons.Progress += prog;
                try { status.Text = await MacIcons.RunAsync(manual: true); }
                finally { MacIcons.Progress -= prog; go!.IsEnabled = true; }
            });
            row.Children.Add(go);
            row.Children.Add(Btn("Usar estos íconos", () => { App.UseMacIconsFolder(); ShowState(); }));
            row.Children.Add(Btn("Ver créditos", () =>
            {
                if (System.IO.File.Exists(MacIcons.CreditsPath)) Launcher.Open(MacIcons.CreditsPath);
                else status.Text = "Todavía no se bajó ningún ícono.";
            }, new Thickness(0)));
            sp.Children.Add(row);
            sp.Children.Add(status);
            ShowState();
            return sp;
        }

        /// <summary>Botones para poner widgets en el escritorio (1.1.0).</summary>
        private UIElement DesktopWidgetsBox()
        {
            var sp = new StackPanel();
            var t = new TextBlock { Text = "Widgets en el escritorio", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 2) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var tip = new TextBlock { Text = "Quedan detrás de las ventanas. Se mueven arrastrándolos; clic derecho para quitarlos.", FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            tip.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            var wrap = new WrapPanel();
            foreach (var (key, name) in WidgetFactory.Kinds)
            {
                var b = new Button { Content = "+ " + name, Margin = new Thickness(0, 0, 6, 6) };
                b.SetResourceReference(StyleProperty, "McButton");
                var k = key;
                b.Click += (_, _) => App.AddDesktopWidget(k);
                wrap.Children.Add(b);
            }
            sp.Children.Add(t);
            sp.Children.Add(tip);
            sp.Children.Add(wrap);
            return sp;
        }

        /// <summary>Calendarios de Google/Outlook por enlace iCal (1.1.0).</summary>
        private UIElement CalendarBox(AppSettings s)
        {
            var sp = new StackPanel();
            var t = new TextBlock { Text = "Calendarios (Google / Outlook)", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 2) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var tip = new TextBlock
            {
                Text = "Pegá un enlace por renglón. Google: Configuración del calendario → Integrar el calendario → «Dirección secreta en formato iCal». Outlook: Configuración → Calendarios compartidos → Publicar → enlace ICS.",
                FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6)
            };
            tip.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            var box = new TextBox { AcceptsReturn = true, Height = 70, TextWrapping = TextWrapping.NoWrap, Text = string.Join(Environment.NewLine, s.CalendarUrls), VerticalContentAlignment = VerticalAlignment.Top };
            box.SetResourceReference(StyleProperty, "McTextBox");
            var status = new TextBlock { FontSize = 11.5, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
            status.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            var refresh = new Button { Content = "Guardar y actualizar", Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            refresh.SetResourceReference(StyleProperty, "McButton");
            refresh.Click += async (_, _) =>
            {
                s.CalendarUrls = box.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                SettingsStore.Save();
                status.Text = "Actualizando…";
                await IcsCalendar.RefreshAsync(force: true);
                status.Text = $"{IcsCalendar.Events.Count} evento(s) en las próximas semanas.";
            };
            sp.Children.Add(t);
            sp.Children.Add(tip);
            sp.Children.Add(box);
            sp.Children.Add(refresh);
            sp.Children.Add(status);
            sp.Children.Add(Switch("Avisar 10 minutos antes de cada evento", s.EventReminders, v => s.EventReminders = v));
            return sp;
        }

        /// <summary>Una esquina activa: nombre + lista de acciones (1.0.0).</summary>
        private UIElement CornerRow(string text, string value, Action<string> set)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
            t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var cb = new ComboBox { Height = 28 };
            foreach (var (key, name) in HotCorners.Actions) cb.Items.Add(new ComboBoxItem { Content = name, Tag = key, IsSelected = key == value });
            cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is ComboBoxItem it && it.Tag is string k) { set(k); Changed(); } };
            Grid.SetColumn(cb, 1);
            g.Children.Add(t);
            g.Children.Add(cb);
            return g;
        }

        /// <summary>Diagnóstico (0.9.0): consumo de McBar en vivo y carpeta de informes de errores.</summary>
        private UIElement DiagnosticsBox()
        {
            var sp = new StackPanel();
            var usageTitle = new TextBlock { Text = "Consumo de McBar", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 2) };
            usageTitle.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var usage = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            usage.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            // 1.5.0: también la fluidez (Core/Perf)
            timer.Tick += (_, _) => usage.Text = Diagnostics.Sample().Format() + "\n" + Perf.Format();
            usage.Text = Diagnostics.Sample().Format() + "\n" + Perf.Format();
            timer.Start();
            Closed += (_, _) => timer.Stop();
            var version = new TextBlock { FontSize = 12, Margin = new Thickness(0, 6, 0, 0), Text = $"Versión {Installer.CurrentVersion.ToString(3)}" };
            version.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            var errors = new Button { Content = "Abrir informes de errores", Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            errors.SetResourceReference(StyleProperty, "McButton");
            errors.Click += (_, _) =>
            {
                System.IO.Directory.CreateDirectory(Recovery.Folder);
                Launcher.Open(Recovery.Folder);
            };
            sp.Children.Add(usageTitle);
            sp.Children.Add(usage);
            sp.Children.Add(version);
            sp.Children.Add(errors);
            var cur = SettingsStore.Current;
            var hang = Switch("Si McBar se traba, reiniciarlo solo (a los 20 segundos)", cur.RestartIfHung, v => cur.RestartIfHung = v); // 1.3.1
            hang.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(hang);
            return sp;
        }

        /// <summary>Paquete de íconos (0.4.3): carpeta con imágenes de otras barras o sistemas.</summary>
        private UIElement IconPackRow(AppSettings s)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 4) };
            var title = new TextBlock { Text = "Paquete de íconos", VerticalAlignment = VerticalAlignment.Center };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var path = new TextBlock { FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) };
            path.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            void Show() => path.Text = string.IsNullOrWhiteSpace(s.IconPackPath)
                ? "Íconos de Windows. Podés elegir una carpeta con imágenes (.png, .ico, .icns) de otra barra, de un tema de Linux o de macOS convertido."
                : s.IconPackPath;
            Show();
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var pick = new Button { Content = "Elegir carpeta…", Margin = new Thickness(0, 0, 8, 0) };
            pick.SetResourceReference(StyleProperty, "McButton");
            pick.Click += (_, _) =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Carpeta del paquete de íconos" };
                if (dlg.ShowDialog(this) == true) { s.IconPackPath = dlg.FolderName; Show(); Changed(); }
            };
            var clear = new Button { Content = "Usar los de Windows" };
            clear.SetResourceReference(StyleProperty, "McButton");
            clear.Click += (_, _) => { s.IconPackPath = ""; Show(); Changed(); };
            row.Children.Add(pick);
            row.Children.Add(clear);
            sp.Children.Add(title);
            sp.Children.Add(path);
            sp.Children.Add(row);
            return sp;
        }

        private UIElement SliderSetting(string text, double min, double max, double value, Action<double> set, double tick = 2, Func<int, string>? format = null)
        {
            var g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            // 0.4.3: la columna del nombre se adapta al texto (antes era fija y cortaba "Transparencia del fondo")
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(format == null ? 44 : 52) });
            var label = new TextBlock { Text = text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, MinWidth = 150, MaxWidth = 260, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0) }; // 1.8.5: mismo tamaño que los interruptores
            label.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            format ??= v => v.ToString();
            var val = new TextBlock { Text = format((int)value), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            val.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            var s = new Slider { Minimum = min, Maximum = max, Value = value, IsSnapToTickEnabled = true, TickFrequency = tick };
            s.SetResourceReference(StyleProperty, "McSlider");
            s.ValueChanged += (_, e) => { val.Text = format((int)e.NewValue); set(e.NewValue); Changed(); };
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
            foreach (var ic in host.Icons.Where(i => i.Icon != null && !i.IsWindowsSystem).GroupBy(i => i.StableId).Select(g => g.First()))
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
            var label = new TextBlock { Text = text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0) }; // 1.8.1: sin cortarse; 1.8.5: mismo tamaño
            label.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            g.Children.Add(label);
            var seg = new StackPanel { Orientation = Orientation.Horizontal };
            var segBorder = new Border { Child = seg, CornerRadius = new CornerRadius(8), Padding = new Thickness(1) };
            segBorder.SetResourceReference(Border.BackgroundProperty, "Mc.Hover");
            string group = "Seg" + text + "_" + (++_segCounter); // grupo único (hay dos "Estilo")
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
            foreach (var (key, name) in new[] { ("auto", "Automática"), ("light", "Clara"), ("dark", "Oscura"), ("glass", "Vidrio") })
            {
                var rb = new RadioButton { Content = name, GroupName = "McTheme", IsChecked = s.Theme == key };
                rb.SetResourceReference(StyleProperty, "McSegment");
                var k = key;
                rb.Checked += (_, _) =>
                {
                    s.Theme = k;
                    if (k == "glass") s.IslandStyle = "glass"; // 1.4.1: la isla acompaña al tema (después se puede cambiar)
                    SettingsStore.Save();
                    ThemeManager.Apply();
                    Changed();
                };
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
