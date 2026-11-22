// McBar — Creado por Andrés Londero
// Carga de íconos en alta resolución en un hilo de fondo (STA) para no trabar la interfaz.
// Usa IShellItemImageFactory, que sirve igual para .exe, .lnk, carpetas, archivos y apps de la Store
// (shell:AppsFolder\<id>). Los íconos del Dock se guardan en un caché chico; los del Launchpad no,
// así se liberan al cerrar el Launchpad (bajo consumo de memoria).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using McBar.Native;

namespace McBar.Core
{
    internal static class IconLoader
    {
        private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly object CacheLock = new object();
        private static Dispatcher? _shell;
        private static readonly object StartLock = new object();

        /// <summary>
        /// Hilo propio del Shell (STA) con su propio "bucle de mensajes" (Dispatcher.Run), como necesitan
        /// los objetos COM del Shell de Windows. Antes era una cola simple y algunas consultas (la lista
        /// de apps del Launchpad) podían quedarse colgadas.
        /// </summary>
        private static Dispatcher ShellDispatcher()
        {
            lock (StartLock)
            {
                if (_shell != null) return _shell;
                var ready = new ManualResetEventSlim();
                var t = new Thread(() =>
                {
                    _shell = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                })
                { IsBackground = true, Name = "McBar.Shell", Priority = ThreadPriority.BelowNormal };
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                ready.Wait();
                return _shell!;
            }
        }

        /// <summary>Ejecuta un trabajo en el hilo del Shell (STA). Los urgentes van antes que los íconos.</summary>
        public static void RunOnShellThread(Action job, bool urgent = false)
        {
            ShellDispatcher().BeginInvoke(urgent ? DispatcherPriority.Normal : DispatcherPriority.Background, new Action(() =>
            {
                try { job(); } catch (Exception ex) { Log.Write("Shell: " + ex.Message); }
            }));
        }

        /// <summary>Pide un ícono. El callback se ejecuta en el hilo de la interfaz.</summary>
        public static void Request(string parsingName, int sizePx, Action<ImageSource?> callback, bool useCache = true)
        {
            var key = parsingName + "|" + sizePx;
            if (useCache)
            {
                lock (CacheLock)
                {
                    if (Cache.TryGetValue(key, out var cached)) { callback(cached); return; }
                }
            }
            var dispatcher = Dispatcher.CurrentDispatcher;
            RunOnShellThread(() =>
            {
                var img = LoadNow(parsingName, sizePx);
                if (img != null && useCache)
                {
                    lock (CacheLock)
                    {
                        if (Cache.Count > 120) Cache.Clear(); // tope simple para no crecer sin límite
                        Cache[key] = img;
                    }
                }
                dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => callback(img)));
            });
        }

        public static void Forget(string parsingName)
        {
            lock (CacheLock)
            {
                var remove = new List<string>();
                foreach (var k in Cache.Keys) if (k.StartsWith(parsingName + "|", StringComparison.OrdinalIgnoreCase)) remove.Add(k);
                foreach (var k in remove) Cache.Remove(k);
            }
        }

        /// <summary>Carga sincrónica (llamar desde el hilo del Shell).</summary>
        public static BitmapSource? LoadNow(string parsingName, int sizePx)
        {
            object? o = null;
            try
            {
                if (ShellCom.SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref ShellCom.IID_IShellItemImageFactory, out o) != 0 || o is not IShellItemImageFactory f)
                    return null;
                if (f.GetImage(new SIZE(sizePx, sizePx), ShellCom.SIIGBF_ICONONLY | ShellCom.SIIGBF_BIGGERSIZEOK, out var hbmp) != 0 || hbmp == IntPtr.Zero)
                    return null;
                return FromHBitmap(hbmp);
            }
            catch { return null; }
            finally { if (o != null) Marshal.ReleaseComObject(o); }
        }

        /// <summary>Convierte un HBITMAP de 32 bits conservando la transparencia y lo libera.</summary>
        private static BitmapSource? FromHBitmap(IntPtr hbmp)
        {
            try
            {
                if (Win32.GetObject(hbmp, Marshal.SizeOf<Win32.BITMAP>(), out var bm) == 0) return null;
                int w = bm.bmWidth, h = Math.Abs(bm.bmHeight);
                if (w <= 0 || h <= 0) return null;
                var bi = new Win32.BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                    biWidth = w,
                    biHeight = -h, // de arriba hacia abajo
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0
                };
                var px = new byte[w * h * 4];
                var hdc = Win32.GetDC(IntPtr.Zero);
                try { if (Win32.GetDIBits(hdc, hbmp, 0, (uint)h, px, ref bi, 0) == 0) return null; }
                finally { Win32.ReleaseDC(IntPtr.Zero, hdc); }

                bool anyAlpha = false;
                for (int i = 3; i < px.Length; i += 4) if (px[i] != 0) { anyAlpha = true; break; }
                if (!anyAlpha) for (int i = 3; i < px.Length; i += 4) px[i] = 255;

                var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, px, w * 4);
                src.Freeze();
                return src;
            }
            finally { Win32.DeleteObject(hbmp); }
        }

        /// <summary>Ícono que la propia ventana informa (último recurso). Llamar en el hilo de interfaz.</summary>
        public static ImageSource? FromWindow(IntPtr hwnd)
        {
            try
            {
                Win32.SendMessageTimeout(hwnd, Win32.WM_GETICON, new IntPtr(Win32.ICON_BIG), IntPtr.Zero, Win32.SMTO_ABORTIFHUNG, 100, out var hIcon);
                if (hIcon == IntPtr.Zero) hIcon = Win32.GetClassLongPtr(hwnd, Win32.GCLP_HICON);
                if (hIcon == IntPtr.Zero) return null;
                var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            catch { return null; }
        }
    }
}
