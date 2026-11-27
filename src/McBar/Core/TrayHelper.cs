// McBar — Creado por Andrés Londero
// 1.9.29: Ayudante del área de notificaciones, con TAREA PROGRAMADA (pide permiso una sola vez).
// Windows no deja que un programa normal (McBar) le mande el clic a un ícono de un programa que corre COMO
// ADMINISTRADOR. Solución: una copia de McBar en "modo ayudante" (McBar.exe --trayhelper), que corre como
// administrador y sí puede reenviarles el clic.
//
// Para no pedir permiso cada vez: se registra una tarea programada que arranca el ayudante al iniciar sesión
// "con privilegios máximos" (el Programador de tareas corre eso como administrador SIN cartel). Crear la tarea
// pide permiso una sola vez. McBar (normal) deja la tubería lista y el ayudante se conecta cuando arranca.
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
        private const string TaskName = "McBar Tray Helper";

        // ---------- lado McBar (normal): servidor de la tubería ----------
        private static volatile bool _serverRunning;
        private static volatile bool _connected;
        private static volatile NamedPipeServerStream? _server; // 1.9.30: para poder destrabar WaitForConnection al frenar
        private static readonly object _startLock = new object();
        // 1.9.31: los clics se encolan y los escribe un hilo aparte; así reenviar NUNCA bloquea la interfaz
        // (antes se escribía en la tubería desde el hilo de la interfaz y, si el ayudante no leía, McBar se colgaba).
        private static readonly System.Collections.Concurrent.ConcurrentQueue<string> _outbox = new System.Collections.Concurrent.ConcurrentQueue<string>();
        private static readonly AutoResetEvent _signal = new AutoResetEvent(false);

        public static bool Ready => _connected;

        /// <summary>Deja la tubería lista y esperando que el ayudante se conecte. Se llama al arrancar (si está activado).</summary>
        public static void StartServer()
        {
            lock (_startLock)
            {
                if (_serverRunning) return;
                _serverRunning = true;
            }
            new Thread(ServerLoop) { IsBackground = true, Name = "McBar.TrayPipe" }.Start();
        }

        private static void ServerLoop()
        {
            bool loggedBusy = false;
            while (_serverRunning)
            {
                NamedPipeServerStream? server = null;
                StreamWriter? writer = null;
                try
                {
                    // 1.9.30: Asíncrona para que Dispose() (desde Stop) destrabe WaitForConnection y no quede un hilo colgado
                    // reteniendo la única instancia de la tubería (era lo que causaba "todas las instancias en uso").
                    server = new NamedPipeServerStream(PipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    _server = server;
                    server.WaitForConnection();
                    if (!_serverRunning) break;
                    loggedBusy = false;
                    writer = new StreamWriter(server) { AutoFlush = true };
                    while (_outbox.TryDequeue(out _)) { } // 1.9.31: descartar clics viejos de antes de conectar
                    _connected = true;
                    Log.Write("Ayudante del systray: conectado.");
                    // 1.9.31: drena la cola mientras siga conectado; si la tubería se cortó, WriteLine tira y reconecta
                    while (_serverRunning)
                    {
                        _signal.WaitOne(1000);
                        while (_outbox.TryDequeue(out var line)) writer.WriteLine(line);
                    }
                }
                catch (Exception ex)
                {
                    if (!_serverRunning) break;
                    if (!loggedBusy) { Log.Write("Ayudante (servidor): " + ex.Message); loggedBusy = true; } // 1.9.30: una sola vez, no spam
                    Thread.Sleep(3000);
                }
                finally
                {
                    _connected = false;
                    try { writer?.Dispose(); } catch { }
                    try { server?.Dispose(); } catch { }
                    if (ReferenceEquals(_server, server)) _server = null;
                }
            }
            _server = null;
        }

        /// <summary>Le pide al ayudante que reenvíe un mensaje a una ventana que corre como administrador.
        /// 1.9.31: solo encola y avisa; la escritura real la hace el hilo del servidor (no bloquea la interfaz).</summary>
        public static void Forward(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (!_serverRunning) return;
            if (_outbox.Count > 256) return; // no acumular sin fin si el ayudante no está leyendo
            _outbox.Enqueue($"{hwnd.ToInt64():X} {msg:X} {wParam.ToInt64():X} {lParam.ToInt64():X}");
            _signal.Set();
        }

        public static void Stop()
        {
            _serverRunning = false;
            _signal.Set();
            try { _server?.Dispose(); } catch { } // 1.9.30: destraba WaitForConnection para que el hilo salga y suelte la tubería
            _server = null;
            while (_outbox.TryDequeue(out _)) { }
        }

        // ---------- activar / desactivar (desde Preferencias) ----------
        /// <summary>Al prender la opción: registra la tarea (pide permiso una vez) y deja la tubería lista.</summary>
        public static void Enable()
        {
            StartServer();
            RunElevated("--installtask");
        }

        /// <summary>Al apagar la opción: saca la tarea (pide permiso una vez) y corta.</summary>
        public static void Disable()
        {
            RunElevated("--uninstalltask");
            Stop();
        }

        private static void RunElevated(string arg)
        {
            // 1.9.50: en otro hilo: el aviso de permisos (UAC) espera la respuesta y congelaba la interfaz
            var t = new Thread(() =>
            {
                try { Process.Start(new ProcessStartInfo(Environment.ProcessPath ?? "", arg) { UseShellExecute = true, Verb = "runas" }); }
                catch (Exception ex) { Log.Write("Ayudante (permiso): " + ex.Message); }
            }) { IsBackground = true, Name = "McBar.Permiso" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        // ---------- modos elevados (McBar.exe --installtask / --uninstalltask) ----------
        /// <summary>Corre como administrador: crea la tarea de inicio y lanza el ayudante ya mismo. Después sale.</summary>
        public static void InstallTask()
        {
            try
            {
                string exe = Environment.ProcessPath ?? "";
                Schtasks($"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\" --trayhelper\" /SC ONLOGON /RL HIGHEST /F");
                // arrancarlo ya (somos administrador, así que el hijo también lo es)
                Process.Start(new ProcessStartInfo(exe, "--trayhelper") { UseShellExecute = false });
            }
            catch (Exception ex) { Log.Write("Ayudante (instalar tarea): " + ex.Message); }
        }

        /// <summary>Corre como administrador: saca la tarea. Después sale.</summary>
        public static void UninstallTask()
        {
            try { Schtasks($"/Delete /TN \"{TaskName}\" /F"); }
            catch (Exception ex) { Log.Write("Ayudante (sacar tarea): " + ex.Message); }
        }

        private static void Schtasks(string args)
        {
            var p = Process.Start(new ProcessStartInfo("schtasks.exe", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            p?.WaitForExit(8000);
        }

        // ---------- lado ayudante (administrador): McBar.exe --trayhelper ----------
        public static void RunServer(int parentPid)
        {
            if (parentPid > 0)
            {
                try { var parent = Process.GetProcessById(parentPid); parent.EnableRaisingEvents = true; parent.Exited += (_, _) => Environment.Exit(0); }
                catch { }
            }
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.In);
                        client.Connect(10000);
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
                            // 1.9.31: habilitar el paso al frente para que la app pueda mostrar su ventana/menú al frente
                            try { Win32.AllowSetForegroundWindow(unchecked((uint)(-1))); Win32.AllowSetForegroundWindow(Win32.ProcessOf(new IntPtr(h))); } catch { }
                            Win32.PostMessage(new IntPtr(h), m, new IntPtr(wp), new IntPtr(lp));
                        }
                    }
                    catch { Thread.Sleep(1500); } // reintentar conectar
                }
            })
            { IsBackground = true, Name = "McBar.TrayHelper" }.Start();
        }
    }
}
