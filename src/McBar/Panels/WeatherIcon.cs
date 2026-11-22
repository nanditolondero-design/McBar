// McBar — Creado por Andrés Londero
// Ícono de clima dibujado (sin imágenes): sol, luna, nubes, lluvia, llovizna, nieve, tormenta y niebla.
// Cambia según el clima y si es de día o de noche. En el panel de widgets se anima (rayos que giran,
// gotas y copos que caen); en la barra de menú queda quieto para no consumir.
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using McBar.Core;

namespace McBar.Panels
{
    internal sealed class WeatherIcon : FrameworkElement
    {
        private static readonly Brush SunBrush = Frozen(Color.FromRgb(0xFF, 0xD6, 0x0A));
        private static readonly Brush MoonBrush = Frozen(Color.FromRgb(0xE5, 0xE5, 0xEA));
        private static readonly Brush CloudBrush = Frozen(Color.FromRgb(0xF2, 0xF2, 0xF7));
        private static readonly Brush DarkCloudBrush = Frozen(Color.FromRgb(0x8E, 0x8E, 0x93));
        private static readonly Brush RainBrush = Frozen(Color.FromRgb(0x5A, 0xC8, 0xFA));
        private static readonly Brush SnowBrush = Frozen(Colors.White);
        private static readonly Brush BoltBrush = Frozen(Color.FromRgb(0xFF, 0xCC, 0x00));
        private static readonly Pen CloudPen = FrozenPen(Color.FromArgb(0x99, 0x63, 0x63, 0x66), 0.8);

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _lastFrame;
        private bool _hooked;

        public WeatherKind Kind { get; private set; } = WeatherKind.Cloudy;
        public bool IsDay { get; private set; } = true;
        public bool Animated { get; set; }

        public WeatherIcon()
        {
            Loaded += (_, _) => UpdateHook();
            Unloaded += (_, _) => Unhook();
            IsVisibleChanged += (_, _) => UpdateHook();
        }

        public void Set(WeatherKind kind, bool isDay)
        {
            Kind = kind;
            IsDay = isDay;
            InvalidateVisual();
            UpdateHook();
        }

        private void UpdateHook()
        {
            bool want = Animated && IsVisible && IsLoaded;
            if (want && !_hooked) { CompositionTarget.Rendering += OnFrame; _hooked = true; }
            else if (!want) Unhook();
        }

        private void Unhook()
        {
            if (_hooked) { CompositionTarget.Rendering -= OnFrame; _hooked = false; }
        }

        private void OnFrame(object? sender, EventArgs e)
        {
            double t = _clock.Elapsed.TotalSeconds;
            if (t - _lastFrame < 1.0 / 30) return; // 30 cuadros por segundo alcanzan
            _lastFrame = t;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 0) return;
            double t = Animated ? _clock.Elapsed.TotalSeconds : 0;
            dc.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
            dc.PushTransform(new ScaleTransform(size / 24.0, size / 24.0));

            switch (Kind)
            {
                case WeatherKind.Clear:
                    if (IsDay) Sun(dc, 12, 12, 1, t); else Moon(dc, 12, 12, 1);
                    break;
                case WeatherKind.PartlyCloudy:
                    if (IsDay) Sun(dc, 8.5, 8.5, 0.75, t); else Moon(dc, 8.5, 8.5, 0.72);
                    Cloud(dc, 2.5 + Math.Sin(t * 0.6) * 0.6, 3, false);
                    break;
                case WeatherKind.Cloudy:
                    Cloud(dc, Math.Sin(t * 0.6) * 0.6, 0, false);
                    break;
                case WeatherKind.Fog:
                    for (int i = 0; i < 3; i++)
                    {
                        double shift = Math.Sin(t * 0.8 + i) * 1.2;
                        var pen = FrozenPen(Color.FromRgb(0xAE, 0xAE, 0xB2), 1.8);
                        dc.DrawLine(pen, new Point(4 + shift, 9 + i * 4), new Point(20 + shift, 9 + i * 4));
                    }
                    break;
                case WeatherKind.Drizzle:
                case WeatherKind.Rain:
                    Cloud(dc, 0, -2.5, false);
                    Drops(dc, t, Kind == WeatherKind.Rain);
                    break;
                case WeatherKind.Snow:
                    Cloud(dc, 0, -2.5, false);
                    Flakes(dc, t);
                    break;
                case WeatherKind.Storm:
                    Cloud(dc, 0, -2.5, true);
                    if (!Animated || (t % 2.4) < 1.9) Bolt(dc);
                    break;
            }
            dc.Pop();
            dc.Pop();
        }

