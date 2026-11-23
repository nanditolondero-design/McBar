// McBar — Creado por Andrés Londero
// Datos de los íconos de la bandeja del sistema (WhatsApp, antivirus, etc.).
// Las apps los mandan con Shell_NotifyIcon: Windows le envía a la ventana "Shell_TrayWnd" un mensaje
// WM_COPYDATA con este formato (siempre en formato de 32 bits, aunque la app sea de 64):
//   0  firma 0x34753423      4  mensaje (NIM_ADD, NIM_MODIFY, NIM_DELETE, NIM_SETVERSION)
//   8  cbSize               12  hWnd     16 uID     20 uFlags    24 uCallbackMessage   28 hIcon
//   32 szTip (128 WCHAR)    288 dwState  292 dwStateMask  296 szInfo (256 WCHAR)
//   808 uVersion/uTimeout   812 szInfoTitle (64 WCHAR)    940 dwInfoFlags  944 guidItem   960 hBalloonIcon
using System;
using System.Text;
using System.Windows.Media;

namespace McBar.Core
{
    public sealed class TrayMessage
    {
        public uint Message { get; init; }
        public uint HWnd { get; init; }
        public uint Uid { get; init; }
        public uint Flags { get; init; }
        public uint CallbackMessage { get; init; }
        public uint HIcon { get; init; }
        public string? Tip { get; init; }
        public uint State { get; init; }
        public uint StateMask { get; init; }
        public uint Version { get; init; }
        public Guid Guid { get; init; }
    }

    public sealed class TrayIcon
    {
        public string Id { get; init; } = "";
        public IntPtr HWnd { get; set; }
        public uint Uid { get; set; }
        public Guid Guid { get; set; }
        public uint CallbackMessage { get; set; }
        public uint Version { get; set; }
        public string Tooltip { get; set; } = "";
        public ImageSource? Icon { get; set; }
        public bool Hidden { get; set; }
        public string ExePath { get; set; } = "";

        /// <summary>
        /// Íconos que pone el propio Windows (volumen, red, batería del Explorador). No se muestran: McBar
        /// tiene los suyos, y tocarlos abría paneles de Windows anclados a la barra escondida que trababan todo.
        /// </summary>
        public bool IsWindowsSystem => ExePath.EndsWith("\\explorer.exe", StringComparison.OrdinalIgnoreCase);

        /// <summary>Identificador que se mantiene entre reinicios (para recordar cuáles ocultar).</summary>
        public string StableId => Guid != Guid.Empty ? "g:" + Guid.ToString("N") : (ExePath.ToLowerInvariant() + "|" + Uid);
    }

    public static class TrayData
    {
        public const uint Signature = 0x34753423;
        public const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETFOCUS = 3, NIM_SETVERSION = 4;
        public const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_STATE = 0x8, NIF_INFO = 0x10, NIF_GUID = 0x20;
        public const uint NIS_HIDDEN = 0x1;

        /// <summary>Interpreta el bloque de datos. Devuelve null si no es un mensaje de bandeja válido.</summary>
        public static TrayMessage? Parse(byte[] d)
        {
            if (d == null || d.Length < 32 || U32(d, 0) != Signature) return null;
            return new TrayMessage
            {
                Message = U32(d, 4),
                HWnd = U32(d, 12),
                Uid = U32(d, 16),
                Flags = U32(d, 20),
                CallbackMessage = U32(d, 24),
                HIcon = U32(d, 28),
                Tip = d.Length >= 32 + 256 ? Str(d, 32, 128) : null,
                State = d.Length >= 292 ? U32(d, 288) : 0,
                StateMask = d.Length >= 296 ? U32(d, 292) : 0,
                Version = d.Length >= 812 ? U32(d, 808) : 0,
                Guid = d.Length >= 960 ? new Guid(new ReadOnlySpan<byte>(d, 944, 16)) : Guid.Empty
            };
        }

        /// <summary>Identificador de un ícono: por GUID si la app usa uno, si no por ventana + número.</summary>
        public static string IdOf(TrayMessage m) =>
            (m.Flags & NIF_GUID) != 0 && m.Guid != Guid.Empty ? "g:" + m.Guid.ToString("N") : $"h:{m.HWnd:X}:{m.Uid}";

        public static uint U32(byte[] d, int offset) => BitConverter.ToUInt32(d, offset);

        public static string Str(byte[] d, int offset, int maxChars)
        {
            var s = Encoding.Unicode.GetString(d, offset, maxChars * 2);
            int z = s.IndexOf('\0');
            return z >= 0 ? s.Substring(0, z) : s;
        }
    }
}
