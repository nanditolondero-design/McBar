// McBar — Creado por Andrés Londero
// 1.9.27: Visor de emojis (como el Visor de caracteres de macOS). Ventana que no roba el foco: al tocar un
// emoji, se "tipea" en la app que estás usando. Botones de categoría arriba para saltar; la × cierra.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using McBar.Native;

namespace McBar.Panels
{
    internal sealed class EmojiViewer : Window
    {
        private static EmojiViewer? _current;

        public static void Toggle()
        {
            if (_current != null) { _current.Close(); return; }
            try { _current = new EmojiViewer(); _current.Closed += (_, _) => _current = null; _current.Show(); }
            catch (Exception ex) { Core.Log.Write("Emojis: " + ex.Message); }
        }

        private static readonly (string name, string glyph, string[] emojis)[] Cats =
        {
            ("Caras", "😀", "😀 😃 😄 😁 😆 😅 😂 🤣 😊 🙂 🙃 😉 😌 😍 🥰 😘 😗 😙 😚 😋 😛 😝 😜 🤪 🤨 🧐 🤓 😎 🥳 😏 😒 😞 😔 😟 😕 🙁 😣 😖 😫 😩 🥺 😢 😭 😤 😠 😡 🤬 🤯 😳 🥵 🥶 😱 😨 😰 😥 😓 🤗 🤔 🤭 🤫 🤥 😶 😐 😑 😬 🙄 😯 😦 😧 😮 😲 🥱 😴 🤤 😪 😵 🤐 🥴 🤢 🤮 🤧 😷 🤒 🤕".Split(' ')),
            ("Gestos", "👍", "👍 👎 👌 ✌️ 🤞 🤟 🤘 🤙 👈 👉 👆 👇 ☝️ ✋ 🤚 🖐️ 🖖 👋 🤝 🙏 ✍️ 💪 🦾 👏 🙌 👐 🤲 🤜 🤛 ✊ 👊".Split(' ')),
            ("Corazones", "❤️", "❤️ 🧡 💛 💚 💙 💜 🖤 🤍 🤎 💔 ❣️ 💕 💞 💓 💗 💖 💘 💝 💟 ♥️".Split(' ')),
            ("Animales", "🐶", "🐶 🐱 🐭 🐹 🐰 🦊 🐻 🐼 🐨 🐯 🦁 🐮 🐷 🐸 🐵 🐔 🐧 🐦 🐤 🦆 🦅 🦉 🐺 🐗 🐴 🦄 🐝 🐛 🦋 🐌 🐞 🐢 🐍 🦎 🐙 🦑 🦀 🐟 🐠 🐬 🐳 🐋 🦈 🐊 🐅 🐆 🦓 🦍 🐘 🦏 🐪 🦒 🐄 🐎 🐖 🐑 🐐 🦌 🐕 🐩 🐈".Split(' ')),
            ("Comida", "🍎", "🍎 🍐 🍊 🍋 🍌 🍉 🍇 🍓 🫐 🍈 🍒 🍑 🥭 🍍 🥥 🥝 🍅 🍆 🥑 🥦 🥬 🥒 🌶️ 🌽 🥕 🧄 🧅 🥔 🍠 🥐 🍞 🥖 🥨 🧀 🥚 🍳 🥞 🧇 🥓 🍔 🍟 🍕 🌭 🥪 🌮 🌯 🥗 🍝 🍜 🍲 🍣 🍤 🍚 🍦 🍩 🍪 🎂 🍰 🍫 🍬 🍭 🍮 ☕ 🍵 🍺 🍷 🥂".Split(' ')),
            ("Actividad", "⚽", "⚽ 🏀 🏈 ⚾ 🥎 🎾 🏐 🏉 🎱 🏓 🏸 🥅 🏒 🏑 🥍 🏏 ⛳ 🎣 🤿 🥊 🥋 🎽 🛹 🛼 🛷 ⛸️ 🥌 🎿 ⛷️ 🏂 🏋️ 🤸 🤼 🤺 🤾 🏌️ 🏇 🧘 🏄 🏊 🤽 🚣 🧗 🚴 🏆 🥇 🥈 🥉 🎮 🎲 🎯 🎸 🎹 🎺 🎻 🥁 🎬 🎤 🎧 🎨".Split(' ')),
            ("Objetos", "💡", "💡 🔦 🕯️ 🔋 🔌 💻 🖥️ ⌨️ 🖱️ 🖨️ 📱 ☎️ 📞 📟 📠 📺 📷 📹 🎥 🔍 🔎 🔭 📡 💾 💿 📀 🧮 🎥 📚 📖 📝 ✏️ 🖊️ 🖌️ 📌 📎 ✂️ 📏 📐 🔒 🔑 🔨 🛠️ ⚙️ 🧰 🧲 🔧 💰 💳 💎 ⏰ ⏱️ ⌛ 🧭 🔔 🎁 🎈 🧨".Split(' ')),
            ("Símbolos", "⭐", "⭐ 🌟 ✨ ⚡ 🔥 💥 ☀️ 🌈 ☁️ ❄️ 💧 🌊 ✅ ❌ ⭕ ❗ ❓ ⚠️ 🚫 💯 🔴 🟠 🟡 🟢 🔵 🟣 ⚫ ⚪ ♻️ ✔️ ➕ ➖ ✖️ ➗ 🔱 ⚜️ 🔰 ♾️ 💲 🆗 🆕 🆒 🔝 🎵 🎶".Split(' ')),
        };

