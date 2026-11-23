// McBar — Creado por Andrés Londero
// Actividades en vivo para la isla y la barra (1.2.0), como las del iPhone:
//  - Temporizador (cuenta regresiva) y Cronómetro, con aviso y sonido al terminar.
//  - Micrófono / cámara en uso: puntito naranja (micrófono) o verde (cámara) con el nombre de la app.
//    Se lee del registro de privacidad de Windows (ConsentStore), el mismo que usa Windows para su ícono.
//  - Descargas: archivos que se están bajando en la carpeta Descargas (.crdownload de Chrome/Edge,
//    .part de Firefox, etc.) con tamaño y velocidad; al terminar, aviso "Descarga completa".
// Todo se actualiza una vez por segundo y SOLO mientras hay algo activo (si no, una mirada cada 2 s a la
// privacidad y el vigilante de la carpeta Descargas, que no gasta nada).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Microsoft.Win32;

namespace McBar.Core
{
    public sealed class DownloadItem
    {
        public string PartialPath { get; init; } = "";
        public string Name { get; init; } = "";
        public long Bytes { get; set; }
        public double BytesPerSecond { get; set; }
    }

    public static class LiveStatus
    {
        // ---------- Temporizador ----------
        public static DateTime? TimerEnd { get; private set; }
        public static TimeSpan TimerPaused { get; private set; }
        public static TimeSpan TimerTotal { get; private set; }
        public static bool TimerRunning => TimerEnd != null;
        public static bool TimerActive => TimerEnd != null || TimerPaused > TimeSpan.Zero;

        // ---------- Cronómetro ----------
        private static DateTime? _swStart;
        private static TimeSpan _swAccum;
        public static bool StopwatchRunning => _swStart != null;
        public static bool StopwatchActive => _swStart != null || _swAccum > TimeSpan.Zero;
        public static TimeSpan StopwatchElapsed => _swAccum + (_swStart != null ? DateTime.Now - _swStart.Value : TimeSpan.Zero);

        // ---------- Privacidad y descargas ----------
        public static List<string> MicApps { get; private set; } = new List<string>();
        public static List<string> CamApps { get; private set; } = new List<string>();
        public static List<DownloadItem> Downloads { get; } = new List<DownloadItem>();

        public static event Action? Changed;
        private static DispatcherTimer? _tick;
        private static FileSystemWatcher? _watcher;
        private static int _privacyCounter;

        public static bool HasLive => TimerActive || StopwatchActive || Downloads.Count > 0;

        public static void Start()
        {
            if (_tick != null) return;
            _tick = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
            _tick.Tick += (_, _) => Tick();
            _tick.Start();
            StartDownloadsWatcher();
            SettingsStore.Changed += OnSettings;
            GameMode.Changed += OnGameMode;
        }

        private static void OnGameMode() => Changed?.Invoke();

        /// <summary>Prender/apagar las descargas desde Preferencias sin reiniciar.</summary>
        private static void OnSettings()
        {
            bool want = SettingsStore.Current.LiveDownloads;
            if (want && _watcher == null) StartDownloadsWatcher();
            else if (!want && _watcher != null)
            {
                _watcher.Dispose();
                _watcher = null;
                Downloads.Clear();
            }
            Changed?.Invoke();
        }

        public static void Stop()
        {
            _tick?.Stop();
            _tick = null;
            SettingsStore.Changed -= OnSettings;
            GameMode.Changed -= OnGameMode;
            _watcher?.Dispose();
            _watcher = null;
        }

        // ================== Temporizador / cronómetro ==================

        public static void StartTimer(TimeSpan length)
        {
            if (length <= TimeSpan.Zero) return;
            TimerTotal = length;
            TimerPaused = TimeSpan.Zero;
            TimerEnd = DateTime.Now + length;
            Changed?.Invoke();
        }

        public static void PauseTimer()
        {
            if (TimerEnd == null) return;
            TimerPaused = TimerEnd.Value - DateTime.Now;
            TimerEnd = null;
            Changed?.Invoke();
        }

        public static void ResumeTimer()
        {
            if (TimerEnd != null || TimerPaused <= TimeSpan.Zero) return;
            TimerEnd = DateTime.Now + TimerPaused;
            TimerPaused = TimeSpan.Zero;
            Changed?.Invoke();
        }

        public static void CancelTimer()
        {
            TimerEnd = null;
            TimerPaused = TimeSpan.Zero;
            Changed?.Invoke();
        }

        public static TimeSpan TimerRemaining =>
            TimerEnd != null ? (TimerEnd.Value - DateTime.Now < TimeSpan.Zero ? TimeSpan.Zero : TimerEnd.Value - DateTime.Now) : TimerPaused;

        public static void StartStopwatch() { if (_swStart == null) { _swStart = DateTime.Now; Changed?.Invoke(); } }
        public static void PauseStopwatch() { if (_swStart != null) { _swAccum += DateTime.Now - _swStart.Value; _swStart = null; Changed?.Invoke(); } }
        public static void ResetStopwatch() { _swStart = null; _swAccum = TimeSpan.Zero; Changed?.Invoke(); }

