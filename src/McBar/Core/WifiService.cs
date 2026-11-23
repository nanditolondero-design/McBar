// McBar — Creado por Andrés Londero
// Wi-Fi propio (0.7.0) con la API de Windows (Windows.Devices.WiFi): buscar redes, conectarse (con
// contraseña si hace falta), desconectarse y prender/apagar el Wi-Fi y el Bluetooth (Windows.Devices.Radios).
// Nota: desde Windows 11 24H2, Windows pide permiso de Ubicación para que una app vea los nombres de las
// redes. Si la lista viene vacía, McBar ofrece abrir esa opción.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Radios;
using Windows.Devices.WiFi;
using Windows.Security.Credentials;

namespace McBar.Core
{
    public sealed class WifiNet
    {
        public string Ssid { get; init; } = "";
        public int Bars { get; init; }
        public bool Secure { get; init; }
        public bool Connected { get; init; }
    }

    public enum WifiResult { Ok, NeedsPassword, Error, NoAdapter }

    public static class WifiService
    {
        private static WiFiAdapter? _adapter;

        private static async Task<WiFiAdapter?> Adapter()
        {
            if (_adapter != null) return _adapter;
            try
            {
                var access = await WiFiAdapter.RequestAccessAsync();
                if (access != WiFiAccessStatus.Allowed) { Log.Write("Wi-Fi: acceso " + access); return null; }
                var list = await WiFiAdapter.FindAllAdaptersAsync();
                _adapter = list.FirstOrDefault();
            }
            catch (Exception ex) { Log.Write("Wi-Fi: " + ex.Message); }
            return _adapter;
        }

        public static async Task<bool> HasAdapterAsync() => await Adapter() != null;

        /// <summary>Redes cercanas: la conectada primero, después por señal; sin repetidos ni ocultas.</summary>
        public static async Task<List<WifiNet>> ScanAsync()
        {
            var ad = await Adapter();
            if (ad == null) return new List<WifiNet>();
            try { await ad.ScanAsync(); } catch (Exception ex) { Log.Write("Wi-Fi buscar: " + ex.Message); }
            string? current = await ConnectedSsidAsync(ad);
            var raw = ad.NetworkReport.AvailableNetworks
                .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
                .Select(n => new WifiNet
                {
                    Ssid = n.Ssid,
                    Bars = n.SignalBars,
                    Secure = n.SecuritySettings.NetworkAuthenticationType != Windows.Networking.Connectivity.NetworkAuthenticationType.Open80211
                             && n.SecuritySettings.NetworkAuthenticationType != Windows.Networking.Connectivity.NetworkAuthenticationType.None,
                    Connected = n.Ssid == current
                });
            return Arrange(raw);
        }

        /// <summary>Saca repetidos (mismo nombre: se queda con la mejor señal) y ordena. Pura, se prueba.</summary>
        public static List<WifiNet> Arrange(IEnumerable<WifiNet> nets) =>
            nets.GroupBy(n => n.Ssid)
                .Select(g => g.OrderByDescending(n => n.Connected).ThenByDescending(n => n.Bars).First())
                .OrderByDescending(n => n.Connected)
                .ThenByDescending(n => n.Bars)
                .ThenBy(n => n.Ssid, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        private static async Task<string?> ConnectedSsidAsync(WiFiAdapter ad)
        {
            try
            {
                var p = await ad.NetworkAdapter.GetConnectedProfileAsync();
                return p?.WlanConnectionProfileDetails?.GetConnectedSsid();
            }
            catch { return null; }
        }

        public static async Task<WifiResult> ConnectAsync(string ssid, string? password)
        {
            var ad = await Adapter();
            if (ad == null) return WifiResult.NoAdapter;
            try
            {
                var net = ad.NetworkReport.AvailableNetworks.FirstOrDefault(n => n.Ssid == ssid);
                if (net == null) { await ad.ScanAsync(); net = ad.NetworkReport.AvailableNetworks.FirstOrDefault(n => n.Ssid == ssid); }
                if (net == null) return WifiResult.Error;
                WiFiConnectionResult r = string.IsNullOrEmpty(password)
                    ? await ad.ConnectAsync(net, WiFiReconnectionKind.Automatic)
                    : await ad.ConnectAsync(net, WiFiReconnectionKind.Automatic, new PasswordCredential { Password = password });
                Log.Write($"Wi-Fi conectar {ssid}: {r.ConnectionStatus}");
                return r.ConnectionStatus switch
                {
                    WiFiConnectionStatus.Success => WifiResult.Ok,
                    WiFiConnectionStatus.InvalidCredential => WifiResult.NeedsPassword,
                    _ => string.IsNullOrEmpty(password) && net.SecuritySettings.NetworkAuthenticationType != Windows.Networking.Connectivity.NetworkAuthenticationType.Open80211
                        ? WifiResult.NeedsPassword : WifiResult.Error
                };
            }
            catch (Exception ex) { Log.Write("Wi-Fi conectar: " + ex.Message); return WifiResult.Error; }
        }

        public static async Task DisconnectAsync()
        {
            var ad = await Adapter();
            try { ad?.Disconnect(); } catch { }
        }

        // ---------- Prender / apagar ----------

        private static async Task<Radio?> RadioOf(RadioKind kind)
        {
            try
            {
                if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed) return null;
                return (await Radio.GetRadiosAsync()).FirstOrDefault(r => r.Kind == kind);
            }
            catch { return null; }
        }

        /// <summary>Estado de la radio: true prendida, false apagada, null si no hay.</summary>
        public static async Task<bool?> GetRadioAsync(bool bluetooth)
        {
            var r = await RadioOf(bluetooth ? RadioKind.Bluetooth : RadioKind.WiFi);
            return r == null ? null : r.State == RadioState.On;
        }

        public static async Task<bool> SetRadioAsync(bool bluetooth, bool on)
        {
            var r = await RadioOf(bluetooth ? RadioKind.Bluetooth : RadioKind.WiFi);
            if (r == null) return false;
            try { return await r.SetStateAsync(on ? RadioState.On : RadioState.Off) == RadioAccessStatus.Allowed; }
            catch (Exception ex) { Log.Write("Radio: " + ex.Message); return false; }
        }
    }
}
