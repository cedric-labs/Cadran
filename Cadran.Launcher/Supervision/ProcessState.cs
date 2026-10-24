namespace Cadran.Launcher.Supervision;

/// <summary>
/// États possibles du cycle de vie du processus serveur Cadran.
/// </summary>
public enum ProcessState
{
    /// <summary>
    /// Le serveur est à l'arrêt.
    /// </summary>
    Stopped = 0,

    /// <summary>
    /// Le serveur est en cours d'initialisation et de démarrage.
    /// </summary>
    Starting = 1,

    /// <summary>
    /// Le serveur est opérationnel et prêt à traiter les requêtes HTTP.
    /// </summary>
    Running = 2,

    /// <summary>
    /// Le serveur est en cours d'arrêt gracieux.
    /// </summary>
    Stopping = 3
}
