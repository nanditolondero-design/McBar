// McBar — Creado por Andrés Londero
// Monitores conectados y "área de trabajo" de cada uno (el espacio que usan las ventanas maximizadas).
// McBar fija el área de trabajo por su cuenta: así desaparece el espacio que Windows guardaba para su
// barra de tareas y se reserva solo lo que ocupan la barra de menú y el Dock.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using McBar.Core;

namespace McBar.Native
{
    public sealed class MonitorInfo
    {
        public IntPtr Handle { get; init; }
        public string Device { get; init; } = "";
        public bool Primary { get; init; }
        /// <summary>Rectángulo completo del monitor, en píxeles.</summary>
        public PxRect Bounds { get; init; }
        public PxRect Work { get; init; }
    }

    public readonly struct PxRect
    {
        public int Left { get; init; }
        public int Top { get; init; }
        public int Right { get; init; }
        public int Bottom { get; init; }
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public PxRect(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; }
        public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
        public override string ToString() => $"{Left},{Top},{Right},{Bottom}";
    }

    internal static class Monitors
    {
        public static List<MonitorInfo> All()
        {
            var list = new List<MonitorInfo>();
            Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref Win32.RECT rc, IntPtr data) =>
            {
                var mi = Get(h);
                if (mi != null) list.Add(mi);
                return true;
            }, IntPtr.Zero);
            list.Sort((a, b) => a.Primary == b.Primary ? a.Bounds.Left.CompareTo(b.Bounds.Left) : (a.Primary ? -1 : 1));
            return list;
        }

        public static MonitorInfo? Get(IntPtr hMonitor)
        {
            var info = new Win32.MONITORINFOEX { cbSize = Marshal.SizeOf<Win32.MONITORINFOEX>(), szDevice = "" };
            if (!Win32.GetMonitorInfo(hMonitor, ref info)) return null;
            return new MonitorInfo
            {
                Handle = hMonitor,
                Device = info.szDevice ?? "",
                Primary = (info.dwFlags & Win32.MONITORINFOF_PRIMARY) != 0,
                Bounds = new PxRect(info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.right, info.rcMonitor.bottom),
                Work = new PxRect(info.rcWork.left, info.rcWork.top, info.rcWork.right, info.rcWork.bottom)
            };
        }

        public static MonitorInfo? FromWindow(IntPtr hwnd) => Get(Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST));
    }

    /// <summary>Administra el área de trabajo de cada monitor.</summary>
    internal static class WorkArea
    {
        private static readonly Dictionary<string, PxRect> Desired = new Dictionary<string, PxRect>();
        private static DateTime _windowStart = DateTime.MinValue;
        private static int _fixesInWindow;

        /// <summary>Calcula el área de trabajo: el monitor menos lo reservado arriba y abajo (pura, se prueba).</summary>
        public static PxRect Compute(PxRect bounds, int topPx, int bottomPx) =>
            new PxRect(bounds.Left, bounds.Top + Math.Max(0, topPx), bounds.Right, bounds.Bottom - Math.Max(0, bottomPx));

        public static void Set(MonitorInfo m, int topPx, int bottomPx)
        {
            var r = Compute(m.Bounds, topPx, bottomPx);
            Desired[m.Device] = r;
            var w = m.Work;
            if (w.Left == r.Left && w.Top == r.Top && w.Right == r.Right && w.Bottom == r.Bottom) return; // ya está bien: no avisar a todas las apps
            Apply(r);
        }

        private static void Apply(PxRect r)
        {
            var rc = new Win32.RECT { left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom };
            Win32.SystemParametersInfoRect(Win32.SPI_SETWORKAREA, 0, ref rc, Win32.SPIF_SENDCHANGE);
        }

        /// <summary>Si Windows (o el Explorador) cambió el área de trabajo, la vuelve a poner como la queremos.</summary>
        public static void Verify()
        {
            if (Desired.Count == 0) return;
            if ((DateTime.Now - _windowStart).TotalSeconds > 10) { _windowStart = DateTime.Now; _fixesInWindow = 0; }
            if (_fixesInWindow > 6) return; // no pelear sin fin con otro programa
            foreach (var m in Monitors.All())
            {
                if (!Desired.TryGetValue(m.Device, out var want)) continue;
                var w = m.Work;
                if (w.Left != want.Left || w.Top != want.Top || w.Right != want.Right || w.Bottom != want.Bottom)
                {
                    _fixesInWindow++;
                    Apply(want);
                }
            }
        }

        /// <summary>Al salir: devuelve todo el monitor (el Explorador recalcula lo suyo al volver su barra).</summary>
        public static void Reset()
        {
            foreach (var m in Monitors.All()) Apply(m.Bounds);
            Desired.Clear();
        }
    }
}
