// McBar — Creado por Andrés Londero
// 1.9.55: avisa cuando hay una versión nueva publicada en GitHub (Releases). No descarga ni instala nada solo:
// avisa con una notificación y el botón de Preferencias abre la página de la versión.
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace McBar.Core
{
    internal static class UpdateCheck
    {
        public const string Repo = "nanditolondero-design/McBar";
        private static HttpClient? _http;
        private static HttpClient Http => _http ??= CreateHttp();
        private static DispatcherTimer? _timer;

        private static HttpClient CreateHttp()
        {
            var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("McBar-update-check");
            h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return h;
        }

        /// <summary>"v1.9.55" o "1.9.55" → Version; null si no se entiende. Pura, se prueba.</summary>
        public static Version? ParseTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var t = tag.Trim().TrimStart('v', 'V');
            return Version.TryParse(t, out var v) ? v : null;
        }

        /// <summary>¿La versión publicada es más nueva que la instalada (comparando 3 números)? Pura, se prueba.</summary>
        public static bool IsNewer(Version? latest, Version current) =>
            latest != null && new Version(latest.Major, latest.Minor, Math.Max(0, latest.Build)) > new Version(current.Major, current.Minor, Math.Max(0, current.Build));

        /// <summary>Revisa a los 40 s de abrir y cada 24 h. Con la edición de la Store no hace nada (la Store actualiza sola).</summary>
        public static void Start()
        {
            if (Edition.IsStore || _timer != null) return;
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(40) };
            _timer.Tick += async (_, _) =>
            {
                _timer!.Interval = TimeSpan.FromHours(24);
                if (!SettingsStore.Current.CheckUpdates || App.SafeMode) return;
                var r = await CheckAsync();
                if (r.newer && r.tag != SettingsStore.Current.LastUpdateNotified)
                {
                    SettingsStore.Current.LastUpdateNotified = r.tag ?? "";
                    SettingsStore.Save();
                    NotificationCenter.Add(new NotificationInfo
                    {
                        App = "McBar",
                        Title = $"Hay una versión nueva de McBar ({r.tag})",
                        Body = "Abrí Preferencias → Diagnóstico → «Buscar actualizaciones» para verla."
                    });
                }
            };
            _timer.Start();
        }

        /// <summary>Consulta la última versión publicada. Nunca lanza.</summary>
        public static async Task<(bool ok, bool newer, string? tag, string? url)> CheckAsync()
        {
            try
            {
                using var resp = await Http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
                if (!resp.IsSuccessStatusCode) return (false, false, null, null);
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                string? tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
                string? url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
                return (true, IsNewer(ParseTag(tag), Installer.CurrentVersion), tag, url);
            }
            catch (Exception ex) { Log.Write("Buscar actualizaciones: " + ex.Message); return (false, false, null, null); }
        }
    }
}
