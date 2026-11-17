// McBar — Creado por Andrés Londero
// Panel de widgets (se abre tocando el reloj): calendario, clima, batería, sistema y notas.
// Todo se actualiza solo mientras el panel está abierto; al cerrarlo se detienen los temporizadores.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using McBar.Core;

namespace McBar.Panels
{
    internal sealed class WidgetsWindow : PanelWindow
    {
        private static readonly CultureInfo Es = new CultureInfo("es-AR");
        private static string NotesPath => Path.Combine(SettingsStore.Folder, "notas.txt");

        private readonly DispatcherTimer _sysTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private DateTime _calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        private StackPanel? _calendarHost;
        private TextBox? _notes;

        public WidgetsWindow() : base(330, true)
        {
            Title = "Widgets";
            Card.Padding = new Thickness(4);
            var stack = new StackPanel();
            var s = SettingsStore.Current;

            // Fecha grande arriba
            var head = new StackPanel { Margin = new Thickness(12, 8, 12, 4) };
            var date = DateTime.Now;
            head.Children.Add(Text(Capital(date.ToString("dddd", Es)), 13, true, true));
            head.Children.Add(Text(date.ToString("d 'de' MMMM", Es), 24, true));
            stack.Children.Add(head);

            if (s.WidgetCalendar) stack.Children.Add(Tile(BuildCalendarHost()));
            if (s.WidgetWeather) stack.Children.Add(Tile(BuildWeather()));
            var battery = SystemInfo.Battery();
            if (s.WidgetBattery && battery.HasBattery) stack.Children.Add(Tile(BuildBattery(battery)));
            if (s.WidgetSystem) stack.Children.Add(Tile(BuildSystem()));
            if (s.WidgetNotes) stack.Children.Add(Tile(BuildNotes()));

            var edit = new Button { Content = "Editar widgets…", Margin = new Thickness(5, 6, 5, 8), HorizontalAlignment = HorizontalAlignment.Center };
            edit.SetResourceReference(StyleProperty, "McButton");
            edit.Click += (_, _) => { CloseAnimated(); App.OpenSettings(); };
            stack.Children.Add(edit);

            Card.Child = new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Closed += (_, _) => { _sysTimer.Stop(); SaveNotes(); };
        }

