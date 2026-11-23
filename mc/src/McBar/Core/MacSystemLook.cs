// McBar — Creado por Andrés Londero
// 1.8.0 — Estilo Mac, partes "del sistema" (todas opcionales, apagadas de fábrica):
//   - AutoAppearance: Windows y las apps en modo oscuro de noche y claro de día (como "Automático" de macOS).
//     Cambia HKCU\...\Themes\Personalize (AppsUseLightTheme / SystemUsesLightTheme) SOLO al pasar de un
//     horario al otro, así si se cambia a mano en el medio, McBar no lo pisa hasta el próximo cambio.
//   - MacCursors: cursores propios de McBar (Assets/Cursores) en HKCU\Control Panel\Cursors. Antes de
//     cambiarlos guarda los que había (cursores-anteriores.json) y al apagar la opción los devuelve.
//   - MacSounds: sonidos propios (Assets/Sonidos) en HKCU\AppEvents\Schemes. Igual: guarda y devuelve
//     (sonidos-anteriores.json). Play() los usa McBar (por ejemplo el obturador de las capturas).
// Todo lo del registro se hace en otro hilo para no frenar la interfaz.
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using McBar.Native;
using Microsoft.Win32;

namespace McBar.Core
{
    public static class AutoAppearance
    {
        private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private static DispatcherTimer? _timer;
        private static bool? _last;

        /// <summary>¿Va oscuro a esa hora? Si darkFrom &gt; lightFrom el oscuro cruza la medianoche. Pura, se prueba.</summary>
        public static bool IsDarkAt(int hour, int darkFrom, int lightFrom)
        {
            if (darkFrom == lightFrom) return false;
            if (darkFrom > lightFrom) return hour >= darkFrom || hour < lightFrom;
            return hour >= darkFrom && hour < lightFrom;
        }

