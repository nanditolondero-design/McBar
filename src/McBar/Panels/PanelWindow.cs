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

        private const double CornerRadiusDip = PanelLook.Radius; // 1.6.0: 18 (antes 16)
        private PanelShadow? _shadow;

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
            Width = width;
            Opacity = 0;
            SetResourceReference(FontFamilyProperty, "Mc.Font");
            FontSize = 13;

            // Tarjeta con esquinas redondeadas recortadas en la ventana (1.9.0: sin desenfoque de Windows, transparencia real).
            Card = new Border
            {
                CornerRadius = new CornerRadius(CornerRadiusDip),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                RenderTransform = _slide
            };
            Card.SetResourceReference(Border.BackgroundProperty, "Mc.PanelCardBg"); // 1.9.0
            Card.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            // 1.6.0: brillo fino arriba, encima del borde
            var root = new Grid();
            root.Children.Add(Card);
            root.Children.Add(PanelLook.Highlight(CornerRadiusDip));
            Content = root;

            SourceInitialized += (_, _) =>
            {
                WindowOps.MakeTool(this);
                // 1.9.0: sin el desenfoque de Windows: en Windows 11 no respeta las esquinas redondeadas y se veía
                // un rectángulo claro detrás del panel (y el texto blanco no se leía). Transparencia real.
            };
            SizeChanged += (_, _) => ClipCorners();
            Deactivated += (_, _) => CloseAnimated();
            KeyDown += (_, e) => { if (e.Key == Key.Escape) CloseAnimated(); };
        }

        private void ClipCorners()
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero) return;
            double sc = WindowOps.DpiScale(this); // 1.5.0: escala del monitor donde está
            int w = (int)Math.Ceiling(ActualWidth * sc), hh = (int)Math.Ceiling(ActualHeight * sc), r = (int)(CornerRadiusDip * 2 * sc);
            Win32.SetWindowRgn(h, Win32.CreateRoundRectRgn(0, 0, w + 1, hh + 1, r, r), true);
        }

        /// <summary>Muestra el panel debajo de la barra de menú del monitor indicado, pegado a la derecha.</summary>
        public void ShowBelowMenuBar(MonitorInfo monitor, double? height = null, bool centered = false)
        {
            double sc = monitor.Scale;
            double top = App.TopInsetDip(monitor) - monitor.Bounds.Top / sc;
            double left = centered ? (monitor.Bounds.Width / sc - Width) / 2 : monitor.Bounds.Width / sc - Width - 8; // 1.6.1: centrado = debajo de la píldora
            WindowOps.PlaceOnMonitor(this, monitor, left, top); // 1.5.0
            if (height != null) Height = height.Value; else SizeToContent = SizeToContent.Height;
            // 1.9.12: en pantallas chicas el panel no pasa del borde de abajo
            MaxHeight = Math.Max(240, monitor.Work.Bottom / sc - monitor.Bounds.Top / sc - top - 12);
            if (height != null && Height > MaxHeight) Height = MaxHeight;
            Show();
            Activate();
            _shadow ??= PanelShadow.Attach(this); // 1.6.0: sombra grande y suave

            // 1.6.0: entrada con resorte (crece desde la esquina de arriba a la derecha con un rebote chiquito)
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            if (_slideFromRight) PanelLook.SpringIn(Card, new Point(1, 0), fromY: 0, fromScale: 0.97, fromX: 50);
            else PanelLook.SpringIn(Card, new Point(centered ? 0.5 : 0.85, 0));
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
            {
                var t = new TranslateTransform();
                Card.RenderTransform = t;
                t.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 40, TimeSpan.FromMilliseconds(130)));
            }
        }

        // ---------- Ayudantes para armar contenido ----------

        internal static TextBlock Text(string text, double size = 13, bool bold = false, bool dim = false)
        {
            var t = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
            if (bold) t.FontWeight = FontWeights.SemiBold;
            t.SetResourceReference(TextBlock.ForegroundProperty, dim ? "Mc.TextDim" : "Mc.Text");
            return t;
        }

        internal static TextBlock Glyph(string glyph, double size = 14)
        {
            var t = new TextBlock { Text = glyph, FontSize = size, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            t.SetResourceReference(TextBlock.FontFamilyProperty, "Mc.Icons");
            return t;
        }

        internal static Border Tile(UIElement child)
        {
            var b = new Border { Child = child };
            b.SetResourceReference(StyleProperty, "McTile");
            return b;
        }

        /// <summary>Botón redondo con un ícono (encendido = color de acento).</summary>
        internal static Button CircleButton(string glyph, bool active, Action onClick, double size = 32)
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

        internal static void SetCircleState(Border circle, TextBlock glyph, bool active)
        {
            circle.SetResourceReference(Border.BackgroundProperty, active ? "Mc.Accent" : "Mc.Hover");
            glyph.SetResourceReference(TextBlock.ForegroundProperty, active ? "Mc.AccentText" : "Mc.Text");
        }

        internal static Slider McSlider(double min, double max, double value)
        {
            var s = new Slider { Minimum = min, Maximum = max, Value = value };
            s.SetResourceReference(StyleProperty, "McSlider");
            return s;
        }
    }
}
