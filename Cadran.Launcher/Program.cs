using Cadran.Launcher.Backups;
using Cadran.Launcher.Configuration;
using Cadran.Launcher.Supervision;
using Cadran.Launcher.UI;

namespace Cadran.Launcher;

internal static class Program
{
    private const string SingleInstanceMutexName = "Global\\Cadran_Launcher_SingleInstance_Mutex";

    [STAThread]
    private static void Main()
    {
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
}
