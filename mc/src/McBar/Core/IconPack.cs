// McBar — Creado por Andrés Londero
// Paquetes de íconos (0.4.3): una carpeta con imágenes (.png, .ico, .jpg, .bmp) — por ejemplo un pack de
// RocketDock/ObjectDock, un tema de íconos de Linux que traiga PNG (los que son solo SVG, como Papirus,
// todavía no se leen) o una
// carpeta armada a mano. El ícono se busca por el nombre del archivo, comparando sin mayúsculas, espacios,
// guiones ni puntos: "google-chrome.png", "Google Chrome.png" o "chrome.ico" sirven para Chrome.
// Se elige en Preferencias → Dock → Paquete de íconos. Si una app no está en el pack, se usa la de Windows.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;

namespace McBar.Core
{
    public static class IconPack
    {
        private static readonly string[] Extensions = { ".png", ".ico", ".jpg", ".jpeg", ".bmp", ".icns" }; // 1.8.2: .icns de macOS
        private static Dictionary<string, string>? _index;
        private static string? _indexedFor;
        private static readonly object Lock = new object();

        /// <summary>"Google-Chrome_2.png" → "googlechrome2". Pura, se prueba.</summary>
        public static string Normalize(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s.ToLowerInvariant().Normalize(NormalizationForm.FormD))
                if (char.IsLetterOrDigit(ch) && ch < 128) sb.Append(ch);
            return sb.ToString();
        }

        /// <summary>
        /// Nombres posibles de una app, del más específico al más general (pura, se prueba):
        /// el nombre visible, el nombre del .exe/.lnk y, para apps de la Tienda, partes del identificador.
        /// </summary>
        public static List<string> Candidates(string parsingName, string? displayName)
        {
            var list = new List<string>();
            void Add(string? s) { var n = s == null ? "" : Normalize(s); if (n.Length >= 2 && !list.Contains(n)) list.Add(n); }
            Add(displayName);
            var id = parsingName;
            const string apps = "shell:AppsFolder\\";
            if (id.StartsWith(apps, StringComparison.OrdinalIgnoreCase)) id = id.Substring(apps.Length);
            var last = id.Split('\\', '/').Last();
            var bang = last.IndexOf('!');
            var pkg = bang > 0 ? last.Substring(0, bang) : last;
            var under = pkg.IndexOf('_');
            if (under > 0) pkg = pkg.Substring(0, under);
            var ext = Path.GetExtension(pkg).ToLowerInvariant();
            if (ext == ".exe" || ext == ".lnk" || ext == ".url") pkg = Path.GetFileNameWithoutExtension(pkg);
            Add(pkg);
            if (pkg.Contains('.'))
            {
                var parts = pkg.Split('.');
                Add(parts[^1]);
                if (parts.Length >= 2) Add(parts[^2] + parts[^1]);
            }
            if (displayName != null && displayName.Contains(' ')) Add(displayName.Split(' ').Last());
            return list;
        }

        /// <summary>Busca el archivo del pack para esa app (o null).</summary>
        public static string? Find(string parsingName, string? displayName)
        {
            var folder = SettingsStore.Current.IconPackPath;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
            var index = Index(folder);
            foreach (var c in Candidates(parsingName, displayName))
                if (index.TryGetValue(c, out var path)) return path;
            return null;
        }

        public static void Reset()
        {
            lock (Lock) { _index = null; _indexedFor = null; }
        }

        private static Dictionary<string, string> Index(string folder)
        {
            lock (Lock)
            {
                if (_index != null && _indexedFor == folder) return _index;
                var map = new Dictionary<string, (string path, long size)>();
                try
                {
                    var opts = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 5, IgnoreInaccessible = true };
                    foreach (var f in Directory.EnumerateFiles(folder, "*", opts))
                    {
                        var ext = Path.GetExtension(f).ToLowerInvariant();
                        if (!Extensions.Contains(ext)) continue;
                        var key = Normalize(Path.GetFileNameWithoutExtension(f));
                        if (key.Length < 2) continue;
                        long size = new FileInfo(f).Length;
                        // si hay varios tamaños (temas de Linux), se queda con el más grande
                        if (!map.TryGetValue(key, out var cur) || size > cur.size) map[key] = (f, size);
                    }
                }
                catch (Exception ex) { Log.Write("Paquete de íconos: " + ex.Message); }
                _index = map.ToDictionary(k => k.Key, v => v.Value.path);
                _indexedFor = folder;
                Log.Write($"Paquete de íconos: {_index.Count} íconos en {folder}");
                return _index;
            }
        }

        /// <summary>Carga la imagen del pack al tamaño pedido (el cuadro más grande si es .ico).</summary>
        public static BitmapSource? Load(string path, int sizePx)
        {
            try
            {
                if (path.EndsWith(".icns", StringComparison.OrdinalIgnoreCase)) return Icns.Load(path, sizePx); // 1.8.2
                using var fs = File.OpenRead(path);
                var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapSource frame = dec.Frames.OrderByDescending(f => f.PixelWidth).First();
                if (frame.PixelWidth > sizePx * 1.5)
                {
                    double k = sizePx / (double)frame.PixelWidth;
                    frame = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(k, k));
                }
                frame.Freeze();
                return frame;
            }
            catch (Exception ex) { Log.Write("Ícono del pack: " + ex.Message); return null; }
        }
    }
}
