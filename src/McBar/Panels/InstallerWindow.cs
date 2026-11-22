// McBar — Creado por Andrés Londero
// Ventana del instalador (0.5.0), estilo macOS: vidrio con desenfoque, botones de semáforo, el ícono de
// McBar flotando, opciones con interruptores, barra de progreso animada y pantalla de "¡Listo!".
// La lógica está en Core/Installer.cs.
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class InstallerWindow : Window
    {
        /// <summary>Qué eligió el usuario: "installed" (instaló), "portable" (probar sin instalar) o "cancel".</summary>
        public string Result { get; private set; } = "cancel";

        private readonly bool _update;
        private readonly InstallOptions _opt = new InstallOptions();
        private readonly Grid _page = new Grid();
        private readonly Border _card;

        public InstallerWindow(bool update)
        {
            _update = update;
            Title = update ? "Actualizar McBar" : "Instalar McBar";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 480;
            Height = 600;
            Topmost = true;
            ShowInTaskbar = true;
            FontFamily = (FontFamily)FindResource("Mc.Font");
            try { Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/McBar.png")); } catch { }

            _card = new Border
            {
                CornerRadius = new CornerRadius(22),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(14),
                Background = new LinearGradientBrush(Color.FromArgb(0xD8, 0x34, 0x34, 0x3C), Color.FromArgb(0xE6, 0x16, 0x16, 0x1A), 90),
                BorderBrush = new LinearGradientBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF), 90),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, ShadowDepth = 8, Opacity = 0.5 },
                Child = _page
            };
            _card.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) try { DragMove(); } catch { } };
            Content = _card;
            ShowWelcome();

            Opacity = 0;
            Loaded += (_, _) =>
            {
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
                var zoom = new ScaleTransform(0.94, 0.94);
                _card.RenderTransformOrigin = new Point(0.5, 0.5);
                _card.RenderTransform = zoom;
                var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(380)) { EasingFunction = ease });
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(380)) { EasingFunction = ease });
            };
        }

        // ================== Piezas ==================

        private static TextBlock Txt(string text, double size, bool bold = false, byte alpha = 0xF2)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private UIElement TrafficLights()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            foreach (var (c, close) in new[] { (Color.FromRgb(0xFF, 0x5F, 0x57), true), (Color.FromRgb(0xFE, 0xBC, 0x2E), false), (Color.FromRgb(0x28, 0xC8, 0x40), false) })
            {
                var dot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(c), Margin = new Thickness(0, 0, 8, 0) };
                if (close)
                {
                    dot.Cursor = Cursors.Hand;
                    dot.ToolTip = "Cerrar";
                    dot.MouseLeftButtonUp += (_, e) => { e.Handled = true; Result = "cancel"; Close(); };
                }
                else dot.Opacity = 0.35;
                sp.Children.Add(dot);
            }
            return sp;
        }

        private static UIElement FloatingIcon()
        {
            var img = new Image { Width = 104, Height = 104, HorizontalAlignment = HorizontalAlignment.Center };
            try { img.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/McBar.png")); } catch { }
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            var move = new TranslateTransform();
            img.RenderTransform = move;
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-4, 4, TimeSpan.FromSeconds(2.2))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            img.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.45 };
            return img;
        }

        private UIElement Option(string text, bool value, Action<bool> set)
        {
            var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = Txt(text, 13.5);
            t.HorizontalAlignment = HorizontalAlignment.Left;
            t.TextAlignment = TextAlignment.Left;
            t.VerticalAlignment = VerticalAlignment.Center;
            var sw = new CheckBox { IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
            sw.SetResourceReference(StyleProperty, "McSwitch");
            sw.Checked += (_, _) => set(true);
            sw.Unchecked += (_, _) => set(false);
            Grid.SetColumn(sw, 1);
            g.Children.Add(t);
            g.Children.Add(sw);
            return g;
        }

        private static Button Big(string text, bool accent)
        {
            var b = new Button { Content = text, Height = 38, MinWidth = 150, FontSize = 14, Margin = new Thickness(6, 0, 6, 0) };
            b.SetResourceReference(StyleProperty, accent ? "McAccentButton" : "McButton");
            return b;
        }

        private void Swap(UIElement content)
        {
            _page.Children.Clear();
            _page.Children.Add(TrafficLights());
            _page.Children.Add(content);
            if (content is FrameworkElement fe)
                fe.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        }

        // ================== Pantallas ==================

        private void ShowWelcome()
        {
            var sp = new StackPanel { Margin = new Thickness(40, 46, 40, 22) };
            sp.Children.Add(FloatingIcon());
            var title = Txt(_update ? "Actualizar McBar" : "Bienvenido a McBar", 26, true);
            title.Margin = new Thickness(0, 16, 0, 4);
            sp.Children.Add(title);
            sp.Children.Add(Txt("Dock y barra estilo macOS para Windows", 13.5, false, 0xB0));
            var ver = Txt($"Versión {Installer.CurrentVersion.ToString(3)}", 11.5, false, 0x80);
            ver.Margin = new Thickness(0, 2, 0, 18);
            sp.Children.Add(ver);

            var opts = new StackPanel();
            if (!_update)
            {
                opts.Children.Add(Option("Iniciar con Windows", _opt.StartWithWindows, v => _opt.StartWithWindows = v));
                opts.Children.Add(Option("Acceso directo en el menú Inicio", _opt.StartMenuShortcut, v => _opt.StartMenuShortcut = v));
                opts.Children.Add(Option("Acceso directo en el escritorio", _opt.DesktopShortcut, v => _opt.DesktopShortcut = v));
                opts.Children.Add(Option("Traer las apps fijadas de mi barra", _opt.ImportTaskbarPins, v => _opt.ImportTaskbarPins = v));
                opts.Children.Add(Option("Usar el tema Vidrio", _opt.GlassTheme, v => _opt.GlassTheme = v));
            }
            else
            {
                // al actualizar se respetan los ajustes que ya tenía
                var s = SettingsStore.Current;
                _opt.StartWithWindows = s.StartWithWindows;
                _opt.ImportTaskbarPins = false;
                _opt.StartMenuShortcut = System.IO.File.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "McBar.lnk"));
                var v = Installer.InstalledVersion();
                opts.Children.Add(Txt($"Tenés la versión {v?.ToString(3) ?? "?"} instalada. Tus ajustes, apps fijadas y carpetas se mantienen.", 13, false, 0xC0));
            }
            var optCard = new Border
            {
                Child = opts,
                Padding = new Thickness(16, 8, 14, 8),
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1)
            };
            sp.Children.Add(optCard);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 0) };
            var install = Big(_update ? "Actualizar" : "Instalar", true);
            install.Click += (_, _) => RunInstall();
            buttons.Children.Add(install);
            if (!_update)
            {
                var portable = Big("Probar sin instalar", false);
                portable.Click += (_, _) => { Result = "portable"; Close(); };
                buttons.Children.Add(portable);
            }
            sp.Children.Add(buttons);

            var credit = Txt("Creado por Andrés Londero", 11, false, 0x70);
            credit.Margin = new Thickness(0, 18, 0, 0);
            sp.Children.Add(credit);
            Swap(sp);
        }

        private async void RunInstall()
        {
            var sp = new StackPanel { Margin = new Thickness(40, 70, 40, 30), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(FloatingIcon());
            var title = Txt(_update ? "Actualizando…" : "Instalando…", 22, true);
            title.Margin = new Thickness(0, 22, 0, 18);
            sp.Children.Add(title);

            var track = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)) };
            var fill = new Border
            {
                Height = 8, Width = 0, CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(Color.FromRgb(0x0A, 0x84, 0xFF), Color.FromRgb(0x5E, 0x5C, 0xE6), 0)
            };
            var bar = new Grid { Width = 320 };
            bar.Children.Add(track);
            bar.Children.Add(fill);
            sp.Children.Add(bar);
            var step = Txt("", 12.5, false, 0xB0);
            step.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(step);
            Swap(sp);

            void Report(double p, string text)
            {
                Dispatcher.Invoke(() =>
                {
                    step.Text = text;
                    fill.BeginAnimation(WidthProperty, new DoubleAnimation(320 * p, TimeSpan.FromMilliseconds(350)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                });
            }

            Exception? error = null;
            await Task.Run(() =>
            {
                try
                {
                    Installer.Install(_opt, (p, t) => { Report(p, t); System.Threading.Thread.Sleep(220); });
                }
                catch (Exception ex) { error = ex; }
            });
            if (error != null)
            {
                Log.Write("Instalación: " + error);
                ShowDone(false, error.Message);
                return;
            }
            await Task.Delay(350);
            ShowDone(true, null);
        }

        private void ShowDone(bool ok, string? error)
        {
            var sp = new StackPanel { Margin = new Thickness(40, 70, 40, 30) };
            var badge = new Border
            {
                Width = 84, Height = 84, CornerRadius = new CornerRadius(42), HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(ok ? Color.FromRgb(0x30, 0xD1, 0x58) : Color.FromRgb(0xFF, 0x45, 0x3A)),
                Child = Txt(ok ? "✓" : "!", 44, true, 0xFF)
            };
            var pop = new ScaleTransform(0.4, 0.4);
            badge.RenderTransformOrigin = new Point(0.5, 0.5);
            badge.RenderTransform = pop;
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 };
            pop.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease });
            pop.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease });
            sp.Children.Add(badge);

            var title = Txt(ok ? "¡Listo!" : "No se pudo instalar", 26, true);
            title.Margin = new Thickness(0, 20, 0, 8);
            sp.Children.Add(title);
            sp.Children.Add(Txt(ok
                ? "McBar quedó instalado. Lo encontrás en el menú Inicio y en Aplicaciones instaladas, desde donde también se desinstala."
                : "Error: " + error + "\nPodés probar de nuevo o usar McBar sin instalar.", 13.5, false, 0xC0));

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 28, 0, 0) };
            if (ok)
            {
                var open = Big("Abrir McBar", true);
                open.Click += (_, _) => { Result = "installed"; Close(); };
                buttons.Children.Add(open);
            }
            else
            {
                var retry = Big("Probar de nuevo", true);
                retry.Click += (_, _) => ShowWelcome();
                var portable = Big("Usar sin instalar", false);
                portable.Click += (_, _) => { Result = "portable"; Close(); };
                buttons.Children.Add(retry);
                buttons.Children.Add(portable);
            }
            sp.Children.Add(buttons);
            Swap(sp);
        }
    }
}
