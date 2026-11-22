// McBar — Creado por Andrés Londero
// Clima para el widget, usando Open-Meteo (gratis y sin clave). Se consulta como mucho cada 15 minutos
// y solo cuando el panel de widgets está abierto.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace McBar.Core
{
    public sealed class WeatherHour { public DateTime Time { get; init; } public double Temp { get; init; } public int Code { get; init; } }

    public sealed class WeatherData
    {
        public string Place { get; init; } = "";
        public double Temp { get; init; }
        public int Code { get; init; }
        public bool IsDay { get; init; } = true;
        public double Max { get; init; }
        public double Min { get; init; }
        public List<WeatherHour> Hours { get; init; } = new List<WeatherHour>();
        public DateTime FetchedAt { get; init; }
    }

    public enum WeatherKind { Clear, PartlyCloudy, Cloudy, Fog, Drizzle, Rain, Snow, Storm }

    public static class WeatherCodes
    {
        /// <summary>Tipo de ícono a dibujar para el código WMO.</summary>
        public static WeatherKind Kind(int code) => code switch
        {
            0 or 1 => WeatherKind.Clear,
            2 => WeatherKind.PartlyCloudy,
            3 => WeatherKind.Cloudy,
            45 or 48 => WeatherKind.Fog,
            >= 51 and <= 57 => WeatherKind.Drizzle,
            >= 61 and <= 67 => WeatherKind.Rain,
            >= 80 and <= 82 => WeatherKind.Rain,
            >= 71 and <= 77 => WeatherKind.Snow,
            85 or 86 => WeatherKind.Snow,
            >= 95 and <= 99 => WeatherKind.Storm,
            _ => WeatherKind.Cloudy
        };

        /// <summary>Texto en castellano y símbolo para el código WMO que devuelve Open-Meteo.</summary>
        public static (string text, string symbol) Describe(int code) => code switch
        {
            0 => ("Despejado", "☀"),
            1 => ("Mayormente despejado", "🌤"),
            2 => ("Parcialmente nublado", "⛅"),
            3 => ("Nublado", "☁"),
            45 or 48 => ("Niebla", "🌫"),
            51 or 53 or 55 or 56 or 57 => ("Llovizna", "🌦"),
            61 or 63 or 66 => ("Lluvia", "🌧"),
            65 or 67 => ("Lluvia fuerte", "🌧"),
            71 or 73 or 75 or 77 => ("Nieve", "❄"),
            80 or 81 or 82 => ("Chaparrones", "🌦"),
            85 or 86 => ("Nevadas", "❄"),
            95 or 96 or 99 => ("Tormenta", "⛈"),
            _ => ("—", "☁")
        };
    }

    public static class WeatherService
    {
        private static HttpClient? _http;
        private static WeatherData? _cache;

        /// <summary>Se dispara cuando llega un dato nuevo (la barra de menú actualiza su ícono).</summary>
        public static event Action<WeatherData>? Updated;

        private static HttpClient Http => _http ??= new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public static async Task<WeatherData?> GetAsync()
        {
            var s = SettingsStore.Current;
            if (_cache != null && (DateTime.Now - _cache.FetchedAt).TotalMinutes < 15 && s.WeatherResolvedFor == s.WeatherCity)
                return _cache;
            try
            {
                if (s.WeatherLat == null || s.WeatherLon == null || s.WeatherResolvedFor != s.WeatherCity)
                {
                    if (!await ResolveCityAsync(s.WeatherCity)) return null;
                }
                var inv = CultureInfo.InvariantCulture;
                var url = "https://api.open-meteo.com/v1/forecast?latitude=" + s.WeatherLat!.Value.ToString(inv) +
                          "&longitude=" + s.WeatherLon!.Value.ToString(inv) +
                          "&current=temperature_2m,weather_code,is_day&hourly=temperature_2m,weather_code" +
                          "&daily=temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=2";
                using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
                _cache = Parse(doc.RootElement, s.WeatherPlaceName ?? s.WeatherCity, DateTime.Now);
                Updated?.Invoke(_cache);
                return _cache;
            }
            catch (Exception ex)
            {
                Log.Write("Clima: " + ex.Message);
                return _cache;
            }
        }

        private static async Task<bool> ResolveCityAsync(string city)
        {
            if (string.IsNullOrWhiteSpace(city)) return false;
            var url = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=es&name=" + Uri.EscapeDataString(city.Trim());
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return false;
            var r = results[0];
            var s = SettingsStore.Current;
            s.WeatherLat = r.GetProperty("latitude").GetDouble();
            s.WeatherLon = r.GetProperty("longitude").GetDouble();
            s.WeatherPlaceName = r.TryGetProperty("name", out var n) ? n.GetString() : city;
            s.WeatherResolvedFor = city;
            SettingsStore.Save();
            return true;
        }

        /// <summary>Interpreta la respuesta de Open-Meteo (separado para poder probarlo).</summary>
        public static WeatherData Parse(JsonElement root, string place, DateTime now)
        {
            var cur = root.GetProperty("current");
            var daily = root.GetProperty("daily");
            var hourly = root.GetProperty("hourly");
            var hours = new List<WeatherHour>();
            var times = hourly.GetProperty("time");
            var temps = hourly.GetProperty("temperature_2m");
            var codes = hourly.GetProperty("weather_code");
            for (int i = 0; i < times.GetArrayLength() && hours.Count < 6; i++)
            {
                var t = DateTime.Parse(times[i].GetString()!, CultureInfo.InvariantCulture);
                if (t < now.AddMinutes(-30)) continue;
                hours.Add(new WeatherHour { Time = t, Temp = temps[i].GetDouble(), Code = codes[i].GetInt32() });
            }
            return new WeatherData
            {
                Place = place,
                Temp = cur.GetProperty("temperature_2m").GetDouble(),
                Code = cur.GetProperty("weather_code").GetInt32(),
                IsDay = !cur.TryGetProperty("is_day", out var day) || day.GetInt32() != 0,
                Max = daily.GetProperty("temperature_2m_max")[0].GetDouble(),
                Min = daily.GetProperty("temperature_2m_min")[0].GetDouble(),
                Hours = hours,
                FetchedAt = now
            };
        }
    }
}
