// McBar — Creado por Andrés Londero
// Diálogos propios con estilo macOS (confirmaciones y "Acerca de").
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace McBar.Panels
{
    internal sealed class McDialog : Window
    {
        private McDialog(string title, string message, string okText, string? cancelText, bool danger, UIElement? extra)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SetResourceReference(FontFamilyProperty, "Mc.Font");
            Title = title;

            var sp = new StackPanel { Width = 280 };
            var logo = new Image { Width = 64, Height = 64, Margin = new Thickness(0, 4, 0, 12), Source = Logo() };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            sp.Children.Add(logo);

            var t = new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
            t.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            sp.Children.Add(t);
            if (!string.IsNullOrEmpty(message))
            {
                var m = new TextBlock { Text = message, FontSize = 12.5, Margin = new Thickness(0, 6, 0, 0), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
                m.SetResourceReference(TextBlock.ForegroundProperty, "Mc.TextDim");
                sp.Children.Add(m);
            }
            if (extra != null) sp.Children.Add(extra);

            var buttons = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            var ok = new Button { Content = okText, IsDefault = true };
            ok.SetResourceReference(StyleProperty, "McAccentButton");
            if (danger) ok.SetResourceReference(BackgroundProperty, "Mc.Danger");
            ok.Click += (_, _) => { DialogResult = true; };
            if (cancelText != null)
            {
                buttons.ColumnDefinitions.Add(new ColumnDefinition());
                buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                buttons.ColumnDefinitions.Add(new ColumnDefinition());
                var cancel = new Button { Content = cancelText, IsCancel = true };
                cancel.SetResourceReference(StyleProperty, "McButton");
                buttons.Children.Add(cancel);
                Grid.SetColumn(ok, 2);
            }
            buttons.Children.Add(ok);
            sp.Children.Add(buttons);

            var card = new Border
            {
                Margin = new Thickness(16),
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                Child = sp,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 26, ShadowDepth = 6, Opacity = 0.4 }
            };
            card.SetResourceReference(Border.BackgroundProperty, "Mc.WindowBg");
            card.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            card.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
            Content = card;
            Loaded += (_, _) => { Activate(); ok.Focus(); };
        }

        public static ImageSource? Logo()
        {
            try
            {
                var bmp = new BitmapImage(new Uri("pack://application:,,,/Assets/McBar.png", UriKind.Absolute));
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        public static bool Confirm(string title, string message, string okText, bool danger = false)
        {
            var d = new McDialog(title, message, okText, "Cancelar", danger, null);
            return d.ShowDialog() == true;
        }

        public static void ShowAbout()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            var extra = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var by = new TextBlock { Text = "Creado por Andrés Londero", FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center };
            by.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            extra.Children.Add(by);
            var d = new McDialog("McBar", $"Versión {v?.Major}.{v?.Minor}.{v?.Build}\nDock y barra de menú estilo macOS para Windows.", "Aceptar", null, false, extra);
            d.ShowDialog();
        }
    }
}
