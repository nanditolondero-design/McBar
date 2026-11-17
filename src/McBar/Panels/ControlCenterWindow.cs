// McBar — Creado por Andrés Londero
// Centro de control estilo macOS: conexiones, modo oscuro, concentración, brillo y sonido.
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class ControlCenterWindow : PanelWindow
    {
        private readonly DispatcherTimer _brightnessDebounce;
        private int _pendingBrightness = -1;

        public ControlCenterWindow() : base(330, false)
        {
            Title = "Centro de control";
            _brightnessDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
            _brightnessDebounce.Tick += (_, _) =>
            {
                _brightnessDebounce.Stop();
                int v = _pendingBrightness;
                Task.Run(() => SystemInfo.SetBrightness(v));
            };
            Card.Child = Build();
        }

        private UIElement Build()
        {
            var root = new StackPanel();

            // --- Fila 1: conexiones (izquierda) + modo oscuro / concentración (derecha)
            var row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition());
            row1.ColumnDefinitions.Add(new ColumnDefinition());

            var (kind, name) = SystemInfo.Network();
            var conn = new StackPanel();
            conn.Children.Add(ConnRow("", "Wi-Fi", kind == NetKind.Wifi ? name : kind == NetKind.Ethernet ? "Ethernet" : "Sin conexión",
                kind != NetKind.None, () => Launcher.Open("ms-availablenetworks:")));
            conn.Children.Add(ConnRow("", "Bluetooth", "Configurar", false, () => Launcher.Open("ms-settings:bluetooth")));
            conn.Children.Add(ConnRow("", "Modo avión", "Configurar", false, () => Launcher.Open("ms-settings:network-airplanemode")));
            var connTile = Tile(conn);
            Grid.SetColumn(connTile, 0);
            row1.Children.Add(connTile);

            var right = new StackPanel();
            right.Children.Add(Tile(DarkModeToggle()));
            right.Children.Add(Tile(SimpleAction("", "Concentración", () => Launcher.Open("ms-settings:quiethours"))));
            Grid.SetColumn(right, 1);
            row1.Children.Add(right);
            root.Children.Add(row1);

            // --- Pantalla (solo si se puede controlar el brillo)
            var brightnessTile = Tile(SliderRow("Pantalla", "", 0, null, v =>
            {
                _pendingBrightness = (int)v;
                _brightnessDebounce.Stop();
                _brightnessDebounce.Start();
            }, out var brightnessSlider, null));
            brightnessTile.Visibility = Visibility.Collapsed;
            root.Children.Add(brightnessTile);
            Task.Run(() => SystemInfo.GetBrightness()).ContinueWith(t =>
            {
                if (t.Result is int b)
                {
                    brightnessSlider.Value = b;
                    brightnessTile.Visibility = Visibility.Visible;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());

            // --- Sonido
            var vol = CoreAudio.GetVolume();
            if (vol != null)
            {
                bool muted = CoreAudio.GetMute();
                root.Children.Add(Tile(SliderRow("Sonido", muted ? "" : "", vol.Value * 100,
                    () => { var m = !CoreAudio.GetMute(); CoreAudio.SetMute(m); return m ? "" : ""; },
                    v => CoreAudio.SetVolume((float)(v / 100.0)), out _, "ms-settings:sound")));
            }

            // --- Accesos
            var row3 = new Grid();
            row3.ColumnDefinitions.Add(new ColumnDefinition());
            row3.ColumnDefinitions.Add(new ColumnDefinition());
            var t1 = Tile(SimpleAction("", "Configuración", () => { CloseAnimated(); Launcher.Open("ms-settings:"); }));
            var t2 = Tile(SimpleAction("", "Preferencias de McBar", () => { CloseAnimated(); App.OpenSettings(); }));
            Grid.SetColumn(t2, 1);
            row3.Children.Add(t1);
            row3.Children.Add(t2);
            root.Children.Add(row3);
            return root;
        }

        private static UIElement ConnRow(string glyph, string title, string subtitle, bool active, Action click)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var btn = CircleButton(glyph, active, click);
            g.Children.Add(btn);
            var sp = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(Text(title, 12.5, true));
            var sub = Text(subtitle, 11, false, true);
            sub.TextWrapping = TextWrapping.NoWrap;
            sub.TextTrimming = TextTrimming.CharacterEllipsis;
            sp.Children.Add(sub);
            Grid.SetColumn(sp, 1);
            g.Children.Add(sp);
            return g;
        }

        private UIElement DarkModeToggle()
        {
            bool dark = SystemInfo.WindowsIsDark();
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var circle = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16) };
            var glyph = Glyph("", 14);
            circle.Child = glyph;
            SetCircleState(circle, glyph, dark);
            var btn = new Button { Content = circle, Padding = new Thickness(0) };
            btn.SetResourceReference(StyleProperty, "McFlatButton");
            btn.Click += (_, _) =>
            {
                dark = !dark;
                SystemInfo.SetWindowsDark(dark);
                SetCircleState(circle, glyph, dark);
            };
            g.Children.Add(btn);
            var label = Text("Modo oscuro", 12.5, true);
            label.Margin = new Thickness(8, 0, 0, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 1);
            g.Children.Add(label);
            return g;
        }

        private static UIElement SimpleAction(string glyph, string title, Action click)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(CircleButton(glyph, false, click));
            var label = Text(title, 12.5, true);
            label.Margin = new Thickness(8, 0, 0, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 1);
            g.Children.Add(label);
            return g;
        }

        /// <summary>Título + fila con ícono (opcionalmente botón) y deslizador, como en macOS.</summary>
        private UIElement SliderRow(string title, string glyph, double value, Func<string>? onGlyphClick, Action<double> onChange,
                                    out Slider slider, string? settingsUri)
        {
            var sp = new StackPanel();
            var head = new Grid();
            head.Children.Add(Text(title, 12.5, true));
            if (settingsUri != null)
            {
                var more = new Button { Content = Glyph("", 11), Padding = new Thickness(6, 2, 6, 2), HorizontalAlignment = HorizontalAlignment.Right, ToolTip = "Configuración" };
                more.SetResourceReference(StyleProperty, "McFlatButton");
                more.Click += (_, _) => { CloseAnimated(); Launcher.Open(settingsUri); };
                head.Children.Add(more);
            }
            sp.Children.Add(head);

            var row = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var g = Glyph(glyph, 14);
            g.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
            if (onGlyphClick != null)
            {
                var b = new Button { Content = g, Padding = new Thickness(4, 0, 4, 0), Width = 30 };
                b.SetResourceReference(StyleProperty, "McFlatButton");
                b.Click += (_, _) => g.Text = onGlyphClick();
                row.Children.Add(b);
            }
            else
            {
                g.Width = 30;
                row.Children.Add(g);
            }
            var s = McSlider(0, 100, value);
            s.Margin = new Thickness(6, 0, 0, 0);
            s.ValueChanged += (_, e) => onChange(e.NewValue);
            Grid.SetColumn(s, 1);
            row.Children.Add(s);
            sp.Children.Add(row);
            slider = s;
            return sp;
        }
    }
}
