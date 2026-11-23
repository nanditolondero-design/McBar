// McBar — Creado por Andrés Londero
// Centro de notificaciones propio de McBar (reemplaza el panel lateral de Windows).
// Guarda en memoria las últimas notificaciones (no se guardan en disco: pueden tener datos privados),
// cuenta las no leídas y avisa a la barra, la isla y el panel cuando cambian.
using System;
using System.Collections.Generic;
using System.Linq;

namespace McBar.Core
{
    public sealed class NotificationEntry
    {
        public long Id { get; init; }
        public string App { get; init; } = "";
        public string Title { get; init; } = "";
        public string Body { get; init; } = "";
        public System.Windows.Media.ImageSource? Icon { get; init; }
        public DateTime Time { get; init; }
        public bool Read { get; set; }

        /// <summary>Nombre para agrupar (la app, o "Notificaciones" si no se supo).</summary>
        public string Group => string.IsNullOrWhiteSpace(App) ? "Notificaciones" : App;
    }

    public static class NotificationCenter
    {
        private const int MaxEntries = 60;
        private static readonly List<NotificationEntry> Items = new List<NotificationEntry>();
        private static long _nextId = 1;

        /// <summary>Cambió la lista (llegó una, se borró, se leyeron).</summary>
        public static event Action? Changed;
        /// <summary>Llegó una notificación nueva.</summary>
        public static event Action<NotificationEntry>? Added;

        public static IReadOnlyList<NotificationEntry> All => Items;
        public static int UnreadCount => Items.Count(i => !i.Read);

        public static NotificationEntry Add(NotificationInfo n, DateTime? when = null)
        {
            var e = new NotificationEntry
            {
                Id = _nextId++,
                App = n.App,
                Title = n.Title,
                Body = n.Body,
                Icon = n.Icon,
                Time = when ?? DateTime.Now
            };
            Items.Insert(0, e);
            if (Items.Count > MaxEntries) Items.RemoveRange(MaxEntries, Items.Count - MaxEntries);
            Added?.Invoke(e);
            Changed?.Invoke();
            return e;
        }

        public static void Remove(long id)
        {
            if (Items.RemoveAll(i => i.Id == id) > 0) Changed?.Invoke();
        }

        public static void RemoveGroup(string group)
        {
            if (Items.RemoveAll(i => i.Group == group) > 0) Changed?.Invoke();
        }

        public static void Clear()
        {
            if (Items.Count == 0) return;
            Items.Clear();
            Changed?.Invoke();
        }

        public static void MarkAllRead()
        {
            bool any = false;
            foreach (var i in Items) if (!i.Read) { i.Read = true; any = true; }
            if (any) Changed?.Invoke();
        }

        /// <summary>Agrupadas por app, la app con la notificación más nueva primero (pura, se prueba).</summary>
        public static List<IGrouping<string, NotificationEntry>> Grouped(IEnumerable<NotificationEntry> items) =>
            items.OrderByDescending(i => i.Time)
                 .GroupBy(i => i.Group)
                 .OrderByDescending(g => g.Max(i => i.Time))
                 .ToList();

        /// <summary>"ahora", "hace 5 min", "hace 2 h", "ayer", "12/11" (pura, se prueba).</summary>
        public static string Ago(DateTime t, DateTime now)
        {
            var d = now - t;
            if (d.TotalMinutes < 1) return "ahora";
            if (d.TotalMinutes < 60) return $"hace {(int)d.TotalMinutes} min";
            if (d.TotalHours < 24 && t.Date == now.Date) return $"hace {(int)d.TotalHours} h";
            if (t.Date == now.Date.AddDays(-1)) return "ayer";
            return t.ToString("dd/MM");
        }

        // ---------- Fuente de notificaciones ----------

        private static NotificationWatcher? _watcher;

        public static string Mode => _watcher?.Mode ?? "Apagado";

        public static void Start()
        {
            if (_watcher != null) return;
            _watcher = new NotificationWatcher();
            _watcher.Notified += n => Add(n);
            _watcher.Start();
        }

        public static void Stop()
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        /// <summary>WindowTracker avisa cada ventana nueva (para leer los cartelitos de notificación de Windows).</summary>
        public static void OnWindowShown(IntPtr hwnd, string cls) => _watcher?.OnWindowShown(hwnd, cls);
    }
}
