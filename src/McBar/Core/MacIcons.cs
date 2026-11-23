// McBar — Creado por Andrés Londero
// 1.4.0 — Íconos de macOSicons.com bajados solos para las apps instaladas.
// McBar lee las apps instaladas (las del Dock primero y después las del Launchpad), busca cada una en la
// API de macOSicons (https://api.macosicons.com/api/v1/search, con la clave GRATIS de la cuenta del
// usuario) y baja el ícono .png a %AppData%\McBar\iconos-macos, que se usa como paquete de íconos.
//  - Solo baja si el nombre coincide (para no poner el ícono de otra app); si no hay, la app sigue con el
//    suyo y se vuelve a probar en 30 días.
//  - Cuida el cupo de la cuenta (gratis: 50 búsquedas por mes): cuenta cuántas usó en el mes y para.
//  - Las apps nuevas se buscan solas (2 minutos después de abrir McBar y cada 6 horas) si está activado.
//  - Los íconos son de sus autores: se guarda el crédito de cada uno en creditos.txt (lo piden los términos
//    de macOSicons) y no se incluyen en McBar: cada usuario los baja con su propia clave.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using McBar.Native;

namespace McBar.Core
{
    public sealed class MacIconHit
    {
        public string AppName { get; init; } = "";
        public string PngUrl { get; init; } = "";
        public string Credit { get; init; } = "";
        public string CreditUrl { get; init; } = "";
        public long Downloads { get; init; }
    }

    public sealed class MacIconEntry
    {
        public string App { get; set; } = "";
        public string File { get; set; } = "";
        public string Credit { get; set; } = "";
        public string CreditUrl { get; set; } = "";
        public DateTime Date { get; set; }
        public bool NotFound { get; set; }
    }

    public static class MacIcons
    {
        private const string SearchUrl = "https://api.macosicons.com/api/v1/search";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = true };

        public static string Folder => Path.Combine(SettingsStore.Folder, "iconos-macos");
        private static string IndexPath => Path.Combine(Folder, "indice.json");
        public static string CreditsPath => Path.Combine(Folder, "creditos.txt");

        public static bool Busy { get; private set; }
        /// <summary>Avisos de progreso (en el hilo que llamó a RunAsync).</summary>
        public static event Action<string>? Progress;

        // ================== Clave ==================

        public static string Key => Dpapi.Unprotect(SettingsStore.Current.MacIconsKey);
        public static bool HasKey => Key.Length > 0;

        public static void SetKey(string key)
        {
            SettingsStore.Current.MacIconsKey = Dpapi.Protect((key ?? "").Trim());
            SettingsStore.Save();
        }

        // ================== Cupo del mes ==================

        /// <summary>Búsquedas que quedan este mes (el contador vuelve a 0 al cambiar de mes). Pura.</summary>
        public static int Remaining(string? month, int used, int limit, DateTime now) =>
            Math.Max(0, limit - (month == now.ToString("yyyy-MM", CultureInfo.InvariantCulture) ? used : 0));

        public static int RemainingNow()
        {
            var s = SettingsStore.Current;
            return Remaining(s.MacIconsMonth, s.MacIconsUsed, s.MacIconsMonthlyLimit, DateTime.Now);
        }

        private static void CountOne()
        {
            var s = SettingsStore.Current;
            var month = DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (s.MacIconsMonth != month) { s.MacIconsMonth = month; s.MacIconsUsed = 0; }
            s.MacIconsUsed++;
        }

        // ================== Puras (con pruebas) ==================

        /// <summary>Apps que no vale la pena buscar (desinstaladores, ayudas, léame…). Pura.</summary>
        public static bool ShouldSkip(string name)
        {
            var n = name.ToLowerInvariant();
            string[] words = { "uninstall", "desinstal", "readme", "léame", "leame", "help", "ayuda", "manual", "documentation",
                               "documentación", "release notes", "license", "licencia", "website", "sitio web", "debug", "safe mode" };
            return n.Length < 2 || words.Any(w => n.Contains(w));
        }

