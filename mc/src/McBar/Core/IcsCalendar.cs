// McBar — Creado por Andrés Londero
// Calendario de Google / Outlook (1.1.0) sin iniciar sesión: con la "dirección secreta en formato iCal"
// (Google Calendar → Configuración del calendario → Integrar el calendario) o el enlace ICS de "Publicar
// calendario" de Outlook. McBar baja el archivo .ics cada 15 minutos y muestra los próximos eventos en
// los widgets, y avisa 10 minutos antes (notificación + isla).
// El lector de iCal es propio (puro, con pruebas): eventos de un día o con hora, zona horaria (TZID o Z) y
// repeticiones simples (diaria, semanal con días, mensual, anual; con INTERVAL, COUNT y UNTIL).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace McBar.Core
{
    public sealed class CalEvent
    {
        public string Title { get; init; } = "";
        public string Location { get; init; } = "";
        public DateTime Start { get; init; }
        public DateTime End { get; init; }
        public bool AllDay { get; init; }
        public string Calendar { get; init; } = "";
    }

    public static class IcsCalendar
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static List<CalEvent> _events = new List<CalEvent>();
        private static DateTime _loadedAt = DateTime.MinValue;
        private static readonly HashSet<string> Notified = new HashSet<string>();
        public static event Action? Changed;

        public static IReadOnlyList<CalEvent> Events => _events;

        /// <summary>Próximos eventos (desde ahora) hasta <paramref name="days"/> días.</summary>
        public static List<CalEvent> Upcoming(int days = 7, int max = 8)
        {
            var now = DateTime.Now;
            return _events.Where(e => e.End > now && e.Start < now.AddDays(days)).OrderBy(e => e.Start).Take(max).ToList();
        }

        public static bool HasEventsOn(DateTime day) => _events.Any(e => e.Start.Date <= day.Date && e.End > day.Date);

        /// <summary>Baja todos los calendarios si pasaron 15 minutos (o si se fuerza).</summary>
        public static async Task RefreshAsync(bool force = false)
        {
            var urls = SettingsStore.Current.CalendarUrls;
            if (urls.Count == 0) { if (_events.Count > 0) { _events = new List<CalEvent>(); Changed?.Invoke(); } return; }
            if (!force && (DateTime.Now - _loadedAt).TotalMinutes < 15) return;
            _loadedAt = DateTime.Now;
            var all = new List<CalEvent>();
            foreach (var raw in urls)
            {
                try
                {
                    var url = raw.Trim().Replace("webcal://", "https://", StringComparison.OrdinalIgnoreCase);
                    var text = await Http.GetStringAsync(url);
                    all.AddRange(Parse(text, DateTime.Now.AddDays(-1), DateTime.Now.AddDays(45)));
                }
                catch (Exception ex) { Log.Write("Calendario: " + ex.Message); }
            }
            _events = all.OrderBy(e => e.Start).ToList();
            Changed?.Invoke();
        }

        /// <summary>Eventos que empiezan dentro de <paramref name="minutes"/> y todavía no se avisaron.</summary>
        public static List<CalEvent> DueForReminder(int minutes)
        {
            var now = DateTime.Now;
            var list = new List<CalEvent>();
            foreach (var e in _events)
            {
                if (e.AllDay || e.Start < now || e.Start > now.AddMinutes(minutes)) continue;
                var key = e.Title + "|" + e.Start.ToString("o");
                if (Notified.Add(key)) list.Add(e);
            }
            return list;
        }

        // ================== Lector iCal (puro) ==================

        /// <summary>Lee un .ics y devuelve los eventos (con sus repeticiones) entre <paramref name="from"/> y <paramref name="to"/>.</summary>
        public static List<CalEvent> Parse(string ics, DateTime from, DateTime to)
        {
            var result = new List<CalEvent>();
            var lines = Unfold(ics);
            string calName = "";
            Dictionary<string, (string value, Dictionary<string, string> prms)>? ev = null;
            var exdates = new List<string>();
            foreach (var line in lines)
            {
                if (line == "BEGIN:VEVENT") { ev = new Dictionary<string, (string, Dictionary<string, string>)>(); exdates.Clear(); continue; }
                if (line == "END:VEVENT" && ev != null)
                {
                    try { Expand(ev, exdates, calName, from, to, result); } catch { }
                    ev = null;
                    continue;
                }
                var (name, prms, value) = SplitLine(line);
                if (ev == null) { if (name == "X-WR-CALNAME") calName = Unescape(value); continue; }
                if (name == "EXDATE") exdates.AddRange(value.Split(','));
                else ev[name] = (value, prms);
            }
            return result.OrderBy(e => e.Start).ToList();
        }

        private static List<string> Unfold(string ics)
        {
            var list = new List<string>();
            foreach (var raw in ics.Replace("\r\n", "\n").Split('\n'))
            {
                if ((raw.StartsWith(" ") || raw.StartsWith("\t")) && list.Count > 0) list[^1] += raw.Substring(1);
                else if (raw.Length > 0) list.Add(raw);
            }
            return list;
        }

        private static (string name, Dictionary<string, string> prms, string value) SplitLine(string line)
        {
            int colon = line.IndexOf(':');
            if (colon < 0) return (line, new Dictionary<string, string>(), "");
            var head = line.Substring(0, colon).Split(';');
            var prms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < head.Length; i++)
            {
                int eq = head[i].IndexOf('=');
                if (eq > 0) prms[head[i].Substring(0, eq)] = head[i].Substring(eq + 1).Trim('"');
            }
            return (head[0].ToUpperInvariant(), prms, line.Substring(colon + 1));
        }

        private static string Unescape(string s) => s.Replace("\\n", " ").Replace("\\N", " ").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");

        /// <summary>Fecha iCal → hora local. "20261122" (día entero), "20261122T150000Z" (UTC) o con TZID.</summary>
        public static (DateTime local, bool allDay) ParseDate(string value, string? tzid)
        {
            value = value.Trim();
            if (value.Length == 8)
                return (DateTime.ParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture), true);
            bool utc = value.EndsWith("Z");
            var dt = DateTime.ParseExact(value.TrimEnd('Z'), "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
            if (utc) return (DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime(), false);
            if (!string.IsNullOrEmpty(tzid) && TimeZoneInfo.TryFindSystemTimeZoneById(tzid, out var tz))
                return (TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), tz, TimeZoneInfo.Local), false);
            return (dt, false); // hora "flotante": se toma como local
        }

        private static void Expand(Dictionary<string, (string value, Dictionary<string, string> prms)> ev, List<string> exdates,
                                   string calName, DateTime from, DateTime to, List<CalEvent> result)
        {
            if (!ev.TryGetValue("DTSTART", out var ds)) return;
            ds.prms.TryGetValue("TZID", out var tz);
            var (start, allDay) = ParseDate(ds.value, tz);
            DateTime end;
            if (ev.TryGetValue("DTEND", out var de)) { de.prms.TryGetValue("TZID", out var tz2); end = ParseDate(de.value, tz2).local; }
            else end = allDay ? start.AddDays(1) : start.AddHours(1);
            var dur = end - start;
            string title = ev.TryGetValue("SUMMARY", out var su) ? Unescape(su.value) : "(sin título)";
            string loc = ev.TryGetValue("LOCATION", out var lo) ? Unescape(lo.value) : "";
            if (ev.TryGetValue("STATUS", out var st) && st.value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) return;
            var skip = new HashSet<DateTime>(exdates.Select(x => { try { return ParseDate(x.Split(':').Last(), tz).local; } catch { return DateTime.MinValue; } }));

            void Add(DateTime s)
            {
                if (skip.Contains(s)) return;
                if (s + dur <= from || s >= to) return;
                result.Add(new CalEvent { Title = title, Location = loc, Start = s, End = s + dur, AllDay = allDay, Calendar = calName });
            }

            if (!ev.TryGetValue("RRULE", out var rr)) { Add(start); return; }
            var rule = rr.value.Split(';').Select(p => p.Split('=')).Where(p => p.Length == 2)
                               .ToDictionary(p => p[0].ToUpperInvariant(), p => p[1], StringComparer.OrdinalIgnoreCase);
            string freq = rule.TryGetValue("FREQ", out var f) ? f.ToUpperInvariant() : "";
            int interval = rule.TryGetValue("INTERVAL", out var iv) && int.TryParse(iv, out var ivn) && ivn > 0 ? ivn : 1;
            int count = rule.TryGetValue("COUNT", out var c) && int.TryParse(c, out var cn) ? cn : int.MaxValue;
            DateTime until = rule.TryGetValue("UNTIL", out var u) ? ParseDate(u, null).local : DateTime.MaxValue;
            if (allDay && until != DateTime.MaxValue) until = until.Date.AddDays(1).AddTicks(-1);
            var byDay = rule.TryGetValue("BYDAY", out var bd)
                ? bd.Split(',').Select(d => DayOf(d.Length >= 2 ? d[^2..] : d)).Where(d => d != null).Select(d => d!.Value).ToHashSet()
                : new HashSet<DayOfWeek>();

            int made = 0;
            if (freq == "WEEKLY" && byDay.Count > 0)
            {
                var weekStart = start.Date.AddDays(-(int)start.DayOfWeek);
                for (int w = 0; made < count && w < 600; w += interval)
                {
                    for (int d = 0; d < 7; d++)
                    {
                        var day = weekStart.AddDays(w * 7 + d);
                        if (!byDay.Contains(day.DayOfWeek)) continue;
                        var s = day + start.TimeOfDay;
                        if (s < start) continue;
                        if (s > until || made >= count) return;
                        made++;
                        Add(s);
                    }
                    if (weekStart.AddDays(w * 7) > to) return;
                }
                return;
            }
            for (int i = 0; made < count && i < 2000; i++)
            {
                DateTime s = freq switch
                {
                    "DAILY" => start.AddDays(i * interval),
                    "WEEKLY" => start.AddDays(7 * i * interval),
                    "MONTHLY" => start.AddMonths(i * interval),
                    "YEARLY" => start.AddYears(i * interval),
                    _ => i == 0 ? start : DateTime.MaxValue
                };
                if (s == DateTime.MaxValue || s > until || s > to) return;
                made++;
                Add(s);
            }
        }

        private static DayOfWeek? DayOf(string code) => code.ToUpperInvariant() switch
        {
            "SU" => DayOfWeek.Sunday, "MO" => DayOfWeek.Monday, "TU" => DayOfWeek.Tuesday, "WE" => DayOfWeek.Wednesday,
            "TH" => DayOfWeek.Thursday, "FR" => DayOfWeek.Friday, "SA" => DayOfWeek.Saturday, _ => null
        };
    }
}