        public static void Apply()
        {
            var s = SettingsStore.Current;
            if (!s.AutoDarkMode) { Stop(); return; }
            if (_timer == null)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(1) };
                _timer.Tick += (_, _) => Check();
            }
            if (!_timer.IsEnabled) { _last = null; _timer.Start(); }
            Check();
        }

        public static void Stop() { _timer?.Stop(); _last = null; }

        private static string _lastHours = "";

        private static void Check()
        {
            var s = SettingsStore.Current;
            var hours = s.AutoDarkFrom + "-" + s.AutoLightFrom;
            if (hours != _lastHours) { _lastHours = hours; _last = null; } // cambiaron los horarios: volver a aplicar
            bool dark = IsDarkAt(DateTime.Now.Hour, s.AutoDarkFrom, s.AutoLightFrom);
            if (_last == dark) return;
            _last = dark;
            Task.Run(() => SetDark(dark));
        }

        private static void SetDark(bool dark)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(Key, true);
                if (k == null) return;
                int light = dark ? 0 : 1;
                bool same = k.GetValue("AppsUseLightTheme") is int a && a == light && k.GetValue("SystemUsesLightTheme") is int b && b == light;
                if (same) return;
                k.SetValue("AppsUseLightTheme", light, RegistryValueKind.DWord);
                k.SetValue("SystemUsesLightTheme", light, RegistryValueKind.DWord);
                Win32.SendMessageTimeoutString((IntPtr)0xFFFF, Win32.WM_SETTINGCHANGE, IntPtr.Zero, "ImmersiveColorSet", Win32.SMTO_ABORTIFHUNG, 300, out _);
                Log.Write("Modo " + (dark ? "oscuro" : "claro") + " automático");
            }
            catch (Exception ex) { Log.Write("Modo oscuro automático: " + ex.Message); }
        }
    }

    public static class MacCursors
    {
        private const string Key = @"Control Panel\Cursors";
        private const string BackupFile = "cursores-anteriores.json";
        private const string Folder = "cursores";

        /// <summary>Nombre del cursor en el registro de Windows → archivo de McBar.</summary>
        public static readonly (string reg, string file)[] Map =
        {
            ("Arrow", "flecha.cur"), ("Hand", "mano.cur"), ("IBeam", "texto.cur"),
            ("SizeNS", "tam-vertical.cur"), ("SizeWE", "tam-horizontal.cur"),
            ("SizeNWSE", "tam-diagonal1.cur"), ("SizeNESW", "tam-diagonal2.cur"), ("SizeAll", "mover.cur"),
            ("Crosshair", "cruz.cur"), ("No", "prohibido.cur"), ("Help", "ayuda.cur"),
            ("Wait", "espera.ani"), ("AppStarting", "trabajando.ani"),
        };

        private static readonly object Gate = new object();
        private static bool? _applied;

        public static void Apply()
        {
            bool want = SettingsStore.Current.MacCursors;
            if (_applied == want) return;
            _applied = want;
            Task.Run(() => { lock (Gate) { if (want) Put(); else Restore(); } });
        }

        private static void Put()
        {
            try
            {
                var names = Map.Select(m => (Key, m.reg)).Append((Key, "")).Append((Key, "Scheme Source"));
                RegBackup.Save(BackupFile, names);
                using var k = Registry.CurrentUser.CreateSubKey(Key, true);
                if (k == null) return;
                bool changed = false;
                foreach (var (reg, file) in Map)
                {
                    var path = Bundled.Extract("Cursores", file, Folder);
                    if (path == null) continue;
                    if (!string.Equals(k.GetValue(reg) as string, path, StringComparison.OrdinalIgnoreCase))
                    {
                        k.SetValue(reg, path, RegistryValueKind.ExpandString);
                        changed = true;
                    }
                }
                if (!changed) return;
                k.SetValue("", "McBar", RegistryValueKind.String);
                k.SetValue("Scheme Source", 1, RegistryValueKind.DWord);
                Reload();
                Log.Write("Cursores de McBar puestos");
            }
            catch (Exception ex) { Log.Write("Cursores: " + ex.Message); }
        }

        /// <summary>Devuelve los cursores que había antes de McBar (si se habían cambiado).</summary>
        public static void Restore()
        {
            try
            {
                if (RegBackup.Restore(BackupFile)) { Reload(); Log.Write("Cursores anteriores devueltos"); }
            }
            catch (Exception ex) { Log.Write("Cursores (volver): " + ex.Message); }
        }

        private const uint SPI_SETCURSORS = 0x0057;
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
        private static extern bool SystemParametersInfoNull(uint action, uint param, IntPtr vparam, uint winIni);

        private static void Reload() => SystemParametersInfoNull(SPI_SETCURSORS, 0, IntPtr.Zero, Win32.SPIF_UPDATEINIFILE | Win32.SPIF_SENDCHANGE);
    }

    public static class MacSounds
    {
        private const string Root = @"AppEvents\Schemes";
        private const string BackupFile = "sonidos-anteriores.json";
        private const string Folder = "sonidos";

        /// <summary>Evento de Windows (bajo AppEvents\Schemes\Apps) → sonido de McBar.</summary>
        public static readonly (string ev, string file)[] Map =
        {
            (@"Apps\.Default\.Default", "pop.wav"),
            (@"Apps\.Default\SystemAsterisk", "aviso.wav"),
            (@"Apps\.Default\SystemNotification", "aviso.wav"),
            (@"Apps\.Default\Notification.Default", "aviso.wav"),
            (@"Apps\.Default\SystemExclamation", "alerta.wav"),
            (@"Apps\.Default\SystemHand", "error.wav"),
            (@"Apps\.Default\DeviceConnect", "conectar.wav"),
            (@"Apps\.Default\DeviceDisconnect", "desconectar.wav"),
            (@"Apps\Explorer\EmptyRecycleBin", "papelera.wav"),
        };

        private static readonly object Gate = new object();
        private static bool? _applied;

        public static void Apply()
        {
            bool want = SettingsStore.Current.MacSounds;
            if (_applied == want) return;
            _applied = want;
            Task.Run(() => { lock (Gate) { if (want) Put(); else Restore(); } });
        }

        private static string KeyFor(string ev) => Root + "\\" + ev + "\\.Current";

        private static void Put()
        {
            try
            {
                var names = Map.Select(m => (KeyFor(m.ev), "")).Append((Root, ""));
                RegBackup.Save(BackupFile, names);
                foreach (var (ev, file) in Map)
                {
                    var path = Bundled.Extract("Sonidos", file, Folder);
                    if (path == null) continue;
                    using var k = Registry.CurrentUser.CreateSubKey(KeyFor(ev), true);
                    k?.SetValue("", path, RegistryValueKind.ExpandString);
                }
                using (var r = Registry.CurrentUser.CreateSubKey(Root, true)) r?.SetValue("", ".Current", RegistryValueKind.String);
                Log.Write("Sonidos de McBar puestos");
            }
            catch (Exception ex) { Log.Write("Sonidos: " + ex.Message); }
        }

        /// <summary>Devuelve los sonidos que había antes de McBar (si se habían cambiado).</summary>
        public static void Restore()
        {
            try { if (RegBackup.Restore(BackupFile)) Log.Write("Sonidos anteriores devueltos"); }
            catch (Exception ex) { Log.Write("Sonidos (volver): " + ex.Message); }
        }

        /// <summary>Reproduce uno de los sonidos de McBar sin esperar (por ejemplo "captura.wav").</summary>
        public static void Play(string file)
        {
            Task.Run(() =>
            {
                var path = Bundled.Extract("Sonidos", file, Folder);
                if (path != null) PlaySound(path, IntPtr.Zero, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
            });
        }

        private const uint SND_ASYNC = 0x0001, SND_NODEFAULT = 0x0002, SND_FILENAME = 0x00020000;
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "PlaySoundW")]
        private static extern bool PlaySound(string sound, IntPtr mod, uint flags);
    }
}
