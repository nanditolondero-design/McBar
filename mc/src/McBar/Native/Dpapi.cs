// McBar — Creado por Andrés Londero
// 1.4.0 — Guarda secretos (la clave de macOSicons) cifrados con la cuenta de Windows del usuario (DPAPI):
// en settings.json queda un texto que solo se puede descifrar con ese usuario en esa PC.
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace McBar.Native
{
    public static class Dpapi
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct DATA_BLOB { public int cbData; public IntPtr pbData; }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DATA_BLOB dataIn, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref DATA_BLOB dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DATA_BLOB dataIn, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref DATA_BLOB dataOut);

        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr mem);

        private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            var bytes = Encoding.UTF8.GetBytes(plain);
            var res = Run(bytes, true);
            return res == null ? "" : Convert.ToBase64String(res);
        }

        public static string Unprotect(string protectedB64)
        {
            if (string.IsNullOrEmpty(protectedB64)) return "";
            try
            {
                var res = Run(Convert.FromBase64String(protectedB64), false);
                return res == null ? "" : Encoding.UTF8.GetString(res);
            }
            catch { return ""; }
        }

        private static byte[]? Run(byte[] data, bool protect)
        {
            var inBlob = new DATA_BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
            var outBlob = new DATA_BLOB();
            try
            {
                Marshal.Copy(data, 0, inBlob.pbData, data.Length);
                bool ok = protect
                    ? CryptProtectData(ref inBlob, "McBar", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob)
                    : CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob);
                if (!ok || outBlob.pbData == IntPtr.Zero) return null;
                var result = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(inBlob.pbData);
                if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
            }
        }
    }
}
