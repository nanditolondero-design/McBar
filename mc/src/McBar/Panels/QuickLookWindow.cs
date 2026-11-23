// McBar — Creado por Andrés Londero
// Quick Look (1.0.0): con un archivo elegido en el Explorador, la barra espaciadora muestra una vista
// previa grande sin abrir ningún programa, como en macOS. Imágenes, videos y audio (se reproducen),
// PDF (primera página), texto y código, carpetas (cantidad de elementos) y el resto con su miniatura
// de Windows. Espacio o Esc cierran; "Abrir" lo abre con su programa.
using System;
using System.IO;
using System.Linq;
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
    internal sealed class QuickLookWindow : Window
    {
        private static QuickLookWindow? _current;
        private readonly string _path;
        private MediaElement? _media;

        /// <summary>Abre (o cierra, si ya estaba abierta) la vista previa del archivo.</summary>
        public static void Toggle(string path)
        {
            if (_current != null) { var same = _current._path == path; _current.Close(); if (same) return; }
            _current = new QuickLookWindow(path);
            _current.Closed += (s, _) => { if (ReferenceEquals(_current, s)) _current = null; };
            _current.Show();
        }

        private QuickLookWindow(string path)
        {
            _path = path;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = (FontFamily)FindResource("Mc.Font");
            var m = App.MonitorOrPrimary(App.ActiveMonitor());
            double sc = m.Scale, mw = m.Work.Width / sc, mh = m.Work.Height / sc;
            Width = Math.Min(1100, mw * 0.7);
            Height = Math.Min(800, mh * 0.78);
            WindowOps.PlaceOnMonitor(this, m, (m.Work.Left - m.Bounds.Left) / sc + (mw - Width) / 2, (m.Work.Top - m.Bounds.Top) / sc + (mh - Height) / 2, Width, Height); // 1.5.0

            // cabecera
            var icon = new Image { Width = 20, Height = 20, Margin = new Thickness(0, 0, 8, 0) };
            IconLoader.Request(path, 32, src => icon.Source = src);
            var name = new TextBlock { Text = Path.GetFileName(path.TrimEnd('\\')), FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            name.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            var open = new Button { Content = "Abrir", Margin = new Thickness(8, 0, 6, 0) };
            open.SetResourceReference(StyleProperty, "McButton");
            open.Click += (_, _) => { Launcher.Launch(path); Close(); };
            var close = new Button { Content = "", FontSize = 11 };
            close.SetResourceReference(StyleProperty, "McFlatButton");
            close.SetResourceReference(FontFamilyProperty, "Mc.Icons");
            close.Click += (_, _) => Close();
            var head = new DockPanel { Margin = new Thickness(14, 10, 10, 8) };
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            right.Children.Add(open);
            right.Children.Add(close);
            DockPanel.SetDock(right, System.Windows.Controls.Dock.Right);
            DockPanel.SetDock(icon, System.Windows.Controls.Dock.Left);
            head.Children.Add(right);
            head.Children.Add(icon);
            head.Children.Add(name);
            head.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) try { DragMove(); } catch { } };

            var body = new Border { Margin = new Thickness(12, 0, 12, 8), CornerRadius = new CornerRadius(10), ClipToBounds = true };
            var info = new TextBlock { FontSize = 11.5, Margin = new Thickness(14, 0, 14, 10), Opacity = 0.7 };
            info.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(head);
            Grid.SetRow(body, 1);
            grid.Children.Add(body);
            Grid.SetRow(info, 2);
            grid.Children.Add(info);
            var card = new Border
            {
                Child = grid,
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(14),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, ShadowDepth = 8, Opacity = 0.45 }
            };
            card.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            card.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            Content = card;

            Fill(body, info);

            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Space || e.Key == Key.Escape) { Close(); e.Handled = true; }
                else if (e.Key == Key.Enter) { Launcher.Launch(path); Close(); e.Handled = true; }
            };
            Loaded += (_, _) =>
            {
                WindowOps.Activate(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                Focus();
                var zoom = new ScaleTransform(0.92, 0.92);
                card.RenderTransformOrigin = new Point(0.5, 0.5);
                card.RenderTransform = zoom;
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            };
            Deactivated += (_, _) => Close();
            Closed += (_, _) => { try { _media?.Stop(); _media?.Close(); } catch { } App.ScheduleTrim(); };
        }

        private async void Fill(Border body, TextBlock info)
        {
            string kind = QuickLookData.KindOf(_path);
            try
            {
                if (kind == "folder")
                {
                    int n = 0;
                    try { n = Directory.EnumerateFileSystemEntries(_path).Count(); } catch { }
                    body.Child = BigIcon();
                    info.Text = $"Carpeta · {n} elemento(s)";
                    return;
                }
                var fi = new FileInfo(_path);
                info.Text = $"{QuickLookData.Size(fi.Length)} · modificado el {fi.LastWriteTime:dd/MM/yyyy HH:mm}";
                switch (kind)
                {
                    case "image":
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.UriSource = new Uri(_path);
                        bi.DecodePixelWidth = 1600;
                        bi.EndInit();
                        body.Child = new Image { Source = bi, Stretch = Stretch.Uniform };
                        info.Text = $"{bi.PixelWidth} × {bi.PixelHeight} · " + info.Text;
                        break;
                    case "video":
                    case "audio":
                        _media = new MediaElement { Source = new Uri(_path), LoadedBehavior = MediaState.Manual, Stretch = Stretch.Uniform };
                        _media.Loaded += (_, _) => _media.Play();
                        body.Child = kind == "audio" ? (UIElement)new Grid { Children = { BigIcon(), _media } } : _media;
                        break;
                    case "pdf":
                        body.Child = BigIcon();
                        var png = await QuickLookData.PdfFirstPageAsync(_path);
                        if (png != null)
                        {
                            var pb = new BitmapImage();
                            pb.BeginInit();
                            pb.CacheOption = BitmapCacheOption.OnLoad;
                            pb.StreamSource = new MemoryStream(png);
                            pb.EndInit();
                            body.Background = Brushes.White;
                            body.Child = new ScrollViewer { Content = new Image { Source = pb, Stretch = Stretch.Uniform }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                            info.Text = "PDF (primera página) · " + info.Text;
                        }
                        break;
                    case "text":
                        string text;
                        using (var r = new StreamReader(_path, detectEncodingFromByteOrderMarks: true))
                        {
                            var buf = new char[200_000];
                            int read = await r.ReadBlockAsync(buf, 0, buf.Length);
                            text = new string(buf, 0, read);
                        }
                        var tb = new TextBox
                        {
                            Text = text, IsReadOnly = true, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12.5,
                            BorderThickness = new Thickness(0), Padding = new Thickness(10), TextWrapping = TextWrapping.NoWrap,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                        };
                        tb.SetResourceReference(BackgroundProperty, "Mc.InputBg");
                        tb.SetResourceReference(ForegroundProperty, "Mc.Text");
                        body.Child = tb;
                        break;
                    default:
                        // miniatura de Windows (documentos de Office, etc.) o el ícono
                        var img = new Image { Width = 256, Height = 256, Stretch = Stretch.Uniform };
                        IconLoader.Request(_path, 256, src => img.Source = src);
                        body.Child = img;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Write("Quick Look: " + ex.Message);
                body.Child = BigIcon();
            }
        }

        private UIElement BigIcon()
        {
            var img = new Image { Width = 160, Height = 160, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            IconLoader.Request(_path, 256, src => img.Source = src);
            return img;
        }
    }
}