        /// <summary>"4:05", "1:02:03" (pura, se prueba). Con décimas para el cronómetro.</summary>
        public static string Format(TimeSpan t, bool tenths = false)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            string s = t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
            return tenths ? s + "," + (t.Milliseconds / 100) : s;
        }

        /// <summary>Lo que muestra la isla compacta (o null si no hay nada en vivo). Prioridad: temporizador, cronómetro, descarga.</summary>
        public static (string glyph, string text)? Summary()
        {
            if (TimerActive) return ("", Format(TimerRemaining));
            if (StopwatchActive) return ("", Format(StopwatchElapsed));
            if (Downloads.Count > 0)
            {
                var d = Downloads[0];
                string extra = Downloads.Count > 1 ? $" +{Downloads.Count - 1}" : "";
                return ("", SizeText(d.Bytes) + extra);
            }
            if (GameMode.On) return ("\uE7FC", "Juego"); // 1.3.0
            return null;
        }

        /// <summary>Texto del globito de los puntitos: "Micrófono: Zoom · Cámara: Teams". Pura, se prueba.</summary>
        public static string PrivacyText(IList<string> mic, IList<string> cam)
        {
            var parts = new List<string>();
            if (mic.Count > 0) parts.Add("Micrófono: " + string.Join(", ", mic));
            if (cam.Count > 0) parts.Add("Cámara: " + string.Join(", ", cam));
            return string.Join(" · ", parts);
        }

        public static string PrivacyText() => PrivacyText(MicApps, CamApps);

        /// <summary>Buscador: "temporizador 10", "timer 5 min", "temporizador 1:30" → duración. Pura, se prueba.</summary>
        public static TimeSpan? ParseTimerCommand(string q)
        {
            q = (q ?? "").Trim().ToLowerInvariant();
            string? rest = null;
            foreach (var w in new[] { "temporizador", "timer", "alarma en" })
                if (q.StartsWith(w + " ")) { rest = q.Substring(w.Length).Trim(); break; }
            if (rest == null) return null;
            bool hours = false;
            foreach (var suf in new[] { " minutos", " minuto", " min", "min", "m" })
                if (rest.EndsWith(suf)) { rest = rest.Substring(0, rest.Length - suf.Length).Trim(); break; }
            foreach (var suf in new[] { " horas", " hora", " h", "h" })
                if (rest.EndsWith(suf)) { rest = rest.Substring(0, rest.Length - suf.Length).Trim(); hours = true; break; }
            rest = rest.Replace(',', '.');
            if (rest.Contains(':'))
            {
                var p = rest.Split(':');
                if (p.Length == 2 && int.TryParse(p[0], out int mm) && int.TryParse(p[1], out int ss) && mm >= 0 && ss >= 0 && ss < 60 && (mm > 0 || ss > 0))
                    return new TimeSpan(0, mm, ss);
                return null;
            }
            if (double.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && n > 0)
            {
                double minutes = hours ? n * 60 : n;
                if (minutes > 24 * 60) return null;
                return TimeSpan.FromSeconds(Math.Round(minutes * 60));
            }
            return null;
        }

        public static string SizeText(long bytes)
        {
            var es = new CultureInfo("es-AR");
            if (bytes < 1048576) return (bytes / 1024.0).ToString("0", es) + " KB";
            if (bytes < 1073741824) return (bytes / 1048576.0).ToString("0.#", es) + " MB";
            return (bytes / 1073741824.0).ToString("0.##", es) + " GB";
        }

        // ================== Cada segundo ==================

        private static void Tick()
        {
            bool changed = false;
            if (TimerEnd != null && DateTime.Now >= TimerEnd.Value)
            {
                TimerEnd = null;
                TimerPaused = TimeSpan.Zero;
                NotificationCenter.Add(new NotificationInfo { App = "Temporizador", Title = "¡Terminó el temporizador!", Body = Format(TimerTotal) });
                Chime(3);
                changed = true;
            }
            if (TimerActive || StopwatchActive) changed = true;
            if (Downloads.Count > 0) { UpdateDownloads(); changed = true; }
            if (!SettingsStore.Current.PrivacyDots && (MicApps.Count > 0 || CamApps.Count > 0))
            {
                MicApps = new List<string>(); CamApps = new List<string>(); changed = true;
            }
            else if (++_privacyCounter % 2 == 0 && SettingsStore.Current.PrivacyDots)
            {
                var mic = InUse("microphone");
                var cam = InUse("webcam");
                if (!mic.SequenceEqual(MicApps) || !cam.SequenceEqual(CamApps)) { MicApps = mic; CamApps = cam; changed = true; }
            }
            if (changed) Changed?.Invoke();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool MessageBeep(uint type);

        /// <summary>Sonido de aviso de Windows, repetido unas veces (cada 0,8 s).</summary>
        private static void Chime(int times)
        {
            try { MessageBeep(0x30); } catch { }
            if (times <= 1) return;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            int left = times - 1;
            t.Tick += (_, _) => { try { MessageBeep(0x30); } catch { } if (--left <= 0) t.Stop(); };
            t.Start();
        }

        // ================== Micrófono / cámara ==================

        private const string Consent = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\";

        /// <summary>Apps que están usando el micrófono o la cámara ahora (LastUsedTimeStop = 0).</summary>
        private static List<string> InUse(string capability)
        {
            var list = new List<string>();
            try
            {
                using var root = Registry.CurrentUser.OpenSubKey(Consent + capability);
                if (root == null) return list;
                foreach (var name in root.GetSubKeyNames())
                {
                    using var k = root.OpenSubKey(name);
                    if (k == null) continue;
                    if (name.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var app in k.GetSubKeyNames())
                        {
                            using var a = k.OpenSubKey(app);
                            if (a != null && IsActive(a)) list.Add(AppName(app));
                        }
                    }
                    else if (IsActive(k)) list.Add(AppName(name));
                }
            }
            catch { }
            return list.Distinct().OrderBy(x => x).ToList();
        }

