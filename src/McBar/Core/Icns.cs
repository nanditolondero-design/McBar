// McBar — Creado por Andrés Londero
// 1.8.2 — Íconos .icns de macOS (pedido de Andrés). Un .icns es un contenedor: "icns" + largo, y adentro
// varias entradas (tipo de 4 letras + largo + datos) con el mismo ícono en distintos tamaños. En los .icns
// modernos (macOS 10.7 o después, y los de macosicons.com) esas imágenes son PNG comunes: McBar busca la que
// mejor le queda al tamaño pedido y la usa como cualquier otro ícono. Los formatos viejos de adentro
// (RLE de Mac OS 9 / 10.6, JPEG 2000) no se leen: en ese caso queda el ícono original.
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

        /// <summary>Carga el .icns al tamaño pedido (en píxeles). Null si no tiene imágenes PNG o falla.</summary>
        public static BitmapSource? Load(string path, int sizePx)
        {
            try
            {
                var data = File.ReadAllBytes(path);
                var pick = BestPng(data, Math.Max(16, sizePx));
                if (pick == null) { Log.Write("Ícono .icns sin imágenes PNG (formato viejo): " + path); return null; }
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = new MemoryStream(data, pick.Value.offset, pick.Value.length, false);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                if (PngWidth(data, pick.Value.offset) > sizePx * 1.5) bi.DecodePixelWidth = Math.Max(16, sizePx);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch (Exception ex) { Log.Write("Ícono .icns: " + ex.Message); return null; }
        }
    }
}
