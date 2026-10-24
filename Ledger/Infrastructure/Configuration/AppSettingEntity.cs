namespace Ledger.Infrastructure.Configuration;

/// <summary>
/// Représente un paramètre de configuration dynamique stocké dans la base SQLite.
/// </summary>
public class AppSettingEntity
{
    public required string Key { get; set; }
    public string? Value { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
