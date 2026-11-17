// McBar — Creado por Andrés Londero
// Un elemento del Dock (app, Launchpad, separador, Descargas o Papelera) y su vista.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using McBar.Core;

namespace McBar.Dock
{
    internal enum DockItemKind { App, Launchpad, Separator, Downloads, Trash }

    internal sealed class DockItem
    {
        public DockItemKind Kind { get; init; }
        public string Name { get; set; } = "";
        /// <summary>Qué se abre al hacer clic (si está fijada). Para apps abiertas sin fijar puede ser null.</summary>
        public string? Target { get; set; }
        public bool IsPinned { get; set; }
        public HashSet<string> Keys { get; set; } = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Para apps abiertas sin fijar: la clave que agrupa sus ventanas.</summary>
        public string? GroupKey { get; set; }
        public List<TrackedWindow> Windows { get; set; } = new List<TrackedWindow>();
        public string? IconSource { get; set; }

        public bool IsRunning => Windows.Count > 0;
        public bool Removing { get; set; }
        public double CurrentSize { get; set; }
        /// <summary>Momento (segundos del reloj del Dock) en que empezó a rebotar, o -1.</summary>
        public double BounceStart { get; set; } = -1;

        public DockItemView View { get; }

        public DockItem(DockItemKind kind)
        {
            Kind = kind;
            View = new DockItemView(this);
        }
    }

    /// <summary>Vista de un elemento: ícono + puntito de "app abierta" (o una línea si es separador).</summary>
    internal sealed class DockItemView : Grid
    {
        private static readonly Brush HitBrush = CreateHitBrush();
        public DockItem Item { get; }
        public Image Icon { get; } = new Image();
        private readonly System.Windows.Shapes.Ellipse _dot = new System.Windows.Shapes.Ellipse();
        private readonly System.Windows.Shapes.Rectangle? _line;
        private readonly TranslateTransform _bounce = new TranslateTransform();

        private static Brush CreateHitBrush()
        {
            // Casi transparente: así los clics no "atraviesan" las partes transparentes del ícono.
            var b = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            b.Freeze();
            return b;
        }

        public DockItemView(DockItem item)
        {
            Item = item;
            Background = HitBrush;
            SnapsToDevicePixels = false;

            if (item.Kind == DockItemKind.Separator)
            {
                _line = new System.Windows.Shapes.Rectangle { Width = 1, HorizontalAlignment = HorizontalAlignment.Center };
                _line.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Mc.Separator");
                Children.Add(_line);
                return;
            }

            RenderOptions.SetBitmapScalingMode(Icon, BitmapScalingMode.HighQuality);
            Icon.Stretch = Stretch.Uniform;
            Icon.VerticalAlignment = VerticalAlignment.Top;
            Icon.RenderTransform = _bounce;
            Children.Add(Icon);

            _dot.Width = 4;
            _dot.Height = 4;
            _dot.VerticalAlignment = VerticalAlignment.Bottom;
            _dot.HorizontalAlignment = HorizontalAlignment.Center;
            _dot.Visibility = Visibility.Hidden;
            _dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Mc.Dot");
            Children.Add(_dot);
        }

        public void SetIcon(ImageSource? src)
        {
            if (src != null) Icon.Source = src;
        }

        /// <summary>Ubica el elemento. <paramref name="iconBottom"/> = borde inferior de los íconos dentro del lienzo.</summary>
        public void Place(double left, double size, double iconBottom, double pad, double bounce, bool showDot, double barHeight, double barTop)
        {
            if (_line != null)
            {
                Width = Math.Max(0, size);
                Height = barHeight;
                _line.Height = barHeight * 0.62;
                _line.VerticalAlignment = VerticalAlignment.Center;
                Canvas.SetLeft(this, left);
                Canvas.SetTop(this, barTop);
                return;
            }
            Width = Math.Max(0, size);
            Height = Math.Max(0, size) + pad;
            Icon.Height = Math.Max(0, size);
            Canvas.SetLeft(this, left);
            Canvas.SetTop(this, iconBottom - size);
            _bounce.Y = -bounce;
            _dot.Margin = new Thickness(0, 0, 0, Math.Max(0, (pad - 4) / 2));
            _dot.Visibility = showDot ? Visibility.Visible : Visibility.Hidden;
            Opacity = Item.Removing ? Math.Clamp(size / 40.0, 0, 1) : 1;
        }
    }
}