        /// <summary>
        /// Elige el ícono para esa app entre los resultados: primero los que tienen el mismo nombre (el más
        /// bajado), después los que empiezan igual. Si ninguno se parece, null (mejor el ícono de siempre que
        /// el de otra app). Pura.
        /// </summary>
        public static MacIconHit? Pick(IEnumerable<MacIconHit> hits, string appName)
        {
            var want = IconPack.Normalize(appName);
            if (want.Length < 2) return null;
            var list = hits.Where(h => !string.IsNullOrEmpty(h.PngUrl)).ToList();
            var exact = list.Where(h => IconPack.Normalize(h.AppName) == want).OrderByDescending(h => h.Downloads).FirstOrDefault();
            if (exact != null) return exact;
            return list.Where(h =>
                {
                    var n = IconPack.Normalize(h.AppName);
                    return n.Length >= 3 && (want.StartsWith(n) || n.StartsWith(want));
                })
                .OrderByDescending(h => h.Downloads).FirstOrDefault();
        }

        /// <summary>Nombre de archivo para el ícono (sin caracteres prohibidos). Pura.</summary>
        public static string SafeFileName(string appName)
        {
            var bad = Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }).ToHashSet();
            var s = new string(appName.Select(c => bad.Contains(c) ? ' ' : c).ToArray()).Trim();
            if (s.Length > 80) s = s.Substring(0, 80).Trim();
            return s.Length == 0 ? "app" : s;
        }

        /// <summary>¿Hay que buscar esta app? (nunca buscada, o "no encontrada" hace más de 30 días). Pura.</summary>
        public static bool NeedsLookup(MacIconEntry? e, DateTime now, string folder) =>
            e == null || (e.NotFound && (now - e.Date).TotalDays > 30) || (!e.NotFound && !System.IO.File.Exists(Path.Combine(folder, e.File)));

        /// <summary>Lee la respuesta de la API (campo "hits"). Pura.</summary>
        public static List<MacIconHit> ParseHits(string json)
        {
            var list = new List<MacIconHit>();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array) return list;
            foreach (var h in hits.EnumerateArray())
            {
                string Str(string n) => h.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                long downloads = h.TryGetProperty("downloads", out var d) && d.ValueKind == JsonValueKind.Number && d.TryGetInt64(out var dl) ? dl : 0;
                list.Add(new MacIconHit
                {
                    AppName = Str("appName"),
                    PngUrl = Str("lowResPngUrl"),
                    Credit = Str("credit").Length > 0 ? Str("credit") : Str("uploadedBy"),
                    CreditUrl = Str("creditUrl"),
                    Downloads = downloads
                });
            }
            return list;
        }

        // ================== Índice ==================

        private static Dictionary<string, MacIconEntry> LoadIndex()
        {
            try
            {
                if (System.IO.File.Exists(IndexPath))
                    return JsonSerializer.Deserialize<Dictionary<string, MacIconEntry>>(System.IO.File.ReadAllText(IndexPath)) ?? new Dictionary<string, MacIconEntry>();
            }
            catch (Exception ex) { Log.Write("macOSicons (índice): " + ex.Message); }
            return new Dictionary<string, MacIconEntry>();
        }

        private static void SaveIndex(Dictionary<string, MacIconEntry> idx)
        {
            Directory.CreateDirectory(Folder);
            System.IO.File.WriteAllText(IndexPath, JsonSerializer.Serialize(idx, Json));
            var sb = new StringBuilder();
            sb.AppendLine("Íconos bajados de macOSicons.com (https://macosicons.com).");
            sb.AppendLine("Cada ícono es de su autor y sigue su licencia: no se pueden vender ni redistribuir como propios.");
            sb.AppendLine();
            foreach (var e in idx.Values.Where(v => !v.NotFound).OrderBy(v => v.App))
                sb.AppendLine($"{e.App}: {(e.Credit.Length > 0 ? e.Credit : "autor sin nombre")}{(e.CreditUrl.Length > 0 ? " — " + e.CreditUrl : "")}");
            System.IO.File.WriteAllText(CreditsPath, sb.ToString());
        }

        public static int DownloadedCount() => LoadIndex().Values.Count(v => !v.NotFound);

        // ================== Bajar ==================

        /// <summary>
        /// Busca y baja los íconos que falten. Devuelve un resumen para mostrar. <paramref name="manual"/> =
        /// lo pidió el usuario (si no, es la búsqueda automática y no avisa nada si no hay novedades).
        /// </summary>
        public static async Task<string> RunAsync(bool manual)
        {
            if (Busy) return "Ya se están bajando íconos…";
            var key = Key;
            if (key.Length == 0) return "Falta la clave de macOSicons.";
            Busy = true;
            int got = 0, notFound = 0, searched = 0;
            string? stop = null;
            try
            {
                var idx = LoadIndex();
                var names = new List<string>();
                foreach (var p in SettingsStore.Current.Pinned) names.Add(p.Name);
                var catalog = AppCatalog.Cached ?? await AppCatalog.LoadAsync();
                foreach (var a in catalog.OrderBy(a => a.Name)) names.Add(a.Name);
                var todo = names.Where(n => !ShouldSkip(n))
                                .GroupBy(IconPack.Normalize).Select(g => g.First())
                                .Where(n => NeedsLookup(idx.TryGetValue(IconPack.Normalize(n), out var e) ? e : null, DateTime.Now, Folder))
                                .ToList();
                if (todo.Count == 0) return manual ? "Todas las apps ya tienen su ícono (o no hay en macOSicons)." : "";

                foreach (var app in todo)
                {
                    if (RemainingNow() <= 0) { stop = "Se terminó el cupo de búsquedas de este mes."; break; }
                    Progress?.Invoke($"Buscando {app}… ({searched + 1} de {todo.Count})");
                    List<MacIconHit> hits;
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Post, SearchUrl)
                        {
                            Content = new StringContent(JsonSerializer.Serialize(new { query = app }), Encoding.UTF8, "application/json")
                        };
                        req.Headers.Add("x-api-key", key);
                        using var res = await Http.SendAsync(req);
                        CountOne();
                        searched++;
                        if (res.StatusCode == HttpStatusCode.Unauthorized || res.StatusCode == HttpStatusCode.Forbidden)
                        { stop = "macOSicons no aceptó la clave. Revisala en macosicons.com."; break; }
                        if ((int)res.StatusCode == 429) { stop = "macOSicons dice que se terminó el cupo de búsquedas."; break; }
                        var body = await res.Content.ReadAsStringAsync();
                        if (!res.IsSuccessStatusCode || !body.TrimStart().StartsWith("{"))
                        { stop = $"macOSicons no respondió bien ({(int)res.StatusCode}). Probá más tarde."; break; }
                        hits = ParseHits(body);
                    }
                    catch (Exception ex) { stop = "No se pudo conectar con macOSicons: " + ex.Message; break; }

                    var pick = Pick(hits, app);
                    var k = IconPack.Normalize(app);
                    if (pick == null)
                    {
                        idx[k] = new MacIconEntry { App = app, Date = DateTime.Now, NotFound = true };
                        notFound++;
                        continue;
                    }
                    try
                    {
                        var bytes = await Http.GetByteArrayAsync(pick.PngUrl);
                        Directory.CreateDirectory(Folder);
                        var file = SafeFileName(app) + ".png";
                        await System.IO.File.WriteAllBytesAsync(Path.Combine(Folder, file), bytes);
                        idx[k] = new MacIconEntry { App = app, File = file, Credit = pick.Credit, CreditUrl = pick.CreditUrl, Date = DateTime.Now };
                        got++;
                    }
                    catch (Exception ex) { Log.Write($"macOSicons ({app}): " + ex.Message); }
                    await Task.Delay(300); // sin apuro: no cargar la API
                }
                SaveIndex(idx);
            }
            catch (Exception ex) { stop = "Error: " + ex.Message; Log.Write("macOSicons: " + ex); }
            finally
            {
                Busy = false;
                SettingsStore.Save();
            }

            var msg = $"Listo: {got} ícono(s) nuevo(s)" + (notFound > 0 ? $", {notFound} app(s) sin ícono en macOSicons" : "") +
                      $". Quedan {RemainingNow()} búsquedas este mes.";
            if (stop != null) msg = (got > 0 ? $"{got} ícono(s) nuevo(s). " : "") + stop;
            Log.Write("macOSicons: " + msg);
            if (got > 0) IconsChanged?.Invoke();
            return msg;
        }

        /// <summary>Se bajaron íconos nuevos (App usa la carpeta como paquete y recarga los íconos).</summary>
        public static event Action? IconsChanged;
    }
}
