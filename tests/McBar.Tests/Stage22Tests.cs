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
        public void EntradaCortadaNoRompe()
        {
            var f = Icns(Entry("ic07", FakePng(128)));
            Array.Resize(ref f, f.Length - 10); // archivo incompleto
            Assert.Null(McBar.Core.Icns.BestPng(f, 64));
        }
    }
}
