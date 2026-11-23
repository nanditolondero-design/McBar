// McBar — Creado por Andrés Londero
// Archivos para el buscador (0.6.0): nombres de archivos y carpetas del usuario (Escritorio, Documentos,
// Descargas, Imágenes, Música, Videos y OneDrive), hasta 5 niveles, sin ocultos ni carpetas de
// programación (node_modules, .git…). Se arma en segundo plano la primera vez y se renueva cada 10 min.
// No usa el indexador de Windows: es propio y no depende de que esté encendido.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace McBar.Core
{
    public sealed class FileHit
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public bool IsFolder { get; init; }
        public DateTime Modified { get; init; }
    }

    public static class FileIndex
    {
        private static List<FileHit> _items = new List<FileHit>();
        private static DateTime _builtAt = DateTime.MinValue;
        private static Task? _building;
        private const int MaxItems = 60000;
        private static readonly HashSet<string> SkipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "node_modules", ".git", ".svn", "bin", "obj", "__pycache__", ".vs", ".idea", "AppData", "$RECYCLE.BIN" };

        /// <summary>Empieza a armar el índice si hace falta (no bloquea).</summary>
        public static void Warm()
        {
            if (_building != null && !_building.IsCompleted) return;
            if ((DateTime.Now - _builtAt).TotalMinutes < 10) return;
            _building = Task.Run(Build);
        }

        public static bool Ready => _builtAt != DateTime.MinValue;

        private static void Build()
        {
            var list = new List<FileHit>();
            var roots = new List<string>();
            foreach (var f in new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments,
                                      Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos })
            {
                var p = Environment.GetFolderPath(f);
                if (!string.IsNullOrEmpty(p)) roots.Add(p);
            }
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            roots.Add(System.IO.Path.Combine(home, "Downloads"));
            var od = Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrEmpty(od)) roots.Add(od);
            foreach (var r in roots.Distinct(StringComparer.OrdinalIgnoreCase))
                if (Directory.Exists(r)) Walk(r, 0, list);
            _items = list;
            _builtAt = DateTime.Now;
            Log.Write($"Buscador: {list.Count} archivos indexados");
        }

        private static void Walk(string dir, int depth, List<FileHit> list)
        {
            if (list.Count >= MaxItems || depth > 5) return;
            try
            {
                var di = new DirectoryInfo(dir);
                foreach (var e in di.EnumerateFileSystemInfos())
                {
                    if (list.Count >= MaxItems) return;
                    if ((e.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                    bool folder = (e.Attributes & FileAttributes.Directory) != 0;
                    if (folder && (SkipDirs.Contains(e.Name) || (e.Attributes & FileAttributes.ReparsePoint) != 0)) continue;
                    list.Add(new FileHit { Name = e.Name, Path = e.FullName, IsFolder = folder, Modified = e.LastWriteTime });
                    if (folder) Walk(e.FullName, depth + 1, list);
                }
            }
            catch { }
        }

        public static List<FileHit> Search(string query, int max = 8) => Rank(_items, query, max);

        /// <summary>
        /// Ordena: primero los que EMPIEZAN con lo buscado, después los que lo contienen (en cualquier palabra);
        /// a igualdad, los modificados más recientemente. Pura, se prueba.
        /// </summary>
        public static List<FileHit> Rank(IEnumerable<FileHit> items, string query, int max)
        {
            query = query.Trim();
            if (query.Length < 2) return new List<FileHit>();
            return items
                .Select(h => (h, score: Score(h.Name, query)))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .ThenByDescending(x => x.h.Modified)
                .Take(max)
                .Select(x => x.h)
                .ToList();
        }

        private static int Score(string name, string q)
        {
            if (name.StartsWith(q, StringComparison.CurrentCultureIgnoreCase)) return 3;
            int i = name.IndexOf(q, StringComparison.CurrentCultureIgnoreCase);
            if (i < 0) return 0;
            char before = name[i - 1 < 0 ? 0 : i - 1];
            return char.IsLetterOrDigit(before) ? 1 : 2; // al principio de una palabra vale más
        }
    }
}
