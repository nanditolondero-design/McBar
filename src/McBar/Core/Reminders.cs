// McBar — Creado por Andrés Londero
// Recordatorios (1.1.0): una lista simple con hora opcional, como los Recordatorios de macOS.
// Se escriben en lenguaje natural: "comprar pan mañana 18:00", "llamar a Juan en 20 min",
// "turno dentista 25/11 9:30", "sacar la basura hoy a las 21". A la hora avisa con una notificación
// (y en la isla). Se guardan en %AppData%\McBar\recordatorios.json.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace McBar.Core
{
    public sealed class Reminder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = "";
        public DateTime? Due { get; set; }
        public bool Done { get; set; }
        public bool Notified { get; set; }
    }

    public static class Reminders
    {
        private static string FilePath => Path.Combine(SettingsStore.Folder, "recordatorios.json");
        private static List<Reminder>? _items;
        public static event Action? Changed;

        public static List<Reminder> Items => _items ??= Load();

        /// <summary>Pendientes primero (por hora), después los hechos.</summary>
        public static List<Reminder> Sorted() =>
            Items.OrderBy(r => r.Done).ThenBy(r => r.Due ?? DateTime.MaxValue).ThenBy(r => r.Text).ToList();

        private static List<Reminder> Load()
        {
            try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<List<Reminder>>(File.ReadAllText(FilePath)) ?? new List<Reminder>(); }
            catch (Exception ex) { Log.Write("Recordatorios: " + ex.Message); }
            return new List<Reminder>();
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { Log.Write("Recordatorios: " + ex.Message); }
            Changed?.Invoke();
        }

        public static Reminder Add(string text)
        {
            var (title, due) = ParseQuick(text, DateTime.Now);
            var r = new Reminder { Text = title, Due = due };
            Items.Add(r);
            Save();
            return r;
        }

        public static void SetDone(Reminder r, bool done) { r.Done = done; Save(); }

        public static void Remove(Reminder r) { Items.Remove(r); Save(); }

        public static void ClearDone() { Items.RemoveAll(r => r.Done); Save(); }

        /// <summary>Los que llegaron a su hora y no se avisaron (los marca como avisados).</summary>
        public static List<Reminder> TakeDue(DateTime now)
        {
            var due = Items.Where(r => !r.Done && !r.Notified && r.Due != null && r.Due <= now).ToList();
            if (due.Count == 0) return due;
            foreach (var r in due) r.Notified = true;
            Save();
            return due;
        }

        /// <summary>"vence hoy 18:00", "mañana 9:30", "lun 25/11 10:00" (pura, se prueba).</summary>
        public static string When(DateTime due, DateTime now)
        {
            var es = new CultureInfo("es-AR");
            string hour = due.TimeOfDay == TimeSpan.Zero ? "" : " " + due.ToString("HH:mm", es);
            if (due.Date == now.Date) return "hoy" + hour;
            if (due.Date == now.Date.AddDays(1)) return "mañana" + hour;
            if (due.Date == now.Date.AddDays(-1)) return "ayer" + hour;
            return due.ToString("ddd dd/MM", es).Replace(".", "") + hour;
        }

        // ================== Lenguaje natural (puro, con pruebas) ==================

        /// <summary>Separa el texto y la hora: "comprar pan mañana 18:00" → ("comprar pan", mañana 18:00).</summary>
        public static (string title, DateTime? due) ParseQuick(string text, DateTime now)
        {
            var t = " " + text.Trim() + " ";
            DateTime? date = null;
            TimeSpan? time = null;

            // "en 20 min", "en 2 horas", "en 3 días"
            var m = Regex.Match(t, @"\s+en\s+(\d+)\s*(min(?:utos?)?|m|h(?:oras?|s)?|d(?:[ií]as?)?)\b", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                int n = int.Parse(m.Groups[1].Value);
                var u = m.Groups[2].Value.ToLowerInvariant();
                var due = u.StartsWith("d") ? now.AddDays(n) : u.StartsWith("h") ? now.AddHours(n) : now.AddMinutes(n);
                return (Clean(t.Remove(m.Index, m.Length)), new DateTime(due.Year, due.Month, due.Day, due.Hour, due.Minute, 0));
            }

            // fecha: hoy / mañana / pasado mañana / dd/mm(/aaaa) / día de la semana
            m = Regex.Match(t, @"\s+(pasado\s+mañana|pasado\s+manana|mañana|manana|hoy)\b", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var w = m.Groups[1].Value.ToLowerInvariant();
                date = w.StartsWith("pasado") ? now.Date.AddDays(2) : w == "hoy" ? now.Date : now.Date.AddDays(1);
                t = t.Remove(m.Index, m.Length) + " ";
            }
            else
            {
                m = Regex.Match(t, @"\s+(?:el\s+)?(\d{1,2})/(\d{1,2})(?:/(\d{2,4}))?\b");
                if (m.Success)
                {
                    int d = int.Parse(m.Groups[1].Value), mo = int.Parse(m.Groups[2].Value);
                    int y = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : now.Year;
                    if (y < 100) y += 2000;
                    try
                    {
                        var dt = new DateTime(y, mo, d);
                        if (!m.Groups[3].Success && dt < now.Date) dt = dt.AddYears(1);
                        date = dt;
                        t = t.Remove(m.Index, m.Length) + " ";
                    }
                    catch { }
                }
                else
                {
                    m = Regex.Match(t, @"\s+(?:el\s+)?(lunes|martes|mi[eé]rcoles|jueves|viernes|s[aá]bado|domingo)\b", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var target = WeekDay(m.Groups[1].Value.ToLowerInvariant());
                        int add = ((int)target - (int)now.DayOfWeek + 7) % 7;
                        if (add == 0) add = 7;
                        date = now.Date.AddDays(add);
                        t = t.Remove(m.Index, m.Length) + " ";
                    }
                }
            }

            // hora: "18:00", "a las 21", "9.30", "a las 8 hs"
            m = Regex.Match(t, @"\s+(?:a\s+las\s+|a\s+la\s+)?(\d{1,2})[:.](\d{2})\s*(?:hs|h)?\b", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(t, @"\s+a\s+las?\s+(\d{1,2})()\s*(?:hs|h)?\b", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                int h = int.Parse(m.Groups[1].Value);
                int min = m.Groups[2].Value.Length > 0 ? int.Parse(m.Groups[2].Value) : 0;
                if (h <= 23 && min <= 59)
                {
                    time = new TimeSpan(h, min, 0);
                    t = t.Remove(m.Index, m.Length) + " ";
                }
            }

            if (date == null && time == null) return (Clean(t), null);
            var day = date ?? now.Date;
            var result = day + (time ?? new TimeSpan(9, 0, 0)); // solo fecha: a las 9
            if (date == null && result <= now) result = result.AddDays(1); // solo hora ya pasada: mañana
            return (Clean(t), result);
        }

        private static DayOfWeek WeekDay(string w) => w switch
        {
            "lunes" => DayOfWeek.Monday, "martes" => DayOfWeek.Tuesday, "jueves" => DayOfWeek.Thursday, "viernes" => DayOfWeek.Friday,
            "domingo" => DayOfWeek.Sunday,
            _ when w.StartsWith("mi") => DayOfWeek.Wednesday,
            _ => DayOfWeek.Saturday
        };

        private static string Clean(string t)
        {
            var s = Regex.Replace(t, @"\s+", " ").Trim();
            return s.Length == 0 ? "Recordatorio" : char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
