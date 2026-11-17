// McBar — Creado por Andrés Londero
// Base de los paneles que se despliegan desde la barra (Centro de control, Widgets):
// tarjeta redondeada con sombra, animación de entrada/salida y cierre al hacer clic afuera.
// Los paneles se crean al abrirse y se destruyen al cerrarse, así no ocupan memoria en reposo.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal abstract class PanelWindow : Window
    {
        protected readonly Border Card;
        private readonly TranslateTransform _slide = new TranslateTransform();
        private bool _closing;
        private readonly bool _slideFromRight;

        public DateTime ClosedAt { get; private set; } = DateTime.MinValue;
        public bool IsClosing => _closing;

        protected PanelWindow(double width, bool slideFromRight)
        {
            _slideFromRight = slideFromRight;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Width = width + 20;
            Opacity = 0;
            SetResourceReference(FontFamilyProperty, "Mc.Font");
            FontSize = 13;

            Card = new Border
            {
                Margin = new Thickness(10),
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                RenderTransform = _slide,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Opacity = 0.38 }
            };
            Card.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            Card.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            Content = Card;

            SourceInitialized += (_, _) => WindowOps.MakeTool(this);
            Deactivated += (_, _) => CloseAnimated();
            KeyDown += (_, e) => { if (e.Key == Key.Escape) CloseAnimated(); };
        }

        /// <summary>Muestra el panel debajo de la barra de menú, pegado a la derecha.</summary>
        public void ShowBelowMenuBar(double? height = null)
        {
            double top = SettingsStore.Current.ShowMenuBar ? MenuBar.MenuBarWindow.BarHeightDip - 4 : 0;
            Left = SystemParameters.PrimaryScreenWidth - Width - 2;
            Top = top;
            if (height != null) Height = height.Value; else SizeToContent = SizeToContent.Height;
            Show();
            Activate();

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            if (_slideFromRight)
                _slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(60, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
            else
                _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
        }

        public void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            ClosedAt = DateTime.Now;
            var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(130));
            fade.Completed += (_, _) => { Close(); App.ScheduleTrim(); };
            BeginAnimation(OpacityProperty, fade);
            if (_slideFromRight)
                _slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 40, TimeSpan.FromMilliseconds(130)));
        }

        // ---------- Ayudantes para armar contenido ----------

        protected static TextBlock Text(string text, double size = 13, bool bold = false, bool dim = false)
        {
            var t = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
            if (bold) t.FontWeight = FontWeights.SemiBold;
            t.SetResourceReference(TextBlock.ForegroundProperty, dim ? "Mc.TextDim" : "Mc.Text");
            return t;
        }

        protected static TextBlock Glyph(string glyph, double size = 14)
        {
            var t = new TextBlock { Text = glyph, FontSize = size, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            t.SetResourceReference(TextBlock.FontFamilyProperty, "Mc.Icons");
            return t;
        }

        protected static Border Tile(UIElement child)
        {
            var b = new Border { Child = child };
            b.SetResourceReference(StyleProperty, "McTile");
            return b;
        }

        /// <summary>Botón redondo con un ícono (encendido = color de acento).</summary>
        protected static Button CircleButton(string glyph, bool active, Action onClick, double size = 32)
        {
            var circle = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2) };
            var g = Glyph(glyph, size * 0.45);
            circle.Child = g;
            SetCircleState(circle, g, active);
            var b = new Button { Content = circle, Padding = new Thickness(0), Tag = null };
            b.SetResourceReference(StyleProperty, "McFlatButton");
            b.Click += (_, _) => onClick();
            return b;
        }

        protected static void SetCircleState(Border circle, TextBlock glyph, bool active)
        {
            circle.SetResourceReference(Border.BackgroundProperty, active ? "Mc.Accent" : "Mc.Hover");
            glyph.SetResourceReference(TextBlock.ForegroundProperty, active ? "Mc.AccentText" : "Mc.Text");
        }

        protected static Slider McSlider(double min, double max, double value)
        {
            var s = new Slider { Minimum = min, Maximum = max, Value = value };
            s.SetResourceReference(StyleProperty, "McSlider");
            return s;
        }
    }
}
