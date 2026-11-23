// McBar — Creado por Andrés Londero
// Widgets (1.1.0) que se pueden poner en el ESCRITORIO (debajo de las ventanas, como en macOS Sonoma) y
// algunos también en el panel del reloj: Reloj, Calendario (con eventos de Google/Outlook), Clima,
// Recordatorios, Notas, Batería y Sistema.
//  - DesktopWidgetWindow: ventana chica de vidrio, siempre al fondo, que se arrastra con el mouse;
//    clic derecho → "Quitar del escritorio". La posición se guarda.
//  - WidgetFactory.Build(tipo): el contenido de cada widget (también lo usa el panel del reloj).
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal static class WidgetFactory
    {
        private static readonly CultureInfo Es = new CultureInfo("es-AR");
        public static string NotesPath => Path.Combine(SettingsStore.Folder, "notas.txt");

        public static readonly (string key, string name)[] Kinds =
        {
            ("clock", "Reloj"), ("calendar", "Calendario"), ("weather", "Clima"), ("reminders", "Recordatorios"),
            ("notes", "Notas"), ("battery", "Batería"), ("system", "Sistema")
        };

        public static string NameOf(string kind) => Kinds.FirstOrDefault(k => k.key == kind).name ?? kind;

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], Es) + s.Substring(1);

        /// <summary>El contenido del widget. <paramref name="owner"/> es el elemento cuyo Unloaded apaga los timers.</summary>
        public static FrameworkElement Build(string kind)
        {
            return kind switch
            {
                "clock" => Clock(),
                "calendar" => Calendar(),
                "weather" => new WeatherView(300),
                "reminders" => RemindersView(),
                "notes" => Notes(),
                "battery" => Battery(),
                "system" => SystemUse(),
                _ => PanelWindow.Text(kind, 12)
            };
        }

        private static void Every(FrameworkElement el, TimeSpan interval, Action tick)
        {
            var t = new DispatcherTimer { Interval = interval };
            t.Tick += (_, _) => tick();
            el.Loaded += (_, _) => { tick(); t.Start(); };
            el.Unloaded += (_, _) => t.Stop();
        }

        // ---------- Reloj ----------
        private static FrameworkElement Clock()
        {
            var sp = new StackPanel { Width = 230, Margin = new Thickness(4) };
            var time = PanelWindow.Text("", 52);
            time.FontWeight = FontWeights.Light;
            var date = PanelWindow.Text("", 14, false, true);
            sp.Children.Add(time);
            sp.Children.Add(date);
            Every(sp, TimeSpan.FromSeconds(1), () =>
            {
                var n = DateTime.Now;
                time.Text = n.ToString("HH:mm", Es);
                date.Text = Cap(n.ToString("dddd d 'de' MMMM", Es));
            });
            return sp;
        }

        // ---------- Calendario + próximos eventos ----------
        private static FrameworkElement Calendar()
        {
            var sp = new StackPanel { Width = 280, Margin = new Thickness(2) };
            void Render()
            {
                sp.Children.Clear();
                var today = DateTime.Today;
                var first = new DateTime(today.Year, today.Month, 1);
                var head = PanelWindow.Text(Cap(first.ToString("MMMM yyyy", Es)), 13, true);
                head.Margin = new Thickness(2, 0, 0, 6);
                sp.Children.Add(head);
                var grid = new UniformGrid { Columns = 7 };
                foreach (var d in new[] { "L", "M", "M", "J", "V", "S", "D" })
                {
                    var t = PanelWindow.Text(d, 10.5, true, true);
                    t.HorizontalAlignment = HorizontalAlignment.Center;
                    grid.Children.Add(t);
                }
                int offset = ((int)first.DayOfWeek + 6) % 7;
                for (int i = 0; i < offset; i++) grid.Children.Add(new Border());
                for (int d = 1; d <= DateTime.DaysInMonth(today.Year, today.Month); d++)
                {
                    var day = new DateTime(today.Year, today.Month, d);
                    var num = PanelWindow.Text(d.ToString(), 11.5);
                    num.HorizontalAlignment = HorizontalAlignment.Center;
                    var cell = new StackPanel { Margin = new Thickness(0, 1, 0, 1) };
                    var circle = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Child = num, HorizontalAlignment = HorizontalAlignment.Center };
                    if (day == today) { circle.SetResourceReference(Border.BackgroundProperty, "Mc.Accent"); num.Foreground = Brushes.White; }
                    cell.Children.Add(circle);
                    if (IcsCalendar.HasEventsOn(day))
                    {
                        var dot = new Border { Width = 4, Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Center };
                        dot.SetResourceReference(Border.BackgroundProperty, "Mc.Accent");
                        cell.Children.Add(dot);
                    }
                    grid.Children.Add(cell);
                }
                sp.Children.Add(grid);
                var events = IcsCalendar.Upcoming(7, 4);
                if (events.Count > 0 || SettingsStore.Current.CalendarUrls.Count > 0)
                {
                    var cap = PanelWindow.Text("Próximos eventos", 11, true, true);
                    cap.Margin = new Thickness(2, 8, 0, 4);
                    sp.Children.Add(cap);
                    if (events.Count == 0) sp.Children.Add(PanelWindow.Text("Nada en los próximos 7 días", 12, false, true));
                    foreach (var e in events) sp.Children.Add(EventRow(e));
                }
            }
            Action onChange = () => sp.Dispatcher.BeginInvoke(new Action(Render));
            sp.Loaded += (_, _) => { IcsCalendar.Changed += onChange; Render(); _ = IcsCalendar.RefreshAsync(); };
            sp.Unloaded += (_, _) => IcsCalendar.Changed -= onChange;
            return sp;
        }

        public static UIElement EventRow(CalEvent e)
        {
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var bar = new Border { CornerRadius = new CornerRadius(2) };
            bar.SetResourceReference(Border.BackgroundProperty, "Mc.Accent");
            g.Children.Add(bar);
            var texts = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            var t = PanelWindow.Text(e.Title, 12.5, true);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            texts.Children.Add(t);
            string when = e.AllDay ? Reminders.When(e.Start.Date, DateTime.Now) + " · todo el día"
                                   : Reminders.When(e.Start, DateTime.Now) + "–" + e.End.ToString("HH:mm", Es);
            if (e.Location.Length > 0) when += " · " + e.Location;
            var w = PanelWindow.Text(when, 11, false, true);
            w.TextTrimming = TextTrimming.CharacterEllipsis;
            texts.Children.Add(w);
            Grid.SetColumn(texts, 1);
            g.Children.Add(texts);
            return g;
        }

        // ---------- Recordatorios ----------
        public static FrameworkElement RemindersView(double width = 280)
        {
            var root = new StackPanel { Width = width, Margin = new Thickness(2) };
            var title = PanelWindow.Text("Recordatorios", 13, true);
            title.Margin = new Thickness(2, 0, 0, 6);
            var list = new StackPanel();
            var box = new TextBox { Margin = new Thickness(0, 6, 0, 0), ToolTip = "Ej.: comprar pan mañana 18:00 · llamar a Juan en 20 min" };
            box.SetResourceReference(FrameworkElement.StyleProperty, "McTextBox");
            var hint = PanelWindow.Text("Nuevo recordatorio… (Enter)", 12, false, true);
            hint.IsHitTestVisible = false;
            hint.Margin = new Thickness(8, 10, 0, 0);
            var boxHost = new Grid();
            boxHost.Children.Add(box);
            boxHost.Children.Add(hint);
            box.TextChanged += (_, _) => hint.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            box.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter || box.Text.Trim().Length == 0) return;
                Reminders.Add(box.Text);
                box.Text = "";
                e.Handled = true;
            };

            void Render()
            {
                list.Children.Clear();
                var items = Reminders.Sorted().Where(r => !r.Done).Take(10).ToList();
                if (items.Count == 0) list.Children.Add(PanelWindow.Text("No hay pendientes", 12, false, true));
                foreach (var r in items)
                {
                    var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    var circle = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1.5), Margin = new Thickness(0, 1, 8, 0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent };
                    circle.SetResourceReference(Border.BorderBrushProperty, "Mc.TextDim");
                    var rr = r;
                    circle.MouseLeftButtonUp += (_, e) => { e.Handled = true; Reminders.SetDone(rr, true); };
                    row.Children.Add(circle);
                    var texts = new StackPanel();
                    var t = PanelWindow.Text(r.Text, 12.5);
                    t.TextWrapping = TextWrapping.Wrap;
                    texts.Children.Add(t);
                    if (r.Due != null)
                    {
                        var when = PanelWindow.Text(Reminders.When(r.Due.Value, DateTime.Now), 11, false, true);
                        if (r.Due < DateTime.Now) when.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Danger");
                        texts.Children.Add(when);
                    }
                    Grid.SetColumn(texts, 1);
                    row.Children.Add(texts);
                    var cm = new ContextMenu();
                    var del = new MenuItem { Header = "Borrar" };
                    del.Click += (_, _) => Reminders.Remove(rr);
                    cm.Items.Add(del);
                    row.ContextMenu = cm;
                    list.Children.Add(row);
                }
            }
            Action onChange = () => root.Dispatcher.BeginInvoke(new Action(Render));
            root.Loaded += (_, _) => { Reminders.Changed += onChange; Render(); };
            root.Unloaded += (_, _) => Reminders.Changed -= onChange;
            root.Children.Add(title);
            root.Children.Add(list);
            root.Children.Add(boxHost);
            return root;
        }

        // ---------- Notas ----------
        private static FrameworkElement Notes()
        {
            var sp = new StackPanel { Width = 260 };
            var t = PanelWindow.Text("Notas", 13, true);
            t.Margin = new Thickness(2, 0, 0, 6);
            var box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 150, VerticalContentAlignment = VerticalAlignment.Top };
            box.SetResourceReference(FrameworkElement.StyleProperty, "McTextBox");
            try { if (File.Exists(NotesPath)) box.Text = File.ReadAllText(NotesPath); } catch { }
            var save = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            save.Tick += (_, _) => { save.Stop(); try { File.WriteAllText(NotesPath, box.Text); } catch { } };
            box.TextChanged += (_, _) => { save.Stop(); save.Start(); };
            sp.Children.Add(t);
            sp.Children.Add(box);
            return sp;
        }

        // ---------- Batería / Sistema ----------
        private static Grid Bar(out Border fill)
        {
            var g = new Grid { Height = 6, Margin = new Thickness(0, 4, 0, 0) };
            var track = new Border { CornerRadius = new CornerRadius(3) };
            track.SetResourceReference(Border.BackgroundProperty, "Mc.Track");
            fill = new Border { CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left };
            fill.SetResourceReference(Border.BackgroundProperty, "Mc.Accent");
            g.Children.Add(track);
            g.Children.Add(fill);
            return g;
        }

        private static void Set(Border fill, double pct)
        {
            if (fill.Parent is Grid g && g.ActualWidth > 0) fill.Width = g.ActualWidth * Math.Clamp(pct, 0, 100) / 100;
        }

        private static FrameworkElement Battery()
        {
            var sp = new StackPanel { Width = 220 };
            var t = PanelWindow.Text("Batería", 13, true);
            var v = PanelWindow.Text("", 24);
            var bar = Bar(out var fill);
            sp.Children.Add(t);
            sp.Children.Add(v);
            sp.Children.Add(bar);
            Every(sp, TimeSpan.FromSeconds(30), () =>
            {
                var b = SystemInfo.Battery();
                v.Text = b.HasBattery ? $"{b.Percent} %{(b.Charging ? " ⚡" : "")}" : "Sin batería";
                Set(fill, b.HasBattery ? b.Percent : 0);
            });
            bar.SizeChanged += (_, _) => { var b = SystemInfo.Battery(); Set(fill, b.HasBattery ? b.Percent : 0); };
            return sp;
        }

        private static FrameworkElement SystemUse()
        {
            var sp = new StackPanel { Width = 240 };
            sp.Children.Add(PanelWindow.Text("Sistema", 13, true));
            var cpu = PanelWindow.Text("Procesador", 12);
            var ram = PanelWindow.Text("Memoria", 12);
            cpu.Margin = new Thickness(0, 6, 0, 0);
            ram.Margin = new Thickness(0, 8, 0, 0);
            var cpuBar = Bar(out var cpuFill);
            var ramBar = Bar(out var ramFill);
            sp.Children.Add(cpu);
            sp.Children.Add(cpuBar);
            sp.Children.Add(ram);
            sp.Children.Add(ramBar);
            SystemInfo.CpuPercent();
            Every(sp, TimeSpan.FromSeconds(2), () =>
            {
                int c = SystemInfo.CpuPercent();
                var (r, used, total) = SystemInfo.Memory();
                cpu.Text = $"Procesador · {c} %";
                ram.Text = $"Memoria · {used:0.0} de {total:0.0} GB";
                Set(cpuFill, c);
                Set(ramFill, r);
            });
            return sp;
        }
    }

    /// <summary>Un widget sobre el escritorio: al fondo de todo, arrastrable, con su posición guardada.</summary>
    internal sealed class DesktopWidgetWindow : Window
    {
        public DesktopWidget Model { get; }

        public DesktopWidgetWindow(DesktopWidget model)
        {
            Model = model;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = (FontFamily)FindResource("Mc.Font");
            Left = model.X;
            Top = model.Y;

            var card = new Border
            {
                Child = WidgetFactory.Build(model.Kind),
                Padding = new Thickness(16, 14, 16, 14),
                CornerRadius = new CornerRadius(22),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(6)
            };
            card.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            card.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            Content = card;

            // arrastrar desde cualquier parte que no sea un campo de texto
            card.MouseLeftButtonDown += (_, e) =>
            {
                if (e.OriginalSource is DependencyObject d && IsInTextBox(d)) return;
                try { DragMove(); } catch { }
                Model.X = Left; Model.Y = Top;
                SettingsStore.Save();
                SendToBack();
            };
            var cm = new ContextMenu();
            var remove = new MenuItem { Header = "Quitar del escritorio" };
            remove.Click += (_, _) => App.RemoveDesktopWidget(this);
            cm.Items.Add(remove);
            card.ContextMenu = cm;

            SourceInitialized += (_, _) =>
            {
                WindowOps.MakeTool(this);
                var src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                src?.AddHook(WndProc);
                SendToBack();
            };
            Activated += (_, _) => SendToBack();
        }

        private static bool IsInTextBox(DependencyObject d)
        {
            for (var p = d; p != null; p = VisualTreeHelper.GetParent(p)) if (p is TextBox) return true;
            return false;
        }

        private void SendToBack()
        {
            var h = new WindowInteropHelper(this).Handle;
            if (h != IntPtr.Zero) Win32.SetWindowPos(h, new IntPtr(1) /*HWND_BOTTOM*/, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 /*NOSIZE|NOMOVE|NOACTIVATE*/);
        }

        // Siempre al fondo: si Windows lo quiere subir, se lo manda abajo de todo.
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_WINDOWPOSCHANGING = 0x0046;
            if (msg == WM_WINDOWPOSCHANGING)
            {
                // WINDOWPOS: hwnd, hwndInsertAfter, x, y, cx, cy, flags
                int offset = IntPtr.Size; // hwndInsertAfter
                System.Runtime.InteropServices.Marshal.WriteIntPtr(lParam, offset, new IntPtr(1)); // HWND_BOTTOM
            }
            return IntPtr.Zero;
        }
    }
}
