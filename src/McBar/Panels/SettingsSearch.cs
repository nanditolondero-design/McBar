// McBar — Creado por Andrés Londero
// 1.9.55: buscador de Preferencias (compara sin mayúsculas ni tildes).
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace McBar.Panels
{
    internal static class SettingsSearch
    {
        /// <summary>Minúsculas y sin tildes: «Batería» y «bateria» son lo mismo. Pura, se prueba.</summary>
        public static string Fold(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var d = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (var c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().ToLowerInvariant();
        }

        /// <summary>¿El texto contiene todas las palabras de la búsqueda (en cualquier orden)? Pura, se prueba.</summary>
        public static bool Matches(string? text, string? query)
        {
            var q = Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (q.Length == 0) return false;
            var t = Fold(text);
            return q.All(w => t.Contains(w, StringComparison.Ordinal));
        }
    }
}
