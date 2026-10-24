namespace Cadran.Launcher.Supervision;

/// <summary>
/// Tampon circulaire en mémoire thread-safe pour la rétention et le filtrage des lignes de journal du serveur.
/// </summary>
/// <param name="capacity">Nombre maximal de lignes conservées en mémoire (par défaut 1000).</param>
public sealed class LogBufferStream(int capacity = 1000)
{
    private readonly Lock _syncRoot = new();
    private readonly LinkedList<LogEntry> _entries = new();
    private readonly int _capacity = Math.Max(100, capacity);

    /// <summary>
    /// Ajoute une entrée de journalisation dans le tampon circulaire.
    /// Évince la plus ancienne si la capacité maximale est atteinte.
    /// </summary>
    /// <param name="entry">Entrée de journal à ajouter.</param>
    public void Append(LogEntry entry)
    {
        lock (_syncRoot)
        {
            if (_entries.Count >= _capacity)
            {
                _entries.RemoveFirst();
            }

            _entries.AddLast(entry);
        }
    }

    /// <summary>
    /// Récupère l'intégralité des entrées actuellement en mémoire.
    /// </summary>
    /// <returns>Une capture instantanée des entrées de journal.</returns>
    public IReadOnlyList<LogEntry> GetAll()
    {
        lock (_syncRoot)
        {
            return _entries.ToList();
        }
    }

    /// <summary>
    /// Filtre les entrées de journal selon un mot-clé textuel et un niveau minimal de sévérité.
    /// </summary>
    /// <param name="keyword">Mot-clé optionnel insensible à la casse.</param>
    /// <param name="minimumLevel">Niveau de sévérité minimal requis.</param>
    /// <returns>Les entrées satisfaisant les critères.</returns>
    public IReadOnlyList<LogEntry> Filter(string? keyword, LogLevel? minimumLevel = null)
    {
        lock (_syncRoot)
        {
            IEnumerable<LogEntry> query = _entries;

            if (minimumLevel.HasValue)
            {
                query = query.Where(e => e.Level >= minimumLevel.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var cleanKeyword = keyword.Trim();
                query = query.Where(e => e.Message.Contains(cleanKeyword, StringComparison.OrdinalIgnoreCase));
            }

            return query.ToList();
        }
    }

    /// <summary>
    /// Vide l'intégralité du tampon de journalisation.
    /// </summary>
    public void Clear()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
        }
    }
}
