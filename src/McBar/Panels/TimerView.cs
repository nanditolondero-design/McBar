// McBar — Creado por Andrés Londero
// 1.2.0 — Panel "Temporizador" de la isla / barra de menú:
//  - Temporizador: atajos (1, 5, 10, 25 min por defecto) o minutos a mano; pausar, seguir, cancelar.
//  - Cronómetro: iniciar, pausar, reiniciar (con décimas).
//  - Descargas en curso (nombre, tamaño, velocidad) y qué app usa el micrófono / la cámara.
// El estado vive en Core/LiveStatus, así que el panel se puede cerrar y el temporizador sigue.
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using McBar.Core;

namespace McBar.Panels
{
    internal sealed class TimerView : StackPanel
    {
        private readonly DispatcherTimer _tick = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        private readonly TextBlock _timerBig = PanelWindow.Text("", 34, true);
        private readonly TextBlock _swBig = PanelWindow.Text("", 34, true);
        private readonly StackPanel _timerButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        private readonly StackPanel _swButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        private readonly StackPanel _extra = new StackPanel();
        private readonly Action _onChanged;
        private string _lastState = "";

        public TimerView(double width = 320)
        {
            Width = width;
            Children.Add(ConnUi.Header("Temporizador", null, _ => Task.CompletedTask));

            _timerBig.HorizontalAlignment = HorizontalAlignment.Center;
            _timerBig.FontFeatures(); // números de ancho fijo para que no "baile"
            Children.Add(_timerBig);
            Children.Add(_timerButtons);

            Children.Add(ConnUi.Caption("Cronómetro"));
            _swBig.HorizontalAlignment = HorizontalAlignment.Center;
            _swBig.FontFeatures();
            Children.Add(_swBig);
            Children.Add(_swButtons);

            Children.Add(_extra);

            _onChanged = () => Dispatcher.BeginInvoke(new Action(Refresh));
            Loaded += (_, _) => { LiveStatus.Changed += _onChanged; _tick.Start(); Refresh(); };
            Unloaded += (_, _) => { LiveStatus.Changed -= _onChanged; _tick.Stop(); };
            _tick.Tick += (_, _) => UpdateNumbers();
            Refresh();
        }

        private void UpdateNumbers()
        {
            _timerBig.Text = LiveStatus.TimerActive ? LiveStatus.Format(LiveStatus.TimerRemaining) : "0:00";
            _timerBig.Opacity = LiveStatus.TimerActive ? 1 : 0.45;
            _swBig.Text = LiveStatus.Format(LiveStatus.StopwatchElapsed, tenths: true);
            _swBig.Opacity = LiveStatus.StopwatchActive ? 1 : 0.45;
        }

        /// <summary>Rearma los botones solo si cambió el estado (para no perder el foco del cuadro de minutos).</summary>
        private void Refresh()
        {
            UpdateNumbers();
            string state = $"{LiveStatus.TimerRunning}{LiveStatus.TimerActive}{LiveStatus.StopwatchRunning}{LiveStatus.StopwatchActive}";
            if (state != _lastState)
            {
                _lastState = state;
                BuildTimerButtons();
                BuildStopwatchButtons();
            }
            BuildExtra();
        }

