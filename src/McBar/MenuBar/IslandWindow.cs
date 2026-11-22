// McBar — Creado por Andrés Londero
// Isla dinámica (estilo iPhone) en lugar de la barra de menú.
//
// Estados:
//  - Reposo: píldora negra chica arriba al centro con la hora (y la tapa del disco + ecualizador si suena algo).
//  - Expandida: al pasar el mouse se agranda y muestra en dos filas todo lo de la barra de menú
//    (menú de McBar, app activa y sus menús, bandeja, clima, batería, red, volumen, búsqueda,
//    Centro de control y reloj), más un mini reproductor si suena algo.
//  - Actividad: se agranda un momento para mostrar volumen, brillo, carga/batería baja, tema nuevo o una notificación.
//  - Reproductor: tocando la isla mientras suena algo: tapa grande, título, artista, progreso y controles.
//
// Consumo: no hay nada animándose en reposo (el ecualizador solo corre mientras suena música).
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;

namespace McBar.MenuBar
{
    internal sealed class IslandWindow : Window
    {
        public const double TopMargin = 6, CompactHeight = 34;
        private const double CompactWidth = 168, CompactMediaWidth = 270;
        private static readonly CultureInfo Es = new CultureInfo("es-AR");

        private enum State { Compact, Expanded, Activity, Media }

        private readonly MonitorInfo _monitor;
        private readonly MenuBarContent _content;
        private readonly Border _pill = new Border();
        private readonly Grid _layers = new Grid();

        // capas
        private readonly Grid _compact = new Grid();
        private readonly Grid _activity = new Grid();
        private readonly StackPanel _expanded = new StackPanel();
        private readonly Grid _media = new Grid();

        // reposo
        private readonly TextBlock _compactTime = new TextBlock();
        private readonly Image _compactArt = new Image { Width = 22, Height = 22 };
        private readonly StackPanel _eq = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _compactBattery = new TextBlock();
        private Storyboard? _eqStory;

        // reproductor y mini reproductor
        private readonly Image _mediaArt = new Image { Width = 72, Height = 72 };
        private readonly TextBlock _mediaTitle = new TextBlock();
        private readonly TextBlock _mediaArtist = new TextBlock();
        private readonly Border _mediaProgress = new Border();
        private readonly Grid _mediaProgressTrack = new Grid();
        private readonly TextBlock _mediaPlayGlyph = new TextBlock();
        private readonly Grid _miniMedia = new Grid();
        private readonly Image _miniArt = new Image { Width = 34, Height = 34 };
        private readonly TextBlock _miniTitle = new TextBlock();
        private readonly TextBlock _miniPlayGlyph = new TextBlock();

        private readonly DispatcherTimer _expandTimer, _collapseTimer, _activityTimer, _clockTimer, _progressTimer;
        private readonly Action<Activity> _onActivity;
        private readonly Action _onMedia;
        private State _state = State.Compact;
        private ActivityKind? _activityKind;
        private IntPtr _hwnd;
        private bool _fullscreen, _wantVisible;

        public MonitorInfo Monitor => _monitor;
        public int ReservePx => _wantVisible && SettingsStore.Current.IslandReserveSpace
            ? (int)Math.Ceiling((TopMargin + CompactHeight + 4) * App.Scale) : 0;

        public IslandWindow(MonitorInfo monitor)
        {
            _monitor = monitor;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Title = "McBar Isla";
            Width = Math.Min(monitor.Bounds.Width / App.Scale, 1120);
            Height = 300;
            SetResourceReference(FontFamilyProperty, "Mc.Font");
            FontSize = 13;

            // La isla siempre es negra: usa la paleta oscura adentro (y en sus menús), sin importar el tema.
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/Dark.xaml", UriKind.Absolute) });
            Resources["Mc.Hover"] = new SolidColorBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));

            _content = new MenuBarContent(monitor);
            _content.SetTwoRows(true);

            _pill.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            _pill.HorizontalAlignment = HorizontalAlignment.Center;
            _pill.VerticalAlignment = VerticalAlignment.Top;
            _pill.Margin = new Thickness(0, TopMargin, 0, 0);
            _pill.Width = CompactWidth;
            _pill.Height = CompactHeight;
            _pill.CornerRadius = new CornerRadius(CompactHeight / 2);
            _pill.ClipToBounds = true;
            _pill.SizeChanged += (_, _) => _pill.CornerRadius = new CornerRadius(Math.Min(_pill.ActualHeight / 2, 26));
            _pill.Child = _layers;

