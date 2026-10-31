namespace Ledger.Infrastructure.Configuration;

/// <summary>
/// Détermine l'adresse d'écoute de Kestrel à partir de la section <c>Server</c> de la configuration.
/// </summary>
public static class ServerUrlResolver
{
    /// <summary>
    /// Calcule l'adresse d'écoute issue de <c>Server:Port</c>, <c>Server:Scheme</c> et <c>Server:AllowNetworkAccess</c>.
    /// </summary>
    /// <param name="configuration">Configuration de l'application, ligne de commande et variables d'environnement comprises.</param>
    /// <returns>
    /// L'adresse à passer à <c>UseUrls</c>, ou <c>null</c> lorsqu'aucun port n'est configuré ou que <c>urls</c>
    /// (<c>--urls</c>, <c>ASPNETCORE_URLS</c>) est déjà renseigné : un appel à <c>UseUrls</c> l'écraserait.
    /// </returns>
    public static string? Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!string.IsNullOrWhiteSpace(configuration["urls"]))
            return null;

        var server = configuration.GetSection("Server");
        var port = server.GetValue<int?>("Port");
        if (port is null)
            return null;

        var scheme = server.GetValue<string>("Scheme") ?? Uri.UriSchemeHttp;
        var host = server.GetValue<bool?>("AllowNetworkAccess") ?? false ? "0.0.0.0" : "localhost";
        return $"{scheme}://{host}:{port.Value}";
    }
}