        private void BuildTimerButtons()
        {
            _timerButtons.Children.Clear();
            if (LiveStatus.TimerActive)
            {
                if (LiveStatus.TimerRunning) _timerButtons.Children.Add(Btn("Pausar", LiveStatus.PauseTimer));
                else _timerButtons.Children.Add(Btn("Seguir", LiveStatus.ResumeTimer));
                _timerButtons.Children.Add(Btn("Cancelar", LiveStatus.CancelTimer));
                return;
            }
            // 1.3.1: atajos en un renglón y "minutos a mano" en otro (en el cajón de 330 px no entraban juntos)
            var presets = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var m in SettingsStore.Current.TimerPresets)
                presets.Children.Add(Btn(m >= 60 && m % 60 == 0 ? $"{m / 60} h" : $"{m} min", () => LiveStatus.StartTimer(TimeSpan.FromMinutes(m))));
            var custom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var both = new StackPanel();
            both.Children.Add(presets);
            both.Children.Add(custom);
            _timerButtons.Children.Add(both);
            var box = new TextBox { Width = 60, Margin = new Thickness(3, 4, 3, 4), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Minutos" };
            box.SetResourceReference(StyleProperty, "McTextBox");
            void Go()
            {
                var t = ParseMinutes(box.Text);
                if (t != null) LiveStatus.StartTimer(t.Value);
            }
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Go(); e.Handled = true; } };
            custom.Children.Add(box);
            custom.Children.Add(Btn("Iniciar", Go));
        }

        private void BuildStopwatchButtons()
        {
            _swButtons.Children.Clear();
            if (LiveStatus.StopwatchRunning) _swButtons.Children.Add(Btn("Pausar", LiveStatus.PauseStopwatch));
            else _swButtons.Children.Add(Btn(LiveStatus.StopwatchActive ? "Seguir" : "Iniciar", LiveStatus.StartStopwatch));
            if (LiveStatus.StopwatchActive) _swButtons.Children.Add(Btn("Reiniciar", LiveStatus.ResetStopwatch));
        }

        private void BuildExtra()
        {
            _extra.Children.Clear();
            if (GameMode.On) // 1.3.0
            {
                _extra.Children.Add(ConnUi.Caption("Modo juego"));
                _extra.Children.Add(ConnUi.Row("\uE7FC", "Modo juego activado", "Tocá para apagarlo", null, true, () => GameMode.Set(false)));
            }
            if (LiveStatus.Downloads.Count > 0)
            {
                _extra.Children.Add(ConnUi.Caption("Descargas"));
                foreach (var d in LiveStatus.Downloads.Take(4))
                {
                    string speed = d.BytesPerSecond > 1024 ? " · " + LiveStatus.SizeText((long)d.BytesPerSecond) + "/s" : "";
                    _extra.Children.Add(ConnUi.Row("", d.Name, LiveStatus.SizeText(d.Bytes) + speed, null, true, OpenDownloads));
                }
            }
            if (LiveStatus.MicApps.Count > 0 || LiveStatus.CamApps.Count > 0)
            {
                _extra.Children.Add(ConnUi.Caption("Privacidad"));
                foreach (var a in LiveStatus.MicApps) _extra.Children.Add(ConnUi.Row("", a, "Está usando el micrófono", null, true, () => Launcher.Open("ms-settings:privacy-microphone")));
                foreach (var a in LiveStatus.CamApps) _extra.Children.Add(ConnUi.Row("", a, "Está usando la cámara", null, true, () => Launcher.Open("ms-settings:privacy-webcam")));
            }
        }

        private static void OpenDownloads() =>
            Launcher.Open(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));

        /// <summary>"10" → 10 min; "1:30" → 1 min 30 s; "1,5" → 90 s. Pura, se prueba.</summary>
        public static TimeSpan? ParseMinutes(string? text)
        {
            text = (text ?? "").Trim().Replace(',', '.');
            if (text.Length == 0) return null;
            if (text.Contains(':'))
            {
                var p = text.Split(':');
                if (p.Length == 2 && int.TryParse(p[0], out int mm) && int.TryParse(p[1], out int ss) && mm >= 0 && ss >= 0 && ss < 60 && (mm > 0 || ss > 0))
                    return new TimeSpan(0, mm, ss);
                return null;
            }
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double m) && m > 0 && m <= 24 * 60)
                return TimeSpan.FromSeconds(Math.Round(m * 60));
            return null;
        }

        private static Button Btn(string text, Action click)
        {
            var b = new Button { Content = text, Margin = new Thickness(3, 4, 3, 4), MinWidth = 52 };
            b.SetResourceReference(StyleProperty, "McButton");
            b.Click += (_, _) => click();
            return b;
        }
    }

    internal static class TextBlockExtensions
    {
        /// <summary>Números tabulares (todos del mismo ancho) para que el reloj no se mueva.</summary>
        public static void FontFeatures(this TextBlock t) => System.Windows.Documents.Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
    }
}
