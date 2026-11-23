// McBar — Creado por Andrés Londero
// Lista del centro de notificaciones propio: agrupada por app, con "hace X min", botón para borrar cada
// una, cada grupo o todas. Tocar una notificación trae al frente la app (si está abierta).
// Se usa en el panel del reloj (barra de menú) y en el cajón de la isla.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using McBar.Core;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class NotificationListView : StackPanel
    {
        private readonly Action _onChanged;
        private readonly DispatcherTimer _agoTimer;
        private readonly int _maxItems;

        /// <param name="maxItems">Cuántas mostrar como mucho (en el cajón de la isla, menos).</param>
        public NotificationListView(int maxItems = 30)
        {
            _maxItems = maxItems;
            _onChanged = () => Dispatcher.BeginInvoke(new Action(Render));
            NotificationCenter.Changed += _onChanged;
            // "hace X min" se actualiza solo mientras la lista está a la vista
            _agoTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(30) };
            _agoTimer.Tick += (_, _) => Render();
            Loaded += (_, _) => { _agoTimer.Start(); NotificationCenter.MarkAllRead(); };
            Unloaded += (_, _) => { _agoTimer.Stop(); NotificationCenter.Changed -= _onChanged; };
            Render();
        }

        private void Render()
        {
            Children.Clear();
            var head = new Grid { Margin = new Thickness(4, 0, 4, 6) };
            head.Children.Add(PanelWindow.Text("Notificaciones", 13, true));
            if (NotificationCenter.All.Count > 0)
            {
                var clear = new Button { Content = "Borrar todo", Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = HorizontalAlignment.Right, FontSize = 12 };
                clear.SetResourceReference(StyleProperty, "McFlatButton");
                clear.Click += (_, _) => NotificationCenter.Clear();
                head.Children.Add(clear);
            }
            Children.Add(head);

            if (NotificationCenter.All.Count == 0)
            {
                var empty = PanelWindow.Text("No hay notificaciones nuevas", 12, false, true);
                empty.Margin = new Thickness(6, 4, 6, 8);
                Children.Add(empty);
                return;
            }

            var now = DateTime.Now;
            int shown = 0;
            foreach (var g in NotificationCenter.Grouped(NotificationCenter.All))
            {
                if (shown >= _maxItems) break;
                var groupHead = new Grid { Margin = new Thickness(6, 6, 4, 2) };
                groupHead.Children.Add(PanelWindow.Text(g.Key, 11.5, true, true));
                var x = new Button { Content = PanelWindow.Glyph("", 9), Padding = new Thickness(6, 2, 6, 2), HorizontalAlignment = HorizontalAlignment.Right, ToolTip = "Borrar las de " + g.Key };
                x.SetResourceReference(StyleProperty, "McFlatButton");
                var key = g.Key;
                x.Click += (_, _) => NotificationCenter.RemoveGroup(key);
                groupHead.Children.Add(x);
                Children.Add(groupHead);
                foreach (var n in g)
                {
                    if (shown++ >= _maxItems) break;
                    Children.Add(Card(n, now));
                }
            }
        }

        private UIElement Card(NotificationEntry n, DateTime now)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new Image { Width = 28, Height = 28, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 10, 0) };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            if (n.Icon != null) icon.Source = n.Icon;
            else LoadAppIcon(n.App, icon);
            g.Children.Add(icon);

            var texts = new StackPanel();
            var top = new Grid();
            var title = PanelWindow.Text(n.Title, 12.5, true);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;
            title.Margin = new Thickness(0, 0, 50, 0);
            top.Children.Add(title);
            var ago = PanelWindow.Text(NotificationCenter.Ago(n.Time, now), 11, false, true);
            ago.HorizontalAlignment = HorizontalAlignment.Right;
            top.Children.Add(ago);
            texts.Children.Add(top);
            if (!string.IsNullOrWhiteSpace(n.Body))
            {
                var body = PanelWindow.Text(n.Body, 12);
                body.MaxHeight = 52;
                body.TextTrimming = TextTrimming.CharacterEllipsis;
                texts.Children.Add(body);
            }
            Grid.SetColumn(texts, 1);
            g.Children.Add(texts);

            var close = new Button { Content = PanelWindow.Glyph("", 9), Padding = new Thickness(6, 3, 6, 3), VerticalAlignment = VerticalAlignment.Top, Opacity = 0.7, ToolTip = "Borrar" };
            close.SetResourceReference(StyleProperty, "McFlatButton");
            close.Click += (_, _) => NotificationCenter.Remove(n.Id);
            Grid.SetColumn(close, 2);
            g.Children.Add(close);

            var card = new Border { Child = g, Padding = new Thickness(10, 8, 6, 8), Margin = new Thickness(0, 3, 0, 3), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
            card.SetResourceReference(Border.BackgroundProperty, "Mc.TileBg");
            card.SetResourceReference(Border.BorderBrushProperty, "Mc.TileBorder");
            card.MouseLeftButtonUp += (_, e) =>
            {
                if (e.OriginalSource is DependencyObject d && IsInsideButton(d)) return;
                ActivateApp(n.App);
            };
            return card;
        }

        private static bool IsInsideButton(DependencyObject d)
        {
            while (d != null)
            {
                if (d is Button) return true;
                d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d)!;
            }
            return false;
        }

        /// <summary>Ícono de la app de la notificación: si está abierta, el de su ventana.</summary>
        private static void LoadAppIcon(string app, Image target)
        {
            var w = FindWindowOf(app);
            if (w != null && !string.IsNullOrEmpty(w.IconSource))
                IconLoader.Request(w.IconSource, 64, img => target.Source = img);
            else target.Visibility = Visibility.Collapsed;
        }

        private static TrackedWindow? FindWindowOf(string app)
        {
            if (string.IsNullOrWhiteSpace(app)) return null;
            foreach (var w in App.Tracker.Windows)
            {
                var name = App.Tracker.AppNameFor(w.Hwnd);
                if (name.Length < 3) continue;
                if (name.Equals(app, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(app, StringComparison.OrdinalIgnoreCase) ||
                    app.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return w;
            }
            return null;
        }

        private static void ActivateApp(string app)
        {
            var w = FindWindowOf(app);
            if (w != null) WindowOps.Activate(w.Hwnd);
        }
    }
}
