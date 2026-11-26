// McBar — Creado por Andrés Londero
// 1.6.0 — Aspecto de los paneles como en macOS (según la vista previa aprobada):
//  - Sombra grande y suave debajo del panel (PanelShadow: una ventanita aparte, porque el panel recorta su
//    forma para el desenfoque y una sombra dentro de él no se vería).
//  - Brillo fino arriba: el borde de vidrio más claro arriba y apagándose hacia abajo (Highlight).
//  - Animación de entrada con resorte: aparece con un pequeño rebote (SpringIn).
// La sombra se dibuja una sola vez (queda como imagen) y no recibe clics.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal static class PanelLook
    {
        public const double Radius = 18;

        /// <summary>Curva del resorte (rebote suave) usada en todas las entradas de McBar.</summary>
        public static IEasingFunction Spring => new BackEase { Amplitude = 0.14, EasingMode = EasingMode.EaseOut }; // 1.9.43: rebote más suave (antes 0,32)

        /// <summary>Borde con brillo arriba (va ENCIMA del contenido, no recibe clics).</summary>
        public static Border Highlight(double radius = Radius)
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x4A, 0xFF, 0xFF, 0xFF), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), 0.10));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.45));
            brush.Freeze();
            return new Border
            {
                CornerRadius = new CornerRadius(radius),
                BorderThickness = new Thickness(1),
                BorderBrush = brush,
                IsHitTestVisible = false
            };
        }

        /// <summary>Entrada con resorte: baja un poco y crece desde 94 % con un rebote chiquito.</summary>
        public static void SpringIn(FrameworkElement el, Point origin, double fromY = -14, double fromScale = 0.94, double fromX = 0)
        {
            var scale = new ScaleTransform(fromScale, fromScale);
            var move = new TranslateTransform(fromX, fromY);
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(move);
            el.RenderTransformOrigin = origin;
            el.RenderTransform = group;
            var dur = TimeSpan.FromMilliseconds(460);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fromScale, 1, dur) { EasingFunction = Spring });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(fromScale, 1, dur) { EasingFunction = Spring });
            if (fromX != 0) move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromX, 0, dur) { EasingFunction = Spring });
            if (fromY != 0) move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, 0, dur) { EasingFunction = Spring });
        }
    }

    /// <summary>Sombra grande y suave que acompaña a un panel (queda justo debajo de él).</summary>
    internal sealed class PanelShadow : Window
    {
        private const double Margin_ = 56;
        private const long WS_EX_TRANSPARENT = 0x20;
        private readonly Window _owner;
        private readonly double _radius;
        private readonly Border _shape = new Border();
        private readonly Grid _clipHost = new Grid();

        public static PanelShadow Attach(Window owner, double radius = PanelLook.Radius) => new PanelShadow(owner, radius);

        private PanelShadow(Window owner, double radius)
        {
            _owner = owner;
            _radius = radius;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            Opacity = 0;

            _shape.Margin = new Thickness(Margin_);
            _shape.CornerRadius = new CornerRadius(radius);
            _shape.Background = Brushes.Black;
            _shape.Effect = new DropShadowEffect { BlurRadius = 48, ShadowDepth = 18, Direction = 270, Opacity = 0.42, Color = Colors.Black, RenderingBias = RenderingBias.Performance };
            _shape.CacheMode = new BitmapCache(); // se dibuja una vez
            _clipHost.Children.Add(_shape);
            Content = _clipHost;

            SourceInitialized += (_, _) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                Win32.SetExStyle(h, Win32.GetExStyle(h) | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
            };
            SizeChanged += (_, _) => UpdateClip();

            owner.LocationChanged += (_, _) => Follow();
            owner.SizeChanged += (_, _) => Follow();
            owner.IsVisibleChanged += (_, _) => { if (owner.IsVisible) Follow(); else Hide(); };
            owner.Closed += (_, _) => { try { Close(); } catch { } };
            // la sombra aparece y se va junto con el panel
            var opacity = new System.Windows.Data.Binding(nameof(Opacity)) { Source = owner };
            SetBinding(OpacityProperty, opacity);
            if (owner.IsVisible) Follow();
        }

        /// <summary>Solo se ve la sombra AFUERA del panel (adentro queda el vidrio del panel, sin oscurecerlo).</summary>
        private void UpdateClip()
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            var outer = new RectangleGeometry(new Rect(0, 0, w, h));
            var inner = new RectangleGeometry(new Rect(Margin_, Margin_, Math.Max(0, w - 2 * Margin_), Math.Max(0, h - 2 * Margin_)), _radius, _radius);
            var clip = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
            clip.Freeze();
            _clipHost.Clip = clip;
        }

        private void Follow()
        {
            var oh = new WindowInteropHelper(_owner).Handle;
            if (oh == IntPtr.Zero || !Win32.GetWindowRect(oh, out var r)) return;
            if (!IsVisible) Show();
            var h = new WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero) return;
            double sc = WindowOps.DpiScale(_owner);
            int m = (int)Math.Round(Margin_ * sc);
            // justo DEBAJO del panel (hWndInsertAfter = el panel)
            Win32.SetWindowPos(h, oh, r.left - m, r.top - m, r.right - r.left + 2 * m, r.bottom - r.top + 2 * m, Win32.SWP_NOACTIVATE);
        }
    }
}
