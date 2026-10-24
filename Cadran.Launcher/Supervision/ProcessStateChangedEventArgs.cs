namespace Cadran.Launcher.Supervision;

/// <summary>
/// Arguments d'événement lors d'un changement d'état du processus serveur.
/// </summary>
/// <param name="state">Nouvel état du processus.</param>
public sealed class ProcessStateChangedEventArgs(ProcessState state) : EventArgs
{
    /// <summary>
    /// Obtient le nouvel état du processus serveur.
    /// </summary>
    public ProcessState State { get; } = state;
}
