// McBar — Creado por Andrés Londero
// 1.9.24: Texto en vivo (como Live Text de macOS). Elegís una zona de la pantalla y McBar lee el texto que haya
// en la imagen (un cartel, un PDF escaneado, una foto) y lo copia al portapapeles. Usa el OCR de Windows.
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using McBar.Native;
using McBar.Panels;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace McBar.Core
{
    public static class LiveText
    {
        private static bool _busy;

        /// <summary>Abre la selección de zona y, al elegir, hace OCR y copia el texto.</summary>
        public static void Start()
        {
            if (_busy) return;
            var m = App.ActiveMonitor() ?? Monitors.All()[0];
            if (Win32.GetCursorPos(out var p)) m = Monitors.FromPoint(p.x, p.y) ?? m;
            var frozen = ScreenCapture.Region(m.Bounds.Left, m.Bounds.Top, m.Bounds.Width, m.Bounds.Height);
            if (frozen == null) return;
            _busy = true;
            var o = new CaptureOverlay(m, frozen, false, async crop => { try { await Recognize(crop); } finally { _busy = false; } });
            o.Closed += (_, _) => _busy = false;
            o.Show();
        }

        private static async Task Recognize(BitmapSource crop)
        {
            try
            {
                var engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine == null) { Notify("Texto en vivo", "No hay un idioma de OCR instalado en Windows."); return; }
                var sb = ToSoftwareBitmap(crop);
                var result = await engine.RecognizeAsync(sb);
                var text = string.Join("\n", result.Lines.Select(l => l.Text)).Trim();
                if (string.IsNullOrWhiteSpace(text)) { Notify("Texto en vivo", "No se encontró texto en esa zona."); return; }
                try { Clipboard.SetText(text); } catch { System.Threading.Thread.Sleep(60); Clipboard.SetText(text); }
                Notify("Texto en vivo", "Texto copiado al portapapeles.");
            }
            catch (Exception ex) { Log.Write("Texto en vivo: " + ex.Message); }
        }

        /// <summary>WPF BitmapSource → WinRT SoftwareBitmap (BGRA8), que es lo que pide el OCR.</summary>
        private static SoftwareBitmap ToSoftwareBitmap(BitmapSource src)
        {
            var bgra = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
            var bytes = new byte[h * stride];
            bgra.CopyPixels(bytes, stride, 0);
            return SoftwareBitmap.CreateCopyFromBuffer(bytes.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
        }

        private static void Notify(string title, string body)
        {
            try { NotificationCenter.Add(new NotificationInfo { App = "McBar", Title = title, Body = body }); } catch { }
        }
    }
}
