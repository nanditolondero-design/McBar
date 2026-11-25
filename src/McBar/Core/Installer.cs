// McBar — Creado por Andrés Londero
// Instalación (0.5.0). McBar.exe es su propio instalador: si se abre desde cualquier lugar que no sea la
// carpeta instalada (por ejemplo Descargas), muestra la ventana "Instalar McBar". No pide permisos de
// administrador: se instala para el usuario en %LocalAppData%\Programs\McBar, como Discord o VS Code.
//
// Instalar:   copia McBar.exe, lo registra en "Aplicaciones instaladas" (con desinstalador), crea los
//             accesos directos elegidos, aplica las opciones y abre la copia instalada.
// Desinstalar: "McBar.exe --uninstall" (lo llama Windows desde Aplicaciones instaladas): devuelve la barra
//             de Windows, quita inicio automático, accesos directos y registro, y borra la carpeta.
//             Los ajustes (%AppData%\McBar) se conservan por si se vuelve a instalar.
// Probar sin instalar: "McBar.exe --portable" (o el botón del instalador).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace McBar.Core
{
    public sealed class InstallOptions
    {
        public bool StartWithWindows { get; set; } = true;
        public bool StartMenuShortcut { get; set; } = true;
        public bool DesktopShortcut { get; set; }
        public bool ImportTaskbarPins { get; set; } = true;
        public bool GlassTheme { get; set; }
    }

    public static class Installer
    {
        public const string AppName = "McBar";
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\McBar";

        public static string InstallDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "McBar");
        public static string InstalledExe => Path.Combine(InstallDir, "McBar.exe");
        private static string StartMenuLink =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "McBar.lnk");
        private static string DesktopLink =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "McBar.lnk");

        public static Version CurrentVersion =>
            typeof(Installer).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

        /// <summary>¿Esta copia es la instalada?</summary>
        public static bool IsRunningInstalled =>
            IsInside(Environment.ProcessPath ?? "", InstallDir);

        /// <summary>¿La ruta está dentro de la carpeta? (pura, se prueba)</summary>
        public static bool IsInside(string path, string folder)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder)) return false;
            var f = folder.TrimEnd('\\', '/') + "\\";
            return path.Replace('/', '\\').StartsWith(f.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
        }

        public static Version? InstalledVersion()
        {
            try
            {
                if (!File.Exists(InstalledExe)) return null;
                var v = FileVersionInfo.GetVersionInfo(InstalledExe).FileVersion;
                return Version.TryParse(v, out var ver) ? ver : null;
            }
            catch { return null; }
        }

        /// <summary>Qué hacer al abrir el .exe desde fuera de la carpeta instalada. (pura, se prueba)</summary>
        public static string Decide(Version current, Version? installed) =>
            installed == null ? "install" : current > installed ? "update" : "launchInstalled";

        // ================== Instalar ==================

        /// <summary>Instala paso a paso; <paramref name="progress"/> recibe (0..1, texto).</summary>
        public static void Install(InstallOptions o, Action<double, string> progress)
        {
            progress(0.05, "Cerrando McBar si estaba abierto…");
            CloseOtherInstances();

            progress(0.2, "Copiando McBar…");
            Directory.CreateDirectory(InstallDir);
            var src = Environment.ProcessPath ?? throw new InvalidOperationException("No se encontró McBar.exe");
            if (!string.Equals(Path.GetFullPath(src), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase))
                CopyWithRetry(src, InstalledExe);

            progress(0.55, "Registrando en Aplicaciones instaladas…");
            RegisterUninstall();

            progress(0.7, "Creando accesos directos…");
            bool menuOk = true;
            if (o.StartMenuShortcut) menuOk = CreateShortcut(StartMenuLink, InstalledExe, "Dock y barra estilo macOS");
            else TryDelete(StartMenuLink);
            if (o.DesktopShortcut) CreateShortcut(DesktopLink, InstalledExe, "Dock y barra estilo macOS");
            if (o.StartMenuShortcut && !menuOk) Log.Write("Aviso: el acceso directo del menú Inicio no se pudo crear.");

            progress(0.85, "Aplicando tus opciones…");
            var s = SettingsStore.Current;
            s.StartWithWindows = o.StartWithWindows;
            if (o.GlassTheme) s.Theme = "glass";
            if (o.ImportTaskbarPins)
                foreach (var p in TaskbarPins())
                    if (!s.Pinned.Any(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
                        s.Pinned.Add(p);
            SettingsStore.Save();
            try
            {
                using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (o.StartWithWindows) run.SetValue("McBar", "\"" + InstalledExe + "\"");
                else run.DeleteValue("McBar", false);
            }
            catch { }

            progress(1, "¡Listo!");
            Log.Write($"Instalado {CurrentVersion} en {InstallDir}");
        }

        public static void LaunchInstalled()
        {
            try { Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true, WorkingDirectory = InstallDir }); }
            catch (Exception ex) { Log.Write("Abrir McBar instalado: " + ex.Message); }
        }

        private static void CopyWithRetry(string src, string dst)
        {
            for (int i = 0; ; i++)
            {
                try { File.Copy(src, dst, true); return; }
                catch (IOException) when (i < 15) { System.Threading.Thread.Sleep(300); } // el viejo todavía se está cerrando
            }
        }

        /// <summary>Cierra las otras copias de McBar (la vieja instalada o una portátil).</summary>
        public static void CloseOtherInstances()
        {
            int me = Environment.ProcessId;
            foreach (var p in Process.GetProcessesByName("McBar"))
            {
                try
                {
                    if (p.Id == me) continue;
                    if (!p.CloseMainWindow()) p.Kill();
                    if (!p.WaitForExit(3000)) p.Kill();
                }
                catch { }
                finally { p.Dispose(); }
            }
        }

        private static void RegisterUninstall()
        {
            using var k = Registry.CurrentUser.CreateSubKey(UninstallKey);
            k.SetValue("DisplayName", "McBar");
            k.SetValue("DisplayVersion", CurrentVersion.ToString(3));
            k.SetValue("Publisher", "Andrés Londero");
            k.SetValue("DisplayIcon", InstalledExe + ",0");
            k.SetValue("InstallLocation", InstallDir);
            k.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
            k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            try { k.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord); } catch { }
        }

        /// <summary>Apps fijadas en la barra de Windows (sus accesos directos), para traerlas al Dock.</summary>
        public static List<PinnedApp> TaskbarPins()
        {
            var list = new List<PinnedApp>();
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
                if (!Directory.Exists(dir)) return list;
                // se copian a la carpeta de McBar: si después se desfijan de Windows, el Dock no los pierde
                var keep = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "McBar", "Accesos");
                Directory.CreateDirectory(keep);
                foreach (var lnk in Directory.GetFiles(dir, "*.lnk").OrderBy(f => File.GetCreationTime(f))) // 1.9.12: en el orden en que se fijaron
                {
                    var name = Path.GetFileNameWithoutExtension(lnk);
                    if (name.Equals("File Explorer", StringComparison.OrdinalIgnoreCase) || name.Equals("Explorador de archivos", StringComparison.OrdinalIgnoreCase))
                        continue; // el Finder ya está
                    var copy = Path.Combine(keep, Path.GetFileName(lnk));
                    try { File.Copy(lnk, copy, true); } catch { copy = lnk; }
                    list.Add(new PinnedApp { Name = name, Target = copy });
                }
            }
            catch (Exception ex) { Log.Write("Apps de la barra: " + ex.Message); }
            return list;
        }

        /// <summary>1.9.29: crea (o rehace) el acceso directo del menú Inicio apuntando a la copia que está corriendo.
        /// Sirve también cuando se está probando sin instalar, para tener el ícono en el menú Inicio igual.</summary>
        public static bool EnsureStartMenuShortcut()
        {
            var exe = IsRunningInstalled ? InstalledExe : (Environment.ProcessPath ?? InstalledExe);
            return CreateShortcut(StartMenuLink, exe, "Dock y barra estilo macOS");
        }

        public static bool CreateShortcut(string linkPath, string target, string description)
        {
            // 1.9.18: forma nativa (IShellLink). El WScript.Shell por COM fallaba sin aviso en la app de un solo
            // archivo y no quedaba el acceso directo.
            var dir = Path.GetDirectoryName(target) ?? InstallDir;
            bool ok = McBar.Native.ShellLink.Create(linkPath, target, dir, description, target, 0);
            if (ok) Log.Write("Acceso directo creado: " + linkPath);
            else Log.Write("No se pudo crear el acceso directo: " + linkPath);
            return ok;
        }

        // ================== Desinstalar ==================

        public static void Uninstall()
        {
            CloseOtherInstances();
            try
            {
                using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                run?.DeleteValue("McBar", false);
            }
            catch { }
            TryDelete(StartMenuLink);
            TryDelete(DesktopLink);
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            MacLook.RestoreAll(); // 1.8.0: cursores y sonidos como estaban antes de McBar
            var s = SettingsStore.Current;
            s.StartWithWindows = false;
            SettingsStore.Save();
            // La carpeta se borra cuando este proceso termine (no se puede borrar el .exe que está corriendo).
            if (IsRunningInstalled && Directory.Exists(InstallDir))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{InstallDir}\"")
                    { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
                }
                catch { }
            }
            Log.Write("McBar desinstalado");
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
