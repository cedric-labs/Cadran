namespace Cadran.Launcher.Supervision;

/// <summary>
/// Arguments d'événement lors de la réception d'une ligne de journal.
/// </summary>
/// <param name="entry">Entrée de journal reçue.</param>
public sealed class LogReceivedEventArgs(LogEntry entry) : EventArgs
{
    /// <summary>
    /// Obtient l'entrée de journal reçue.
    /// </summary>
    public LogEntry Entry { get; } = entry;
}
