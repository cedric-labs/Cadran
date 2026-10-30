using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;

namespace Cadran.Launcher.Supervision;

/// <summary>
/// Superviseur gérant le cycle de vie, la détection et la communication avec le sous-processus Kestrel.
/// </summary>
/// <param name="applicationExecutablePath">Chemin physique de l'exécutable serveur Cadran.</param>
/// <param name="workingDirectory">Répertoire de travail du processus.</param>
/// <param name="port">Port d'écoute TCP local.</param>
/// <param name="httpClient">Client HTTP pour les requêtes de sonde et de clôture.</param>
public sealed class CadranProcessSupervisor(
    string applicationExecutablePath,
    string workingDirectory,
    int port,
    HttpClient? httpClient = null) : ICadranProcessSupervisor
{
    private readonly HttpClient _client = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Lock _stateLock = new();
    private Process? _process;
    private ProcessState _state = ProcessState.Stopped;
    private volatile bool _isServerListening;

    /// <inheritdoc />
    public event EventHandler<ProcessStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<LogReceivedEventArgs>? LogReceived;

    /// <inheritdoc />
    public ProcessState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                if (_state == value)
                {
                    return;
                }
                _state = value;
            }
            StateChanged?.Invoke(this, new ProcessStateChangedEventArgs(value));
        }
    }

    /// <inheritdoc />
    public int? ProcessId => _process is { HasExited: false } ? _process.Id : null;

    /// <inheritdoc />
    public string ServerUrl => $"http://localhost:{port}";

    /// <inheritdoc />
    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (State is ProcessState.Running or ProcessState.Starting)
        {
            return true;
        }

        if (!File.Exists(applicationExecutablePath))
        {
            EmitLog(LogLevel.Error, $"L'exécutable serveur est introuvable au chemin : {applicationExecutablePath}");
            return false;
        }

        State = ProcessState.Starting;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = applicationExecutablePath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.EnvironmentVariables["Server__Port"] = port.ToString(CultureInfo.InvariantCulture);
            startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = ServerUrl;
            // Ne pas ouvrir le navigateur depuis Kestrel lui-même car le superviseur s'en charge selon les paramètres de l'UI
            startInfo.EnvironmentVariables["Server__AutoLaunchBrowser"] = "false";

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            _isServerListening = false;

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    var level = LogEntry.ParseLogLevel(args.Data);
                    EmitLog(level, args.Data);

                    if (args.Data.Contains("Now listening on", StringComparison.OrdinalIgnoreCase) ||
                        args.Data.Contains("Application started", StringComparison.OrdinalIgnoreCase))
                    {
                        _isServerListening = true;
                    }
                }
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    EmitLog(LogLevel.Error, args.Data);
                }
            };

            process.Exited += (_, _) =>
            {
                State = ProcessState.Stopped;
                EmitLog(LogLevel.Information, "Le processus serveur s'est arrêté.");
            };

            if (!process.Start())
            {
                State = ProcessState.Stopped;
                EmitLog(LogLevel.Error, "Impossible de démarrer le sous-processus.");
                return false;
            }

            _process = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Attente active non bloquante de la disponibilité du serveur Kestrel
            var ready = await WaitForServerReadyAsync(cancellationToken).ConfigureAwait(false);
            if (ready)
            {
                State = ProcessState.Running;
                EmitLog(LogLevel.Information, $"Serveur Cadran opérationnel sur {ServerUrl} (PID: {process.Id})");
                return true;
            }

            EmitLog(LogLevel.Warning, "Le serveur n'a pas répondu dans le délai imparti de 15 secondes.");
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            State = ProcessState.Stopped;
            EmitLog(LogLevel.Critical, $"Erreur inattendue au démarrage du processus : {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null || _process.HasExited)
        {
            State = ProcessState.Stopped;
            return true;
        }

        State = ProcessState.Stopping;
        EmitLog(LogLevel.Information, "Arrêt gracieux du serveur en cours...");

        try
        {
            // Tentative d'arrêt propre via l'endpoint dédié
            try
            {
                var response = await _client.PostAsync($"{ServerUrl}/api/system/shutdown", null, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    EmitLog(LogLevel.Information, "Signal d'arrêt accepté par Kestrel.");
                }
            }
            catch (HttpRequestException)
            {
                // Serveur peut-être déjà en cours d'arrêt ou inaccessible
            }

            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            delayCts.CancelAfter(TimeSpan.FromSeconds(4));

            try
            {
                await _process.WaitForExitAsync(delayCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Timeout gracieux atteint : fermeture forcée
                if (!_process.HasExited)
                {
                    EmitLog(LogLevel.Warning, "Délai de grâce expiré. Fermeture forcée du processus...");
                    _process.Kill(entireProcessTree: true);
                }
            }

            State = ProcessState.Stopped;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            EmitLog(LogLevel.Error, $"Erreur lors de l'arrêt du processus : {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_process is not null)
        {
            if (!_process.HasExited)
            {
                await StopAsync().ConfigureAwait(false);
            }
            _process.Dispose();
            _process = null;
        }

        _client.Dispose();
    }

    private async Task<bool> WaitForServerReadyAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (_process is null || _process.HasExited)
            {
                return false;
            }

            // 1. Détection immédiate via signal des logs Kestrel
            if (_isServerListening)
            {
                return true;
            }

            // 2. Détection par sonde TCP locale non bloquante
            if (await CheckTcpPortOpenAsync(port, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            try
            {
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Terminaison normale de la boucle d'attente sur demande d'annulation
                return false;
            }
        }

        return false;
    }

    private static async Task<bool> CheckTcpPortOpenAsync(int targetPort, CancellationToken cancellationToken)
    {
        try
        {
            using var tcpClient = new System.Net.Sockets.TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(250);
            await tcpClient.ConnectAsync("127.0.0.1", targetPort, connectCts.Token).ConfigureAwait(false);
            return tcpClient.Connected;
        }
        catch (System.Net.Sockets.SocketException)
        {
            // Port non encore prêt ou connexion refusée pendant le démarrage
            return false;
        }
        catch (OperationCanceledException)
        {
            // Délai de 250ms dépassé ou demande d'annulation globale reçue
            return false;
        }
    }

    private void EmitLog(LogLevel level, string message)
    {
        var entry = new LogEntry(DateTimeOffset.UtcNow, level, message);
        LogReceived?.Invoke(this, new LogReceivedEventArgs(entry));
    }
}