            BuildCompact();
            BuildExpanded();
            BuildMedia();
            foreach (var layer in new FrameworkElement[] { _compact, _activity, _expanded, _media })
            {
                layer.Visibility = Visibility.Collapsed;
                layer.Opacity = 0;
                _layers.Children.Add(layer);
            }
            _compact.Visibility = Visibility.Visible;
            _compact.Opacity = 1;

            var root = new Grid();
            root.Children.Add(_pill);
            Content = root;

            _expandTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(170) };
            _expandTimer.Tick += (_, _) => { _expandTimer.Stop(); if (_state == State.Compact) GoExpanded(); };
            _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(480) };
            _collapseTimer.Tick += (_, _) =>
            {
                if (_pill.IsMouseOver) { _collapseTimer.Stop(); return; }
                if (PopupGuard.AnyOpen) return; // esperar a que se cierre el menú
                _collapseTimer.Stop();
                GoCompact();
            };
            _activityTimer = new DispatcherTimer();
            _activityTimer.Tick += (_, _) =>
            {
                _activityTimer.Stop();
                if (_state != State.Activity) return;
                if (_pill.IsMouseOver) GoExpanded(); else GoCompact();
            };
            _clockTimer = new DispatcherTimer(DispatcherPriority.Background);
            _clockTimer.Tick += (_, _) => { UpdateCompact(); ScheduleClock(); };
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _progressTimer.Tick += (_, _) => UpdateProgress();

            _pill.MouseEnter += (_, _) =>
            {
                _collapseTimer.Stop();
                if (_state == State.Compact) _expandTimer.Start();
            };
            _pill.MouseLeave += (_, _) =>
            {
                _expandTimer.Stop();
                if (_state == State.Expanded || _state == State.Media) _collapseTimer.Start();
            };
            _compact.MouseLeftButtonUp += (_, e) =>
            {
                _expandTimer.Stop();
                if (ActivityHub.Media?.Current != null) GoMedia(); else GoExpanded();
                e.Handled = true;
            };

            _onActivity = ShowActivity;
            _onMedia = OnMediaChanged;
            ActivityHub.Raised += _onActivity;
            ActivityHub.MediaChanged += _onMedia;

            SourceInitialized += (_, _) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                WindowOps.MakeToolNoActivate(this);
            };

            UpdateCompact();
            OnMediaChanged();
            ScheduleClock();
        }

        // ================== Ventana ==================

        public void SetVisible(bool visible)
        {
            _wantVisible = visible;
            if (visible)
            {
                if (!_fullscreen) Show();
                ApplyGeometry();
            }
            else Hide();
        }

        public void ApplyGeometry()
        {
            if (_hwnd == IntPtr.Zero || !_wantVisible) return;
            var b = _monitor.Bounds;
            int w = (int)Math.Ceiling(Width * App.Scale), h = (int)Math.Ceiling(Height * App.Scale);
            Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, b.Left + (b.Width - w) / 2, b.Top, w, h, Win32.SWP_NOACTIVATE);
        }

        public void SetFullscreen(bool fullscreen)
        {
            _fullscreen = fullscreen;
            if (!_wantVisible) return;
            if (fullscreen) Hide(); else { Show(); ApplyGeometry(); }
        }

        public void Teardown()
        {
            ActivityHub.Raised -= _onActivity;
            ActivityHub.MediaChanged -= _onMedia;
            _expandTimer.Stop(); _collapseTimer.Stop(); _activityTimer.Stop(); _clockTimer.Stop(); _progressTimer.Stop();
            _eqStory?.Stop(this);
            _content.Teardown();
        }

        public PxRect? TrayIconRect(TrayIcon ic) => _content.TrayIconRect(ic);

        // ================== Estados ==================

        private void AnimateSize(double w, double h)
        {
            var ease = new BackEase { Amplitude = 0.28, EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(420);
            _pill.BeginAnimation(WidthProperty, new DoubleAnimation(Math.Min(w, Width - 12), dur) { EasingFunction = ease });
            _pill.BeginAnimation(HeightProperty, new DoubleAnimation(h, dur) { EasingFunction = ease });
        }

        private void ShowLayer(FrameworkElement target)
        {
            foreach (FrameworkElement layer in _layers.Children)
            {
                if (layer == target)
                {
                    layer.Visibility = Visibility.Visible;
                    layer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(90) });
                }
                else if (layer.Visibility == Visibility.Visible)
                {
                    var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(90));
                    var l = layer;
                    fade.Completed += (_, _) => { if (l != CurrentLayer) l.Visibility = Visibility.Collapsed; };
                    layer.BeginAnimation(OpacityProperty, fade);
                }
            }
        }

        private FrameworkElement CurrentLayer => _state switch
        {
            State.Expanded => _expanded,
            State.Activity => _activity,
            State.Media => _media,
            _ => _compact
        };

        private void GoCompact()
        {
            _state = State.Compact;
            _activityKind = null;
            _progressTimer.Stop();
            ShowLayer(_compact);
            bool media = ActivityHub.Media?.Current != null;
            AnimateSize(media ? CompactMediaWidth : CompactWidth, CompactHeight);
            UpdateEqualizer();
        }

        private void GoExpanded()
        {
            _state = State.Expanded;
            _activityKind = null;
            _progressTimer.Stop();
            UpdateMiniMedia();
            _content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double w = Math.Max(520, _content.DesiredSize.Width + 40);
            double h = 80 + (_miniMedia.Visibility == Visibility.Visible ? 54 : 0);
            ShowLayer(_expanded);
            AnimateSize(w, h);
            UpdateEqualizer();
        }

        private void GoMedia()
        {
            if (ActivityHub.Media?.Current == null) { GoExpanded(); return; }
            _state = State.Media;
            _activityKind = null;
            UpdateMediaView();
            ShowLayer(_media);
            AnimateSize(420, 150);
            _progressTimer.Start();
            UpdateEqualizer();
        }

        // ================== Actividades ==================

        private void ShowActivity(Activity a)
        {
            if (!_wantVisible || _fullscreen) return;
            if (_state == State.Expanded || _state == State.Media) return; // no interrumpir al usuario

            bool sameKind = _state == State.Activity && _activityKind == a.Kind;
            _activity.Children.Clear();
            _activity.ColumnDefinitions.Clear();
            _activity.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _activity.ColumnDefinitions.Add(new ColumnDefinition());
            _activity.Margin = new Thickness(14, 10, 18, 10);

            double w = 360, h = 64, seconds = 1.8;
            switch (a.Kind)
            {
                case ActivityKind.Volume:
                    AddIcon(Glyph(a.Muted || a.Value < 0.01 ? "" : "", 20, Brushes.White));
                    AddBar("Volumen", a.Muted ? 0 : a.Value, Color.FromRgb(0xFF, 0xFF, 0xFF));
                    break;
                case ActivityKind.Brightness:
                    AddIcon(Glyph("", 20, new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x0A))));
                    AddBar("Brillo", a.Value, Color.FromRgb(0xFF, 0xD6, 0x0A));
                    break;
                case ActivityKind.Charging:
                case ActivityKind.Unplugged:
                case ActivityKind.LowBattery:
                    var color = a.Kind == ActivityKind.LowBattery ? Color.FromRgb(0xFF, 0x45, 0x3A) : a.Kind == ActivityKind.Charging ? Color.FromRgb(0x30, 0xD1, 0x58) : Colors.White;
                    AddIcon(BatteryIcon(a.Value, color, a.Kind == ActivityKind.Charging));
                    AddTexts(a.Kind == ActivityKind.Charging ? "Cargando" : a.Kind == ActivityKind.Unplugged ? "Usando la batería" : "Batería baja",
                             $"{Math.Round(a.Value * 100)} %", color);
                    seconds = 3;
                    break;
                case ActivityKind.Track:
                    if (a.Media == null) return;
                    AddIcon(RoundedImage(a.Media.Art, 40, 9));
                    AddTexts(a.Media.Title, a.Media.Artist, Colors.White);
                    w = 380;
                    seconds = 3.5;
                    break;
                case ActivityKind.Notification:
                    if (a.Notification == null) return;
                    var n = a.Notification;
                    AddIcon(n.Icon != null ? RoundedImage(n.Icon, 36, 8) : Glyph("", 20, Brushes.White));
                    var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
                    if (!string.IsNullOrWhiteSpace(n.App)) sp.Children.Add(Txt(n.App, 11, false, 0.6));
                    sp.Children.Add(Txt(n.Title, 13, true, 1));
                    if (!string.IsNullOrWhiteSpace(n.Body))
                    {
                        var body = Txt(n.Body, 12, false, 0.85);
                        body.TextWrapping = TextWrapping.Wrap;
                        body.MaxHeight = 34;
                        body.TextTrimming = TextTrimming.CharacterEllipsis;
                        sp.Children.Add(body);
                    }
                    Grid.SetColumn(sp, 1);
                    _activity.Children.Add(sp);
                    w = 420;
                    h = string.IsNullOrWhiteSpace(n.Body) ? 64 : 88;
                    seconds = 5;
                    break;
            }

            _activityKind = a.Kind;
            _activityTimer.Stop();
            _activityTimer.Interval = TimeSpan.FromSeconds(seconds);
            _activityTimer.Start();
            if (sameKind) return; // ya está abierta: solo se actualiza el contenido (p. ej. al seguir subiendo el volumen)
            _state = State.Activity;
            ShowLayer(_activity);
            AnimateSize(w, h);
            UpdateEqualizer();
        }

        private void AddIcon(UIElement icon)
        {
            if (icon is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(icon, 0);
            _activity.Children.Add(icon);
        }

        private void AddTexts(string title, string subtitle, Color subColor)
        {
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            sp.Children.Add(Txt(title, 13, true, 1));
            var sub = Txt(subtitle, 12, false, 0.75);
            sub.Foreground = new SolidColorBrush(subColor);
            sp.Children.Add(sub);
            Grid.SetColumn(sp, 1);
            _activity.Children.Add(sp);
        }

        private void AddBar(string label, double value, Color color)
        {
            var g = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            var track = new Grid { Height = 6, VerticalAlignment = VerticalAlignment.Center };
            track.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)) });
            var fill = new Border { CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(color), HorizontalAlignment = HorizontalAlignment.Left };
            track.Children.Add(fill);
            double v = Math.Clamp(value, 0, 1);
            track.SizeChanged += (_, _) => fill.Width = track.ActualWidth * v;
            g.Children.Add(track);
            var pct = Txt($"{Math.Round(v * 100)}", 12, true, 0.85);
            pct.HorizontalAlignment = HorizontalAlignment.Right;
            pct.ToolTip = label;
            Grid.SetColumn(pct, 1);
            g.Children.Add(pct);
            Grid.SetColumn(g, 1);
            _activity.Children.Add(g);
        }

        // ================== Reposo ==================

        private void BuildCompact()
        {
            _compact.Margin = new Thickness(8, 0, 10, 0);
            _compact.Background = Brushes.Transparent; // para recibir clics
            _compact.Cursor = Cursors.Hand;
            _compact.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _compact.ColumnDefinitions.Add(new ColumnDefinition());
            _compact.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _compactArt.Clip = new RectangleGeometry(new Rect(0, 0, 22, 22), 5, 5);
            _compactArt.VerticalAlignment = VerticalAlignment.Center;
            RenderOptions.SetBitmapScalingMode(_compactArt, BitmapScalingMode.HighQuality);
            _compact.Children.Add(_compactArt);

            _compactTime.Foreground = Brushes.White;
            _compactTime.FontWeight = FontWeights.SemiBold;
            _compactTime.HorizontalAlignment = HorizontalAlignment.Center;
            _compactTime.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_compactTime, 1);
            _compact.Children.Add(_compactTime);

            var right = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var colors = new[] { Color.FromRgb(0xFF, 0x9F, 0x0A), Color.FromRgb(0xFF, 0x37, 0x5F), Color.FromRgb(0xBF, 0x5A, 0xF2), Color.FromRgb(0x0A, 0x84, 0xFF) };
            for (int i = 0; i < 4; i++)
            {
                var bar = new Border
                {
                    Width = 3, Height = 14, Margin = new Thickness(1, 0, 1, 0), CornerRadius = new CornerRadius(1.5),
                    Background = new SolidColorBrush(colors[i]),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1, 0.4)
                };
                _eq.Children.Add(bar);
            }
            right.Children.Add(_eq);
            _compactBattery.FontSize = 11;
            _compactBattery.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));
            _compactBattery.VerticalAlignment = VerticalAlignment.Center;
            right.Children.Add(_compactBattery);
            Grid.SetColumn(right, 2);
            _compact.Children.Add(right);
        }

        private void ScheduleClock()
        {
            var now = DateTime.Now;
            _clockTimer.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50);
            _clockTimer.Start();
        }

        private void UpdateCompact()
        {
            _compactTime.Text = DateTime.Now.ToString("HH:mm", Es);
            var b = SystemInfo.Battery();
            bool low = b.HasBattery && !b.Charging && b.Percent <= 20;
            bool media = ActivityHub.Media?.Current != null;
            _compactBattery.Text = low && !media ? $"{b.Percent} %" : "";
        }

        private void UpdateEqualizer()
        {
            var m = ActivityHub.Media?.Current;
            bool run = m != null && m.Playing && _state == State.Compact && IsVisible;
            _eq.Visibility = m != null ? Visibility.Visible : Visibility.Collapsed;
            if (run && _eqStory == null)
            {
                _eqStory = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                double[] durations = { 0.42, 0.31, 0.5, 0.37 };
                for (int i = 0; i < _eq.Children.Count; i++)
                {
                    var anim = new DoubleAnimation(0.3, 1.0, TimeSpan.FromSeconds(durations[i])) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                    Storyboard.SetTarget(anim, _eq.Children[i]);
                    Storyboard.SetTargetProperty(anim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
                    _eqStory.Children.Add(anim);
                }
                _eqStory.Begin(this, true);
            }
            else if (!run && _eqStory != null)
            {
                _eqStory.Stop(this);
                _eqStory = null;
            }
        }

        // ================== Expandida ==================

        private void BuildExpanded()
        {
            _expanded.Margin = new Thickness(10, 8, 10, 8);
            _expanded.Children.Add(_content);

            _miniMedia.Margin = new Thickness(10, 8, 10, 0);
            _miniMedia.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _miniMedia.ColumnDefinitions.Add(new ColumnDefinition());
            _miniMedia.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _miniArt.Clip = new RectangleGeometry(new Rect(0, 0, 34, 34), 7, 7);
            RenderOptions.SetBitmapScalingMode(_miniArt, BitmapScalingMode.HighQuality);
            _miniMedia.Children.Add(_miniArt);
            _miniTitle.Foreground = Brushes.White;
            _miniTitle.Margin = new Thickness(10, 0, 10, 0);
            _miniTitle.VerticalAlignment = VerticalAlignment.Center;
            _miniTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(_miniTitle, 1);
            _miniMedia.Children.Add(_miniTitle);
            var controls = MediaControls(_miniPlayGlyph, 14);
            Grid.SetColumn(controls, 2);
            _miniMedia.Children.Add(controls);
            _miniMedia.Background = Brushes.Transparent;
            _miniMedia.MouseLeftButtonUp += (_, e) => { if (!(e.OriginalSource is TextBlock tb && tb.Tag as string == "ctl")) GoMedia(); };
            _expanded.Children.Add(_miniMedia);
        }

        private void UpdateMiniMedia()
        {
            var m = ActivityHub.Media?.Current;
            _miniMedia.Visibility = m != null ? Visibility.Visible : Visibility.Collapsed;
            if (m == null) return;
            _miniArt.Source = m.Art;
            _miniTitle.Text = string.IsNullOrWhiteSpace(m.Artist) ? m.Title : $"{m.Title} — {m.Artist}";
            _miniPlayGlyph.Text = m.Playing ? "" : "";
        }

        // ================== Reproductor ==================

        private void BuildMedia()
        {
            _media.Margin = new Thickness(16, 14, 16, 12);
            _media.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _media.ColumnDefinitions.Add(new ColumnDefinition());
            _mediaArt.Clip = new RectangleGeometry(new Rect(0, 0, 72, 72), 12, 12);
            _mediaArt.VerticalAlignment = VerticalAlignment.Top;
            RenderOptions.SetBitmapScalingMode(_mediaArt, BitmapScalingMode.HighQuality);
            _media.Children.Add(_mediaArt);

            var right = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
            _mediaTitle.Foreground = Brushes.White;
            _mediaTitle.FontWeight = FontWeights.SemiBold;
            _mediaTitle.FontSize = 14;
            _mediaTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            _mediaArtist.Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
            _mediaArtist.TextTrimming = TextTrimming.CharacterEllipsis;
            right.Children.Add(_mediaTitle);
            right.Children.Add(_mediaArtist);

            _mediaProgressTrack.Height = 4;
            _mediaProgressTrack.Margin = new Thickness(0, 12, 0, 6);
            _mediaProgressTrack.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)) });
            _mediaProgress.CornerRadius = new CornerRadius(2);
            _mediaProgress.Background = Brushes.White;
            _mediaProgress.HorizontalAlignment = HorizontalAlignment.Left;
            _mediaProgressTrack.Children.Add(_mediaProgress);
            right.Children.Add(_mediaProgressTrack);

            var controls = MediaControls(_mediaPlayGlyph, 20);
            controls.HorizontalAlignment = HorizontalAlignment.Center;
            right.Children.Add(controls);
            Grid.SetColumn(right, 1);
            _media.Children.Add(right);
        }

        private StackPanel MediaControls(TextBlock playGlyph, double size)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(ControlButton("", size, () => ActivityHub.Media?.Previous()));
            playGlyph.Text = "";
            sp.Children.Add(ControlButton(null, size, () => ActivityHub.Media?.PlayPause(), playGlyph));
            sp.Children.Add(ControlButton("", size, () => ActivityHub.Media?.Next()));
            return sp;
        }

        private Button ControlButton(string? glyph, double size, Action click, TextBlock? existing = null)
        {
            var t = existing ?? new TextBlock { Text = glyph };
            t.FontSize = size;
            t.Foreground = Brushes.White;
            t.Tag = "ctl";
            t.SetResourceReference(TextBlock.FontFamilyProperty, "Mc.Icons");
            var b = new Button { Content = t, Padding = new Thickness(10, 4, 10, 4) };
            b.SetResourceReference(StyleProperty, "McFlatButton");
            b.Click += (_, _) => click();
            return b;
        }

        private void UpdateMediaView()
        {
            var m = ActivityHub.Media?.Current;
            if (m == null) return;
            _mediaArt.Source = m.Art;
            _mediaTitle.Text = m.Title;
            _mediaArtist.Text = m.Artist;
            _mediaPlayGlyph.Text = m.Playing ? "" : "";
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            var m = ActivityHub.Media?.Current;
            if (m == null || m.Duration <= TimeSpan.Zero) { _mediaProgress.Width = 0; return; }
            double frac = Math.Clamp(m.LivePosition.TotalSeconds / m.Duration.TotalSeconds, 0, 1);
            _mediaProgress.Width = _mediaProgressTrack.ActualWidth * frac;
        }

        private void OnMediaChanged()
        {
            var m = ActivityHub.Media?.Current;
            _compactArt.Source = m?.Art;
            _compactArt.Visibility = m?.Art != null ? Visibility.Visible : Visibility.Collapsed;
            UpdateCompact();
            UpdateMiniMedia();
            if (_state == State.Media)
            {
                if (m == null) GoCompact(); else UpdateMediaView();
            }
            else if (_state == State.Compact)
            {
                AnimateSize(m != null ? CompactMediaWidth : CompactWidth, CompactHeight);
            }
            UpdateEqualizer();
        }

        // ================== Ayudantes ==================

        private static TextBlock Txt(string text, double size, bool bold, double opacity) => new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = Brushes.White,
            Opacity = opacity,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        private static TextBlock Glyph(string glyph, double size, Brush brush)
        {
            var t = new TextBlock { Text = glyph, FontSize = size, Foreground = brush, Width = 36, TextAlignment = TextAlignment.Center };
            t.SetResourceReference(TextBlock.FontFamilyProperty, "Mc.Icons");
            return t;
        }

        private static FrameworkElement RoundedImage(ImageSource? src, double size, double radius)
        {
            if (src == null) return Glyph("", 20, Brushes.White); // nota musical
            var img = new Image { Source = src, Width = size, Height = size, Clip = new RectangleGeometry(new Rect(0, 0, size, size), radius, radius) };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }

        private static FrameworkElement BatteryIcon(double value, Color color, bool charging)
        {
            var g = new Grid { Width = 36, Height = 18 };
            g.Children.Add(new Border { Width = 32, HorizontalAlignment = HorizontalAlignment.Left, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(4), BorderBrush = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)) });
            g.Children.Add(new Border { Width = 2.5, Height = 6, HorizontalAlignment = HorizontalAlignment.Right, CornerRadius = new CornerRadius(0, 1.5, 1.5, 0), Background = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)) });
            g.Children.Add(new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(3, 3, 0, 3),
                Width = Math.Max(2, 26 * Math.Clamp(value, 0, 1)),
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(color)
            });
            if (charging) g.Children.Add(new TextBlock { Text = "⚡", FontSize = 11, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            return g;
        }
    }
}
