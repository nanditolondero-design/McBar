// McBar — Creado por Andrés Londero
// Datos del sistema para la barra y los widgets: batería, CPU, memoria, red, papelera, brillo, tema.
using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using McBar.Native;

namespace McBar.Core
{
    public readonly struct BatteryState
    {
        public bool HasBattery { get; init; }
        public int Percent { get; init; }
        public bool Charging { get; init; }
    }

    public enum NetKind { None, Wifi, Ethernet, Other }

    public static class SystemInfo
    {
        // ---------- Batería ----------
        public static BatteryState Battery()
        {
            if (!Win32.GetSystemPowerStatus(out var s)) return new BatteryState();
            bool has = s.BatteryFlag != 255 && (s.BatteryFlag & 128) == 0;
            int pct = s.BatteryLifePercent == 255 ? 100 : s.BatteryLifePercent;
            return new BatteryState { HasBattery = has, Percent = pct, Charging = s.ACLineStatus == 1 };
        }

        // ---------- CPU y memoria ----------
        private static long _lastIdle, _lastTotal;

        /// <summary>Uso de CPU (0..100) desde la llamada anterior.</summary>
        public static int CpuPercent()
        {
            if (!Win32.GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
            long total = kernel + user; // kernel incluye el tiempo ocioso
            long dIdle = idle - _lastIdle, dTotal = total - _lastTotal;
            _lastIdle = idle; _lastTotal = total;
            if (dTotal <= 0) return 0;
            return (int)Math.Round(Math.Clamp(100.0 * (1.0 - (double)dIdle / dTotal), 0, 100));
        }

        public static (int percent, double usedGb, double totalGb) Memory()
        {
            var m = new Win32.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<Win32.MEMORYSTATUSEX>() };
            if (!Win32.GlobalMemoryStatusEx(ref m)) return (0, 0, 0);
            double total = m.ullTotalPhys / 1073741824.0, used = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
            return ((int)m.dwMemoryLoad, used, total);
        }

        // ---------- Red ----------
        public static (NetKind kind, string name) Network()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    var props = ni.GetIPProperties();
                    if (!props.GatewayAddresses.Any()) continue;
                    var kind = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? NetKind.Wifi
                             : ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? NetKind.Ethernet : NetKind.Other;
                    return (kind, ni.Name);
                }
            }
            catch { }
            return (NetKind.None, "");
        }

        // ---------- Papelera ----------
        public static bool RecycleBinHasItems()
        {
            var info = new Win32.SHQUERYRBINFO { cbSize = Marshal.SizeOf<Win32.SHQUERYRBINFO>() };
            return Win32.SHQueryRecycleBin(null, ref info) == 0 && info.i64NumItems > 0;
        }

        public static void EmptyRecycleBin()
        {
            // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI (la confirmación la pide McBar con su propio diálogo)
            Taskbar.EnforceBurst(4); // vaciar la papelera hace que el Explorador muestre su barra en algún monitor
            Win32.SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4 /* sin sonido */);
            Taskbar.EnforceBurst(4);
        }

        public static bool SendToRecycleBin(string[] paths)
        {
            if (paths.Length == 0) return false;
            var op = new Win32.SHFILEOPSTRUCT
            {
                wFunc = 3, // FO_DELETE
                pFrom = string.Join("\0", paths) + "\0\0",
                fFlags = 0x40 | 0x10 | 0x4 // FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
            };
            Taskbar.EnforceBurst(3);
            bool ok = Win32.SHFileOperation(ref op) == 0;
            Taskbar.EnforceBurst(3);
            return ok;
        }

        // ---------- Brillo (solo pantallas que lo permiten, normalmente notebooks) ----------
        public static int? GetBrightness()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
                foreach (ManagementObject o in searcher.Get())
                    using (o) return Convert.ToInt32(o["CurrentBrightness"]);
            }
            catch { }
            return null;
        }

        public static void SetBrightness(int value)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
                foreach (ManagementObject o in searcher.Get())
                    using (o) o.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)Math.Clamp(value, 0, 100) });
            }
            catch { }
        }

        // ---------- Tema de Windows ----------
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static bool WindowsIsDark()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
                var v = k?.GetValue("SystemUsesLightTheme");
                return v is int i && i == 0;
            }
            catch { return true; }
        }

        /// <summary>Cambia el modo oscuro de Windows (apps y sistema), como el interruptor del Centro de control de macOS.</summary>
        public static void SetWindowsDark(bool dark)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(PersonalizeKey);
                k.SetValue("AppsUseLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
                k.SetValue("SystemUsesLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
                Win32.SendMessageTimeoutString(Win32.HWND_BROADCAST, Win32.WM_SETTINGCHANGE, IntPtr.Zero, "ImmersiveColorSet", Win32.SMTO_ABORTIFHUNG, 200, out _);
            }
            catch (Exception ex) { Log.Write("Modo oscuro: " + ex.Message); }
        }

        // ---------- Inicio con Windows ----------
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        // 1.9.1: Windows espera unos segundos antes de abrir los programas de inicio (para que el escritorio
        // aparezca rápido). Mientras tanto se ve su barra de tareas. Esta clave (del usuario, sin administrador)
        // saca esa espera. Se guarda lo que había (inicio-anterior.json) y se devuelve al apagar la opción.
        private const string SerializeKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize";
        private const string FastBackup = "inicio-anterior.json";

        public static void SetFastStartup(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    RegBackup.Save(FastBackup, new[] { (SerializeKey, "StartupDelayInMSec"), (SerializeKey, "WaitForIdleState") });
                    using var k = Registry.CurrentUser.CreateSubKey(SerializeKey, true);
                    k?.SetValue("StartupDelayInMSec", 0, RegistryValueKind.DWord);
                    k?.SetValue("WaitForIdleState", 0, RegistryValueKind.DWord);
                }
                else RegBackup.Restore(FastBackup);
            }
            catch (Exception ex) { Log.Write("Inicio rápido: " + ex.Message); }
        }

        public static void SetStartWithWindows(bool enabled)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(RunKey);
                if (enabled && !string.IsNullOrEmpty(Environment.ProcessPath)) k.SetValue("McBar", "\"" + Environment.ProcessPath + "\"");
                else k.DeleteValue("McBar", false);
            }
            catch (Exception ex) { Log.Write("Inicio con Windows: " + ex.Message); }
        }
    }
}
