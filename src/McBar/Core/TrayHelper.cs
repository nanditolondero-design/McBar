// McBar — Creado por Andrés Londero
// 1.9.28: Ayudante del área de notificaciones. Windows no deja que un programa normal (McBar) le mande el clic
// a un ícono de un programa que corre COMO ADMINISTRADOR (NVIDIA y similares). La solución: McBar lanza una vez
// una copia de sí mismo como administrador, en "modo ayudante" (McBar.exe --trayhelper <pid>). El ayudante,
// al ser administrador, SÍ puede reenviarles el clic. Se comunican por una tubería con nombre.
//
// La tubería la crea McBar (normal, integridad media) y el ayudante (administrador, integridad alta) se conecta:
// alto puede abrir lo que creó medio, así que no hay problema de permisos.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using McBar.Native;

namespace McBar.Core
{
    public static class TrayHelper
    {
        private const string PipeName = "McBarTrayHelper";

        // ---------- lado McBar (normal): servidor de la tubería + lanzar al ayudante ----------
        private static NamedPipeServerStream? _server;
        private static StreamWriter? _writer;
        private static Process? _helper;
        private static bool _starting;

        /// <summary>true si el ayudante ya está conectado y listo para reenviar.</summary>
        public static bool Ready => _writer != null;

        /// <summary>Lanza el ayudante como administrador (una sola vez; pide permiso a Windows).</summary>
        public static void EnsureStarted()
        {
            if (_writer != null) return;
            if (_helper != null && !_helper.HasExited) return;
            if (_starting) return;
            _starting = true;
            try
            {
                _server?.Dispose();
                _server = new NamedPipeServerStream(PipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                var psi = new ProcessStartInfo(Environment.ProcessPath ?? "", "--trayhelper " + Environment.ProcessId)
                {
                    UseShellExecute = true,
                    Verb = "runas" // pide permiso de administrador (cartel de Windows)
                };
                _helper = Process.Start(psi);
                _server.BeginWaitForConnection(ar =>
                {
                    try { _server.EndWaitForConnection(ar); _writer = new StreamWriter(_server) { AutoFlush = true }; Log.Write("Ayudante del systray: conectado."); }
                    catch (Exception ex) { Log.Write("Ayudante del systray (conexión): " + ex.Message); }
                }, null);
            }
            catch (Exception ex) { Log.Write("Ayudante del systray (lanzar): " + ex.Message); _helper = null; }
            finally { _starting = false; }
        }

        /// <summary>Le pide al ayudante que reenvíe un mensaje a una ventana (que corre como administrador).</summary>
        public static void Forward(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            var w = _writer;
            if (w == null) { EnsureStarted(); return; }
            try { w.WriteLine($"{hwnd.ToInt64():X} {msg:X} {wParam.ToInt64():X} {lParam.ToInt64():X}"); }
            catch
            {
                try { _writer = null; _server?.Dispose(); _server = null; } catch { }
                EnsureStarted();
            }
        }

        public static void Stop()
        {
            try { _writer?.Dispose(); } catch { }
            try { _server?.Dispose(); } catch { }
            try { if (_helper != null && !_helper.HasExited) _helper.Kill(); } catch { }
            _writer = null; _server = null; _helper = null;
        }

        // ---------- lado ayudante (administrador): McBar.exe --trayhelper <pid> ----------
        /// <summary>Se ejecuta cuando McBar se abre como administrador en modo ayudante. No muestra ventanas.</summary>
        public static void RunServer(int parentPid)
        {
            // si McBar (el padre) se cierra, el ayudante también
            try
            {
                var parent = Process.GetProcessById(parentPid);
                parent.EnableRaisingEvents = true;
                parent.Exited += (_, _) => Environment.Exit(0);
            }
            catch { }

            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.In);
                        client.Connect(8000);
                        using var r = new StreamReader(client);
                        string? line;
                        while ((line = r.ReadLine()) != null)
                        {
                            var p = line.Split(' ');
                            if (p.Length != 4) continue;
                            if (!long.TryParse(p[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h)) continue;
                            if (!uint.TryParse(p[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var m)) continue;
                            if (!long.TryParse(p[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var wp)) continue;
                            if (!long.TryParse(p[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lp)) continue;
                            Win32.PostMessage(new IntPtr(h), m, new IntPtr(wp), new IntPtr(lp));
                        }
                    }
                    catch { Thread.Sleep(1000); } // la tubería se cortó: reintentar conectar
                }
            })
            { IsBackground = true, Name = "McBar.TrayHelper" }.Start();
        }
    }
}
