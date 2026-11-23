// McBar — Creado por Andrés Londero
// Un elemento del Dock y su vista.
// La vista tiene un tamaño FIJO (el tamaño ampliado máximo) y se agranda/achica/mueve solo con
// transformaciones (escala + traslación). Así la tarjeta de video hace todo el trabajo y WPF no tiene
// que recalcular el diseño en cada cuadro: las animaciones quedan mucho más fluidas.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using McBar.Core;

namespace McBar.Dock
{
    internal enum DockItemKind { App, Launchpad, Separator, Downloads, Trash, Folder, File, AppFolder /* 1.8.3: carpeta de apps */ }

    internal sealed class DockItem
    {
        public DockItemKind Kind { get; init; }
        public string Name { get; set; } = "";
        /// <summary>Qué se abre al hacer clic (si está fijado). Para apps abiertas sin fijar puede ser null.</summary>
        public string? Target { get; set; }
        public bool IsPinned { get; set; }
        /// <summary>true si está del lado derecho (Descargas, Papelera, carpetas, archivos).</summary>
        public bool IsStack { get; init; }
        public HashSet<string> Keys { get; set; } = new HashSet<string>(StringComparer.Ordinal);
        public string? GroupKey { get; set; }
        public List<TrackedWindow> Windows { get; set; } = new List<TrackedWindow>();
        public string? IconSource { get; set; }

        public bool IsRunning => Windows.Count > 0;
        public bool Removing { get; set; }
        /// <summary>Momento en que empezó la "nubecita" al quitarlo arrastrando, o -1.</summary>
        public double PoofStart { get; set; } = -1;
        /// <summary>Cuánto más arriba de la barra se soltó (para que la "nubecita" salga ahí).</summary>
        public double PoofUp { get; set; }
        public double CurrentSize { get; set; }
        public double BounceStart { get; set; } = -1;

        // Última posición dibujada (en el lienzo del Dock), para saber qué hay bajo el mouse.
        public double LayoutLeft { get; set; }
        public double LayoutSize { get; set; }

        public DockItemView View { get; }

        public DockItem(DockItemKind kind, bool isStack = false)
        {
            Kind = kind;
            IsStack = isStack;
            View = new DockItemView(this);
        }

        public bool Removable => Kind != DockItemKind.Separator && (IsPinned || IsStack);
    }

    internal sealed class DockItemView : Grid
    {
        private static readonly Brush HitBrush = CreateHitBrush();
        public DockItem Item { get; }
        public Image Icon { get; } = new Image();
        public System.Windows.Shapes.Ellipse Dot { get; } = new System.Windows.Shapes.Ellipse();
        private readonly System.Windows.Shapes.Rectangle? _line;
        private readonly ScaleTransform _scale = new ScaleTransform(1, 1);
        private readonly TranslateTransform _move = new TranslateTransform();
        private readonly TranslateTransform _dotMove = new TranslateTransform();
        private double _max = 96;

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
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            var group = new TransformGroup();
            group.Children.Add(_scale);
            group.Children.Add(_move);
            RenderTransform = group;

            Dot.Width = 4;
            Dot.Height = 4;
            Dot.Visibility = Visibility.Hidden;
            Dot.IsHitTestVisible = false;
            Dot.RenderTransform = _dotMove;
            Dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Mc.Dot");

            if (item.Kind == DockItemKind.Separator)
            {
                Background = HitBrush;
                _line = new System.Windows.Shapes.Rectangle { Width = 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                _line.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Mc.Separator");
                Children.Add(_line);
                return;
            }

            Background = HitBrush;
            RenderOptions.SetBitmapScalingMode(Icon, BitmapScalingMode.HighQuality);
            Icon.Stretch = Stretch.Uniform;
            Children.Add(Icon);
        }

        /// <summary>1.8.3: marca la app/carpeta sobre la que se va a soltar otra para armar una carpeta.</summary>
        public void SetMergeHighlight(bool on) => Icon.Opacity = on ? 0.55 : 1.0;

        public void SetIcon(ImageSource? src)
        {
            if (src != null) Icon.Source = src;
        }

        /// <summary>Tamaño fijo de la vista = tamaño ampliado máximo.</summary>
        public void SetMaxSize(double max, double sepWidth, double barHeight)
        {
            _max = Math.Max(8, max);
            if (_line != null)
            {
                Width = sepWidth;
                Height = barHeight;
                _line.Height = barHeight * 0.62;
                return;
            }
            Width = _max;
            Height = _max;
            Icon.Width = _max;
            Icon.Height = _max;
            _scale.CenterX = _max / 2;
            _scale.CenterY = _max;
        }

        /// <summary>
        /// Ubica el elemento: <paramref name="left"/> y <paramref name="size"/> son el lugar que ocupa,
        /// <paramref name="iconBottom"/> el borde inferior de los íconos.
        /// </summary>
        public void Place(double left, double size, double iconBottom, double bounce, double extraScale, double opacity,
                          bool showDot, double dotY, double barTop)
        {
            if (_line != null)
            {
                _move.X = left + (size - Width) / 2;
                _move.Y = barTop;
                Opacity = opacity;
                return;
            }
            double s = Math.Max(0, size / _max) * extraScale;
            _scale.ScaleX = s;
            _scale.ScaleY = s;
            _move.X = left + size / 2 - _max / 2;
            _move.Y = iconBottom - _max - bounce;
            Opacity = opacity;

            Dot.Visibility = showDot ? Visibility.Visible : Visibility.Hidden;
            _dotMove.X = left + size / 2 - Dot.Width / 2;
            _dotMove.Y = dotY;
        }
    }
}
