// McBar — Creado por Andrés Londero
// Bluetooth (0.7.0) con la API clásica de Windows (bthprops): lista los dispositivos vinculados, si están
// conectados y de qué tipo son (auriculares, mouse, teclado, teléfono…).
// Conectar/desconectar: Windows no tiene una función pública para "conectar" un dispositivo vinculado.
// Para los de AUDIO (auriculares, parlantes) se usa el mismo truco que otras apps: activar sus servicios de
// audio fuerza la conexión; desactivarlos la corta (al volver a conectar desde McBar se reactivan).
// Los demás (mouse, teclado) se conectan solos al usarlos: se muestra su estado.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using McBar.Core;

namespace McBar.Native
{
    public enum BtKind { Audio, Mouse, Keyboard, Phone, Computer, Other }

    public sealed class BtDevice
    {
        public string Name { get; init; } = "";
        public ulong Address { get; init; }
        public bool Connected { get; init; }
        public BtKind Kind { get; init; }
        public uint ClassOfDevice { get; init; }
    }

    internal static class BluetoothDevices
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SearchParams
        {
            public uint dwSize;
            public int fReturnAuthenticated, fReturnRemembered, fReturnUnknown, fReturnConnected, fIssueInquiry;
            public byte cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemTime { public ushort y, mo, dw, d, h, mi, s, ms; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DeviceInfo
        {
            public uint dwSize;
            public ulong Address;
            public uint ulClassofDevice;
            public int fConnected, fRemembered, fAuthenticated;
            public SystemTime stLastSeen, stLastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string szName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RadioParams { public uint dwSize; }

        [DllImport("bthprops.cpl", SetLastError = true)] private static extern IntPtr BluetoothFindFirstDevice(ref SearchParams p, ref DeviceInfo info);
        [DllImport("bthprops.cpl", SetLastError = true)] private static extern bool BluetoothFindNextDevice(IntPtr find, ref DeviceInfo info);
        [DllImport("bthprops.cpl")] private static extern bool BluetoothFindDeviceClose(IntPtr find);
        [DllImport("bthprops.cpl", SetLastError = true)] private static extern IntPtr BluetoothFindFirstRadio(ref RadioParams p, out IntPtr radio);
        [DllImport("bthprops.cpl")] private static extern bool BluetoothFindRadioClose(IntPtr find);
        [DllImport("bthprops.cpl", SetLastError = true)] private static extern uint BluetoothSetServiceState(IntPtr radio, ref DeviceInfo info, ref Guid service, uint flags);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);

        private static readonly Guid AudioSink = new Guid("0000110B-0000-1000-8000-00805F9B34FB");
        private static readonly Guid Handsfree = new Guid("0000111E-0000-1000-8000-00805F9B34FB");
        private static readonly Guid Headset = new Guid("00001108-0000-1000-8000-00805F9B34FB");

        /// <summary>Tipo según la "clase de dispositivo" Bluetooth (pura, se prueba).</summary>
        public static BtKind KindOf(uint cod)
        {
            uint major = (cod >> 8) & 0x1F;
            uint minor = (cod >> 2) & 0x3F;
            switch (major)
            {
                case 0x04: return BtKind.Audio;
                case 0x02: return BtKind.Phone;
                case 0x01: return BtKind.Computer;
                case 0x05:
                    // periférico: bits 6-7 del menor = teclado (01) / puntero (10)
                    uint p = (minor >> 4) & 0x3;
                    return p == 1 ? BtKind.Keyboard : p == 2 ? BtKind.Mouse : p == 3 ? BtKind.Keyboard : BtKind.Other;
                default: return BtKind.Other;
            }
        }

        private static DeviceInfo NewInfo() => new DeviceInfo { dwSize = (uint)Marshal.SizeOf<DeviceInfo>(), szName = "" };

        public static List<BtDevice> List()
        {
            var list = new List<BtDevice>();
            try
            {
                var sp = new SearchParams
                {
                    dwSize = (uint)Marshal.SizeOf<SearchParams>(),
                    fReturnAuthenticated = 1, fReturnRemembered = 1, fReturnConnected = 1, fReturnUnknown = 0, fIssueInquiry = 0
                };
                var info = NewInfo();
                var find = BluetoothFindFirstDevice(ref sp, ref info);
                if (find == IntPtr.Zero) return list;
                do
                {
                    if (!string.IsNullOrWhiteSpace(info.szName) && !list.Exists(d => d.Address == info.Address))
                        list.Add(new BtDevice { Name = info.szName, Address = info.Address, Connected = info.fConnected != 0, ClassOfDevice = info.ulClassofDevice, Kind = KindOf(info.ulClassofDevice) });
                    info = NewInfo();
                } while (BluetoothFindNextDevice(find, ref info));
                BluetoothFindDeviceClose(find);
            }
            catch (DllNotFoundException) { }
            catch (Exception ex) { Log.Write("Bluetooth: " + ex.Message); }
            list.Sort((a, b) => a.Connected != b.Connected ? (a.Connected ? -1 : 1) : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }

        /// <summary>¿Hay radio Bluetooth?</summary>
        public static bool HasRadio()
        {
            try
            {
                var rp = new RadioParams { dwSize = (uint)Marshal.SizeOf<RadioParams>() };
                var f = BluetoothFindFirstRadio(ref rp, out var radio);
                if (f == IntPtr.Zero) return false;
                CloseHandle(radio);
                BluetoothFindRadioClose(f);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Conecta (true) o desconecta (false) un dispositivo de audio. Devuelve si se pudo.</summary>
        public static bool SetAudioConnected(BtDevice dev, bool connect)
        {
            IntPtr f = IntPtr.Zero, radio = IntPtr.Zero;
            try
            {
                var rp = new RadioParams { dwSize = (uint)Marshal.SizeOf<RadioParams>() };
                f = BluetoothFindFirstRadio(ref rp, out radio);
                if (f == IntPtr.Zero) return false;
                var info = NewInfo();
                info.Address = dev.Address;
                info.ulClassofDevice = dev.ClassOfDevice;
                info.szName = dev.Name;
                bool ok = false;
                foreach (var g in new[] { AudioSink, Handsfree, Headset })
                {
                    var svc = g;
                    if (connect)
                    {
                        // desactivar y volver a activar fuerza a Windows a conectarse
                        BluetoothSetServiceState(radio, ref info, ref svc, 0 /*DISABLE*/);
                        ok |= BluetoothSetServiceState(radio, ref info, ref svc, 1 /*ENABLE*/) == 0;
                    }
                    else ok |= BluetoothSetServiceState(radio, ref info, ref svc, 0) == 0;
                }
                return ok;
            }
            catch (Exception ex) { Log.Write("Bluetooth conectar: " + ex.Message); return false; }
            finally
            {
                if (radio != IntPtr.Zero) CloseHandle(radio);
                if (f != IntPtr.Zero) BluetoothFindRadioClose(f);
            }
        }
    }
}
