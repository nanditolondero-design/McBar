// McBar — Creado por Andrés Londero
// Pruebas de la 1.8.2: lectura de íconos .icns de macOS.
using System;
using System.Collections.Generic;
using System.Linq;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage22Tests
    {
        private static byte[] BE(int v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

        /// <summary>Un "PNG" mínimo: firma + encabezado IHDR con el ancho (alcanza para elegir).</summary>
        private static byte[] FakePng(int w)
        {
            var b = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            b.AddRange(BE(13)); b.AddRange("IHDR"u8.ToArray()); b.AddRange(BE(w)); b.AddRange(BE(w));
            b.AddRange(new byte[9]);
            return b.ToArray();
        }

        private static byte[] Entry(string type, byte[] data) =>
            System.Text.Encoding.ASCII.GetBytes(type).Concat(BE(data.Length + 8)).Concat(data).ToArray();

        private static byte[] Icns(params byte[][] entries)
        {
            var body = entries.SelectMany(e => e).ToArray();
            return "icns"u8.ToArray().Concat(BE(body.Length + 8)).Concat(body).ToArray();
        }

        [Fact]
        public void LeeLasEntradas()
        {
            var f = Icns(Entry("ic07", FakePng(128)), Entry("it32", new byte[40]), Entry("ic10", FakePng(1024)));
            var e = McBar.Core.Icns.Entries(f);
            Assert.Equal(new[] { "ic07", "it32", "ic10" }, e.Select(x => x.type).ToArray());
        }

        [Fact]
        public void EligeElPngJusto()
        {
            var f = Icns(Entry("ic07", FakePng(128)), Entry("ic08", FakePng(256)), Entry("ic10", FakePng(1024)));
            var pick = McBar.Core.Icns.BestPng(f, 200)!.Value;
            Assert.Equal(256, McBar.Core.Icns.PngWidth(f, pick.offset));        // el más chico que alcanza
            pick = McBar.Core.Icns.BestPng(f, 2000)!.Value;
            Assert.Equal(1024, McBar.Core.Icns.PngWidth(f, pick.offset));       // si ninguno alcanza, el más grande
        }

        [Fact]
        public void FormatoViejoONoIcns_Null()
        {
            Assert.Null(McBar.Core.Icns.BestPng(Icns(Entry("it32", new byte[40])), 64)); // solo RLE viejo
            Assert.Null(McBar.Core.Icns.BestPng(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 64));
            Assert.Empty(McBar.Core.Icns.Entries(Array.Empty<byte>()));
        }

        [Fact]
        public void Rle_Descomprime()
        {
            // 0x02 = copiar 3 bytes (1,2,3); 0x81 = repetir 4 veces el 9
            var d = new byte[] { 0x02, 1, 2, 3, 0x81, 9 };
            Assert.Equal(new byte[] { 1, 2, 3, 9, 9, 9, 9 }, McBar.Core.Icns.UnRle(d, 0, d.Length, 1, 7));
            Assert.Null(McBar.Core.Icns.UnRle(d, 0, d.Length, 1, 20)); // le faltan datos
        }

        private static byte[] RleChannel(byte v, int count)
        {
            var l = new List<byte>();
            int left = count;
            while (left > 0)
            {
                int c = Math.Min(130, left);
                if (c < 3) { l.Add((byte)(c - 1)); for (int i = 0; i < c; i++) l.Add(v); }
                else { l.Add((byte)(0x80 + c - 3)); l.Add(v); }
                left -= c;
            }
            return l.ToArray();
        }

        [Fact]
        public void FormatoViejo_Is32ConMascara()
        {
            var rgb = RleChannel(200, 256).Concat(RleChannel(10, 256)).Concat(RleChannel(30, 256)).ToArray();
            var mask = Enumerable.Repeat((byte)255, 256).ToArray();
            var f = Icns(Entry("is32", rgb), Entry("s8mk", mask));
            Assert.Null(McBar.Core.Icns.BestPng(f, 64));
            var img = McBar.Core.Icns.DecodeOld(f);
            Assert.NotNull(img);
            Assert.Equal(16, img!.PixelWidth);
            var px = new byte[4];
            img.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), px, 4, 0);
            Assert.Equal(new byte[] { 30, 10, 200, 255 }, px); // BGRA
        }

        [Fact]
        public void EntradaCortadaNoRompe()
        {
            var f = Icns(Entry("ic07", FakePng(128)));
            Array.Resize(ref f, f.Length - 10); // archivo incompleto
            Assert.Null(McBar.Core.Icns.BestPng(f, 64));
        }
    }
}
