// McBar — Creado por Andrés Londero
// Avisos (notificaciones) de Windows para mostrarlos en la isla.
//
// Plan A: el "lector de notificaciones" oficial de Windows (pide permiso la primera vez). Windows solo lo
//         habilita para algunas apps; si no está disponible...
// Plan B: cuando Windows muestra el cartelito de una notificación, McBar lee su texto con la
//         accesibilidad de Windows (UI Automation), igual que un lector de pantalla.
// El cartelito de Windows se sigue mostrando: McBar solo lo repite en la isla.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace McBar.Core
{
    public sealed class NotificationInfo
    {
        public string App { get; init; } = "";
        public string Title { get; init; } = "";
        public string Body { get; init; } = "";
        public ImageSource? Icon { get; init; }
    }

    internal sealed class NotificationWatcher : IDisposable
    {
        private readonly Dispatcher _ui;
        private UserNotificationListener? _listener;
        private bool _usingListener;
        private DateTime _lastToast = DateTime.MinValue;
        private string _lastText = "";

        /// <summary>Llegó una notificación (en el hilo de la interfaz).</summary>
        public event Action<NotificationInfo>? Notified;

        /// <summary>"Lector oficial", "Lectura de pantalla" o "No disponible" (se muestra en Preferencias).</summary>
        public string Mode { get; private set; } = "Iniciando…";

        public NotificationWatcher() => _ui = Dispatcher.CurrentDispatcher;

        public async void Start()
        {
            try
            {
                _listener = UserNotificationListener.Current;
                var access = await _listener.RequestAccessAsync();
                if (access == UserNotificationListenerAccessStatus.Allowed)
                {
                    _listener.NotificationChanged += OnListenerChanged;
                    _usingListener = true;
                    Mode = "Lector oficial de Windows";
                    return;
                }
            }
            catch (Exception ex) { Log.Write("Notificaciones (plan A no disponible): " + ex.Message); }
            _usingListener = false;
            Mode = "Lectura de los avisos en pantalla";
        }

        // ---------- Plan A ----------

        private void OnListenerChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
        {
            if (args.ChangeKind != UserNotificationChangedKind.Added) return;
            try
            {
                var n = sender.GetNotification(args.UserNotificationId);
                if (n == null) return;
                var binding = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
                var texts = binding?.GetTextElements().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>();
                string app = n.AppInfo?.DisplayInfo?.DisplayName ?? "";
                _ = PublishAsync(app, texts, n.AppInfo?.DisplayInfo);
            }
            catch (Exception ex) { Log.Write("Notificaciones: " + ex.Message); }
        }

        private async Task PublishAsync(string app, List<string> texts, Windows.ApplicationModel.AppDisplayInfo? info)
        {
            ImageSource? icon = null;
            try
            {
                if (info != null)
                {
                    var stream = await info.GetLogo(new Windows.Foundation.Size(48, 48)).OpenReadAsync();
                    using var st = stream.AsStreamForRead();
                    var ms = new MemoryStream();
                    await st.CopyToAsync(ms);
                    ms.Position = 0;
                    var bi = new BitmapImage();
                    bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
                    icon = bi;
                }
            }
            catch { }
            var ni = new NotificationInfo
            {
                App = app,
                Title = texts.Count > 0 ? texts[0] : app,
                Body = string.Join(" ", texts.Skip(1)),
                Icon = icon
            };
            _ = _ui.BeginInvoke(new Action(() => Notified?.Invoke(ni)));
        }

        // ---------- Plan B ----------

        /// <summary>Llamar cuando aparece una ventana de primer nivel (WindowTracker avisa).</summary>
        public void OnWindowShown(IntPtr hwnd, string cls)
        {
            if (_usingListener || cls != "Windows.UI.Core.CoreWindow") return;
            var proc = ProcessName(hwnd);
            if (!proc.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase)) return;
            if ((DateTime.Now - _lastToast).TotalMilliseconds < 300) return;
            _lastToast = DateTime.Now;
            Task.Run(async () =>
            {
                await Task.Delay(450); // dejar que se dibuje el contenido
                try
                {
                    var el = AutomationElement.FromHandle(hwnd);
                    var texts = el.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                        .Cast<AutomationElement>()
                        .Select(e => e.Current.Name?.Trim() ?? "")
                        .Where(t => t.Length > 0)
                        .Distinct()
                        .Take(4)
                        .ToList();
                    if (texts.Count == 0)
                    {
                        var name = el.Current.Name?.Trim();
                        if (!string.IsNullOrEmpty(name)) texts.Add(name);
                    }
                    if (texts.Count == 0) return;
                    var all = string.Join("|", texts);
                    if (all == _lastText) return;
                    _lastText = all;
                    var ni = new NotificationInfo
                    {
                        App = texts.Count > 2 ? texts[0] : "",
                        Title = texts.Count > 2 ? texts[1] : texts[0],
                        Body = string.Join(" ", texts.Skip(texts.Count > 2 ? 2 : 1))
                    };
                    _ = _ui.BeginInvoke(new Action(() => Notified?.Invoke(ni)));
                }
                catch (Exception ex) { Log.Write("Notificaciones (pantalla): " + ex.Message); }
            });
        }

        private static string ProcessName(IntPtr hwnd)
        {
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById((int)Native.Win32.ProcessOf(hwnd));
                return p.ProcessName;
            }
            catch { return ""; }
        }

        public void Dispose()
        {
            try { if (_listener != null && _usingListener) _listener.NotificationChanged -= OnListenerChanged; } catch { }
        }
    }
}