        private static void Sun(DrawingContext dc, double cx, double cy, double scale, double t)
        {
            var pen = FrozenPen(Color.FromRgb(0xFF, 0xD6, 0x0A), 1.6 * scale);
            double angle = t * 20 * Math.PI / 180; // giro lento
            for (int i = 0; i < 8; i++)
            {
                double a = angle + i * Math.PI / 4;
                dc.DrawLine(pen,
                    new Point(cx + Math.Cos(a) * 6.6 * scale, cy + Math.Sin(a) * 6.6 * scale),
                    new Point(cx + Math.Cos(a) * 9.2 * scale, cy + Math.Sin(a) * 9.2 * scale));
            }
            dc.DrawEllipse(SunBrush, null, new Point(cx, cy), 4.6 * scale, 4.6 * scale);
        }

        private static void Moon(DrawingContext dc, double cx, double cy, double scale)
        {
            var full = new EllipseGeometry(new Point(cx, cy), 6.5 * scale, 6.5 * scale);
            var bite = new EllipseGeometry(new Point(cx + 3.6 * scale, cy - 2.6 * scale), 5.4 * scale, 5.4 * scale);
            dc.DrawGeometry(MoonBrush, null, new CombinedGeometry(GeometryCombineMode.Exclude, full, bite));
        }

        private static void Cloud(DrawingContext dc, double dx, double dy, bool dark)
        {
            var g = new GeometryGroup { FillRule = FillRule.Nonzero };
            g.Children.Add(new RectangleGeometry(new Rect(4 + dx, 12.5 + dy, 16, 6.5), 3.25, 3.25));
            g.Children.Add(new EllipseGeometry(new Point(9 + dx, 12.5 + dy), 4, 4));
            g.Children.Add(new EllipseGeometry(new Point(14.2 + dx, 11 + dy), 5.2, 5.2));
            var outline = g.GetOutlinedPathGeometry();
            dc.DrawGeometry(dark ? DarkCloudBrush : CloudBrush, CloudPen, outline);
        }

        private static void Drops(DrawingContext dc, double t, bool heavy)
        {
            var pen = FrozenPen(Color.FromRgb(0x5A, 0xC8, 0xFA), heavy ? 1.5 : 1.2);
            for (int i = 0; i < 3; i++)
            {
                double phase = (t * (heavy ? 1.4 : 1.0) + i * 0.33) % 1.0;
                double y = 18 + phase * 4.5;
                double x = 8 + i * 4 - phase * 1.2;
                if (heavy) dc.DrawLine(pen, new Point(x, y), new Point(x - 0.8, y + 2.4));
                else dc.DrawEllipse(RainBrush, null, new Point(x, y + 1), 0.8, 0.8);
            }
        }

        private static void Flakes(DrawingContext dc, double t)
        {
            for (int i = 0; i < 3; i++)
            {
                double phase = (t * 0.5 + i * 0.33) % 1.0;
                double y = 18 + phase * 4.5;
                double x = 8 + i * 4 + Math.Sin((t + i) * 2) * 0.8;
                dc.DrawEllipse(SnowBrush, CloudPen, new Point(x, y + 1), 1.1, 1.1);
            }
        }

        private static void Bolt(DrawingContext dc)
        {
            var geo = Geometry.Parse("M 12.5,15.5 L 9.5,20 L 12,20 L 10.5,23.5 L 15,18.5 L 12.4,18.5 L 14,15.5 Z");
            dc.DrawGeometry(BoltBrush, null, geo);
        }

        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        private static Pen FrozenPen(Color c, double w)
        {
            var p = new Pen(new SolidColorBrush(c), w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            p.Freeze();
            return p;
        }
    }
}
