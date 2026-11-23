// McBar — Creado por Andrés Londero
// Widget del clima propio (0.4.1), estilo macOS: ahora, sensación térmica, humedad y viento; las próximas
// 24 horas (deslizable) y los próximos 7 días con barra de mínima/máxima.
// Datos de Open-Meteo (sin clave, sin nada de Windows). Se usa en el cajón de la isla y en el panel que
// abre el clima de la barra de menú.
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using McBar.Core;

namespace McBar.Panels
{
    internal sealed class WeatherView : StackPanel
    {
        private static readonly CultureInfo Es = new CultureInfo("es-AR");

        public WeatherView(double width = 340)
        {
            Width = width;
            Children.Add(PanelWindow.Text("Cargando el clima…", 12, false, true));
            Load();
        }

        private async void Load()
        {
            WeatherData? w = null;
            try { w = await WeatherService.GetAsync(); } catch { }
            Children.Clear();
            if (w == null)
            {
                Children.Add(PanelWindow.Text("No se pudo obtener el clima. Revisá la conexión o la ciudad en Preferencias → Widgets.", 12, false, true));
                return;
            }
            Build(w);
        }

        private void Build(WeatherData w)
        {
            var (desc, _) = WeatherCodes.Describe(w.Code);

            // ---- Ahora ----
            var top = new Grid { Margin = new Thickness(4, 0, 4, 8) };
            var left = new StackPanel();
            left.Children.Add(PanelWindow.Text(w.Place, 15, true));
            left.Children.Add(PanelWindow.Text($"{Math.Round(w.Temp)}°", 44));
            top.Children.Add(left);
            var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
            var sym = new WeatherIcon { Width = 54, Height = 54, Animated = true, HorizontalAlignment = HorizontalAlignment.Right };
            sym.Set(WeatherCodes.Kind(w.Code), w.IsDay);
            right.Children.Add(sym);
            var d = PanelWindow.Text(desc, 12.5);
            d.HorizontalAlignment = HorizontalAlignment.Right;
            right.Children.Add(d);
            var mm = PanelWindow.Text($"Máx. {Math.Round(w.Max)}°  Mín. {Math.Round(w.Min)}°", 11.5, false, true);
            mm.HorizontalAlignment = HorizontalAlignment.Right;
            right.Children.Add(mm);
            top.Children.Add(right);
            Children.Add(top);

            var extra = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 6) };
            extra.Children.Add(Chip("Sensación", $"{Math.Round(w.FeelsLike)}°"));
            extra.Children.Add(Chip("Humedad", $"{w.Humidity} %"));
            extra.Children.Add(Chip("Viento", $"{Math.Round(w.Wind)} km/h"));
            Children.Add(Tile(extra));

