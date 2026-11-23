// McBar — Creado por Andrés Londero
// Calculadora del buscador (0.6.0): "12*3+4", "(2,5+1)^2", "15% de 300", "raiz(81)", "2^10".
// Acepta coma o punto decimal. Analizador recursivo propio (sin evaluar código). Pura, se prueba.
using System;
using System.Globalization;

namespace McBar.Core
{
    public static class Calc
    {
        /// <summary>¿Parece una cuenta? (tiene al menos un número y un operador o función)</summary>
        public static bool LooksLikeMath(string s)
        {
            s = s.Trim();
            if (s.Length < 2) return false;
            bool digit = false, op = false;
            foreach (var c in s)
            {
                if (char.IsDigit(c)) digit = true;
                else if ("+-*/^%x×÷()".IndexOf(c) >= 0) op = true;
                else if (char.IsLetter(c) || c == ',' || c == '.' || c == ' ') continue;
                else return false;
            }
            return digit && (op || s.StartsWith("raiz", StringComparison.OrdinalIgnoreCase) || s.StartsWith("sqrt", StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryEval(string expr, out double result)
        {
            result = 0;
            try
            {
                var e = Normalize(expr);
                if (e.Length == 0) return false;
                var p = new Parser(e);
                result = p.Expr();
                p.Skip();
                return p.Done && !double.IsNaN(result) && !double.IsInfinity(result);
            }
            catch { return false; }
        }

        /// <summary>"1.234,5" o "1234.5" → número; "x"/"×" → "*"; "÷" → "/"; "de" → "*" (para "15% de 300").</summary>
        private static string Normalize(string s)
        {
            s = s.Trim().ToLowerInvariant().Replace("×", "*").Replace("÷", "/").Replace(" de ", "*").Replace("raiz", "r").Replace("sqrt", "r");
            // "2x3" → "2*3" (solo una x entre números)
            var chars = s.ToCharArray();
            for (int i = 1; i < chars.Length - 1; i++)
                if (chars[i] == 'x' && (char.IsDigit(chars[i - 1]) || chars[i - 1] == ' ' || chars[i - 1] == ')') && (char.IsDigit(chars[i + 1]) || chars[i + 1] == ' ' || chars[i + 1] == '('))
                    chars[i] = '*';
            return new string(chars).Replace(" ", "");
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;
            public Parser(string s) { _s = s; }
            public bool Done => _i >= _s.Length;
            public void Skip() { while (_i < _s.Length && _s[_i] == ' ') _i++; }
            private char Peek => _i < _s.Length ? _s[_i] : '\0';

            public double Expr()
            {
                double v = Term();
                while (Peek == '+' || Peek == '-')
                {
                    char op = _s[_i++];
                    double r = Term();
                    v = op == '+' ? v + r : v - r;
                }
                return v;
            }

            private double Term()
            {
                double v = Power();
                while (Peek == '*' || Peek == '/')
                {
                    char op = _s[_i++];
                    double r = Power();
                    v = op == '*' ? v * r : v / r;
                }
                return v;
            }

            private double Power()
            {
                double b = Unary();
                if (Peek == '^') { _i++; return Math.Pow(b, Power()); }
                return b;
            }

            private double Unary()
            {
                if (Peek == '-') { _i++; return -Unary(); }
                if (Peek == '+') { _i++; return Unary(); }
                return Postfix();
            }

            private double Postfix()
            {
                double v = Atom();
                while (Peek == '%') { _i++; v /= 100; }
                return v;
            }

            private double Atom()
            {
                if (Peek == '(')
                {
                    _i++;
                    double v = Expr();
                    if (Peek != ')') throw new FormatException();
                    _i++;
                    return v;
                }
                if (Peek == 'r') { _i++; return Math.Sqrt(Atom()); }
                int start = _i;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == ',' || _s[_i] == '.')) _i++;
                if (start == _i) throw new FormatException();
                return ParseNumber(_s.Substring(start, _i - start));
            }

            /// <summary>Con coma y punto, el último es el decimal ("1.234,5"); con solo coma, es decimal.</summary>
            private static double ParseNumber(string t)
            {
                int c = t.LastIndexOf(','), d = t.LastIndexOf('.');
                if (c >= 0 && d >= 0)
                {
                    if (c > d) t = t.Replace(".", "").Replace(',', '.');
                    else t = t.Replace(",", "");
                }
                else if (c >= 0) t = t.Replace(',', '.');
                return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Resultado lindo: "1.234,5" (formato argentino), sin decimales de más.</summary>
        public static string Format(double v)
        {
            var es = new CultureInfo("es-AR");
            return Math.Abs(v - Math.Round(v)) < 1e-9 && Math.Abs(v) < 1e15
                ? Math.Round(v).ToString("#,0", es)
                : v.ToString("#,0.##########", es);
        }
    }
}
