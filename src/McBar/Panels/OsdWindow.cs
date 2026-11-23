// McBar — Creado por Andrés Londero
// Cartel de volumen estilo macOS (0.6.0): cuadrado de vidrio redondeado abajo al centro, con el ícono y
// 16 rayitas. Aparece al usar las teclas de volumen en modo barra (en modo isla lo muestra la isla) y se
// desvanece solo. Una sola ventana que se reutiliza; no roba el foco.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class OsdWindow : Window
    {
        private static OsdWindow? _current;
        private readonly TextBlock _glyph = new TextBlock { FontSize = 64, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 26, 0, 22) };
        private readonly StackPanel _bars = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        private readonly DispatcherTimer _hide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };

        public static void ShowVolume(double value, bool muted)
        {
            _current ??= new OsdWindow();
            _current.Set(muted ? "" : value < 0.34 ? "" : value < 0.67 ? "" : "", muted ? 0 : value);
        }

        private OsdWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            Width = 210;
            Height = 210;
            IsHitTestVisible = false;
            _glyph.SetResourceReference(TextBlock.FontFamilyProperty, "Mc.Icons");
            _glyph.SetResourceReference(TextBlock.ForegroundProperty, "Mc.Text");
            for (int i = 0; i < 16; i++)
                _bars.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(1.2, 0, 1.2, 0) });
            var sp = new StackPanel();
            sp.Children.Add(_glyph);
            sp.Children.Add(_bars);
            var card = new Border { Child = sp, CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1), Margin = new Thickness(12) };
            card.SetResourceReference(Border.BackgroundProperty, "Mc.PanelBg");
            card.SetResourceReference(Border.BorderBrushProperty, "Mc.PanelBorder");
            Content = card;
            SourceInitialized += (_, _) => WindowOps.MakeToolNoActivate(this);
            _hide.Tick += (_, _) =>
            {
                _hide.Stop();
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(350));
                fade.Completed += (_, _) => { if (!_hide.IsEnabled) Hide(); };
                BeginAnimation(OpacityProperty, fade);
            };
        }

        private void Set(string glyph, double value)
        {
            _glyph.Text = glyph;
            int on = (int)Math.Round(Math.Clamp(value, 0, 1) * 16);
            for (int i = 0; i < 16; i++)
            {
                var b = (Border)_bars.Children[i];
                if (i < on) b.SetResourceReference(Border.BackgroundProperty, "Mc.Text");
                else b.SetResourceReference(Border.BackgroundProperty, "Mc.Track");
            }
            var m = App.MonitorOrPrimary(App.ActiveMonitor());
            double sc = App.Scale;
            Left = m.Bounds.Left / sc + (m.Bounds.Width / sc - Width) / 2;
            Top = m.Bounds.Top / sc + m.Bounds.Height / sc - Height - 150;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            if (!IsVisible) Show();
            _hide.Stop();
            _hide.Start();
        }
    }
}
