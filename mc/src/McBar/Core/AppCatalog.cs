// McBar — Creado por Andrés Londero
// Lista de aplicaciones instaladas (programas de escritorio y de la Store) leyendo shell:AppsFolder,
// la misma lista que usa el menú Inicio. Se usa en el Launchpad.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using McBar.Native;

namespace McBar.Core
{
    public sealed class CatalogApp
    {
        public string Name { get; init; } = "";
        /// <summary>"shell:AppsFolder\&lt;id&gt;" — sirve para abrirla, sacar el ícono y fijarla en el Dock.</summary>
        public string Target { get; init; } = "";
    }

    public static class AppCatalog
    {
        private static List<CatalogApp>? _last;

        /// <summary>Última lista conocida (para que el Launchpad abra al instante la segunda vez).</summary>
        public static IReadOnlyList<CatalogApp>? Cached => _last;

        public static Task<List<CatalogApp>> LoadAsync()
        {
            var tcs = new TaskCompletionSource<List<CatalogApp>>();
            IconLoader.RunOnShellThread(() =>
            {
                try
                {
                    var l = Enumerate();
                    if (l.Count < 5) l = MergeStartMenu(l); // plan B si la carpeta de apps no respondió
                    Log.Write($"Launchpad: {l.Count} apps");
                    _last = l;
                    tcs.TrySetResult(l);
                }
                catch (Exception ex)
                {
                    Log.Write("AppCatalog: " + ex.Message);
                    tcs.TrySetResult(_last ?? MergeStartMenu(new List<CatalogApp>()));
                }
            }, urgent: true);
            return tcs.Task;
        }

        private static List<CatalogApp> Enumerate()
        {
            var result = new List<CatalogApp>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            object? folderObj = null, enumObj = null;
            try
            {
                // Carpeta "Aplicaciones" (FOLDERID_AppsFolder); si falla, por su nombre.
                var appsFolderId = new Guid("1e87508d-89c2-42f0-8a7e-645a0f50ca58");
                if (Win32.SHGetKnownFolderItem(appsFolderId, 0, IntPtr.Zero, ShellCom.IID_IShellItem, out folderObj) != 0 || folderObj is not IShellItem)
                {
                    if (folderObj != null) Marshal.ReleaseComObject(folderObj);
                    folderObj = null;
                    ShellCom.SHCreateItemFromParsingName("shell:AppsFolder", IntPtr.Zero, ref ShellCom.IID_IShellItem, out folderObj);
                }
                if (folderObj is not IShellItem folder) return result;
                var bhid = ShellCom.BHID_EnumItems;
                var iid = ShellCom.IID_IEnumShellItems;
                folder.BindToHandler(IntPtr.Zero, ref bhid, ref iid, out enumObj);
                if (enumObj is not IEnumShellItems en) return result;

                var buf = new IShellItem[1];
                while (en.Next(1, buf, out var fetched) == 0 && fetched == 1)
                {
                    var item = buf[0];
                    try
                    {
                        var name = ShellCom.GetDisplayName(item, ShellCom.SIGDN_NORMALDISPLAY);
                        var id = ShellCom.GetDisplayName(item, ShellCom.SIGDN_PARENTRELATIVEPARSING);
                        if (ShouldInclude(name, id) && names.Add(name!))
                            result.Add(new CatalogApp { Name = name!, Target = AppIdentity.AppsFolderPrefix + id });
                    }
                    finally { Marshal.ReleaseComObject(item); }
                }
            }
            finally
            {
                if (enumObj != null) Marshal.ReleaseComObject(enumObj);
                if (folderObj != null) Marshal.ReleaseComObject(folderObj);
            }

            var cmp = StringComparer.Create(new CultureInfo("es-AR"), true);
            result.Sort((a, b) => cmp.Compare(a.Name, b.Name));
            return result;
        }

        /// <summary>Plan B: accesos directos del menú Inicio (del usuario y de todos los usuarios).</summary>
        private static List<CatalogApp> MergeStartMenu(List<CatalogApp> current)
        {
            var names = new HashSet<string>(current.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            };
            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories).ToList(); }
                catch { continue; }
                foreach (var f in files)
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (ShouldInclude(name, f) && names.Add(name))
                        current.Add(new CatalogApp { Name = name, Target = f });
                }
            }
            var cmp = StringComparer.Create(new CultureInfo("es-AR"), true);
            current.Sort((a, b) => cmp.Compare(a.Name, b.Name));
            return current;
        }

        /// <summary>Descarta desinstaladores, páginas web, manuales y otras entradas que no son apps.</summary>
        public static bool ShouldInclude(string? name, string? id)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) return false;
            var n = name.ToLowerInvariant();
            if (n.Contains("uninstall") || n.Contains("desinstal") || n.Contains("readme") || n.Contains("léame")) return false;
            var i = id.ToLowerInvariant();
            if (i.StartsWith("http:") || i.StartsWith("https:")) return false;
            foreach (var ext in new[] { ".url", ".chm", ".txt", ".pdf", ".htm", ".html", ".rtf", ".ini", ".hlp" })
                if (i.EndsWith(ext)) return false;
            return true;
        }
    }
}
