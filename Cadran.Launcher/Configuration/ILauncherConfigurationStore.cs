namespace Cadran.Launcher.Configuration;

/// <summary>
/// Contrat du magasin de persistance des paramètres du superviseur Cadran.
/// </summary>
public interface ILauncherConfigurationStore
{
    /// <summary>
    /// Charge les paramètres persistés sur disque ou renvoie les valeurs par défaut si absent.
    /// </summary>
    /// <returns>Les paramètres du superviseur.</returns>
    LauncherSettings LoadSettings();

    /// <summary>
    /// Persiste les paramètres fournis sur le disque de manière atomique.
    /// </summary>
    /// <param name="settings">Paramètres à enregistrer.</param>
    void SaveSettings(LauncherSettings settings);
}
