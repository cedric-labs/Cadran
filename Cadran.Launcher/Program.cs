using Cadran.Launcher.Backups;
using Cadran.Launcher.Configuration;
using Cadran.Launcher.Supervision;
using Cadran.Launcher.UI;

namespace Cadran.Launcher;

internal static class Program
{
    private const string SingleInstanceMutexName = "Global\\Cadran_Launcher_SingleInstance_Mutex";

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--screenshot", StringComparison.Ordinal))
        {
            var culture = args.Length > 1 ? args[1] : "en";
            CaptureScreenshot(culture);
            return;
        }

        ApplicationConfiguration.Initialize();

        // 1. Contrôle d'instance unique par Mutex nommé
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isCreatedNew);
        if (!isCreatedNew)
        {
            MessageBox.Show(
                "Une instance de Cadran est déjà en cours d'exécution.\nVeuillez vérifier la zone de notification (près de l'horloge Windows).",
                "Cadran déjà démarré",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // 2. Résolution des répertoires d'exécution et de déploiement
        var baseDir = AppContext.BaseDirectory;
        var (appExecutablePath, workingDirectory) = ResolveServerPaths(baseDir);

        var dataDirectory = Path.Combine(workingDirectory, "data");
        var databaseFilePath = Path.Combine(dataDirectory, "cadran.db");
        var backupsDirectory = Path.Combine(workingDirectory, "backups");
        var configFilePath = Path.Combine(baseDir, "launcher.json");

        // 3. Initialisation des services
        var configStore = new LauncherConfigurationStore(configFilePath);
        var settings = configStore.LoadSettings();

        var backupSupervisor = new DatabaseBackupSupervisor(databaseFilePath, backupsDirectory);
        var processSupervisor = new CadranProcessSupervisor(appExecutablePath, workingDirectory, settings.Port);

        // 4. Lancement de la fenêtre principale
        using var mainForm = new MainLauncherForm(configStore, backupSupervisor, processSupervisor);
        Application.Run(mainForm);
    }

    private static (string ExecutablePath, string WorkingDirectory) ResolveServerPaths(string baseDirectory)
    {
        // 1. Structure de production déployée : Cadran.Launcher.exe à la racine, binaire dans ./app/Cadran.exe
        var productionAppDir = Path.Combine(baseDirectory, "app");
        var productionExePath = Path.Combine(productionAppDir, "Cadran.exe");
        if (File.Exists(productionExePath))
        {
            return (productionExePath, productionAppDir);
        }

        // 2. Structure locale à plat (si tous les fichiers sont dans le même répertoire)
        var flatExePath = Path.Combine(baseDirectory, "Cadran.exe");
        if (File.Exists(flatExePath))
        {
            return (flatExePath, baseDirectory);
        }

        // 3. Environnement de développement (exécution depuis Visual Studio / dotnet run)
        var devRelativePath = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Ledger", "bin", "Debug", "net9.0", "Cadran.exe"));
        if (File.Exists(devRelativePath))
        {
            var devWorkingDir = Path.GetDirectoryName(devRelativePath) ?? baseDirectory;
            return (devRelativePath, devWorkingDir);
        }

        // Repli par défaut sur la cible de production
        return (productionExePath, productionAppDir);
    }

    private static void CaptureScreenshot(string culture)
    {
        ApplicationConfiguration.Initialize();
        var baseDir = AppContext.BaseDirectory;
        var (_, workingDirectory) = ResolveServerPaths(baseDir);

        var dataDirectory = Path.Combine(workingDirectory, "data");
        Directory.CreateDirectory(dataDirectory);
        var databaseFilePath = Path.Combine(dataDirectory, "cadran.db");
        if (!File.Exists(databaseFilePath))
        {
            File.WriteAllText(databaseFilePath, "dummy");
        }

        var backupsDirectory = Path.Combine(workingDirectory, "backups");
        Directory.CreateDirectory(backupsDirectory);

        var b1 = Path.Combine(backupsDirectory, "cadran-backup-2026-10-25-080000-auto.db");
        var b2 = Path.Combine(backupsDirectory, "cadran-backup-2026-10-24-193000-manual.db");
        if (!File.Exists(b1)) File.WriteAllText(b1, new string('0', 1420000));
        if (!File.Exists(b2)) File.WriteAllText(b2, new string('0', 1395000));

        var configFilePath = Path.Combine(baseDir, "launcher.json");
        var configStore = new LauncherConfigurationStore(configFilePath);
        var settings = configStore.LoadSettings() with { Language = culture };

        var backupSupervisor = new DatabaseBackupSupervisor(databaseFilePath, backupsDirectory);
        var processSupervisor = new CadranProcessSupervisor("", workingDirectory, settings.Port);

        using var mainForm = new MainLauncherForm(configStore, backupSupervisor, processSupervisor);
        mainForm.PrepareForDocumentationScreenshot(culture, tabIndex: 1);
        mainForm.Size = new Size(880, 600);
        mainForm.StartPosition = FormStartPosition.CenterScreen;
        mainForm.Show();
        Application.DoEvents();
        Thread.Sleep(300);
        Application.DoEvents();

        using var bmp = new Bitmap(mainForm.ClientSize.Width, mainForm.ClientSize.Height);
        mainForm.DrawToBitmap(bmp, new Rectangle(0, 0, mainForm.ClientSize.Width, mainForm.ClientSize.Height));
        mainForm.Close();

        var docsDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "docs", "images", "screens"));
        Directory.CreateDirectory(docsDir);
        var artifactsDir = @"C:\Users\user\.gemini\antigravity\brain\bdc279ef-1c47-4c3b-8899-6796af37fe8a";

        var suffix = string.Equals(culture, "fr", StringComparison.Ordinal) ? "-fr" : "";
        var cleanPath = Path.Combine(docsDir, $"24-backups-supervision-screen{suffix}.png");
        bmp.Save(cleanPath, System.Drawing.Imaging.ImageFormat.Png);
        if (Directory.Exists(artifactsDir))
        {
            File.Copy(cleanPath, Path.Combine(artifactsDir, $"24-backups-supervision-screen{suffix}.png"), overwrite: true);
        }

        using var annotatedBmp = new Bitmap(bmp);
        using var g = Graphics.FromImage(annotatedBmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var items = mainForm.GetScreenshotPinDefinitions();
        foreach (var item in items)
        {
            using var ringPen = new Pen(item.Color, 2f);
            g.DrawRectangle(ringPen, item.Rect.X - 3, item.Rect.Y - 3, item.Rect.Width + 6, item.Rect.Height + 6);

            var pinX = item.Rect.X - 10;
            var pinY = item.Rect.Y - 10;
            using var brush = new SolidBrush(item.Color);
            g.FillEllipse(brush, pinX, pinY, 24, 24);
            using var whitePen = new Pen(Color.White, 2f);
            g.DrawEllipse(whitePen, pinX, pinY, 24, 24);

            using var textBrush = new SolidBrush(Color.White);
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(item.Number, font, textBrush, new RectangleF(pinX, pinY, 24, 24), sf);
        }

        var annotatedPath = Path.Combine(docsDir, $"24-backups-supervision-screen{suffix}-annotated.png");
        annotatedBmp.Save(annotatedPath, System.Drawing.Imaging.ImageFormat.Png);
        if (Directory.Exists(artifactsDir))
        {
            File.Copy(annotatedPath, Path.Combine(artifactsDir, $"24-backups-supervision-screen{suffix}-annotated.png"), overwrite: true);
        }
    }
}
