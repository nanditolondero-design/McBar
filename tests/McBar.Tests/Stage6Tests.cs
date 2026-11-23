// McBar — Creado por Andrés Londero
// Pruebas de la etapa 6 (0.6.0): buscador (calculadora y archivos) y teclas de volumen.
using System;
using System.Linq;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage6Tests
    {
        [Theory]
        [InlineData("12*3+4", 40)]
        [InlineData("(2,5+1)^2", 12.25)]
        [InlineData("15% de 300", 45)]
        [InlineData("raiz(81)", 9)]
        [InlineData("2^10", 1024)]
        [InlineData("1.234,5 + 0,5", 1235)]
        [InlineData("7 x 6", 42)]
        [InlineData("-3 + 10 / 4", -0.5)]
        public void Calculadora_Cuentas(string expr, double expected)
        {
            Assert.True(Calc.LooksLikeMath(expr));
            Assert.True(Calc.TryEval(expr, out var v));
            Assert.Equal(expected, v, 6);
        }

        [Theory]
        [InlineData("chrome")]
        [InlineData("word 2016")]
        [InlineData("1")]
        public void Calculadora_NoConfundeConTexto(string text)
        {
            Assert.False(Calc.LooksLikeMath(text) && Calc.TryEval(text, out _));
        }

        [Fact]
        public void Calculadora_FormatoArgentino()
        {
            Assert.Equal("1.234", Calc.Format(1234));
            Assert.Equal("2,5", Calc.Format(2.5));
        }

        [Fact]
        public void Archivos_PrimeroLosQueEmpiezanConLoBuscado()
        {
            var now = new DateTime(2026, 11, 22);
            var items = new[]
            {
                new FileHit { Name = "Informe final.docx", Modified = now.AddDays(-5) },
                new FileHit { Name = "Mi informe.pdf", Modified = now },
                new FileHit { Name = "Transformers.mp4", Modified = now },
                new FileHit { Name = "Notas.txt", Modified = now }
            };
            var r = FileIndex.Rank(items, "inform", 10);
            Assert.Equal(new[] { "Informe final.docx", "Mi informe.pdf" }, r.Select(x => x.Name));
            Assert.Empty(FileIndex.Rank(items, "x", 10)); // muy corto
        }

        [Fact]
        public void Volumen_PasosDeADos()
        {
            Assert.Equal(0.52f, VolumeKeys.Next(0.50f, true), 3);
            Assert.Equal(0.48f, VolumeKeys.Next(0.50f, false), 3);
            Assert.Equal(1f, VolumeKeys.Next(0.99f, true), 3);
            Assert.Equal(0f, VolumeKeys.Next(0.01f, false), 3);
        }
    }
}
