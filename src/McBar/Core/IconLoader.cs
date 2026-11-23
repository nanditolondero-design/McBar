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
        private static readonly Queue<string> Order = new Queue<string>(); // orden de llegada (para sacar el más viejo)
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
        public static void Request(string parsingName, int sizePx, Action<ImageSource?> callback, bool useCache = true, string? displayName = null, bool appShape = false)
        {
            // 1.3.2: appShape = es el ícono de una app (Dock, Launchpad, Alt+Tab): si en Preferencias se eligió
            // "Como en Mac", se le da la forma de los íconos de macOS (Core/IconShaper).
            bool mac = appShape && SettingsStore.Current.IconShape == "mac";
            var key = parsingName + "|" + sizePx + (mac ? "|mac" : "");
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
                // 0.4.3: primero el paquete de íconos elegido (si hay uno y tiene esta app)
                BitmapSource? img = null;
                var packed = IconPack.Find(parsingName, displayName);
                if (packed != null) img = IconPack.Load(packed, sizePx);
                img ??= LoadNow(parsingName, sizePx);
                if (img != null && mac) img = IconShaper.Shape(img, sizePx) ?? img;
                if (img != null && useCache)
                {
                    lock (CacheLock)
                    {
                        // 1.5.0: tope más grande (antes 120: con el Launchpad abierto se vaciaba todo y se volvían a
                        // cargar los íconos del Dock y del Launchpad cada vez). Se saca el más viejo, no todo.
                        if (Cache.Count >= 400 && Order.Count > 0) { Cache.Remove(Order.Dequeue()); }
                        if (!Cache.ContainsKey(key)) Order.Enqueue(key);
                        Cache[key] = img;
                    }
                }
                dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => callback(img)));
            });
        }

        /// <summary>Vacía todo el caché (al cambiar el paquete de íconos).</summary>
        public static void ClearCache()
        {
            lock (CacheLock) { Cache.Clear(); Order.Clear(); }
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

        /// <summary>1.6.1: carga una imagen suelta (.png, .jpg, .ico, .icns…) como ícono, del tamaño pedido. Null si falla.</summary>
        public static ImageSource? FromImageFile(string path, int sizePx)
        {
            try
            {
                if (path.EndsWith(".icns", StringComparison.OrdinalIgnoreCase)) return Icns.Load(path, sizePx); // 1.8.2
                if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    var dec = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                    BitmapFrame? best = null;
                    foreach (var f in dec.Frames) if (best == null || f.PixelWidth > best.PixelWidth) best = f;
                    best?.Freeze();
                    return best;
                }
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.DecodePixelWidth = Math.Max(16, sizePx);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch (Exception ex) { Log.Write("Ícono propio: " + ex.Message); return null; }
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
