using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Cadran.Launcher.Backups;
using Cadran.Launcher.Configuration;
using Cadran.Launcher.Supervision;
using Cadran.Launcher.UI;

namespace Cadran.Launcher.Tests.UI;

/// <summary>
/// Épingle l'arborescence de contrôles construite par le constructeur de <see cref="MainLauncherForm"/>
/// avant son découpage par zones : aucun test d'UI n'existait pour détecter une régression de disposition.
/// </summary>
public sealed class MainLauncherFormTests
{
    private const string ControlTreeSha256 = "DFBB1FB9D3083828E141FB4EFCB43EBBC2F6A93DDC784340536E271231467E9C";

    [Fact]
    public void Constructor_WhenSettingsAreFrench_BuildsFourLocalizedTabsInOrder()
    {
        var tabTitles = RunOnStaThread(() =>
        {
            using var form = CreateForm();
            return Flatten(form).OfType<TabPage>().Select(tab => tab.Text).ToArray();
        });

        tabTitles.Should().HaveCount(4);
        tabTitles.Should().OnlyContain(title => !string.IsNullOrWhiteSpace(title) && !title.Contains('.'));
    }

    [Fact]
    public void Constructor_WhenSettingsProvided_InitializesSettingsControlsFromSettings()
    {
        var snapshot = RunOnStaThread(() =>
        {
            using var form = CreateForm();
            var controls = Flatten(form).ToArray();
            var numerics = controls.OfType<NumericUpDown>().Select(n => n.Value).ToArray();
            var checks = controls.OfType<CheckBox>().Select(c => c.Checked).ToArray();
            return (numerics, checks);
        });

        snapshot.numerics.Should().Equal(5123m, 7m, 21m);
        snapshot.checks.Should().Equal(true, false, true, false, true);
    }

    [Fact]
    public void Constructor_WhenSettingsProvided_SelectsSettingsLanguageAndRetentionMode()
    {
        var selected = RunOnStaThread(() =>
        {
            using var form = CreateForm();
            return Flatten(form).OfType<ComboBox>().Select(c => c.SelectedIndex).ToArray();
        });

        selected.Should().Equal(0, 2, 0);
    }

    [Fact]
    public void Constructor_WhenBuilt_ProducesPinnedControlTree()
    {
        var dump = RunOnStaThread(() =>
        {
            using var form = CreateForm();
            return DescribeTree(form);
        });

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dump)));

        hash.Should().Be(ControlTreeSha256, because: dump);
    }

    private static MainLauncherForm CreateForm()
    {
        var settings = new LauncherSettings(
            Port: 5123,
            Language: "fr",
            AutoStartServer: false,
            AutoOpenBrowser: true,
            StartMinimizedInTray: false,
            EnableAutomaticBackups: true,
            RetentionPolicy: new BackupRetentionOptions(BackupRetentionMode.Both, 7, 21));
        return new MainLauncherForm(new FixedConfigurationStore(settings), new EmptyBackupSupervisor(), new StoppedProcessSupervisor());
    }

    private static IEnumerable<Control> Flatten(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private static string DescribeTree(Control root)
    {
        var lines = new List<string> { Describe(root, 0) };
        AppendChildren(root, 1, lines);
        return string.Join('\n', lines);
    }

    private static void AppendChildren(Control parent, int depth, List<string> lines)
    {
        // Les enfants internes d'un NumericUpDown (flèches, zone de saisie) sont créés par WinForms, pas par le formulaire.
        if (parent is NumericUpDown)
        {
            return;
        }

        foreach (Control child in parent.Controls)
        {
            lines.Add(Describe(child, depth));
            AppendChildren(child, depth + 1, lines);
        }
    }

    private static string Describe(Control control, int depth)
    {
        var extra = control switch
        {
            NumericUpDown n => $"value={n.Value};min={n.Minimum};max={n.Maximum}",
            CheckBox c => $"checked={c.Checked}",
            ComboBox c => $"index={c.SelectedIndex};items={string.Join(',', c.Items.Cast<object>())}",
            ListView l => $"columns={string.Join(',', l.Columns.Cast<ColumnHeader>().Select(h => $"{h.Text}:{h.Width}"))}",
            _ => string.Empty
        };

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{new string(' ', depth)}{control.GetType().Name}|{control.Text}|{control.Dock}|{control.Location}|{DescribeSize(control)}|{control.BackColor.ToArgb()}|{control.Font.Name}:{control.Font.Size}:{control.Font.Style}|{extra}");
    }

    // Les tailles mesurées (AutoSize) ou dérivées de l'ancrage dépendent de la police et du DPI de la machine : seules les tailles fixées par le code sont épinglées.
    private static string DescribeSize(Control control) =>
        control.AutoSize || control.Dock != DockStyle.None || control is Form or TabPage
            ? "derived"
            : control.Size.ToString();

    private static T RunOnStaThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                // L'exception est relancée sur le thread du test pour que xUnit la rapporte.
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("La construction du formulaire a échoué sur le thread STA.", failure);
        }

        return result;
    }

    private sealed class FixedConfigurationStore(LauncherSettings initialSettings) : ILauncherConfigurationStore
    {
        public LauncherSettings LoadSettings() => initialSettings;

        public void SaveSettings(LauncherSettings settings)
        {
        }
    }

    private sealed class EmptyBackupSupervisor : IDatabaseBackupSupervisor
    {
        public Task<BackupResult> CreateBackupAsync(string reason, BackupRetentionOptions? retentionOptions = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IReadOnlyList<BackupMetadata> GetAvailableBackups() => [];

        public Task<RestoreResult> RestoreBackupAsync(string backupFilePath, bool isServerRunning, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public int ApplyRetentionPolicy(BackupRetentionOptions options) => 0;
    }

    internal sealed class StoppedProcessSupervisor : ICadranProcessSupervisor
    {
        public event EventHandler<ProcessStateChangedEventArgs>? StateChanged;

        public event EventHandler<LogReceivedEventArgs>? LogReceived;

        public ProcessState State => ProcessState.Stopped;

        public int? ProcessId => null;

        public string ServerUrl => "http://localhost:5123";

        public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<bool> StopAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void RaiseStateChanged(ProcessState state) => StateChanged?.Invoke(this, new ProcessStateChangedEventArgs(state));

        public void RaiseLogReceived(LogEntry entry) => LogReceived?.Invoke(this, new LogReceivedEventArgs(entry));
    }
}
