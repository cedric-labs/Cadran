using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cadran.Launcher.Configuration;

/// <summary>
/// Gestionnaire de persistance JSON des paramètres du superviseur.
/// </summary>
/// <param name="configurationFilePath">Chemin d'accès au fichier de configuration JSON.</param>
public sealed class LauncherConfigurationStore(string configurationFilePath) : ILauncherConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <inheritdoc />
    public LauncherSettings LoadSettings()
    {
        if (!File.Exists(configurationFilePath))
        {
            return new LauncherSettings();
        }

        try
        {
            var json = File.ReadAllText(configurationFilePath);
            var settings = JsonSerializer.Deserialize<LauncherSettings>(json, SerializerOptions);
            return settings?.Normalize() ?? new LauncherSettings();
        }
        catch (JsonException)
        {
            // En cas de corruption accidentelle du fichier de configuration, repli sain sur les valeurs par défaut
            return new LauncherSettings();
        }
    }

    /// <inheritdoc />
    public void SaveSettings(LauncherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize();
        var directory = Path.GetDirectoryName(configurationFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        var tempFilePath = $"{configurationFilePath}.tmp";

        File.WriteAllText(tempFilePath, json);
        File.Move(tempFilePath, configurationFilePath, overwrite: true);
    }
}
