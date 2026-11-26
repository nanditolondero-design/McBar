// McBar — Creado por Andrés Londero
// Wi-Fi, Bluetooth y Sonido propios (0.7.0), estilo macOS: se usan en los cajones de la isla y en los
// paneles que abren la barra de menú y el Centro de control, sin abrir Configuración de Windows.
//  - WifiView: prender/apagar, redes cercanas con señal y candado, conectar (pide contraseña si hace
//    falta) y desconectar.
//  - BluetoothView: prender/apagar, dispositivos vinculados con su estado; los de audio se conectan y
//    desconectan desde acá.
//  - SoundView: volumen, salida (parlantes/auriculares) y entrada (micrófono) con un clic.
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal static class ConnUi
    {
        public static StackPanel Header(string title, bool? on, Func<bool, Task> toggle)
        {
            var g = new Grid { Margin = new Thickness(4, 0, 2, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(PanelWindow.Text(title, 14, true));
            var sp = new StackPanel();
            if (on != null)
            {
                var sw = new CheckBox { IsChecked = on, VerticalAlignment = VerticalAlignment.Center };
                sw.SetResourceReference(FrameworkElement.StyleProperty, "McSwitch");
                sw.Click += async (_, _) => { sw.IsEnabled = false; await toggle(sw.IsChecked == true); sw.IsEnabled = true; };
                Grid.SetColumn(sw, 1);
                g.Children.Add(sw);
            }
            sp.Children.Add(g);
            return sp;
        }

        public static TextBlock Caption(string text)
        {
            var t = PanelWindow.Text(text, 11, true, true);
            t.Margin = new Thickness(6, 8, 0, 4);
            return t;
        }

        /// <summary>Fila clickeable: ícono, nombre, detalle y algo a la derecha (✓, candado…).</summary>
        public static Button Row(string glyph, string name, string? detail, string? right, bool active, Action click)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var circle = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), HorizontalAlignment = HorizontalAlignment.Left };
            var gl = PanelWindow.Glyph(glyph, 13);
            circle.Child = gl;
            PanelWindow.SetCircleState(circle, gl, active);
            g.Children.Add(circle);
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var n = PanelWindow.Text(name, 13);
            n.TextTrimming = TextTrimming.CharacterEllipsis;
            texts.Children.Add(n);
            if (!string.IsNullOrEmpty(detail))
            {
                var d = PanelWindow.Text(detail, 11, false, true);
                d.TextTrimming = TextTrimming.CharacterEllipsis;
                texts.Children.Add(d);
            }
            Grid.SetColumn(texts, 1);
            g.Children.Add(texts);
            if (right != null)
            {
                var r = PanelWindow.Glyph(right, 12);
                r.Opacity = 0.7;
                r.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(r, 2);
                g.Children.Add(r);
            }
            var b = new Button { Content = g, Margin = new Thickness(-2, 0, -2, 0) };
            b.SetResourceReference(FrameworkElement.StyleProperty, "McRowButton");
            b.Click += (_, _) => click();
            return b;
        }

        public static Button Link(string text, Action click)
        {
            var b = new Button { Content = text, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
            b.SetResourceReference(FrameworkElement.StyleProperty, "McRowButton");
            b.Click += (_, _) => click();
            return b;
        }

        public static Separator Sep()
        {
            var s = new Separator { Margin = new Thickness(4, 6, 4, 2) };
            s.SetResourceReference(Control.BackgroundProperty, "Mc.Separator");
            return s;
        }
    }

    // ================== Wi-Fi ==================

    internal sealed class WifiView : StackPanel
    {
        private readonly StackPanel _list = new StackPanel();
        private string? _askingFor;

        public WifiView(double width = 320)
        {
            Width = width;
            Load();
        }

        private async void Load()
        {
            Children.Clear();
            bool? on = await WifiService.GetRadioAsync(false);
            Children.Add(ConnUi.Header("Wi-Fi", on, async v => { await WifiService.SetRadioAsync(false, v); await Task.Delay(1500); Refresh(); }));
            Children.Add(_list);
            Children.Add(ConnUi.Sep());
            Children.Add(ConnUi.Link("Configuración de red…", () => Launcher.Open("ms-settings:network")));
            Refresh();
        }

        private async void Refresh()
        {
            _list.Children.Clear();
            _list.Children.Add(PanelWindow.Text("Buscando redes…", 12, false, true));
            var nets = await WifiService.ScanAsync();
            _list.Children.Clear();
            if (!await WifiService.HasAdapterAsync())
            {
                _list.Children.Add(PanelWindow.Text("Este equipo no tiene Wi-Fi (o está apagado).", 12, false, true));
                return;
            }
            if (nets.Count == 0)
            {
                var t = PanelWindow.Text("No se ven redes. Si el Wi-Fi está prendido, Windows puede necesitar permiso de Ubicación para mostrarlas.", 12, false, true);
                t.TextWrapping = TextWrapping.Wrap;
                _list.Children.Add(t);
                _list.Children.Add(ConnUi.Link("Abrir permiso de Ubicación…", () => Launcher.Open("ms-settings:privacy-location")));
                return;
            }
            var cur = nets.Where(n => n.Connected).ToList();
            if (cur.Count > 0)
            {
                _list.Children.Add(ConnUi.Caption("Red conectada"));
                foreach (var n in cur) AddNet(n);
            }
            _list.Children.Add(ConnUi.Caption("Otras redes"));
            foreach (var n in nets.Where(n => !n.Connected).Take(12)) AddNet(n);
        }

        private static string SignalGlyph(int bars) => bars >= 4 ? "" : bars == 3 ? "" : bars == 2 ? "" : "";

        private void AddNet(WifiNet n)
        {
            _list.Children.Add(ConnUi.Row(SignalGlyph(n.Bars), n.Ssid, n.Connected ? "Conectado · clic para desconectar" : null,
                n.Secure ? "" : null, n.Connected, async () =>
                {
                    if (n.Connected) { await WifiService.DisconnectAsync(); await Task.Delay(800); Refresh(); return; }
                    await Connect(n.Ssid, null);
                }));
            if (_askingFor == n.Ssid) _list.Children.Add(PasswordBox(n.Ssid));
        }

        private UIElement PasswordBox(string ssid)
        {
            var sp = new StackPanel { Margin = new Thickness(36, 2, 4, 8) };
            var pb = new PasswordBox { Height = 30, Padding = new Thickness(8, 4, 8, 4), FontSize = 13 };
            pb.SetResourceReference(Control.BackgroundProperty, "Mc.InputBg");
            pb.SetResourceReference(Control.ForegroundProperty, "Mc.Text");
            pb.SetResourceReference(Control.BorderBrushProperty, "Mc.PanelBorder");
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "Cancelar", Margin = new Thickness(0, 0, 6, 0) };
            cancel.SetResourceReference(FrameworkElement.StyleProperty, "McButton");
            cancel.Click += (_, _) => { _askingFor = null; Refresh(); };
            var ok = new Button { Content = "Conectar", IsDefault = true };
            ok.SetResourceReference(FrameworkElement.StyleProperty, "McAccentButton");
            ok.Click += async (_, _) => await Connect(ssid, pb.Password);
            pb.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Connect(ssid, pb.Password); };
            row.Children.Add(cancel);
            row.Children.Add(ok);
            sp.Children.Add(PanelWindow.Text("Contraseña", 11, false, true));
            sp.Children.Add(pb);
            sp.Children.Add(row);
            Dispatcher.BeginInvoke(new Action(() => pb.Focus()), System.Windows.Threading.DispatcherPriority.Input);
            return sp;
        }

        private async Task Connect(string ssid, string? password)
        {
            _list.Children.Insert(0, PanelWindow.Text($"Conectando a {ssid}…", 12, false, true));
            var r = await WifiService.ConnectAsync(ssid, password);
            if (r == WifiResult.NeedsPassword)
            {
                _askingFor = ssid;
                Refresh();
                if (password != null) McDialog.Info("No se pudo conectar", "La contraseña no es correcta. Probá de nuevo.");
                return;
            }
            _askingFor = null;
            if (r == WifiResult.Error) McDialog.Info("No se pudo conectar", $"No se pudo conectar a {ssid}.");
            await Task.Delay(600);
            Refresh();
        }
    }

    // ================== Bluetooth ==================

    internal sealed class BluetoothView : StackPanel
    {
        private readonly StackPanel _list = new StackPanel();

        public BluetoothView(double width = 320)
        {
            Width = width;
            Load();
        }

        private async void Load()
        {
            Children.Clear();
            bool? on = await WifiService.GetRadioAsync(true);
            Children.Add(ConnUi.Header("Bluetooth", on, async v => { await WifiService.SetRadioAsync(true, v); await Task.Delay(1200); Refresh(); }));
            Children.Add(_list);
            Children.Add(ConnUi.Sep());
            Children.Add(ConnUi.Link("Agregar un dispositivo…", () => Launcher.Open("ms-settings:bluetooth")));
            Refresh();
        }

        private static string GlyphOf(BtKind k) => k switch
        {
            BtKind.Audio => "",
            BtKind.Mouse => "",
            BtKind.Keyboard => "",
            BtKind.Phone => "",
            BtKind.Computer => "",
            _ => ""
        };

        private async void Refresh()
        {
            _list.Children.Clear();
            var devs = await Task.Run(BluetoothDevices.List);
            if (devs.Count == 0)
            {
                _list.Children.Add(PanelWindow.Text(await Task.Run(BluetoothDevices.HasRadio) ? "No hay dispositivos vinculados." : "Bluetooth apagado o no disponible.", 12, false, true));
                return;
            }
            _list.Children.Add(ConnUi.Caption("Dispositivos"));
            foreach (var d in devs)
            {
                bool audio = d.Kind == BtKind.Audio;
                string detail = d.Connected ? (audio ? "Conectado · clic para desconectar" : "Conectado")
                                            : (audio ? "No conectado · clic para conectar" : "No conectado · se conecta al usarlo");
                _list.Children.Add(ConnUi.Row(GlyphOf(d.Kind), d.Name, detail, null, d.Connected, async () =>
                {
                    if (!audio) return;
                    _list.Children.Insert(0, PanelWindow.Text(d.Connected ? "Desconectando…" : $"Conectando {d.Name}…", 12, false, true));
                    bool ok = await Task.Run(() => BluetoothDevices.SetAudioConnected(d, !d.Connected));
                    if (!ok) McDialog.Info("Bluetooth", $"No se pudo {(d.Connected ? "desconectar" : "conectar")} {d.Name}.");
                    await Task.Delay(2500);
                    Refresh();
                }));
            }
        }
    }

    // ================== Sonido ==================

    internal sealed class SoundView : StackPanel
    {
        public SoundView(double width = 320)
        {
            Width = width;
            Build();
        }

        private void Build()
        {
            Children.Clear();
            Children.Add(ConnUi.Header("Sonido", null, _ => Task.CompletedTask));
            var vol = CoreAudio.GetVolume();
            if (vol != null)
            {
                var slider = PanelWindow.McSlider(0, 100, vol.Value * 100);
                slider.Margin = new Thickness(6, 0, 6, 6);
                slider.ValueChanged += (_, e) => CoreAudio.SetVolume((float)(e.NewValue / 100.0));
                Children.Add(slider);
            }
            AddDevices("Salida", false);
            AddDevices("Entrada", true);
            Children.Add(ConnUi.Sep());
            Children.Add(ConnUi.Link("Configuración de sonido…", () => Launcher.Open("ms-settings:sound")));
        }

        private void AddDevices(string title, bool input)
        {
            var devs = AudioDevices.List(input);
            if (devs.Count == 0) return;
            Children.Add(ConnUi.Caption(title));
            foreach (var d in devs)
            {
                string glyph = input ? "" : d.Name.Contains("auricular", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("headphone", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("headset", StringComparison.OrdinalIgnoreCase) ? "" : "";
                Children.Add(ConnUi.Row(glyph, d.Name, null, d.IsDefault ? "" : null, d.IsDefault, () =>
                {
                    if (d.IsDefault) return;
                    if (AudioDevices.SetDefault(d.Id))
                    {
                        // la isla escucha el volumen del dispositivo anterior: se vuelve a enganchar
                        if (SettingsStore.Current.IsIslandLike) ActivityHub.Restart();
                    }
                    Build();
                }));
            }
        }
    }

    /// <summary>Panel que muestra una de estas vistas en modo barra de menú.</summary>
    internal sealed class ViewWindow : PanelWindow
    {
        public string Kind { get; }

        public ViewWindow(string kind, FrameworkElement view) : base(356, false) // 1.9.43: 340 era más angosto que la vista (320 + márgenes) y cortaba el borde derecho del Wi-Fi
        {
            Kind = kind;
            Title = kind;
            view.Margin = new Thickness(4);
            // 1.9.44: en pantallas chicas el panel no pasa del borde de abajo: si la lista es larga, se desplaza con la rueda.
            Card.Child = new ScrollViewer
            {
                Content = view,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }
    }
}
