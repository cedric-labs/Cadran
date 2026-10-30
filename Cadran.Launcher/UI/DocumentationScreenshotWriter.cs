using System.Drawing.Imaging;

namespace Cadran.Launcher.UI;

/// <summary>
/// Enregistre une capture au format PNG et la copie dans le répertoire d'artefacts de l'environnement de développement lorsqu'il existe.
/// </summary>
internal static class DocumentationScreenshotWriter
{
    /// <summary>
    /// Écrit l'image à <paramref name="path"/> puis, si <paramref name="artifactsDirectory"/> existe, la copie sous le même nom de fichier.
    /// </summary>
    public static void Save(Bitmap bitmap, string path, string artifactsDirectory)
    {
        bitmap.Save(path, ImageFormat.Png);
        if (Directory.Exists(artifactsDirectory))
        {
            File.Copy(path, Path.Combine(artifactsDirectory, Path.GetFileName(path)), overwrite: true);
        }
    }
}
