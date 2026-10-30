namespace Cadran.Launcher.UI;

/// <summary>
/// Crée, sous le répertoire de travail du serveur, une base et deux sauvegardes factices pour que la capture de documentation affiche une liste non vide.
/// </summary>
internal static class DocumentationScreenshotWorkspace
{
    private static readonly (string FileName, int Length)[] SampleBackups =
    [
        ("cadran-backup-2026-10-25-080000-auto.db", 1420000),
        ("cadran-backup-2026-10-24-193000-manual.db", 1395000)
    ];

    /// <summary>
    /// Prépare les fichiers factices manquants sans toucher à ceux qui existent déjà.
    /// </summary>
    /// <param name="workingDirectory">Répertoire de travail du serveur Cadran.</param>
    /// <returns>Le chemin de la base factice et celui du dossier des sauvegardes.</returns>
    public static (string DatabaseFilePath, string BackupsDirectory) Prepare(string workingDirectory)
    {
        var dataDirectory = Path.Combine(workingDirectory, "data");
        Directory.CreateDirectory(dataDirectory);
        var databaseFilePath = Path.Combine(dataDirectory, "cadran.db");
        WriteIfMissing(databaseFilePath, "dummy");

        var backupsDirectory = Path.Combine(workingDirectory, "backups");
        Directory.CreateDirectory(backupsDirectory);
        foreach (var (fileName, length) in SampleBackups)
        {
            WriteIfMissing(Path.Combine(backupsDirectory, fileName), new string('0', length));
        }

        return (databaseFilePath, backupsDirectory);
    }

    private static void WriteIfMissing(string path, string content)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, content);
        }
    }
}
