// McBar — Creado por Andrés Londero
// Lo que se está reproduciendo (Spotify, YouTube en el navegador, el reproductor de Windows, etc.),
// usando los "controles multimedia" de Windows (los mismos del cartelito de volumen de Windows).
// Todo por eventos: Windows avisa cuando cambia el tema o se pausa.
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace McBar.Core
{
    public sealed class MediaInfo
    {
        public string Title { get; init; } = "";
        public string Artist { get; init; } = "";
        public ImageSource? Art { get; init; }
        public bool Playing { get; init; }
        public string AppId { get; init; } = "";
        public TimeSpan Position { get; init; }
        public TimeSpan Duration { get; init; }
        public DateTimeOffset PositionUpdated { get; init; }

        /// <summary>Posición estimada ahora (las apps solo la informan de vez en cuando).</summary>
        public TimeSpan LivePosition
        {
            get
            {
                if (!Playing || PositionUpdated == default) return Position;
                var p = Position + (DateTimeOffset.Now - PositionUpdated);
                return Duration > TimeSpan.Zero && p > Duration ? Duration : p;
            }
        }
    }

    internal sealed class MediaWatcher : IDisposable
    {
        private readonly Dispatcher _ui;
        private GlobalSystemMediaTransportControlsSessionManager? _mgr;
        private GlobalSystemMediaTransportControlsSession? _session;
        private string _lastKey = "";
        private bool _disposed;

        public MediaInfo? Current { get; private set; }

        /// <summary>Cambió algo (reproducción, pausa, tema). Se dispara en el hilo de la interfaz.</summary>
        public event Action? Changed;
        /// <summary>Empezó otro tema (para mostrarlo un momento en la isla).</summary>
        public event Action<MediaInfo>? TrackChanged;

        public MediaWatcher() => _ui = Dispatcher.CurrentDispatcher;

        public async void Start()
        {
            try
            {
                _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                _mgr.CurrentSessionChanged += (_, _) => _ui.BeginInvoke(new Action(Attach));
                _ = _ui.BeginInvoke(new Action(Attach)); // descartado a propósito (evita el aviso CS4014)
            }
            catch (Exception ex) { Log.Write("Multimedia: " + ex.Message); }
        }

        private void Attach()
        {
            if (_disposed || _mgr == null) return;
            GlobalSystemMediaTransportControlsSession? s = null;
            try { s = _mgr.GetCurrentSession(); } catch { }
            if (!ReferenceEquals(s, _session))
            {
                if (_session != null)
                {
                    _session.MediaPropertiesChanged -= OnProps;
                    _session.PlaybackInfoChanged -= OnPlayback;
                    _session.TimelinePropertiesChanged -= OnTimeline;
                }
                _session = s;
                if (_session != null)
                {
                    _session.MediaPropertiesChanged += OnProps;
                    _session.PlaybackInfoChanged += OnPlayback;
                    _session.TimelinePropertiesChanged += OnTimeline;
                }
            }
            _ = RefreshAsync();
        }

        private void OnProps(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs a) => _ui.BeginInvoke(new Action(() => _ = RefreshAsync()));
        private void OnPlayback(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs a) => _ui.BeginInvoke(new Action(() => _ = RefreshAsync()));
        private void OnTimeline(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs a) => _ui.BeginInvoke(new Action(() => _ = RefreshAsync()));

        private async Task RefreshAsync()
        {
            try
            {
                var session = _session;
                if (session == null)
                {
                    if (Current != null) { Current = null; Changed?.Invoke(); }
                    return;
                }
                var props = await session.TryGetMediaPropertiesAsync();
                var pb = session.GetPlaybackInfo();
                var tl = session.GetTimelineProperties();
                string key = (props?.Title ?? "") + "|" + (props?.Artist ?? "");
                ImageSource? art = Current?.Art;
                bool newTrack = key != _lastKey;
                if (newTrack && props?.Thumbnail != null) art = await LoadThumbAsync(props.Thumbnail);
                else if (newTrack) art = null;

                Current = new MediaInfo
                {
                    Title = props?.Title ?? "",
                    Artist = props?.Artist ?? "",
                    Art = art,
                    Playing = pb?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    AppId = session.SourceAppUserModelId ?? "",
                    Position = tl?.Position ?? TimeSpan.Zero,
                    Duration = tl != null ? tl.EndTime - tl.StartTime : TimeSpan.Zero,
                    PositionUpdated = tl?.LastUpdatedTime ?? default
                };
                if (string.IsNullOrWhiteSpace(Current.Title)) Current = null;
                Changed?.Invoke();
                if (newTrack)
                {
                    _lastKey = key;
                    if (Current != null && Current.Playing) TrackChanged?.Invoke(Current);
                }
            }
            catch (Exception ex) { Log.Write("Multimedia: " + ex.Message); }
        }

        private static async Task<ImageSource?> LoadThumbAsync(IRandomAccessStreamReference reference)
        {
            try
            {
                using var ras = await reference.OpenReadAsync();
                using var st = ras.AsStreamForRead();
                var ms = new MemoryStream();
                await st.CopyToAsync(ms);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 160; // chica: ahorra memoria
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        public async void PlayPause() { try { if (_session != null) await _session.TryTogglePlayPauseAsync(); } catch { } }
        public async void Next() { try { if (_session != null) await _session.TrySkipNextAsync(); } catch { } }
        public async void Previous() { try { if (_session != null) await _session.TrySkipPreviousAsync(); } catch { } }

        public void Dispose()
        {
            _disposed = true;
            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnProps;
                _session.PlaybackInfoChanged -= OnPlayback;
                _session.TimelinePropertiesChanged -= OnTimeline;
            }
            _session = null;
            _mgr = null;
        }
    }
}