        private static bool IsActive(RegistryKey k) =>
            k.GetValue("LastUsedTimeStop") is long stop && stop == 0 && k.GetValue("LastUsedTimeStart") is long start && start > 0;

        /// <summary>"C:#Program Files#Zoom#bin#Zoom.exe" → "Zoom"; "Microsoft.WindowsCamera_8wekyb3d8bbwe" → "WindowsCamera". Pura, se prueba.</summary>
        public static string AppName(string key)
        {
            var k = key;
            int sep = k.LastIndexOfAny(new[] { '#', '\\', '/' });
            if (k.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                k = sep >= 0 ? k.Substring(sep + 1) : k;
                return k.Substring(0, k.Length - 4);
            }
            int u = k.IndexOf('_');
            if (u > 0) k = k.Substring(0, u);
            int dot = k.LastIndexOf('.');
            return dot >= 0 && dot < k.Length - 1 ? k.Substring(dot + 1) : k;
        }

        // ================== Descargas ==================

        private static readonly string[] PartialExt = { ".crdownload", ".part", ".partial", ".download", ".opdownload" };

        /// <summary>¿Es un archivo a medio bajar? Devuelve el nombre final ("foto.jpg.crdownload" → "foto.jpg"). Pura.</summary>
        public static string? FinalName(string fileName)
        {
            foreach (var ext in PartialExt)
                if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    var n = fileName.Substring(0, fileName.Length - ext.Length);
                    // Chrome usa "Unconfirmed 12345.crdownload" al principio
                    if (n.StartsWith("Unconfirmed ", StringComparison.OrdinalIgnoreCase)) return "archivo";
                    return n.Length == 0 ? null : n;
                }
            return null;
        }

        private static string DownloadsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        private static void StartDownloadsWatcher()
        {
            if (!SettingsStore.Current.LiveDownloads || !Directory.Exists(DownloadsFolder)) return;
            try
            {
                _watcher = new FileSystemWatcher(DownloadsFolder) { IncludeSubdirectories = false, EnableRaisingEvents = true };
                _watcher.Created += (_, e) => App.Current?.Dispatcher.BeginInvoke(new Action(() => OnCreated(e.FullPath)));
                _watcher.Renamed += (_, e) => App.Current?.Dispatcher.BeginInvoke(new Action(() => OnRenamed(e.OldFullPath, e.FullPath)));
                _watcher.Deleted += (_, e) => App.Current?.Dispatcher.BeginInvoke(new Action(() => { Downloads.RemoveAll(d => d.PartialPath == e.FullPath); Changed?.Invoke(); }));
            }
            catch (Exception ex) { Log.Write("Descargas: " + ex.Message); }
        }

        private static void OnCreated(string path)
        {
            var name = FinalName(Path.GetFileName(path));
            if (name == null || Downloads.Any(d => d.PartialPath == path)) return;
            Downloads.Add(new DownloadItem { PartialPath = path, Name = name });
            Changed?.Invoke();
        }

        private static void OnRenamed(string oldPath, string newPath)
        {
            var item = Downloads.FirstOrDefault(d => d.PartialPath == oldPath);
            if (FinalName(Path.GetFileName(newPath)) != null)
            {
                // Chrome pasa de "Unconfirmed 123.crdownload" a "foto.jpg.crdownload"
                if (item != null) Downloads.Remove(item);
                OnCreated(newPath);
                return;
            }
            if (item == null) return;
            Downloads.Remove(item);
            var final = newPath;
            NotificationCenter.Add(new NotificationInfo { App = "Descargas", Title = "Descarga completa", Body = Path.GetFileName(final) });
            Changed?.Invoke();
        }

        private static void UpdateDownloads()
        {
            foreach (var d in Downloads.ToList())
            {
                try
                {
                    var fi = new FileInfo(d.PartialPath);
                    if (!fi.Exists) { Downloads.Remove(d); continue; }
                    long now = fi.Length;
                    d.BytesPerSecond = d.BytesPerSecond * 0.6 + Math.Max(0, now - d.Bytes) * 0.4;
                    d.Bytes = now;
                }
                catch { }
            }
        }
    }
}
