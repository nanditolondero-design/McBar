// McBar — Creado por Andrés Londero
// Junta todo lo que la isla dinámica puede mostrar: volumen, brillo, batería, música/video y notificaciones.
// Solo se enciende en modo isla (en modo barra no gasta nada) y todo funciona por eventos.
using System;
using System.Management;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Core
{
    public enum ActivityKind { Volume, Brightness, Charging, Unplugged, LowBattery, Track, Notification }

    public sealed class Activity
    {
        public ActivityKind Kind { get; init; }
        public double Value { get; init; }          // 0..1 para volumen/brillo/batería
        public bool Muted { get; init; }
        public MediaInfo? Media { get; init; }
        public NotificationInfo? Notification { get; init; }
    }

    internal static class ActivityHub
    {
        private static Dispatcher? _ui;
        private static VolumeWatcher? _volume;
        private static ManagementEventWatcher? _brightness;
        private static Action<NotificationEntry>? _onNotification;
        private static bool _running;
        private static BatteryState _lastBattery;
        private static Microsoft.Win32.PowerModeChangedEventHandler? _powerHandler;

        public static MediaWatcher? Media { get; private set; }
        public static string NotificationMode => NotificationCenter.Mode;

        /// <summary>Algo para mostrar un momento en la isla (en el hilo de la interfaz).</summary>
        public static event Action<Activity>? Raised;
        /// <summary>Cambió lo que se está reproduciendo.</summary>
        public static event Action? MediaChanged;

        public static void Start()
        {
            if (_running) return;
            _running = true;
            _ui = Dispatcher.CurrentDispatcher;
            var s = SettingsStore.Current;

            if (s.IslandVolume)
            {
                _volume = new VolumeWatcher();
                _volume.Changed += (v, m) => Post(new Activity { Kind = ActivityKind.Volume, Value = v, Muted = m });
                if (!_volume.Start()) { _volume.Dispose(); _volume = null; }

                // WMI puede tardar varios segundos en PCs de escritorio (sin control de brillo): en segundo plano.
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var w = new ManagementEventWatcher(new ManagementScope(@"root\WMI"),
                            new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
                        w.EventArrived += (_, e) =>
                        {
                            try { Post(new Activity { Kind = ActivityKind.Brightness, Value = Convert.ToDouble(e.NewEvent["Brightness"]) / 100.0 }); }
                            catch { }
                        };
                        w.Start();
                        if (_running) _brightness = w; else { w.Stop(); w.Dispose(); }
                    }
                    catch { _brightness = null; } // PC de escritorio sin control de brillo
                });
            }

            if (s.IslandBattery)
            {
                _lastBattery = SystemInfo.Battery();
                _powerHandler = (_, e) => _ui?.BeginInvoke(new Action(CheckBattery));
                Microsoft.Win32.SystemEvents.PowerModeChanged += _powerHandler;
            }

            if (s.IslandMedia)
            {
                Media = new MediaWatcher();
                Media.Changed += () => MediaChanged?.Invoke();
                Media.TrackChanged += m => Raised?.Invoke(new Activity { Kind = ActivityKind.Track, Media = m });
                Media.Start();
            }

            if (s.IslandNotifications)
            {
                // Las notificaciones las junta el centro de notificaciones (funciona con barra o con isla).
                _onNotification = e => Raised?.Invoke(new Activity
                {
                    Kind = ActivityKind.Notification,
                    Notification = new NotificationInfo { App = e.App, Title = e.Title, Body = e.Body, Icon = e.Icon }
                });
                NotificationCenter.Added += _onNotification;
            }
        }

        public static void Stop()
        {
            if (!_running) return;
            _running = false;
            try { _volume?.Dispose(); } catch { }
            try { _brightness?.Stop(); _brightness?.Dispose(); } catch { }
            try { Media?.Dispose(); } catch { }
            if (_onNotification != null) NotificationCenter.Added -= _onNotification;
            if (_powerHandler != null) Microsoft.Win32.SystemEvents.PowerModeChanged -= _powerHandler;
            _volume = null; _brightness = null; Media = null; _onNotification = null; _powerHandler = null;
            MediaChanged?.Invoke();
        }

        public static void Restart() { Stop(); Start(); }


        private static void Post(Activity a) => _ui?.BeginInvoke(new Action(() => Raised?.Invoke(a)));

        private static void CheckBattery()
        {
            var b = SystemInfo.Battery();
            if (!b.HasBattery) return;
            var prev = _lastBattery;
            _lastBattery = b;
            var kind = BatteryActivity(prev, b);
            if (kind != null) Raised?.Invoke(new Activity { Kind = kind.Value, Value = b.Percent / 100.0 });
        }

        /// <summary>Qué mostrar al cambiar la batería (pura, se prueba).</summary>
        public static ActivityKind? BatteryActivity(BatteryState prev, BatteryState now)
        {
            if (!now.HasBattery) return null;
            if (now.Charging && !prev.Charging) return ActivityKind.Charging;
            if (!now.Charging && prev.Charging) return ActivityKind.Unplugged;
            if (!now.Charging && ((prev.Percent > 20 && now.Percent <= 20) || (prev.Percent > 10 && now.Percent <= 10)))
                return ActivityKind.LowBattery;
            return null;
        }
    }
}