        private readonly ScrollViewer _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        private EmojiViewer()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false; // no roba el foco
            Topmost = true;
            Width = 380; Height = 420;
            FontFamily = (FontFamily)FindResource("Mc.Font");

            var sections = new StackPanel { Margin = new Thickness(6) };
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 6, 6, 2) };
            foreach (var cat in Cats)
            {
                var anchor = new StackPanel { Margin = new Thickness(2, 8, 2, 2) };
                anchor.Children.Add(new TextBlock { Text = cat.name, Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9E)), FontSize = 12, Margin = new Thickness(6, 2, 0, 4) });
                var grid = new WrapPanel();
                foreach (var em in cat.emojis.Where(e => e.Length > 0))
                {
                    var e = em;
                    var b = new Button { Content = e, FontSize = 22, Width = 40, Height = 40, Margin = new Thickness(1), Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
                    b.Click += (_, _) => WindowOps.TypeUnicode(e);
                    grid.Children.Add(b);
                }
                anchor.Children.Add(grid);
                sections.Children.Add(anchor);

                var c = cat;
                int idx = sections.Children.Count - 1;
                var tb = new Button { Content = cat.glyph, FontSize = 18, Width = 36, Height = 32, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = cat.name };
                tb.Click += (_, _) => { if (sections.Children[idx] is FrameworkElement fe) fe.BringIntoView(); };
                tabs.Children.Add(tb);
            }
            _scroll.Content = sections;

            var close = new Button { Content = "✕", FontSize = 13, Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Right, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
            close.Click += (_, _) => Close();
            var top = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            top.Children.Add(tabs);
            top.Children.Add(close);

            var root = new DockPanel();
            DockPanel.SetDock(top, System.Windows.Controls.Dock.Top);
            root.Children.Add(top);
            root.Children.Add(_scroll);

            var card = new Border
            {
                CornerRadius = new CornerRadius(14), Margin = new Thickness(10),
                Background = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.5 },
                Child = root
            };
            Content = card;

            // ubicar abajo a la derecha del monitor activo
            var m = App.ActiveMonitor() ?? Monitors.All()[0];
            if (Win32.GetCursorPos(out var cp)) m = Monitors.FromPoint(cp.x, cp.y) ?? m;
            double sc = m.Scale;
            WindowOps.PlaceOnMonitor(this, m, (m.Work.Right - m.Bounds.Left) / sc - Width - 8, (m.Work.Bottom - m.Bounds.Top) / sc - Height - 8);
            SourceInitialized += (_, _) => WindowOps.MakeToolNoActivate(this);
        }
    }
}
