// McBar — Creado por Andrés Londero
// 1.9.34: reglas puras (sin Windows) para la bandeja leída por UI Automation. Están separadas de Native/TrayUia.cs
// para poder probarlas en los tests: qué botones de la bandeja de Windows 11 son íconos de apps y cuáles son
// controles del propio Windows, cómo se limpia el nombre, y cuándo dos nombres son "el mismo" ícono.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace McBar.Core
{
    public static class TrayUiaRules
    {
        // En Windows 11 (22H2 en adelante) cada ícono de app de la bandeja es un botón XAML con esta clase / este Id.
        public const string IconClass = "SystemTray.NormalButton";
        public const string IconAutomationId = "SystemTrayIcon";

        // Controles del propio Windows dentro de la misma bandeja (reloj, ajustes rápidos, centro de notificaciones,
        // flechita ^). McBar tiene los suyos, así que no se importan.
        private static readonly Regex SystemName = new Regex(
            @"^\s*(volume|volumen|sound|sonido|speakers|altavoces|battery|bater[ií]a|wi-?fi|internet|clock|reloj|" +
            @"notification center|centro de notificaciones|show hidden icons|mostrar iconos ocultos|mostrar [ií]conos ocultos|" +
            @"input indicator|indicador de entrada|safely remove hardware|quitar hardware)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly string[] SystemIdParts = { "Chevron", "Clock", "ControlCenter", "NotificationCenter", "Omni", "Show Desktop", "ShowDesktop" };

        /// <summary>¿Este botón de la bandeja es el ícono de una app (y no un control de Windows)?</summary>
        public static bool IsAppIcon(string? name, string? automationId, string? className)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            className ??= ""; automationId ??= "";
            if (className.IndexOf("Omni", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (className.IndexOf("Chevron", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            foreach (var p in SystemIdParts)
                if (automationId.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !string.Equals(automationId, IconAutomationId, StringComparison.OrdinalIgnoreCase)) return false;
            if (SystemName.IsMatch(name)) return false;
            bool looksLikeTrayButton = string.Equals(automationId, IconAutomationId, StringComparison.OrdinalIgnoreCase)
                                    || className.StartsWith("SystemTray.", StringComparison.OrdinalIgnoreCase);
            return looksLikeTrayButton;
        }

        /// <summary>Primera línea del nombre, sin espacios sobrantes (Windows a veces agrega el estado en otra línea).</summary>
        public static string CleanName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var first = name.Replace("\r", "").Split('\n')[0].Trim();
            return first;
        }

        /// <summary>Clave estable para comparar nombres (minúsculas, sin sufijos que agrega Windows).</summary>
        public static string Normalize(string? name)
        {
            var s = CleanName(name).ToLowerInvariant();
            foreach (var suffix in new[] { " notification icon", " ícono de notificación", " icono de notificacion", " icono de notificación" })
                if (s.EndsWith(suffix, StringComparison.Ordinal)) s = s.Substring(0, s.Length - suffix.Length);
            return s.Trim();
        }

        /// <summary>¿Dos nombres de ícono son el mismo? (igual, o uno contiene al otro cuando es lo bastante largo).</summary>
        public static bool SameIcon(string? a, string? b)
        {
            var x = Normalize(a); var y = Normalize(b);
            if (x.Length == 0 || y.Length == 0) return false;
            if (x == y) return true;
            if (x.Length >= 5 && y.Length >= 5) return x.Contains(y, StringComparison.Ordinal) || y.Contains(x, StringComparison.Ordinal);
            return false;
        }

        private static readonly HashSet<string> Stop = new HashSet<string>(StringComparer.Ordinal)
        {
            "windows","notification","icon","settings","the","and","for","app","application","service","control","panel",
            "center","centre","manager","tool","tools","with","from","this","that","corporation","inc","ltd","microsoft"
        };

        /// <summary>Palabras significativas de un nombre (para adivinar a qué programa pertenece el ícono).</summary>
        public static List<string> Tokens(string? text)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            foreach (Match m in Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+"))
            {
                var t = m.Value;
                if (t.Length >= 3 && !Stop.Contains(t) && !list.Contains(t)) list.Add(t);
            }
            return list;
        }

        /// <summary>Cuántas palabras del nombre del ícono aparecen en los datos de un programa (nombre, producto, descripción).</summary>
        public static int Score(string? iconName, params string?[] programInfo)
        {
            var want = Tokens(CleanName(iconName));
            if (want.Count == 0) return 0;
            var have = new HashSet<string>(programInfo.SelectMany(Tokens));
            return want.Count(have.Contains);
        }
    }
}
