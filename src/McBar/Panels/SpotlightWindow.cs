// McBar — Creado por Andrés Londero
// Buscador estilo Spotlight (0.6.0): Win+S, la lupa de la barra o la de la isla.
// Busca apps (las del Launchpad), archivos (Core/FileIndex), hace cuentas (Core/Calc), abre partes de
// Configuración y, al final, ofrece buscar en la web. Flechas para elegir, Enter para abrir, Esc para salir.
// Ctrl+Enter en un archivo: mostrarlo en su carpeta. Enter en una cuenta: copia el resultado.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class SpotlightWindow : Window
    {
        private sealed class Result
        {
            public string Section = "";
            public string Title = "";
            public string Subtitle = "";
            public string? IconTarget;   // para IconLoader (app o archivo)
            public string Glyph = "";    // si no hay ícono
            public Action Open = () => { };
            public Action? Reveal;       // Ctrl+Enter
        }

        private static readonly (string keys, string title, string uri)[] SettingsPages =
        {
            ("wifi red internet", "Wi-Fi y red", "ms-settings:network-wifi"),
            ("bluetooth dispositivos", "Bluetooth y dispositivos", "ms-settings:bluetooth"),
            ("sonido audio volumen parlantes auriculares", "Sonido", "ms-settings:sound"),
            ("pantalla monitor resolucion escala brillo", "Pantalla", "ms-settings:display"),
            ("fondo wallpaper personalizacion", "Fondo de pantalla", "ms-settings:personalization-background"),
            ("actualizaciones windows update", "Windows Update", "ms-settings:windowsupdate"),
            ("aplicaciones apps desinstalar programas", "Aplicaciones instaladas", "ms-settings:appsfeatures"),
            ("impresoras escaner", "Impresoras y escáneres", "ms-settings:printers"),
            ("bateria energia ahorro", "Batería y energía", "ms-settings:batterysaver"),
            ("mouse raton", "Mouse", "ms-settings:mousetouchpad"),
            ("teclado idioma", "Idioma y teclado", "ms-settings:regionlanguage"),
            ("cuentas usuario contraseña", "Cuentas", "ms-settings:yourinfo"),
            ("privacidad seguridad antivirus defender", "Seguridad de Windows", "windowsdefender:"),
            ("almacenamiento disco espacio", "Almacenamiento", "ms-settings:storagesense"),
            ("hora fecha reloj", "Fecha y hora", "ms-settings:dateandtime"),
            ("notificaciones", "Notificaciones", "ms-settings:notifications"),
        };

        private Grid _shell = new Grid();
        private readonly TextBox _box = new TextBox();
        private readonly StackPanel _list = new StackPanel();
        private readonly Border _results;
        private List<Result> _items = new List<Result>();
        private int _sel;
        private List<CatalogApp> _apps = new List<CatalogApp>();
        private bool _closing;
        public DateTime ClosedAt { get; private set; } = DateTime.MinValue;
        public bool IsClosing => _closing;

        public SpotlightWindow(MonitorInfo m)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            SizeToContent = SizeToContent.Height;
            Width = 680;
            FontFamily = (FontFamily)FindResource("Mc.Font");
            double sc = m.Scale;
            WindowOps.PlaceOnMonitor(this, m, (m.Bounds.Width / sc - Width) / 2, m.Bounds.Height / sc * 0.2); // 1.5.0

            var icon = new TextBlock { Text = "", FontSize = 22, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Opacity = 0.7 };
            icon.SetResourceReference(TextBlock.FontFamilyProperty, "Mc.Icons");
            icon.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            _box.FontSize = 24;
            _box.BorderThickness = new Thickness(0);
            _box.Background = Brushes.Transparent;
            _box.VerticalContentAlignment = VerticalAlignment.Center;
            _box.SetResourceReference(TextBox.ForegroundProperty, "Mc.Text");
            _box.SetResourceReference(TextBox.CaretBrushProperty, "Mc.Text");
            var hint = new TextBlock { Text = "Buscar apps, archivos, cuentas…", FontSize = 24, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0), Opacity = 0.4 };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            _box.TextChanged += (_, _) => { hint.Visibility = _box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Search(); };
            var boxHost = new Grid();
            boxHost.Children.Add(hint);
            boxHost.Children.Add(_box);
            var top = new DockPanel { Margin = new Thickness(20, 14, 20, 14) };
            DockPanel.SetDock(icon, System.Windows.Controls.Dock.Left);
            top.Children.Add(icon);
            top.Children.Add(boxHost);

            _results = new Border { Child = new ScrollViewer { Content = _list, MaxHeight = 440, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Visibility = Visibility.Collapsed, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(8, 6, 8, 8) };
            _results.SetResourceReference(Border.BorderBrushProperty, "Mc.Separator");

            var sp = new StackPanel();
            sp.Children.Add(top);
            sp.Children.Add(_results);
            var card = new Border
            {
                Child = sp,
                CornerRadius = new CornerRadius(PanelLook.Radius),
                BorderThickness = new Thickness(1)
            };
            card.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            card.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            // 1.6.0: la sombra va en una capa APARTE (antes estaba en la tarjeta y se recalculaba con cada letra
            // escrita, porque cambiaban los resultados) y es más grande y suave; brillo fino arriba.
            var shadow = new Border
            {
                CornerRadius = new CornerRadius(PanelLook.Radius),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 40, ShadowDepth = 14, Direction = 270, Opacity = 0.42, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance },
                CacheMode = new BitmapCache()
            };
            shadow.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg"); // mismo color: no oscurece la tarjeta
            _shell = new Grid { Margin = new Thickness(24, 16, 24, 32) };
            _shell.Children.Add(shadow);
            _shell.Children.Add(card);
            _shell.Children.Add(PanelLook.Highlight());
            Content = _shell;

            PreviewKeyDown += OnKey;
            Deactivated += (_, _) => CloseAnimated();
            // 1.9.43: mientras el buscador está abierto la isla queda chica (no se pisa con el cuadro de búsqueda)
            bool islandLocked = false;
            Closed += (_, _) => { if (islandLocked) { islandLocked = false; McBar.MenuBar.IslandWindow.Suppress(false); } };
            Loaded += async (_, _) =>
            {
                if (!islandLocked) { islandLocked = true; McBar.MenuBar.IslandWindow.Suppress(true); }
                WindowOps.Activate(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                _box.Focus();
                Keyboard.Focus(_box);
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
                PanelLook.SpringIn(_shell, new Point(0.5, 0), fromY: -10, fromScale: 0.96); // 1.6.0
                FileIndex.Warm();
                if (AppCatalog.Cached != null) _apps = AppCatalog.Cached.ToList();
                try { _apps = await AppCatalog.LoadAsync(); } catch { }
                if (_box.Text.Length > 0) Search();
            };
        }

        public void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            ClosedAt = DateTime.Now;
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110));
            fade.Completed += (_, _) => { Close(); App.ScheduleTrim(); };
            BeginAnimation(OpacityProperty, fade);
        }

        // ================== Buscar ==================

        private void Search()
        {
            var q = _box.Text.Trim();
            var items = new List<Result>();
            if (q.Length > 0)
            {
                // cuenta
                if (Calc.LooksLikeMath(q) && Calc.TryEval(q, out var v))
                {
                    var txt = Calc.Format(v);
                    items.Add(new Result { Section = "Calculadora", Title = "= " + txt, Subtitle = "Enter para copiar el resultado", Glyph = "", Open = () => { try { Clipboard.SetText(txt); } catch { } } });
                }
                // 1.2.0: "temporizador 10" / "cronómetro"
                var timer = LiveStatus.ParseTimerCommand(q);
                if (timer != null)
                {
                    var len = timer.Value;
                    items.Add(new Result { Section = "Temporizador", Title = "Temporizador de " + LiveStatus.Format(len), Subtitle = "Enter para empezar", Glyph = "\uE916", Open = () => LiveStatus.StartTimer(len) });
                }
                else if (Strip(q).StartsWith("cronom"))
                    items.Add(new Result { Section = "Temporizador", Title = "Iniciar el cronómetro", Subtitle = "Enter para empezar", Glyph = "\uE916", Open = LiveStatus.StartStopwatch });
                // 1.3.0: "modo juego" y perfiles
                if (Strip(q).StartsWith("modo j"))
                    items.Add(new Result { Section = "McBar", Title = GameMode.On ? "Apagar el modo juego" : "Prender el modo juego", Subtitle = "Enter para cambiar", Glyph = "\uE7FC", Open = GameMode.Toggle });
                var sq = Strip(q);
                if (sq.Length >= 3)
                    foreach (var p in SettingsStore.Current.Profiles.Where(p => Strip(p.Name).StartsWith(sq) || ("perfil " + Strip(p.Name)).StartsWith(sq)).Take(3))
                    {
                        var pn = p.Name;
                        items.Add(new Result { Section = "McBar", Title = "Usar el perfil " + pn, Subtitle = pn == SettingsStore.Current.ActiveProfile ? "Es el que está en uso" : "Perfil de McBar", Glyph = "\uE77B", Open = () => App.UseProfile(pn) });
                    }
                // apps
                foreach (var a in _apps.Where(a => a.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase))
                                       .OrderByDescending(a => a.Name.StartsWith(q, StringComparison.CurrentCultureIgnoreCase))
                                       .ThenBy(a => a.Name).Take(6))
                    items.Add(new Result { Section = "Apps", Title = a.Name, Subtitle = "Aplicación", IconTarget = a.Target, Open = () => Launcher.Launch(a.Target) });
                // configuración
                var nq = Strip(q);
                foreach (var p in SettingsPages.Where(p => Strip(p.title).Contains(nq) || p.keys.Split(' ').Any(k => k.StartsWith(nq))).Take(3))
                    items.Add(new Result { Section = "Configuración", Title = p.title, Subtitle = "Configuración de Windows", Glyph = "", Open = () => Launcher.Open(p.uri) });
                if ("preferencias mcbar".Contains(nq))
                    items.Add(new Result { Section = "Configuración", Title = "Preferencias de McBar", Subtitle = "McBar", Glyph = "", Open = App.OpenSettings });
                // archivos
                foreach (var f in FileIndex.Search(q, 8))
                {
                    var path = f.Path;
                    items.Add(new Result
                    {
                        Section = "Archivos",
                        Title = f.Name,
                        Subtitle = System.IO.Path.GetDirectoryName(path) ?? "",
                        IconTarget = path,
                        Open = () => Launcher.Launch(path),
                        Reveal = () => Launcher.ShowInExplorer(path)
                    });
                }
                if (!FileIndex.Ready && q.Length >= 2)
                    items.Add(new Result { Section = "Archivos", Title = "Preparando la búsqueda de archivos…", Subtitle = "En unos segundos aparecen", Glyph = "" });
                // web
                items.Add(new Result { Section = "Web", Title = $"Buscar «{q}» en la web", Subtitle = "Navegador", Glyph = "", Open = () => Launcher.Open("https://www.google.com/search?q=" + Uri.EscapeDataString(q)) });
            }
            _items = items;
            _sel = items.FindIndex(i => !i.Title.StartsWith("Preparando"));
            if (_sel < 0) _sel = 0;
            Render();
        }

        private static string Strip(string s)
        {
            var n = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            return new string(n.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        }

        private void Render()
        {
            _list.Children.Clear();
            _results.Visibility = _items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            string? section = null;
            for (int i = 0; i < _items.Count; i++)
            {
                var r = _items[i];
                if (r.Section != section)
                {
                    section = r.Section;
                    var h = new TextBlock { Text = section, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(10, 8, 0, 3), Opacity = 0.6 };
                    h.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
                    _list.Children.Add(h);
                }
                _list.Children.Add(Row(r, i));
            }
        }

        private UIElement Row(Result r, int index)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            FrameworkElement ic;
            if (r.IconTarget != null)
            {
                var img = new Image { Width = 26, Height = 26 };
                IconLoader.Request(r.IconTarget, 32, src => img.Source = src, displayName: r.Section == "Apps" ? r.Title : null);
                ic = img;
            }
            else
            {
                var t = new TextBlock { Text = r.Glyph, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center };
                t.SetResourceReference(TextBlock.FontFamilyProperty, "Mc.Icons");
                t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
                ic = t;
            }
            ic.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(ic);
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            var title = new TextBlock { Text = r.Title, FontSize = 14.5, TextTrimming = TextTrimming.CharacterEllipsis };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var sub = new TextBlock { Text = r.Subtitle, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.65 };
            sub.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            texts.Children.Add(title);
            if (r.Subtitle.Length > 0) texts.Children.Add(sub);
            Grid.SetColumn(texts, 1);
            g.Children.Add(texts);
            var row = new Border { Child = g, Padding = new Thickness(6, 6, 10, 6), CornerRadius = new CornerRadius(10), Cursor = Cursors.Hand };
            if (index == _sel) row.SetResourceReference(Border.BackgroundProperty, "Mc.Accent");
            else row.Background = Brushes.Transparent;
            if (index == _sel) { title.Foreground = Brushes.White; sub.Foreground = Brushes.White; if (ic is TextBlock tb) tb.Foreground = Brushes.White; }
            row.MouseEnter += (_, _) => { if (_sel != index) { _sel = index; Render(); } };
            row.MouseLeftButtonUp += (_, _) => { _sel = index; Activate(false); };
            return row;
        }

        private void Activate(bool reveal)
        {
            if (_sel < 0 || _sel >= _items.Count) return;
            var r = _items[_sel];
            CloseAnimated();
            try { if (reveal && r.Reveal != null) r.Reveal(); else r.Open(); }
            catch (Exception ex) { Log.Write("Buscador: " + ex.Message); }
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    if (_box.Text.Length > 0) _box.Text = ""; else CloseAnimated();
                    e.Handled = true;
                    break;
                case Key.Down:
                    if (_items.Count > 0) { _sel = (_sel + 1) % _items.Count; Render(); }
                    e.Handled = true;
                    break;
                case Key.Up:
                    if (_items.Count > 0) { _sel = (_sel - 1 + _items.Count) % _items.Count; Render(); }
                    e.Handled = true;
                    break;
                case Key.Enter:
                    Activate(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                    e.Handled = true;
                    break;
            }
        }
    }
}
