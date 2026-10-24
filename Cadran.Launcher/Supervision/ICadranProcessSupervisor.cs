namespace Cadran.Launcher.Supervision;

/// <summary>
/// Contrat de supervision et d'orchestration du processus serveur Cadran.
/// </summary>
public interface ICadranProcessSupervisor : IAsyncDisposable
{
    /// <summary>
    /// Événement notifié lors d'un changement d'état du serveur.
    /// </summary>
    event EventHandler<ProcessStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Événement notifié lors de la réception d'une ligne de journal depuis la sortie console du serveur.
    /// </summary>
    event EventHandler<LogReceivedEventArgs>? LogReceived;

    /// <summary>
    /// Obtient l'état courant du processus serveur.
    /// </summary>
    ProcessState State { get; }

    /// <summary>
    /// Obtient l'identifiant du processus OS s'il est en cours d'exécution.
    /// </summary>
    int? ProcessId { get; }

    /// <summary>
    /// Obtient l'URL HTTP d'accès local au serveur.
    /// </summary>
    string ServerUrl { get; }

    /// <summary>
    /// Démarre le serveur en arrière-plan sans fenêtre de terminal.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>True si le serveur a démarré et est opérationnel, False sinon.</returns>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Arrête proprement le serveur en ordonnant sa clôture gracieuse.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>True si le serveur s'est arrêté correctement, False sinon.</returns>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);
}