        private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], Es) + s.Substring(1);

        // ---------- Calendario ----------
        private UIElement BuildCalendarHost()
        {
            _calendarHost = new StackPanel();
            RenderCalendar();
            return _calendarHost;
        }

        private void RenderCalendar()
        {
            if (_calendarHost == null) return;
            _calendarHost.Children.Clear();

            var header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            header.Children.Add(Text(Capital(_calendarMonth.ToString("MMMM yyyy", Es)), 14, true));
            var nav = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            nav.Children.Add(NavButton("", () => { _calendarMonth = _calendarMonth.AddMonths(-1); RenderCalendar(); }));
            nav.Children.Add(NavButton("", () => { _calendarMonth = _calendarMonth.AddMonths(1); RenderCalendar(); }));
            header.Children.Add(nav);
            _calendarHost.Children.Add(header);

            var grid = new UniformGrid { Columns = 7 };
            foreach (var d in new[] { "L", "M", "M", "J", "V", "S", "D" })
            {
                var t = Text(d, 11, true, true);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.Margin = new Thickness(0, 0, 0, 4);
                grid.Children.Add(t);
            }
            int offset = ((int)_calendarMonth.DayOfWeek + 6) % 7; // lunes = 0
            var first = _calendarMonth.AddDays(-offset);
            for (int i = 0; i < 42; i++)
            {
                var day = first.AddDays(i);
                if (i >= 35 && day.Month != _calendarMonth.Month) break;
                bool today = day == DateTime.Today;
                var t = Text(day.Day.ToString(), 12, today, day.Month != _calendarMonth.Month);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.VerticalAlignment = VerticalAlignment.Center;
                var cell = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Child = t, Margin = new Thickness(1) };
                if (today)
                {
                    cell.SetResourceReference(Border.BackgroundProperty, "Mc.Accent");
                    t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.AccentText");
                }
                grid.Children.Add(cell);
            }
            _calendarHost.Children.Add(grid);
        }

        private static Button NavButton(string glyph, Action click)
        {
            var b = new Button { Content = Glyph(glyph, 10), Padding = new Thickness(7, 3, 7, 3) };
            b.SetResourceReference(StyleProperty, "McFlatButton");
            b.Click += (_, _) => click();
            return b;
        }

        // ---------- Clima ----------
        private UIElement BuildWeather()
        {
            var host = new StackPanel();
            host.Children.Add(Text("Clima", 12, true, true));
            var body = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            body.Children.Add(Text("Cargando…", 12, false, true));
            host.Children.Add(body);
            LoadWeather(body);
            return host;
        }

        private async void LoadWeather(StackPanel body)
        {
            var w = await WeatherService.GetAsync();
            body.Children.Clear();
            if (w == null)
            {
                body.Children.Add(Text("No se pudo obtener el clima. Revisá la conexión o la ciudad en Preferencias.", 12, false, true));
                return;
            }
            var (desc, symbol) = WeatherCodes.Describe(w.Code);
            var top = new Grid();
            var left = new StackPanel();
            left.Children.Add(Text(w.Place, 13, true));
            left.Children.Add(Text($"{Math.Round(w.Temp)}°", 38));
            top.Children.Add(left);
            var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
            var sym = Text(symbol, 26);
            sym.SetResourceReference(TextBlock.FontFamilyProperty, "Mc.Emoji");
            sym.HorizontalAlignment = HorizontalAlignment.Right;
            right.Children.Add(sym);
            right.Children.Add(Text(desc, 12));
            right.Children.Add(Text($"Máx. {Math.Round(w.Max)}°  Mín. {Math.Round(w.Min)}°", 11.5, false, true));
            top.Children.Add(right);
            body.Children.Add(top);

            var hours = new UniformGrid { Rows = 1, Margin = new Thickness(0, 10, 0, 0) };
            foreach (var h in w.Hours)
            {
                var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var hh = Text(h.Time.ToString("HH", Es) + " h", 11, false, true);
                hh.HorizontalAlignment = HorizontalAlignment.Center;
                var s = Text(WeatherCodes.Describe(h.Code).symbol, 15);
                s.SetResourceReference(TextBlock.FontFamilyProperty, "Mc.Emoji");
                s.HorizontalAlignment = HorizontalAlignment.Center;
                var tt = Text($"{Math.Round(h.Temp)}°", 12, true);
                tt.HorizontalAlignment = HorizontalAlignment.Center;
                sp.Children.Add(hh);
                sp.Children.Add(s);
                sp.Children.Add(tt);
                hours.Children.Add(sp);
            }
            body.Children.Add(hours);
        }

        // ---------- Batería ----------
        private static UIElement BuildBattery(BatteryState b)
        {
            var sp = new StackPanel();
            sp.Children.Add(Text("Batería", 12, true, true));
            var row = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            row.Children.Add(Text($"{b.Percent} %", 24, true));
            var state = Text(b.Charging ? "Cargando" : "Con batería", 12, false, true);
            state.HorizontalAlignment = HorizontalAlignment.Right;
            state.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(state);
            sp.Children.Add(row);
            sp.Children.Add(Bar(b.Percent, b.Percent <= 20 && !b.Charging, out _));
            return sp;
        }

        // ---------- Sistema ----------
        private UIElement BuildSystem()
        {
            var sp = new StackPanel();
            sp.Children.Add(Text("Sistema", 12, true, true));
            var cpuLabel = Text("Procesador", 12);
            var ramLabel = Text("Memoria", 12);
            cpuLabel.Margin = new Thickness(0, 6, 0, 2);
            ramLabel.Margin = new Thickness(0, 8, 0, 2);
            sp.Children.Add(cpuLabel);
            sp.Children.Add(Bar(0, false, out var cpuFill));
            sp.Children.Add(ramLabel);
            sp.Children.Add(Bar(0, false, out var ramFill));

            void Update()
            {
                int cpu = SystemInfo.CpuPercent();
                var (ram, used, total) = SystemInfo.Memory();
                cpuLabel.Text = $"Procesador · {cpu} %";
                ramLabel.Text = $"Memoria · {used:0.0} de {total:0.0} GB";
                SetFill(cpuFill, cpu);
                SetFill(ramFill, ram);
            }
            SystemInfo.CpuPercent(); // primera lectura (referencia)
            _sysTimer.Tick += (_, _) => Update();
            Loaded += (_, _) => { Update(); _sysTimer.Start(); };
            return sp;
        }

        private static Grid Bar(double percent, bool danger, out Border fill)
        {
            var g = new Grid { Height = 6, Margin = new Thickness(0, 4, 0, 0) };
            var track = new Border { CornerRadius = new CornerRadius(3) };
            track.SetResourceReference(Border.BackgroundProperty, "Mc.Track");
            fill = new Border { CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left };
            fill.SetResourceReference(Border.BackgroundProperty, danger ? "Mc.Danger" : "Mc.Accent");
            g.Children.Add(track);
            g.Children.Add(fill);
            var f = fill;
            g.SizeChanged += (_, _) => SetFill(f, (double)(f.Tag ?? percent));
            f.Tag = percent;
            return g;
        }

        private static void SetFill(Border fill, double percent)
        {
            fill.Tag = percent;
            if (fill.Parent is Grid g && g.ActualWidth > 0)
                fill.Width = Math.Max(0, g.ActualWidth * Math.Clamp(percent, 0, 100) / 100.0);
        }

        // ---------- Notas ----------
        private UIElement BuildNotes()
        {
            var sp = new StackPanel();
            sp.Children.Add(Text("Notas", 12, true, true));
            _notes = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 120,
                Margin = new Thickness(0, 6, 0, 0),
                VerticalContentAlignment = VerticalAlignment.Top
            };
            _notes.SetResourceReference(StyleProperty, "McTextBox");
            try { if (File.Exists(NotesPath)) _notes.Text = File.ReadAllText(NotesPath); } catch { }
            var save = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            save.Tick += (_, _) => { save.Stop(); SaveNotes(); };
            _notes.TextChanged += (_, _) => { save.Stop(); save.Start(); };
            sp.Children.Add(_notes);
            return sp;
        }

        private void SaveNotes()
        {
            if (_notes == null) return;
            try
            {
                Directory.CreateDirectory(SettingsStore.Folder);
                File.WriteAllText(NotesPath, _notes.Text);
            }
            catch (Exception ex) { Log.Write("Notas: " + ex.Message); }
        }
    }
}
