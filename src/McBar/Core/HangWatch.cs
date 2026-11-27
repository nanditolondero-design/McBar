// McBar — Creado por Andrés Londero
// 1.3.1 — Vigilante de "trabadas". Si la interfaz de McBar deja de responder (Dock e isla quietos), un hilo
// aparte lo nota:
//  - a los 5 s anota en mcbar.log qué estaba haciendo McBar en ese momento (la tarea que no termina);
//  - a los 20 s guarda un informe en %AppData%\McBar\errores y McBar se REINICIA solo (abre una copia nueva
//    con --reinicio, que espera a que esta se cierre, y esta se cierra).
// No gasta: un "ping" por segundo al hilo de la interfaz.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;

namespace McBar.Core
{
    public static class HangWatch
    {
        private static Dispatcher? _ui;
        private static Thread? _thread;
        private static long _lastPong;          // Stopwatch ticks de la última respuesta de la interfaz
        private static DispatcherOperation? _current;
        private static long _currentSince;
        private static volatile bool _running;
        private static readonly PropertyInfo? NameProp =
            typeof(DispatcherOperation).GetProperty("Name", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public const int WarnSeconds = 5;
        public const int RestartSeconds = 20;

        /// <summary>Qué hacer según los segundos sin respuesta: "ok", "warn" o "restart". Pura, se prueba.</summary>
        public static string Decide(double secondsWithoutAnswer, bool restartEnabled) =>
            secondsWithoutAnswer >= RestartSeconds && restartEnabled ? "restart" : secondsWithoutAnswer >= WarnSeconds ? "warn" : "ok";

        public static void Start(Dispatcher ui)
        {
            if (_running) return;
            _ui = ui;
            _running = true;
            _lastPong = Stopwatch.GetTimestamp();
            ui.Hooks.OperationStarted += (_, e) => { _current = e.Operation; _currentSince = Stopwatch.GetTimestamp(); };
            ui.Hooks.OperationCompleted += (_, _) => _current = null;
            ui.Hooks.OperationAborted += (_, _) => _current = null;
            _thread = new Thread(Loop) { IsBackground = true, Name = "McBar vigilante", Priority = ThreadPriority.AboveNormal };
            _thread.Start();
        }

        public static void Stop() => _running = false;

        private static double Seconds(long from) => (Stopwatch.GetTimestamp() - from) / (double)Stopwatch.Frequency;

        private static string Describe()
        {
            var op = _current;
            if (op == null) return "un mensaje de Windows (no una tarea de McBar)";
            string name = "";
            try { name = NameProp?.GetValue(op) as string ?? ""; } catch { }
            if (name.Length == 0) name = "tarea desconocida";
            return $"{name} (prioridad {op.Priority}, hace {Seconds(_currentSince):0.0} s)";
        }

        private static void Loop()
        {
            bool warned = false;
            while (_running)
            {
                try
                {
                    _ui?.BeginInvoke(DispatcherPriority.Normal, new Action(() => _lastPong = Stopwatch.GetTimestamp()));
                    Thread.Sleep(1000);
                    double s = Seconds(Interlocked.Read(ref _lastPong));
                    switch (Decide(s, SettingsStore.Current.RestartIfHung))
                    {
                        case "ok":
                            if (warned) Log.Write($"La interfaz volvió a responder ({s:0.0} s).");
                            warned = false;
                            break;
                        case "warn":
                            if (!warned) Log.Write($"La interfaz no responde hace {s:0} s. Ocupada en: {Describe()}");
                            warned = true;
                            break;
                        case "restart":
                            var what = Describe();
                            Log.Write($"La interfaz sigue trabada ({s:0} s) en: {what}. McBar se reinicia.");
                            // 1.9.48: el informe va en otro hilo y se espera poco: si algo lo traba, McBar igual se reinicia
                            var rep = new Thread(() => { try { Recovery.SaveReport($"McBar se trabó {s:0} segundos y se reinició solo.\r\nOcupada en: {what}"); } catch { } }) { IsBackground = true };
                            rep.Start(); rep.Join(1500);
                            RestartNow();
                            return;
                    }
                }
                catch (Exception ex) { try { Log.Write("Vigilante: " + ex.Message); } catch { } Thread.Sleep(2000); }
            }
        }

        /// <summary>Abre una copia nueva (que espera a que esta se cierre) y cierra esta de golpe.</summary>
        internal static void RestartNow()
        {
            try
            {
                var exe = Edition.IsStore ? Edition.StoreAlias : Environment.ProcessPath; // 1.9.2: la Store se abre por su alias
                if (!string.IsNullOrEmpty(exe)) Process.Start(new ProcessStartInfo(exe, "--reinicio") { UseShellExecute = false });
            }
            catch (Exception ex) { Log.Write("No se pudo reabrir McBar: " + ex.Message); }
            Process.GetCurrentProcess().Kill();
        }
    }
}
