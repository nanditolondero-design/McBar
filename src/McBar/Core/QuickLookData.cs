// McBar — Creado por Andrés Londero
// Datos para Quick Look (1.0.0): qué archivo está elegido en el Explorador y la primera página de un PDF.
//  - Selección: con el objeto "Shell.Application" de Windows (la misma API que usan los scripts).
//  - PDF: Windows.Data.Pdf (incluido en Windows 10/11) dibuja la página como imagen.
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace McBar.Core
{
    public static class QuickLookData
    {
        /// <summary>Ruta del primer elemento elegido en la ventana del Explorador <paramref name="hwnd"/> (o null).</summary>
        public static string? SelectedInExplorer(IntPtr hwnd)
        {
            try
            {
                var t = Type.GetTypeFromProgID("Shell.Application");
                if (t == null) return null;
                dynamic shell = Activator.CreateInstance(t)!;
                dynamic windows = shell.Windows();
                int n = windows.Count;
                for (int i = 0; i < n; i++)
                {
                    dynamic? w = windows.Item(i);
                    if (w == null) continue;
                    long h;
                    try { h = (long)w.HWND; } catch { continue; }
                    if (h != hwnd.ToInt64()) continue;
                    dynamic items = w.Document.SelectedItems();
                    if (items.Count < 1) return null;
                    return (string)items.Item(0).Path;
                }
            }
            catch (Exception ex) { Log.Write("Quick Look (selección): " + ex.Message); }
            return null;
        }

        /// <summary>Tipo de vista previa según la extensión (pura, se prueba).</summary>
        public static string KindOf(string path)
        {
            if (Directory.Exists(path)) return "folder";
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".tif" or ".tiff" or ".ico" or ".webp" or ".heic" => "image",
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" or ".m4v" => "video",
                ".mp3" or ".wav" or ".flac" or ".m4a" or ".aac" or ".ogg" or ".wma" => "audio",
                ".pdf" => "pdf",
                ".txt" or ".md" or ".log" or ".csv" or ".json" or ".xml" or ".ini" or ".cfg" or ".yml" or ".yaml" or ".cs" or ".js" or ".ts"
                    or ".py" or ".html" or ".htm" or ".css" or ".bat" or ".ps1" or ".sh" or ".c" or ".cpp" or ".h" or ".java" or ".sql" => "text",
                _ => "other"
            };
        }

        /// <summary>Primera página de un PDF como PNG (bytes), o null.</summary>
        public static async Task<byte[]?> PdfFirstPageAsync(string path, uint width = 1100)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                var doc = await PdfDocument.LoadFromFileAsync(file);
                if (doc.PageCount == 0) return null;
                using var page = doc.GetPage(0);
                using var ras = new InMemoryRandomAccessStream();
                await page.RenderToStreamAsync(ras, new PdfPageRenderOptions { DestinationWidth = width });
                ras.Seek(0);
                using var st = ras.AsStreamForRead();
                var ms = new MemoryStream();
                await st.CopyToAsync(ms);
                return ms.ToArray();
            }
            catch (Exception ex) { Log.Write("Quick Look (PDF): " + ex.Message); return null; }
        }

        /// <summary>"1,2 MB" (pura, se prueba).</summary>
        public static string Size(long bytes)
        {
            var es = new System.Globalization.CultureInfo("es-AR");
            if (bytes < 1024) return bytes + " bytes";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#", es) + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1048576.0).ToString("0.#", es) + " MB";
            return (bytes / 1073741824.0).ToString("0.##", es) + " GB";
        }
    }
}
