// McBar — Creado por Andrés Londero
// Carpetas del Launchpad (0.4.0): lógica pura, sin interfaz, para poder probarla.
//
// Reglas (parecidas a macOS):
//  - Soltar una app sobre otra crea una carpeta con las dos.
//  - Soltar una app sobre una carpeta la agrega. Una app está en UNA sola carpeta a la vez.
//  - Sacar apps de una carpeta: cuando queda vacía, desaparece.
//  - Las carpetas se muestran primero (en el orden en que se crearon) y después las apps sueltas.
//  - Las carpetas se guardan con el "Target" de cada app (shell:AppsFolder\...), en settings.json.
using System;
using System.Collections.Generic;
using System.Linq;

namespace McBar.Core
{
    /// <summary>Un lugar de la grilla del Launchpad: una app suelta o una carpeta con sus apps.</summary>
    public sealed class LaunchpadEntry<T>
    {
        public T? App { get; init; }
        public LaunchpadFolder? Folder { get; init; }
        public List<T> Apps { get; init; } = new List<T>();
        public bool IsFolder => Folder != null;
        /// <summary>Clave única: "folder:&lt;id&gt;" o el Target de la app (orden y ocultas, 0.4.3).</summary>
        public string Key(Func<T, string> target) => IsFolder ? LaunchpadLayout.FolderKey(Folder!) : target(App!);
    }

    public static class LaunchpadLayout
    {
        public const string DefaultName = "Carpeta";

        public static string FolderKey(LaunchpadFolder f) => "folder:" + f.Id;

        /// <summary>
        /// Ordena según el orden guardado (lo que el usuario movió). Lo que no está en el orden (apps nuevas)
        /// va al final, en el orden de siempre. (pura, se prueba) — 0.4.3
        /// </summary>
        public static List<LaunchpadEntry<T>> ApplyOrder<T>(List<LaunchpadEntry<T>> entries, IList<string> order, Func<T, string> target)
        {
            if (order.Count == 0) return entries;
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < order.Count; i++) index.TryAdd(order[i], i);
            var known = entries.Where(e => index.ContainsKey(e.Key(target))).OrderBy(e => index[e.Key(target)]);
            var rest = entries.Where(e => !index.ContainsKey(e.Key(target)));
            return known.Concat(rest).ToList();
        }

        /// <summary>
        /// Mueve <paramref name="key"/> antes (o después) de <paramref name="targetKey"/>. Devuelve el orden
        /// completo nuevo para guardar. (pura, se prueba)
        /// </summary>
        public static List<string> Move(IEnumerable<string> currentKeys, string key, string targetKey, bool after)
        {
            var list = currentKeys.ToList();
            int from = list.FindIndex(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (from < 0 || string.Equals(key, targetKey, StringComparison.OrdinalIgnoreCase)) return list;
            list.RemoveAt(from);
            int to = list.FindIndex(k => string.Equals(k, targetKey, StringComparison.OrdinalIgnoreCase));
            if (to < 0) { list.Insert(Math.Min(from, list.Count), key); return list; }
            list.Insert(after ? to + 1 : to, key);
            return list;
        }

        /// <summary>Saca las entradas ocultas (apps y carpetas) salvo que se pidan todas.</summary>
        public static List<LaunchpadEntry<T>> WithoutHidden<T>(List<LaunchpadEntry<T>> entries, ICollection<string> hidden, Func<T, string> target, bool showHidden)
        {
            if (showHidden || hidden.Count == 0) return entries;
            var set = new HashSet<string>(hidden, StringComparer.OrdinalIgnoreCase);
            return entries.Where(e => !set.Contains(e.Key(target))).ToList();
        }

        /// <summary>Arma la grilla: carpetas primero (solo las apps que existen) y después las apps sueltas.</summary>
        public static List<LaunchpadEntry<T>> Arrange<T>(IEnumerable<T> apps, IList<LaunchpadFolder> folders, Func<T, string> target)
        {
            var list = apps.ToList();
            var byTarget = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in list) byTarget.TryAdd(target(a), a);

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<LaunchpadEntry<T>>();
            foreach (var f in folders)
            {
                var inside = new List<T>();
                foreach (var t in f.Targets)
                    if (byTarget.TryGetValue(t, out var a) && used.Add(t)) inside.Add(a);
                if (inside.Count > 0) result.Add(new LaunchpadEntry<T> { Folder = f, Apps = inside });
            }
            foreach (var a in list)
                if (!used.Contains(target(a))) result.Add(new LaunchpadEntry<T> { App = a });
            return result;
        }