            // ---- Por hora (24 h, deslizable con la rueda) ----
            var hours = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var h in w.Hours24)
            {
                var sp = new StackPanel { Width = 46, HorizontalAlignment = HorizontalAlignment.Center };
                var hh = PanelWindow.Text(h.Time.Hour == DateTime.Now.Hour && h.Time.Date == DateTime.Today ? "Ahora" : h.Time.ToString("HH", Es) + " h", 11, false, true);
                hh.HorizontalAlignment = HorizontalAlignment.Center;
                var icon = new WeatherIcon { Width = 22, Height = 22, Margin = new Thickness(0, 4, 0, 4), HorizontalAlignment = HorizontalAlignment.Center };
                icon.Set(WeatherCodes.Kind(h.Code), h.Time.Hour >= 7 && h.Time.Hour < 20);
                var tt = PanelWindow.Text($"{Math.Round(h.Temp)}°", 12, true);
                tt.HorizontalAlignment = HorizontalAlignment.Center;
                sp.Children.Add(hh);
                sp.Children.Add(icon);
                sp.Children.Add(tt);
                hours.Children.Add(sp);
            }
            var scroller = new ScrollViewer
            {
                Content = hours,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            scroller.PreviewMouseWheel += (_, e) => { scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta / 2.0); e.Handled = true; };
            Children.Add(Tile(scroller));

            // ---- Próximos días ----
            if (w.Days.Count > 0)
            {
                double lo = w.Days.Min(x => x.Min), hi = w.Days.Max(x => x.Max);
                var days = new StackPanel();
                days.Children.Add(Caption("Próximos días"));
                foreach (var day in w.Days)
                {
                    var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

                    string name = day.Date.Date == DateTime.Today ? "Hoy" : Es.TextInfo.ToTitleCase(day.Date.ToString("ddd", Es).TrimEnd('.'));
                    var n = PanelWindow.Text(name, 12.5, true);
                    n.VerticalAlignment = VerticalAlignment.Center;
                    row.Children.Add(n);

                    var icon = new WeatherIcon { Width = 22, Height = 22, VerticalAlignment = VerticalAlignment.Center };
                    icon.Set(WeatherCodes.Kind(day.Code), true);
                    Grid.SetColumn(icon, 1);
                    row.Children.Add(icon);

                    var lowT = PanelWindow.Text($"{Math.Round(day.Min)}°", 12, false, true);
                    lowT.HorizontalAlignment = HorizontalAlignment.Right;
                    lowT.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(lowT, 2);
                    row.Children.Add(lowT);

                    var range = RangeBar(day, lo, hi);
                    Grid.SetColumn(range, 3);
                    row.Children.Add(range);

                    var highT = PanelWindow.Text($"{Math.Round(day.Max)}°", 12, true);
                    highT.HorizontalAlignment = HorizontalAlignment.Right;
                    highT.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(highT, 4);
                    row.Children.Add(highT);

                    if (day.RainChance >= 30) row.ToolTip = $"Probabilidad de lluvia: {day.RainChance} %";
                    days.Children.Add(row);
                }
                Children.Add(Tile(days));
            }

            var foot = PanelWindow.Text($"Actualizado {w.FetchedAt:HH:mm} · Open-Meteo", 10.5, false, true);
            foot.HorizontalAlignment = HorizontalAlignment.Center;
            foot.Margin = new Thickness(0, 6, 0, 0);
            Children.Add(foot);
        }

        /// <summary>Barrita de mínima a máxima del día dentro del rango de la semana (como en macOS).</summary>
        private static FrameworkElement RangeBar(WeatherDay day, double lo, double hi)
        {
            var g = new Grid { Height = 5, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            var track = new Border { CornerRadius = new CornerRadius(2.5) };
            track.SetResourceReference(Border.BackgroundProperty, "Mc.Track");
            g.Children.Add(track);
            var fill = new Border
            {
                CornerRadius = new CornerRadius(2.5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(Color.FromRgb(0x5A, 0xC8, 0xFA), Color.FromRgb(0xFF, 0x9F, 0x0A), 0)
            };
            g.Children.Add(fill);
            g.SizeChanged += (_, _) =>
            {
                double span = Math.Max(1, hi - lo), w = g.ActualWidth;
                double a = (day.Min - lo) / span * w, b = (day.Max - lo) / span * w;
                fill.Margin = new Thickness(a, 0, 0, 0);
                fill.Width = Math.Max(5, b - a);
            };
            return g;
        }

        private static UIElement Chip(string label, string value)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var l = PanelWindow.Text(label, 10.5, false, true);
            l.HorizontalAlignment = HorizontalAlignment.Center;
            var v = PanelWindow.Text(value, 13, true);
            v.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(l);
            sp.Children.Add(v);
            return sp;
        }

        private static TextBlock Caption(string text)
        {
            var t = PanelWindow.Text(text, 11, true, true);
            t.Margin = new Thickness(0, 0, 0, 4);
            return t;
        }

        private static Border Tile(UIElement child)
        {
            var b = PanelWindow.Tile(child);
            b.Margin = new Thickness(0, 4, 0, 4);
            return b;
        }
    }

    /// <summary>Panel del clima que abre el clima de la barra de menú.</summary>
    internal sealed class WeatherWindow : PanelWindow
    {
        public WeatherWindow() : base(370, false)
        {
            Title = "Clima";
            Card.Child = new WeatherView(350) { Margin = new Thickness(2) };
        }
    }
}
