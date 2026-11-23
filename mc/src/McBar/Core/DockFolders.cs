// McBar — Creado por Andrés Londero
// 1.8.3 — Carpetas de apps en el Dock, como en Android:
//   - Se arma arrastrando una app del Dock y dejándola un momento ENCIMA de otra (o de una carpeta).
//   - También con clic derecho en una app fija → "Agregar a una carpeta".
//   - El ícono de la carpeta muestra las primeras 4 apps en chiquito; al tocarla se abre un panel con todas.
//   - Si a la carpeta le queda una sola app, la carpeta se deshace y queda esa app.
// En los ajustes: DockFolders guarda cada carpeta (id, nombre, apps) y en Pinned va "mcbar:appfolder:&lt;id&gt;"
// en el lugar de la carpeta. Lo de acá es la parte pura (se prueba); el Dock solo dibuja y llama a esto.
using System;
using System.Collections.Generic;
using System.Linq;

namespace McBar.Core
{
    public sealed class DockAppFolder
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "Carpeta";
        public List<string> Targets { get; set; } = new List<string>();
    }

    public static class DockFolders
    {
        public const string Prefix = "mcbar:appfolder:";

        public static bool IsFolder(string? target) => target != null && target.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        public static string TargetFor(string id) => Prefix + id;
        public static string IdOf(string target) => IsFolder(target) ? target.Substring(Prefix.Length) : "";

        public static DockAppFolder? Find(IEnumerable<DockAppFolder> folders, string? target)
        {
            if (!IsFolder(target)) return null;
            var id = IdOf(target!);
            return folders.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Suelta la app <paramref name="app"/> sobre <paramref name="onto"/> (otra app fija o una carpeta) y
        /// devuelve la lista nueva de fijados. Si <paramref name="onto"/> es una app, se crea una carpeta en su
        /// lugar con las dos. La app que se soltó sale de los fijados (ahora está en la carpeta). Pura, se prueba.
        /// </summary>
        public static List<PinnedApp> Drop(List<PinnedApp> pinned, List<DockAppFolder> folders, string app, string onto, Func<string> newId)
        {
            var result = pinned.Where(p => !string.Equals(p.Target, app, StringComparison.OrdinalIgnoreCase)).ToList();
            var existing = Find(folders, onto);
            if (existing != null)
            {
                if (!existing.Targets.Contains(app, StringComparer.OrdinalIgnoreCase)) existing.Targets.Add(app);
                return result;
            }
            int at = result.FindIndex(p => string.Equals(p.Target, onto, StringComparison.OrdinalIgnoreCase));
            if (at < 0 || string.Equals(app, onto, StringComparison.OrdinalIgnoreCase)) return pinned.ToList();
            var f = new DockAppFolder { Id = newId(), Name = "Carpeta", Targets = new List<string> { onto, app } };
            folders.Add(f);
            result[at] = new PinnedApp { Name = f.Name, Target = TargetFor(f.Id) };
            return result;
        }

        /// <summary>Crea una carpeta con una sola app (desde el clic derecho) en su lugar. Pura, se prueba.</summary>
        public static List<PinnedApp> NewWith(List<PinnedApp> pinned, List<DockAppFolder> folders, string app, string name, Func<string> newId)
        {
            var result = pinned.ToList();
            int at = result.FindIndex(p => string.Equals(p.Target, app, StringComparison.OrdinalIgnoreCase));
            var f = new DockAppFolder { Id = newId(), Name = name, Targets = new List<string> { app } };
            folders.Add(f);
            var pin = new PinnedApp { Name = f.Name, Target = TargetFor(f.Id) };
            if (at >= 0) result[at] = pin; else result.Add(pin);
            return result;
        }

        /// <summary>
        /// Saca una app de la carpeta y la deja fija justo después. Si la carpeta queda con una sola app (o
        /// ninguna) se deshace: la que queda vuelve al Dock en su lugar. Pura, se prueba.
        /// </summary>
        public static List<PinnedApp> TakeOut(List<PinnedApp> pinned, List<DockAppFolder> folders, string folderTarget, string app, Func<string, string> nameOf)
        {
            var f = Find(folders, folderTarget);
            var result = pinned.ToList();
            if (f == null) return result;
            int at = result.FindIndex(p => string.Equals(p.Target, folderTarget, StringComparison.OrdinalIgnoreCase));
            f.Targets.RemoveAll(t => string.Equals(t, app, StringComparison.OrdinalIgnoreCase));
            if (at < 0) at = result.Count - 1;
            if (!result.Any(p => string.Equals(p.Target, app, StringComparison.OrdinalIgnoreCase)))
                result.Insert(Math.Min(at + 1, result.Count), new PinnedApp { Name = nameOf(app), Target = app });
            if (f.Targets.Count <= 1) return Dissolve(result, folders, folderTarget, nameOf);
            return result;
        }

        /// <summary>Deshace la carpeta: sus apps vuelven al Dock en su lugar, en orden. Pura, se prueba.</summary>
        public static List<PinnedApp> Dissolve(List<PinnedApp> pinned, List<DockAppFolder> folders, string folderTarget, Func<string, string> nameOf)
        {
            var f = Find(folders, folderTarget);
            var result = pinned.ToList();
            int at = result.FindIndex(p => string.Equals(p.Target, folderTarget, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) result.RemoveAt(at); else at = result.Count;
            if (f != null)
            {
                var add = f.Targets.Where(t => !result.Any(p => string.Equals(p.Target, t, StringComparison.OrdinalIgnoreCase)))
                                   .Select(t => new PinnedApp { Name = nameOf(t), Target = t }).ToList();
                result.InsertRange(Math.Min(at, result.Count), add);
                folders.Remove(f);
            }
            return result;
        }

        public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 10);
    }
}