        /// <summary>Busca: apps (también las de adentro de carpetas) y carpetas cuyo nombre coincide.</summary>
        public static List<LaunchpadEntry<T>> Search<T>(IEnumerable<T> apps, IList<LaunchpadFolder> folders, Func<T, string> target, Func<T, string> name, string query)
        {
            var arranged = Arrange(apps, folders, target);
            var result = arranged.Where(e => e.IsFolder && e.Folder!.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
            foreach (var e in arranged)
            {
                if (e.IsFolder) result.AddRange(e.Apps.Where(a => name(a).Contains(query, StringComparison.CurrentCultureIgnoreCase)).Select(a => new LaunchpadEntry<T> { App = a }));
                else if (name(e.App!).Contains(query, StringComparison.CurrentCultureIgnoreCase)) result.Add(e);
            }
            return result;
        }

        /// <summary>Saca las apps ocultas (salvo que se pidan todas). 0.4.2.</summary>
        public static List<T> Visible<T>(IEnumerable<T> apps, ICollection<string> hidden, Func<T, string> target, bool showHidden)
        {
            if (showHidden || hidden.Count == 0) return apps.ToList();
            var set = new HashSet<string>(hidden, StringComparer.OrdinalIgnoreCase);
            return apps.Where(a => !set.Contains(target(a))).ToList();
        }

        /// <summary>Oculta o vuelve a mostrar una app. Devuelve true si quedó oculta.</summary>
        public static bool ToggleHidden(IList<string> hidden, string target)
        {
            int i = -1;
            for (int k = 0; k < hidden.Count; k++) if (string.Equals(hidden[k], target, StringComparison.OrdinalIgnoreCase)) { i = k; break; }
            if (i >= 0) { hidden.RemoveAt(i); return false; }
            hidden.Add(target);
            return true;
        }

        public static LaunchpadFolder? FolderOf(IList<LaunchpadFolder> folders, string target) =>
            folders.FirstOrDefault(f => f.Targets.Contains(target, StringComparer.OrdinalIgnoreCase));

        /// <summary>Crea una carpeta con <paramref name="first"/> y (si se da) <paramref name="second"/>.</summary>
        public static LaunchpadFolder CreateFolder(IList<LaunchpadFolder> folders, string first, string? second = null, string name = DefaultName)
        {
            Detach(folders, first);
            if (second != null) Detach(folders, second);
            var f = new LaunchpadFolder { Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim() };
            f.Targets.Add(first);
            if (second != null && !string.Equals(first, second, StringComparison.OrdinalIgnoreCase)) f.Targets.Add(second);
            folders.Add(f);
            Prune(folders);
            return f;
        }

        /// <summary>Mete una app en una carpeta (sacándola de la que estaba).</summary>
        public static bool AddToFolder(IList<LaunchpadFolder> folders, string folderId, string target)
        {
            var f = folders.FirstOrDefault(x => x.Id == folderId);
            if (f == null) return false;
            if (f.Targets.Contains(target, StringComparer.OrdinalIgnoreCase)) return true;
            Detach(folders, target);
            f.Targets.Add(target);
            Prune(folders);
            return true;
        }

        /// <summary>Saca una app de su carpeta. Si la carpeta queda vacía, desaparece.</summary>
        public static bool RemoveFromFolder(IList<LaunchpadFolder> folders, string target)
        {
            bool removed = Detach(folders, target);
            Prune(folders);
            return removed;
        }

        public static bool Rename(IList<LaunchpadFolder> folders, string folderId, string name)
        {
            var f = folders.FirstOrDefault(x => x.Id == folderId);
            if (f == null) return false;
            f.Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
            return true;
        }

        /// <summary>Deshace la carpeta: sus apps vuelven a quedar sueltas.</summary>
        public static bool Dissolve(IList<LaunchpadFolder> folders, string folderId)
        {
            var f = folders.FirstOrDefault(x => x.Id == folderId);
            return f != null && folders.Remove(f);
        }

        /// <summary>Quita de las carpetas las apps que ya no están instaladas (solo con el catálogo completo).</summary>
        public static void Clean(IList<LaunchpadFolder> folders, IEnumerable<string> installed)
        {
            var set = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
            foreach (var f in folders) f.Targets.RemoveAll(t => !set.Contains(t));
            Prune(folders);
        }

        private static bool Detach(IList<LaunchpadFolder> folders, string target)
        {
            bool any = false;
            foreach (var f in folders) any |= f.Targets.RemoveAll(t => string.Equals(t, target, StringComparison.OrdinalIgnoreCase)) > 0;
            return any;
        }

        private static void Prune(IList<LaunchpadFolder> folders)
        {
            for (int i = folders.Count - 1; i >= 0; i--)
                if (folders[i].Targets.Count == 0) folders.RemoveAt(i);
        }
    }
}
