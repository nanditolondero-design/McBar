// McBar — Creado por Andrés Londero
// Escritorios virtuales de Windows (1.0.0) para la franja de Win+Tab (como Spaces de macOS).
// La lista y el actual se leen del registro (donde Windows los guarda); cambiar, crear y cerrar se hace
// con los atajos de Windows (Ctrl+Win+←/→, Ctrl+Win+D, Ctrl+Win+F4), que funcionan en 10 y 11.
// Windows no deja que otra app mueva ventanas ajenas entre escritorios, así que eso no se ofrece.
using System;
using System.Collections.Generic;
using System.Threading;
using McBar.Native;
using Microsoft.Win32;

namespace McBar.Core
{
    public sealed class DesktopInfo
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public bool Current { get; init; }
    }

    public static class VirtualDesktops
    {
        private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

        /// <summary>Lista de GUIDs pegados de a 16 bytes (formato del registro). Pura, se prueba.</summary>
        public static List<Guid> ParseIds(byte[]? data)
        {
            var list = new List<Guid>();
            if (data == null) return list;
            for (int i = 0; i + 16 <= data.Length; i += 16)
            {
                var b = new byte[16];
                Array.Copy(data, i, b, 0, 16);
                list.Add(new Guid(b));
            }
            return list;
        }

        public static List<DesktopInfo> List()
        {
            var result = new List<DesktopInfo>();
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(Key);
                var ids = ParseIds(k?.GetValue("VirtualDesktopIDs") as byte[]);
                Guid current = Guid.Empty;
                if (k?.GetValue("CurrentVirtualDesktop") is byte[] cur && cur.Length == 16) current = new Guid(cur);
                else
                {
                    // Windows 10: el actual está en la sesión
                    using var sess = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\{System.Diagnostics.Process.GetCurrentProcess().SessionId}\VirtualDesktops");
                    if (sess?.GetValue("CurrentVirtualDesktop") is byte[] c2 && c2.Length == 16) current = new Guid(c2);
                }
                for (int i = 0; i < ids.Count; i++)
                {
                    string name = $"Escritorio {i + 1}";
                    using var dk = Registry.CurrentUser.OpenSubKey($@"{Key}\Desktops\{ids[i]:B}");
                    if (dk?.GetValue("Name") is string n && !string.IsNullOrWhiteSpace(n)) name = n;
                    result.Add(new DesktopInfo { Id = ids[i], Name = name, Current = ids[i] == current });
                }
            }
            catch (Exception ex) { Log.Write("Escritorios: " + ex.Message); }
            if (result.Count == 0) result.Add(new DesktopInfo { Name = "Escritorio 1", Current = true });
            return result;
        }

        private const ushort VK_CONTROL = 0x11, VK_LEFT = 0x25, VK_RIGHT = 0x27, VK_D = 0x44, VK_F4 = 0x73;

        /// <summary>Pasa del escritorio actual al de índice <paramref name="target"/>.</summary>
        public static void SwitchTo(int from, int target)
        {
            int steps = target - from;
            for (int i = 0; i < Math.Abs(steps); i++)
            {
                WindowOps.SendChord(VK_CONTROL, WindowOps.VK_LWIN, steps > 0 ? VK_RIGHT : VK_LEFT);
                Thread.Sleep(120);
            }
        }

        public static void Add() => WindowOps.SendChord(VK_CONTROL, WindowOps.VK_LWIN, VK_D);
        public static void CloseCurrent() => WindowOps.SendChord(VK_CONTROL, WindowOps.VK_LWIN, VK_F4);

        /// <summary>1.8.1: cierra el escritorio <paramref name="target"/> (sus ventanas pasan al de al lado, como en
        /// Windows). Se va a ese escritorio y lo cierra con Ctrl+Win+F4. En otro hilo para no frenar la interfaz.</summary>
        public static void Remove(int from, int target)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (target != from) { SwitchTo(from, target); Thread.Sleep(250); }
                    CloseCurrent();
                }
                catch (Exception ex) { Log.Write("Cerrar escritorio: " + ex.Message); }
            });
        }
    }
}
