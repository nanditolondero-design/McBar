// McBar — Creado por Andrés Londero
// 1.8.2 — Íconos .icns de macOS. Un .icns es un contenedor: "icns" + largo, y adentro
// varias entradas (tipo de 4 letras + largo + datos) con el mismo ícono en distintos tamaños. En los .icns
// modernos (macOS 10.7 o después, y los de macosicons.com) esas imágenes son PNG comunes: McBar busca la que
// mejor le queda al tamaño pedido y la usa como cualquier otro ícono.
// 1.8.4: también lee los formatos viejos de adentro (colores comprimidos "RLE" it32/ih32/il32/is32 con su
// máscara t8mk/h8mk/l8mk/s8mk, y ARGB ic04/ic05) cuando no hay PNG. JPEG 2000 no (Windows no lo trae): en
// ese caso McBar avisa al elegirlo y queda el ícono original.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace McBar.Core
{
    public static class Icns
    {
        private static readonly byte[] PngSig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static int BE32(byte[] d, int o) => (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];

        /// <summary>Entradas del archivo: (tipo, comienzo de los datos, largo de los datos). Pura, se prueba.</summary>
        public static List<(string type, int offset, int length)> Entries(byte[] data)
        {
            var list = new List<(string, int, int)>();
            if (data == null || data.Length < 8 || data[0] != 'i' || data[1] != 'c' || data[2] != 'n' || data[3] != 's') return list;
            int total = Math.Min(data.Length, BE32(data, 4));
            int pos = 8;
            while (pos + 8 <= total)
            {
                string type = System.Text.Encoding.ASCII.GetString(data, pos, 4);
                int len = BE32(data, pos + 4);
                if (len < 8 || pos + len > total) break;
                list.Add((type, pos + 8, len - 8));
                pos += len;
            }
            return list;
        }

        public static bool IsPng(byte[] d, int offset, int length)
        {
            if (length < 24 || offset + 24 > d.Length) return false;
            for (int i = 0; i < 8; i++) if (d[offset + i] != PngSig[i]) return false;
            return true;
        }

        /// <summary>Ancho de un PNG (del encabezado IHDR). Pura.</summary>
        public static int PngWidth(byte[] d, int offset) => BE32(d, offset + 16);

        /// <summary>
        /// La imagen PNG que mejor sirve: la más chica que sea al menos del tamaño pedido (se ve nítida y no se
        /// carga de más); si ninguna llega, la más grande. Devuelve (comienzo, largo) o null. Pura, se prueba.
        /// </summary>
        public static (int offset, int length)? BestPng(byte[] data, int sizePx)
        {
            (int off, int len, int w)? best = null, biggest = null;
            foreach (var (_, off, len) in Entries(data))
            {
                if (!IsPng(data, off, len)) continue;
                int w = PngWidth(data, off);
                if (w <= 0) continue;
                if (biggest == null || w > biggest.Value.w) biggest = (off, len, w);
                if (w >= sizePx && (best == null || w < best.Value.w)) best = (off, len, w);
            }
            var pick = best ?? biggest;
            return pick == null ? null : (pick.Value.off, pick.Value.len);
        }

        // ---------- 1.8.4: formatos viejos ----------

        /// <summary>Tamaño de cada entrada vieja de colores (RLE) y su máscara.</summary>
        private static readonly (string color, string mask, int size)[] OldTypes =
        {
            ("it32", "t8mk", 128), ("ih32", "h8mk", 48), ("il32", "l8mk", 32), ("is32", "s8mk", 16),
        };

        /// <summary>
        /// Descomprime el RLE de Apple: un byte n &lt; 0x80 = copiar n+1 bytes; n ≥ 0x80 = repetir el próximo byte
        /// n−0x80+3 veces. Llena <paramref name="count"/> bytes por canal, uno tras otro. Pura, se prueba.
        /// </summary>
        public static byte[]? UnRle(byte[] d, int offset, int length, int channels, int count)
        {
            var outp = new byte[channels * count];
            int pos = offset, end = offset + length, o = 0, total = channels * count;
            while (o < total && pos < end)
            {
                int n = d[pos++];
                if (n < 0x80)
                {
                    int c = n + 1;
                    if (pos + c > end) return null;
                    for (int i = 0; i < c && o < total; i++) outp[o++] = d[pos++];
                }
                else
                {
                    int c = n - 0x80 + 3;
                    if (pos >= end) return null;
                    byte v = d[pos++];
                    for (int i = 0; i < c && o < total; i++) outp[o++] = v;
                }
            }
            return o == total ? outp : null;
        }

        /// <summary>Arma la imagen (BGRA) de un formato viejo. Null si el archivo no tiene ninguno legible.</summary>
        public static BitmapSource? DecodeOld(byte[] data)
        {
            var entries = Entries(data);
            (string type, int off, int len) Get(string t) => entries.Find(e => e.type == t);
            // ARGB (ic05 = 32 px, ic04 = 16 px): "ARGB" + RLE de A, R, G, B
            foreach (var (t, size) in new[] { ("ic05", 32), ("ic04", 16) })
            {
                var e = Get(t);
                if (e.type == null || e.len < 4 || data[e.off] != 'A' || data[e.off + 1] != 'R') continue;
                var ch = UnRle(data, e.off + 4, e.len - 4, 4, size * size);
                if (ch == null) continue;
                return Build(size, i => (ch[i], ch[size * size + i], ch[2 * size * size + i], ch[3 * size * size + i]));
            }
            foreach (var (color, mask, size) in OldTypes)
            {
                var e = Get(color);
                if (e.type == null) continue;
                int n = size * size;
                byte[]? rgb;
                if (e.len == n * 4) // sin comprimir (algunos archivos viejos): ARGB
                {
                    var raw = new byte[n * 3];
                    for (int i = 0; i < n; i++) { raw[i] = data[e.off + i * 4 + 1]; raw[n + i] = data[e.off + i * 4 + 2]; raw[2 * n + i] = data[e.off + i * 4 + 3]; }
                    rgb = raw;
                }
                else
                {
                    int skip = color == "it32" ? 4 : 0; // it32 empieza con 4 bytes en cero
                    rgb = UnRle(data, e.off + skip, e.len - skip, 3, n);
                }
                if (rgb == null) continue;
                var m = Get(mask);
                byte[]? alpha = m.type != null && m.len >= n ? data.AsSpan(m.off, n).ToArray() : null;
                return Build(size, i => (alpha?[i] ?? (byte)255, rgb[i], rgb[n + i], rgb[2 * n + i]));
            }
            return null;
        }

        private static BitmapSource Build(int size, Func<int, (byte a, byte r, byte g, byte b)> px)
        {
            var bgra = new byte[size * size * 4];
            for (int i = 0; i < size * size; i++)
            {
                var (a, r, g, b) = px(i);
                bgra[i * 4] = b; bgra[i * 4 + 1] = g; bgra[i * 4 + 2] = r; bgra[i * 4 + 3] = a;
            }
            var src = BitmapSource.Create(size, size, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, bgra, size * 4);
            src.Freeze();
            return src;
        }

        /// <summary>1.8.5: decodifica un PNG (de memoria) de la forma más tolerante posible. Null si no se puede.</summary>
        private static bool _pngLogged;
        private static BitmapSource? DecodePng(byte[] png, int decodeWidth)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = new MemoryStream(png);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.IgnoreColorProfile;
                if (decodeWidth > 0) bi.DecodePixelWidth = decodeWidth;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch (Exception ex) { if (!_pngLogged) { _pngLogged = true; Log.Write("Ícono .icns (PNG, se usa el decodificador alternativo): " + ex.Message); } }
            try
            {
                var dec = new PngBitmapDecoder(new MemoryStream(png), BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var f = dec.Frames[0];
                var copy = new WriteableBitmap(f); // copia propia de los píxeles (sin metadatos)
                copy.Freeze();
                return copy;
            }
            catch (Exception ex) { Log.Write("Ícono .icns (PNG directo): " + ex.Message); return null; }
        }

        /// <summary>Tipos de entrada que trae el archivo (para el registro cuando no se puede leer).</summary>
        public static string Describe(byte[] data) => string.Join(", ", Entries(data).ConvertAll(e => e.type));

        /// <summary>Carga el .icns al tamaño pedido (en píxeles). Null si no tiene imágenes legibles o falla.</summary>
        public static BitmapSource? Load(string path, int sizePx)
        {
            try
            {
                var data = File.ReadAllBytes(path);
                var pick = BestPng(data, Math.Max(16, sizePx));
                if (pick == null)
                {
                    var old = DecodeOld(data); // 1.8.4
                    if (old == null) Log.Write("Ícono .icns sin imágenes legibles (" + Describe(data) + "): " + path);
                    return old;
                }
                // 1.8.5: el PNG se copia a su propio arreglo (sin "corrimiento" dentro del archivo) y se decodifica
                // ignorando los metadatos (algunos .icns traen un bloque eXIf que a Windows no le gusta); si igual
                // falla, se prueba con el decodificador de PNG directo y, si no, con otro tamaño del mismo archivo.
                var png = new byte[pick.Value.length];
                Buffer.BlockCopy(data, pick.Value.offset, png, 0, png.Length);
                var img = DecodePng(png, PngWidth(data, pick.Value.offset) > sizePx * 1.5 ? Math.Max(16, sizePx) : 0);
                if (img != null) return img;
                foreach (var (_, off, len) in Entries(data))
                {
                    if (off == pick.Value.offset || !IsPng(data, off, len)) continue;
                    var other = new byte[len];
                    Buffer.BlockCopy(data, off, other, 0, len);
                    img = DecodePng(other, 0);
                    if (img != null) return img;
                }
                Log.Write("Ícono .icns: Windows no pudo abrir los PNG de " + path + " (" + Describe(data) + ")");
                return DecodeOld(data);
            }
            catch (Exception ex) { Log.Write("Ícono .icns: " + ex.Message); return null; }
        }
    }
}
