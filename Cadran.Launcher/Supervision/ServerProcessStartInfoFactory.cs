using System.Diagnostics;
using System.Globalization;

namespace Cadran.Launcher.Supervision;

/// <summary>
/// Prépare le démarrage du processus serveur : sorties redirigées pour la supervision, port et adresse transmis par variables d'environnement.
/// </summary>
internal static class ServerProcessStartInfoFactory
{
    /// <summary>
    /// Crée les paramètres de démarrage sans fenêtre ; le serveur n'ouvre pas le navigateur, le superviseur s'en charge selon les réglages de l'interface.
    /// </summary>
    public static ProcessStartInfo Create(string executablePath, string workingDirectory, int port, string serverUrl)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.EnvironmentVariables["Server__Port"] = port.ToString(CultureInfo.InvariantCulture);
        startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = serverUrl;
        startInfo.EnvironmentVariables["Server__AutoLaunchBrowser"] = "false";
        return startInfo;
    }
}
