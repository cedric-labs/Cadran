using System.Diagnostics;
using System.Globalization;
using Cadran.Launcher.Backups;
using Cadran.Launcher.Configuration;
using Cadran.Launcher.Supervision;
using LocaleForge.Localization;

namespace Cadran.Launcher.UI;

/// <summary>
/// Fenêtre principale de contrôle, de supervision et de paramétrage de Cadran.
/// </summary>
public sealed class MainLauncherForm : Form
{
    private static readonly Color GreenColor = Color.FromArgb(25, 135, 84);
    private static readonly Color RedColor = Color.FromArgb(220, 53, 69);
    private static readonly Color BlueColor = Color.FromArgb(13, 110, 253);
    private static readonly Color GrayColor = Color.FromArgb(108, 117, 125);

    private readonly ILauncherConfigurationStore _configStore;
    private readonly IDatabaseBackupSupervisor _backupSupervisor;
    private readonly ICadranProcessSupervisor _processSupervisor;
    private readonly JsonStringLocalizer _localizer;
    private readonly LogBufferStream _logBuffer = new(1500);

    private LauncherSettings _currentSettings;
    private bool _isExplicitExit;
    private bool _hasShownTrayBalloon;
    private bool _isUpdatingLanguage;

    // Éléments de la zone de notification (Systray)
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayContextMenu;
    private readonly ToolStripMenuItem _trayOpenBrowserItem;
    private readonly ToolStripMenuItem _trayStatusItem;
    private readonly ToolStripMenuItem _trayStartServerItem;
    private readonly ToolStripMenuItem _trayStopServerItem;
    private readonly ToolStripMenuItem _trayShowWindowItem;
    private readonly ToolStripMenuItem _trayExitItem;

    // Barre d'en-tête
    private readonly Label _statusDot;
    private readonly Label _statusLabel;
    private readonly LinkLabel _urlLink;
    private readonly Label _headerLangLabel;
    private readonly ComboBox _headerLangCombo;

    // Onglets principaux
    private readonly TabControl _tabControl;
    private readonly TabPage _dashboardTab;
    private readonly TabPage _backupsTab;
    private readonly TabPage _logsTab;
    private readonly TabPage _settingsTab;

    // Onglet 1 : Tableau de bord
    private readonly Panel _cardPanel;
    private readonly Label _cardStatusTitle;
    private readonly Label _cardStatusDescription;
    private readonly Button _dashboardOpenBrowserButton;
    private readonly GroupBox _dashboardActionsGroup;
    private readonly Button _dashboardStartButton;
    private readonly Button _dashboardStopButton;
    private readonly Button _dashboardRestartButton;
    private readonly GroupBox _dashboardInfoGroup;
    private readonly Label _lastBackupLabel;
    private readonly Button _quickBackupButton;

    // Onglet 2 : Sauvegardes & Restauration
    private readonly ListView _backupsListView;
    private readonly Button _createBackupButton;
    private readonly Button _restoreBackupButton;
    private readonly Button _openBackupsFolderButton;

    // Onglet 3 : Console & Journaux
    private readonly RichTextBox _logBox;
    private readonly Label _searchLabel;
    private readonly TextBox _searchBox;
    private readonly CheckBox _autoScrollCheckBox;
    private readonly Button _clearLogsButton;
    private readonly Button _copyLogsButton;

    // Onglet 4 : Paramètres
    private readonly Label _portLabel;
    private readonly NumericUpDown _portInput;
    private readonly CheckBox _autoStartCheckBox;
    private readonly CheckBox _autoOpenBrowserCheckBox;
    private readonly CheckBox _startMinimizedCheckBox;
    private readonly CheckBox _enableAutoBackupsCheckBox;
    private readonly Label _settingsLangLabel;
    private readonly ComboBox _settingsLangCombo;
    private readonly GroupBox _retentionGroupBox;
    private readonly Label _retentionModeLabel;
    private readonly ComboBox _retentionModeCombo;
    private readonly Label _maxFilesLabel;
    private readonly NumericUpDown _maxBackupFilesInput;
    private readonly Label _maxDaysLabel;
    private readonly NumericUpDown _maxBackupDaysInput;
    private readonly Button _saveSettingsButton;

    /// <summary>
    /// Initialise une nouvelle instance du formulaire de supervision.
    /// </summary>
    public MainLauncherForm(
        ILauncherConfigurationStore configStore,
        IDatabaseBackupSupervisor backupSupervisor,
        ICadranProcessSupervisor processSupervisor)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _backupSupervisor = backupSupervisor ?? throw new ArgumentNullException(nameof(backupSupervisor));
        _processSupervisor = processSupervisor ?? throw new ArgumentNullException(nameof(processSupervisor));

        _currentSettings = _configStore.LoadSettings().Normalize();

        var cultureCode = _currentSettings.Language ?? "fr";
        CultureInfo.CurrentUICulture = new CultureInfo(cultureCode);
        CultureInfo.CurrentCulture = new CultureInfo(cultureCode);

        var localesDirectory = ResolveLocalesDirectory();
        _localizer = new JsonStringLocalizer(localesDirectory);

        // 1. Paramétrage global de la fenêtre
        Text = _localizer["App.Title"];
        Size = new Size(900, 680);
        MinimumSize = new Size(800, 580);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        var appIcon = LoadApplicationIcon();
        if (appIcon is not null)
        {
            Icon = appIcon;
        }

        // 2. Barre d'en-tête supérieure
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            BackColor = Color.FromArgb(248, 249, 250),
            Padding = new Padding(16, 10, 16, 10)
        };

        _statusDot = new Label
        {
            Size = new Size(16, 16),
            Location = new Point(18, 20),
            BackColor = Color.Gray
        };

        _statusLabel = new Label
        {
            Text = _localizer["Header.StatusInitializing"],
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            Location = new Point(42, 16),
            AutoSize = true
        };

        _urlLink = new LinkLabel
        {
            Text = _processSupervisor.ServerUrl,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Location = new Point(44, 42),
            AutoSize = true
        };
        _urlLink.LinkClicked += (_, _) => OpenBrowser();

        var headerLangPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 160,
            BackColor = Color.Transparent
        };

        _headerLangLabel = new Label
        {
            Text = "🌐",
            Location = new Point(12, 26),
            Size = new Size(24, 24),
            Font = new Font("Segoe UI", 11f)
        };

        _headerLangCombo = new ComboBox
        {
            Location = new Point(38, 23),
            Size = new Size(110, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9.5f)
        };
        _headerLangCombo.Items.AddRange(["Français", "English"]);
        _headerLangCombo.SelectedIndex = cultureCode.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _headerLangCombo.SelectedIndexChanged += (_, _) => OnLanguageSelectionChanged();

        headerLangPanel.Controls.AddRange([
            _headerLangLabel,
            _headerLangCombo
        ]);

        headerPanel.Controls.AddRange([
            _statusDot, _statusLabel, _urlLink, headerLangPanel
        ]);

        // 3. Onglets principaux
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(16, 8)
        };

        // =========================================================================
        // ONGLET 1 : TABLEAU DE BORD (Accueil)
        // =========================================================================
        _dashboardTab = new TabPage(_localizer["Dashboard.TabTitle"]);
        var dashboardPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(24),
            BackColor = Color.White
        };

        _cardPanel = new Panel
        {
            Location = new Point(24, 20),
            Size = new Size(800, 155),
            BackColor = Color.FromArgb(245, 248, 250),
            Padding = new Padding(20)
        };

        _cardStatusTitle = new Label
        {
            Text = _localizer["Dashboard.StatusStoppedTitle"],
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            Location = new Point(20, 18),
            AutoSize = true,
            ForeColor = Color.FromArgb(70, 70, 70)
        };

        _cardStatusDescription = new Label
        {
            Text = _localizer["Dashboard.StatusStoppedDesc"],
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            Location = new Point(22, 56),
            AutoSize = true,
            ForeColor = Color.FromArgb(110, 110, 110)
        };

        _dashboardOpenBrowserButton = new Button
        {
            Text = _localizer["Dashboard.OpenBrowser"],
            Location = new Point(20, 92),
            Size = new Size(340, 44),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold)
        };
        _dashboardOpenBrowserButton.FlatAppearance.BorderSize = 1;
        _dashboardOpenBrowserButton.Click += (_, _) => OpenBrowser();

        _cardPanel.Controls.AddRange([_cardStatusTitle, _cardStatusDescription, _dashboardOpenBrowserButton]);

        // Contrôles du serveur
        _dashboardActionsGroup = new GroupBox
        {
            Text = _localizer["Dashboard.ServerControlGroup"],
            Location = new Point(24, 195),
            Size = new Size(800, 110),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Padding = new Padding(16)
        };

        _dashboardStartButton = new Button
        {
            Text = _localizer["Dashboard.StartServer"],
            Location = new Point(24, 38),
            Size = new Size(220, 44),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        _dashboardStartButton.FlatAppearance.BorderSize = 1;
        _dashboardStartButton.Click += (_, _) => RunAsyncSafe(StartServerWorkflowAsync, "Démarrage du serveur");

        _dashboardStopButton = new Button
        {
            Text = _localizer["Dashboard.StopServer"],
            Location = new Point(260, 38),
            Size = new Size(220, 44),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        _dashboardStopButton.FlatAppearance.BorderSize = 1;
        _dashboardStopButton.Click += (_, _) => RunAsyncSafe(StopServerWorkflowAsync, "Arrêt du serveur");

        _dashboardRestartButton = new Button
        {
            Text = _localizer["Dashboard.RestartServer"],
            Location = new Point(496, 38),
            Size = new Size(180, 44),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        _dashboardRestartButton.FlatAppearance.BorderSize = 1;
        _dashboardRestartButton.Click += (_, _) => RunAsyncSafe(RestartServerWorkflowAsync, "Redémarrage du serveur");

        _dashboardActionsGroup.Controls.AddRange([_dashboardStartButton, _dashboardStopButton, _dashboardRestartButton]);

        // Sauvegardes rapides
        _dashboardInfoGroup = new GroupBox
        {
            Text = _localizer["Dashboard.DatabaseGroup"],
            Location = new Point(24, 325),
            Size = new Size(800, 130),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Padding = new Padding(16)
        };

        _lastBackupLabel = new Label
        {
            Text = _localizer["Dashboard.LastBackupNone"],
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Location = new Point(24, 34),
            AutoSize = true
        };

        _quickBackupButton = new Button
        {
            Text = _localizer["Dashboard.QuickBackup"],
            Location = new Point(24, 68),
            Size = new Size(340, 38),
            BackColor = Color.FromArgb(245, 247, 250),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _quickBackupButton.FlatAppearance.BorderColor = Color.FromArgb(205, 210, 215);
        _quickBackupButton.Click += (_, _) => RunAsyncSafe(CreateManualBackupAsync, "Création manuelle de sauvegarde");

        _dashboardInfoGroup.Controls.AddRange([_lastBackupLabel, _quickBackupButton]);

        dashboardPanel.Controls.AddRange([_cardPanel, _dashboardActionsGroup, _dashboardInfoGroup]);
        _dashboardTab.Controls.Add(dashboardPanel);

        // =========================================================================
        // ONGLET 2 : SAUVEGARDES & RESTAURATION
        // =========================================================================
        _backupsTab = new TabPage(_localizer["Backups.TabTitle"]);
        var backupsHeader = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8) };

        _createBackupButton = new Button
        {
            Text = _localizer["Backups.Create"],
            Location = new Point(8, 8),
            Size = new Size(190, 34),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _createBackupButton.FlatAppearance.BorderSize = 1;
        ApplyButtonStyle(_createBackupButton, enabled: true, Color.FromArgb(245, 247, 250), Color.FromArgb(30, 30, 30));
        _createBackupButton.Click += (_, _) => OnCreateBackupClicked();

        _restoreBackupButton = new Button
        {
            Text = _localizer["Backups.Restore"],
            Location = new Point(208, 8),
            Size = new Size(200, 34),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _restoreBackupButton.FlatAppearance.BorderSize = 1;
        ApplyButtonStyle(_restoreBackupButton, enabled: true, Color.FromArgb(245, 247, 250), Color.FromArgb(30, 30, 30));
        _restoreBackupButton.Click += (_, _) => OnRestoreBackupClicked();

        _openBackupsFolderButton = new Button
        {
            Text = _localizer["Backups.OpenFolder"],
            Location = new Point(418, 8),
            Size = new Size(210, 34),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _openBackupsFolderButton.FlatAppearance.BorderSize = 1;
        ApplyButtonStyle(_openBackupsFolderButton, enabled: true, Color.FromArgb(245, 247, 250), Color.FromArgb(30, 30, 30));
        _openBackupsFolderButton.Click += (_, _) => OpenBackupsFolder();

        backupsHeader.Controls.AddRange([_createBackupButton, _restoreBackupButton, _openBackupsFolderButton]);

        _backupsListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false
        };
        _backupsListView.Columns.Add(_localizer["Backups.ColDate"], 180);
        _backupsListView.Columns.Add(_localizer["Backups.ColName"], 300);
        _backupsListView.Columns.Add(_localizer["Backups.ColSize"], 110);
        _backupsListView.Columns.Add(_localizer["Backups.ColReason"], 160);

        _backupsTab.Controls.AddRange([_backupsListView, backupsHeader]);

        // =========================================================================
        // ONGLET 3 : CONSOLE & JOURNAUX
        // =========================================================================
        _logsTab = new TabPage(_localizer["Logs.TabTitle"]);
        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Consolas", 9.5f),
            BorderStyle = BorderStyle.None
        };

        var logsBottomBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(8, 6, 8, 6)
        };

        _searchLabel = new Label { Text = _localizer["Logs.Filter"], Location = new Point(8, 12), AutoSize = true };
        _searchBox = new TextBox { Location = new Point(68, 8), Width = 220 };
        _searchBox.TextChanged += (_, _) => RefreshLogsView();

        _autoScrollCheckBox = new CheckBox { Text = _localizer["Logs.AutoScroll"], Location = new Point(300, 10), Checked = true, AutoSize = true };

        _clearLogsButton = new Button { Text = _localizer["Logs.Clear"], Location = new Point(440, 7), Width = 90, Height = 28 };
        _clearLogsButton.Click += (_, _) =>
        {
            _logBuffer.Clear();
            _logBox.Clear();
        };

        _copyLogsButton = new Button { Text = _localizer["Logs.CopyAll"], Location = new Point(540, 7), Width = 110, Height = 28 };
        _copyLogsButton.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_logBox.Text))
            {
                Clipboard.SetText(_logBox.Text);
            }
        };

        logsBottomBar.Controls.AddRange([_searchLabel, _searchBox, _autoScrollCheckBox, _clearLogsButton, _copyLogsButton]);
        _logsTab.Controls.AddRange([_logBox, logsBottomBar]);

        // =========================================================================
        // ONGLET 4 : PARAMÈTRES
        // =========================================================================
        _settingsTab = new TabPage(_localizer["Settings.TabTitle"]);
        var settingsPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24) };

        _portLabel = new Label { Text = _localizer["Settings.HttpPort"], Location = new Point(20, 24), AutoSize = true };
        _portInput = new NumericUpDown { Location = new Point(240, 22), Width = 110, Minimum = 1024, Maximum = 65535, Value = _currentSettings.Port };

        _autoStartCheckBox = new CheckBox { Text = _localizer["Settings.AutoStart"], Location = new Point(20, 60), AutoSize = true, Checked = _currentSettings.AutoStartServer };
        _autoOpenBrowserCheckBox = new CheckBox { Text = _localizer["Settings.AutoOpen"], Location = new Point(20, 92), AutoSize = true, Checked = _currentSettings.AutoOpenBrowser };
        _startMinimizedCheckBox = new CheckBox { Text = _localizer["Settings.StartMinimized"], Location = new Point(20, 124), AutoSize = true, Checked = _currentSettings.StartMinimizedInTray };
        _enableAutoBackupsCheckBox = new CheckBox { Text = _localizer["Settings.AutoBackups"], Location = new Point(20, 156), AutoSize = true, Checked = _currentSettings.EnableAutomaticBackups };

        _settingsLangLabel = new Label { Text = _localizer["Settings.InterfaceLanguage"], Location = new Point(20, 192), AutoSize = true };
        _settingsLangCombo = new ComboBox
        {
            Location = new Point(240, 188),
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _settingsLangCombo.Items.AddRange(["Français", "English"]);
        _settingsLangCombo.SelectedIndex = cultureCode.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _settingsLangCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_isUpdatingLanguage)
            {
                return;
            }

            _headerLangCombo.SelectedIndex = _settingsLangCombo.SelectedIndex;
        };

        _retentionGroupBox = new GroupBox
        {
            Text = _localizer["Settings.RetentionGroup"],
            Location = new Point(20, 234),
            Size = new Size(640, 150)
        };

        _retentionModeLabel = new Label { Text = _localizer["Settings.RetentionMode"], Location = new Point(16, 32), AutoSize = true };
        _retentionModeCombo = new ComboBox
        {
            Location = new Point(220, 28),
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _retentionModeCombo.Items.AddRange([
            _localizer["Settings.ModeFileCount"].Value,
            _localizer["Settings.ModeDays"].Value,
            _localizer["Settings.ModeBoth"].Value
        ]);
        _retentionModeCombo.SelectedIndex = (int)_currentSettings.RetentionPolicy.Mode;

        _maxFilesLabel = new Label { Text = _localizer["Settings.MaxFiles"], Location = new Point(16, 68), AutoSize = true };
        _maxBackupFilesInput = new NumericUpDown { Location = new Point(220, 66), Width = 90, Minimum = 1, Maximum = 200, Value = _currentSettings.RetentionPolicy.MaxBackupFiles };

        _maxDaysLabel = new Label { Text = _localizer["Settings.MaxDays"], Location = new Point(16, 104), AutoSize = true };
        _maxBackupDaysInput = new NumericUpDown { Location = new Point(220, 102), Width = 90, Minimum = 1, Maximum = 365, Value = _currentSettings.RetentionPolicy.MaxBackupDays };

        _retentionGroupBox.Controls.AddRange([_retentionModeLabel, _retentionModeCombo, _maxFilesLabel, _maxBackupFilesInput, _maxDaysLabel, _maxBackupDaysInput]);

        _saveSettingsButton = new Button
        {
            Text = _localizer["Settings.Save"],
            Location = new Point(20, 404),
            Size = new Size(240, 42),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        _saveSettingsButton.FlatAppearance.BorderSize = 1;
        ApplyButtonStyle(_saveSettingsButton, enabled: true, BlueColor);
        _saveSettingsButton.Click += (_, _) => SaveSettings();

        settingsPanel.Controls.AddRange([
            _portLabel, _portInput,
            _autoStartCheckBox, _autoOpenBrowserCheckBox, _startMinimizedCheckBox, _enableAutoBackupsCheckBox,
            _settingsLangLabel, _settingsLangCombo,
            _retentionGroupBox,
            _saveSettingsButton
        ]);
        _settingsTab.Controls.Add(settingsPanel);

        // Ajout des onglets : Tableau de bord en première position
        _tabControl.TabPages.AddRange([_dashboardTab, _backupsTab, _logsTab, _settingsTab]);
        Controls.AddRange([_tabControl, headerPanel]);

        // 4. Configuration de la zone de notification (Systray)
        _trayContextMenu = new ContextMenuStrip();
        _trayOpenBrowserItem = new ToolStripMenuItem(_localizer["Tray.OpenBrowser"], null, (_, _) => OpenBrowser())
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Enabled = false
        };
        _trayStatusItem = new ToolStripMenuItem(_localizer["Tray.StatusOffline"]) { Enabled = false };
        _trayStartServerItem = new ToolStripMenuItem(_localizer["Tray.StartServer"], null, (_, _) => RunAsyncSafe(StartServerWorkflowAsync, "Démarrage du serveur"));
        _trayStopServerItem = new ToolStripMenuItem(_localizer["Tray.StopServer"], null, (_, _) => RunAsyncSafe(StopServerWorkflowAsync, "Arrêt du serveur")) { Enabled = false };
        _trayShowWindowItem = new ToolStripMenuItem(_localizer["Tray.ShowWindow"], null, (_, _) => RestoreFromTray());
        _trayExitItem = new ToolStripMenuItem(_localizer["Tray.Exit"], null, (_, _) => RunAsyncSafe(RequestExitAsync, "Quitter l'application"));

        _trayContextMenu.Items.AddRange([
            _trayOpenBrowserItem,
            new ToolStripSeparator(),
            _trayStatusItem,
            _trayStartServerItem,
            _trayStopServerItem,
            _trayShowWindowItem,
            new ToolStripSeparator(),
            _trayExitItem
        ]);

        _trayIcon = new NotifyIcon
        {
            Icon = appIcon ?? SystemIcons.Application,
            Text = _localizer["App.SystrayTitle"],
            Visible = true,
            ContextMenuStrip = _trayContextMenu
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        // 5. Abonnements aux événements du superviseur
        _processSupervisor.StateChanged += (_, e) => BeginInvoke(() => OnProcessStateChanged(e.State));
        _processSupervisor.LogReceived += (_, e) => BeginInvoke(() => OnLogReceived(e.Entry));

        FormClosing += (_, e) =>
        {
            if (!_isExplicitExit)
            {
                e.Cancel = true;
                MinimizeToTray();
            }
        };

        Load += (_, _) => RunAsyncSafe(InitializeLauncherAsync, "Initialisation de Cadran");
    }

    private void OnLanguageSelectionChanged()
    {
        if (_isUpdatingLanguage)
        {
            return;
        }

        var selectedCode = _headerLangCombo.SelectedIndex == 1 ? "en" : "fr";
        _settingsLangCombo.SelectedIndex = _headerLangCombo.SelectedIndex;

        if (string.Equals(_currentSettings.Language, selectedCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CultureInfo.CurrentUICulture = new CultureInfo(selectedCode);
        CultureInfo.CurrentCulture = new CultureInfo(selectedCode);

        _currentSettings = _currentSettings with { Language = selectedCode };
        _configStore.SaveSettings(_currentSettings);

        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        _isUpdatingLanguage = true;
        try
        {
            Text = _localizer["App.Title"];

            _dashboardTab.Text = _localizer["Dashboard.TabTitle"];
            _backupsTab.Text = _localizer["Backups.TabTitle"];
            _logsTab.Text = _localizer["Logs.TabTitle"];
            _settingsTab.Text = _localizer["Settings.TabTitle"];

            _dashboardOpenBrowserButton.Text = _localizer["Dashboard.OpenBrowser"];
            _dashboardActionsGroup.Text = _localizer["Dashboard.ServerControlGroup"];
            _dashboardStartButton.Text = _localizer["Dashboard.StartServer"];
            _dashboardStopButton.Text = _localizer["Dashboard.StopServer"];
            _dashboardRestartButton.Text = _localizer["Dashboard.RestartServer"];
            _dashboardInfoGroup.Text = _localizer["Dashboard.DatabaseGroup"];
            _quickBackupButton.Text = _localizer["Dashboard.QuickBackup"];

            _createBackupButton.Text = _localizer["Backups.Create"];
            _restoreBackupButton.Text = _localizer["Backups.Restore"];
            _openBackupsFolderButton.Text = _localizer["Backups.OpenFolder"];

            _backupsListView.Columns[0].Text = _localizer["Backups.ColDate"];
            _backupsListView.Columns[1].Text = _localizer["Backups.ColName"];
            _backupsListView.Columns[2].Text = _localizer["Backups.ColSize"];
            _backupsListView.Columns[3].Text = _localizer["Backups.ColReason"];

            _searchLabel.Text = _localizer["Logs.Filter"];
            _autoScrollCheckBox.Text = _localizer["Logs.AutoScroll"];
            _clearLogsButton.Text = _localizer["Logs.Clear"];
            _copyLogsButton.Text = _localizer["Logs.CopyAll"];

            _portLabel.Text = _localizer["Settings.HttpPort"];
            _autoStartCheckBox.Text = _localizer["Settings.AutoStart"];
            _autoOpenBrowserCheckBox.Text = _localizer["Settings.AutoOpen"];
            _startMinimizedCheckBox.Text = _localizer["Settings.StartMinimized"];
            _enableAutoBackupsCheckBox.Text = _localizer["Settings.AutoBackups"];
            _settingsLangLabel.Text = _localizer["Settings.InterfaceLanguage"];
            _settingsLangCombo.SelectedIndex = _headerLangCombo.SelectedIndex;
            _retentionGroupBox.Text = _localizer["Settings.RetentionGroup"];
            _retentionModeLabel.Text = _localizer["Settings.RetentionMode"];

            var currentRetentionIdx = _retentionModeCombo.SelectedIndex;
            _retentionModeCombo.Items.Clear();
            _retentionModeCombo.Items.AddRange([
                _localizer["Settings.ModeFileCount"].Value,
                _localizer["Settings.ModeDays"].Value,
                _localizer["Settings.ModeBoth"].Value
            ]);
            _retentionModeCombo.SelectedIndex = Math.Clamp(currentRetentionIdx, 0, 2);

            _maxFilesLabel.Text = _localizer["Settings.MaxFiles"];
            _maxDaysLabel.Text = _localizer["Settings.MaxDays"];
            _saveSettingsButton.Text = _localizer["Settings.Save"];

            _trayOpenBrowserItem.Text = _localizer["Tray.OpenBrowser"];
            _trayStartServerItem.Text = _localizer["Tray.StartServer"];
            _trayStopServerItem.Text = _localizer["Tray.StopServer"];
            _trayShowWindowItem.Text = _localizer["Tray.ShowWindow"];
            _trayExitItem.Text = _localizer["Tray.Exit"];

            UpdateStateDisplay(_processSupervisor.State);
            RefreshBackupsList();
        }
        finally
        {
            _isUpdatingLanguage = false;
        }
    }

    private async Task InitializeLauncherAsync()
    {
        UpdateStateDisplay(ProcessState.Stopped);
        RefreshBackupsList();

        if (_currentSettings.StartMinimizedInTray)
        {
            WindowState = FormWindowState.Minimized;
            Hide();
        }

        if (_currentSettings.AutoStartServer)
        {
            await StartServerWorkflowAsync();
        }
    }

    private async Task StartServerWorkflowAsync()
    {
        SetButtonsBusyState(isStarting: true);

        if (_currentSettings.EnableAutomaticBackups)
        {
            await _backupSupervisor.CreateBackupAsync("lancement", _currentSettings.RetentionPolicy);
            RefreshBackupsList();
        }

        var started = await _processSupervisor.StartAsync();
        if (started && _currentSettings.AutoOpenBrowser)
        {
            OpenBrowser();
        }
    }

    private async Task StopServerWorkflowAsync()
    {
        SetButtonsBusyState(isStarting: false);

        await _processSupervisor.StopAsync();

        if (_currentSettings.EnableAutomaticBackups)
        {
            await _backupSupervisor.CreateBackupAsync("arret", _currentSettings.RetentionPolicy);
            RefreshBackupsList();
        }
    }

    private async Task RestartServerWorkflowAsync()
    {
        await StopServerWorkflowAsync();
        await StartServerWorkflowAsync();
    }

    private void SetButtonsBusyState(bool isStarting)
    {
        ApplyButtonStyle(_dashboardStartButton, enabled: false, GreenColor);
        ApplyButtonStyle(_dashboardStopButton, enabled: false, RedColor);
        ApplyButtonStyle(_dashboardRestartButton, enabled: false, GrayColor);
        ApplyButtonStyle(_dashboardOpenBrowserButton, enabled: false, BlueColor);

        _trayStartServerItem.Enabled = false;
        _trayStopServerItem.Enabled = false;
        _trayOpenBrowserItem.Enabled = false;

        _statusDot.BackColor = Color.FromArgb(255, 193, 7);

        if (isStarting)
        {
            _statusLabel.Text = _localizer["Header.StatusStarting"];
            _statusLabel.ForeColor = Color.FromArgb(170, 120, 0);
            _cardStatusTitle.Text = _localizer["Dashboard.StatusStartingTitle"];
            _cardStatusDescription.Text = _localizer["Dashboard.StatusStartingDesc"];
            _cardPanel.BackColor = Color.FromArgb(255, 251, 235);
            _trayStatusItem.Text = _localizer["Tray.StatusStarting"];
            _trayIcon.Text = _localizer["App.Starting"];
        }
        else
        {
            _statusLabel.Text = _localizer["Header.StatusStopping"];
            _statusLabel.ForeColor = Color.FromArgb(170, 120, 0);
            _cardStatusTitle.Text = _localizer["Dashboard.StatusStoppingTitle"];
            _cardStatusDescription.Text = _localizer["Dashboard.StatusStoppingDesc"];
            _cardPanel.BackColor = Color.FromArgb(255, 251, 235);
            _trayStatusItem.Text = _localizer["Tray.StatusStopping"];
            _trayIcon.Text = _localizer["App.Stopping"];
        }
    }

    private void OnProcessStateChanged(ProcessState state)
    {
        UpdateStateDisplay(state);
    }

    private void UpdateStateDisplay(ProcessState state)
    {
        switch (state)
        {
            case ProcessState.Running:
                _statusDot.BackColor = GreenColor;
                _statusLabel.Text = string.Format(CultureInfo.CurrentCulture, _localizer["Header.StatusRunning"], _currentSettings.Port);
                _statusLabel.ForeColor = GreenColor;

                // Application nette du style actif / grisé
                ApplyButtonStyle(_dashboardStartButton, enabled: false, GreenColor);
                ApplyButtonStyle(_dashboardStopButton, enabled: true, RedColor);
                ApplyButtonStyle(_dashboardRestartButton, enabled: true, GrayColor);
                ApplyButtonStyle(_dashboardOpenBrowserButton, enabled: true, BlueColor);

                _cardStatusTitle.Text = _localizer["Dashboard.StatusRunningTitle"];
                _cardStatusTitle.ForeColor = GreenColor;
                _cardStatusDescription.Text = string.Format(CultureInfo.CurrentCulture, _localizer["Dashboard.StatusRunningDesc"], _processSupervisor.ServerUrl);
                _cardPanel.BackColor = Color.FromArgb(238, 250, 242);

                _trayOpenBrowserItem.Enabled = true;
                _trayStartServerItem.Enabled = false;
                _trayStopServerItem.Enabled = true;
                _trayStatusItem.Text = string.Format(CultureInfo.CurrentCulture, _localizer["Tray.StatusOnline"], _processSupervisor.ServerUrl);
                _trayIcon.Text = string.Format(CultureInfo.CurrentCulture, _localizer["App.RunningOnPort"], _currentSettings.Port);
                break;

            case ProcessState.Starting:
                SetButtonsBusyState(isStarting: true);
                break;

            case ProcessState.Stopping:
                SetButtonsBusyState(isStarting: false);
                break;

            case ProcessState.Stopped:
            default:
                _statusDot.BackColor = RedColor;
                _statusLabel.Text = _localizer["Header.StatusStopped"];
                _statusLabel.ForeColor = Color.FromArgb(108, 117, 125);

                // Application nette du style actif / grisé
                ApplyButtonStyle(_dashboardStartButton, enabled: true, GreenColor);
                ApplyButtonStyle(_dashboardStopButton, enabled: false, RedColor);
                ApplyButtonStyle(_dashboardRestartButton, enabled: false, GrayColor);
                ApplyButtonStyle(_dashboardOpenBrowserButton, enabled: false, BlueColor);

                _cardStatusTitle.Text = _localizer["Dashboard.StatusStoppedTitle"];
                _cardStatusTitle.ForeColor = Color.FromArgb(70, 70, 70);
                _cardStatusDescription.Text = _localizer["Dashboard.StatusStoppedDesc"];
                _cardPanel.BackColor = Color.FromArgb(245, 248, 250);

                _trayOpenBrowserItem.Enabled = false;
                _trayStartServerItem.Enabled = true;
                _trayStopServerItem.Enabled = false;
                _trayStatusItem.Text = _localizer["Tray.StatusOffline"];
                _trayIcon.Text = _localizer["App.Stopped"];
                break;
        }

        _urlLink.Text = _processSupervisor.ServerUrl;
    }

    private static void ApplyButtonStyle(Button button, bool enabled, Color enabledBackColor, Color? enabledForeColor = null)
    {
        button.Enabled = enabled;
        if (enabled)
        {
            button.BackColor = enabledBackColor;
            button.ForeColor = enabledForeColor ?? Color.White;
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.BorderColor = enabledBackColor;
        }
        else
        {
            // Aspect nettement grisé et estompé
            button.BackColor = Color.FromArgb(232, 235, 238);
            button.ForeColor = Color.FromArgb(155, 160, 166);
            button.Cursor = Cursors.Default;
            button.FlatAppearance.BorderColor = Color.FromArgb(215, 220, 225);
        }
    }

    private void OnLogReceived(LogEntry entry)
    {
        _logBuffer.Append(entry);

        if (!string.IsNullOrWhiteSpace(_searchBox.Text) &&
            !entry.Message.Contains(_searchBox.Text.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AppendLogToBox(entry);
    }

    private void AppendLogToBox(LogEntry entry)
    {
        var color = entry.Level switch
        {
            LogLevel.Error or LogLevel.Critical => Color.FromArgb(255, 100, 100),
            LogLevel.Warning => Color.FromArgb(255, 200, 80),
            LogLevel.Debug or LogLevel.Trace => Color.FromArgb(150, 150, 150),
            _ => Color.FromArgb(220, 220, 220)
        };

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor = color;
        _logBox.AppendText($"[{entry.Timestamp:HH:mm:ss}] {entry.Message}{Environment.NewLine}");
        _logBox.SelectionColor = _logBox.ForeColor;

        if (_autoScrollCheckBox.Checked)
        {
            _logBox.ScrollToCaret();
        }
    }

    private void RefreshLogsView()
    {
        _logBox.Clear();
        var filter = _searchBox.Text.Trim();
        var entries = string.IsNullOrEmpty(filter) ? _logBuffer.GetAll() : _logBuffer.Filter(filter);

        foreach (var entry in entries)
        {
            AppendLogToBox(entry);
        }
    }

    private void OnCreateBackupClicked()
    {
        RunAsyncSafe(CreateManualBackupAsync, "Création manuelle de sauvegarde");
    }

    private async Task CreateManualBackupAsync()
    {
        BeginInvoke(() => ApplyButtonStyle(_createBackupButton, enabled: false, Color.FromArgb(245, 247, 250), Color.FromArgb(30, 30, 30)));
        try
        {
            var result = await _backupSupervisor.CreateBackupAsync("manuel", _currentSettings.RetentionPolicy);
            if (result.IsSuccess)
            {
                BeginInvoke(RefreshBackupsList);
                ShowMessageBox(
                    _localizer["Backups.SuccessMessage"],
                    _localizer["Backups.SuccessTitle"],
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                ShowMessageBox(
                    result.ErrorMessage ?? "Échec inconnu lors de la sauvegarde.",
                    _localizer["Backups.ErrorTitle"],
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            BeginInvoke(() => ApplyButtonStyle(_createBackupButton, enabled: true, Color.FromArgb(245, 247, 250), Color.FromArgb(30, 30, 30)));
        }
    }

    private void OnRestoreBackupClicked()
    {
        if (_processSupervisor.State is ProcessState.Running or ProcessState.Starting)
        {
            ShowMessageBox(
                _localizer["Backups.ServerRunningMessage"],
                _localizer["Backups.ServerRunningTitle"],
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (_backupsListView.SelectedItems.Count == 0)
        {
            ShowMessageBox(
                _localizer["Backups.SelectRequiredMessage"],
                _localizer["Backups.SelectRequiredTitle"],
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var selectedItem = _backupsListView.SelectedItems[0];
        if (selectedItem.Tag is not BackupMetadata backup)
        {
            return;
        }

        var warningMessage = _localizer["Backups.RestoreConfirmWarning"].Value;

        var confirmation = ShowMessageBox(
            warningMessage,
            _localizer["Backups.RestoreConfirmTitle"],
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        RunAsyncSafe(() => RestoreBackupAsync(backup), "Restauration de la base de données");
    }

    private async Task RestoreBackupAsync(BackupMetadata backup)
    {
        BeginInvoke(() => ApplyButtonStyle(_restoreBackupButton, enabled: false, Color.FromArgb(245, 247, 250), Color.FromArgb(30, 30, 30)));
        try
        {
            var result = await _backupSupervisor.RestoreBackupAsync(backup.FilePath, isServerRunning: false);
            if (result.IsSuccess)
            {
                BeginInvoke(RefreshBackupsList);
                ShowMessageBox(
                    _localizer["Backups.RestoreSuccessMessage"],
                    _localizer["Backups.RestoreSuccessTitle"],
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                ShowMessageBox(
                    result.ErrorMessage ?? "Erreur inconnue lors de la restauration.",
                    _localizer["Backups.RestoreErrorTitle"],
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            BeginInvoke(() => ApplyButtonStyle(_restoreBackupButton, enabled: true, Color.FromArgb(245, 247, 250), Color.FromArgb(30, 30, 30)));
        }
    }

    private void RefreshBackupsList()
    {
        _backupsListView.Items.Clear();
        var backups = _backupSupervisor.GetAvailableBackups();

        foreach (var backup in backups)
        {
            var item = new ListViewItem(backup.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture));
            item.SubItems.Add(backup.FileName);
            item.SubItems.Add(FormatFileSize(backup.SizeInBytes));
            item.SubItems.Add(LocalizeBackupReason(backup.Reason));
            item.Tag = backup;
            _backupsListView.Items.Add(item);
        }

        if (backups.Count > 0)
        {
            var latest = backups[0];
            _lastBackupLabel.Text = string.Format(
                CultureInfo.CurrentCulture,
                _localizer["Dashboard.LastBackupFormat"],
                latest.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
                FormatFileSize(latest.SizeInBytes),
                LocalizeBackupReason(latest.Reason));
        }
        else
        {
            _lastBackupLabel.Text = _localizer["Dashboard.LastBackupNone"];
        }
    }

    private string LocalizeBackupReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return _localizer["Backups.ReasonAutomatic"];
        }

        var normalized = reason.Trim().ToLowerInvariant();
        var key = $"Backups.Reason_{normalized}";
        var localized = _localizer[key];
        if (!localized.ResourceNotFound)
        {
            return localized.Value;
        }

        return normalized switch
        {
            "lancement" or "startup" => _localizer["Backups.ReasonStartup"],
            "fermeture" or "shutdown" => _localizer["Backups.ReasonShutdown"],
            "manuel" or "manual" => _localizer["Backups.ReasonManual"],
            _ => reason
        };
    }

    private void OpenBackupsFolder()
    {
        var backups = _backupSupervisor.GetAvailableBackups();
        var folder = backups.Count > 0
            ? Path.GetDirectoryName(backups[0].FilePath)
            : Path.Combine(AppContext.BaseDirectory, "app", "backups");

        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        else
        {
            ShowMessageBox(
                _localizer["Backups.FolderNotInitializedMessage"],
                _localizer["Backups.FolderNotInitializedTitle"],
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void SaveSettings()
    {
        var retentionMode = (BackupRetentionMode)_retentionModeCombo.SelectedIndex;
        var retentionOptions = new BackupRetentionOptions(
            retentionMode,
            (int)_maxBackupFilesInput.Value,
            (int)_maxBackupDaysInput.Value);

        var selectedLang = _headerLangCombo.SelectedIndex == 1 ? "en" : "fr";

        var updated = new LauncherSettings(
            (int)_portInput.Value,
            selectedLang,
            _autoStartCheckBox.Checked,
            _autoOpenBrowserCheckBox.Checked,
            _startMinimizedCheckBox.Checked,
            _enableAutoBackupsCheckBox.Checked,
            retentionOptions);

        _configStore.SaveSettings(updated);
        _currentSettings = updated;

        ShowMessageBox(
            _localizer["Settings.SavedMessage"],
            _localizer["Settings.SavedTitle"],
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OpenBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _processSupervisor.ServerUrl,
                UseShellExecute = true
            });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            OnLogReceived(new LogEntry(DateTimeOffset.Now, LogLevel.Warning, $"Impossible d'ouvrir le navigateur : {ex.Message}"));
            ShowMessageBox(
                string.Format(CultureInfo.CurrentCulture, _localizer["Errors.BrowserOpenError"], ex.Message),
                "Erreur",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (InvalidOperationException ex)
        {
            OnLogReceived(new LogEntry(DateTimeOffset.Now, LogLevel.Warning, $"Processus de navigateur invalide : {ex.Message}"));
            ShowMessageBox(
                string.Format(CultureInfo.CurrentCulture, _localizer["Errors.BrowserOpenError"], ex.Message),
                "Erreur",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void MinimizeToTray()
    {
        Hide();
        if (!_hasShownTrayBalloon)
        {
            _trayIcon.ShowBalloonTip(
                3000,
                _localizer["App.BalloonTitle"],
                _localizer["App.BalloonMessage"],
                ToolTipIcon.Info);
            _hasShownTrayBalloon = true;
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }

    private async Task RequestExitAsync()
    {
        _isExplicitExit = true;
        await PerformGracefulShutdownAsync();
        BeginInvoke(Close);
    }

    private async Task PerformGracefulShutdownAsync()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        if (_processSupervisor.State is ProcessState.Running or ProcessState.Starting)
        {
            await _processSupervisor.StopAsync();

            if (_currentSettings.EnableAutomaticBackups)
            {
                await _backupSupervisor.CreateBackupAsync("fermeture", _currentSettings.RetentionPolicy);
            }
        }

        await _processSupervisor.DisposeAsync();
    }

    private void RunAsyncSafe(Func<Task> action, string actionDescription)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                // Opération annulée à la demande de l'utilisateur ou lors de la clôture
            }
            catch (Exception ex)
            {
                OnLogReceived(new LogEntry(DateTimeOffset.Now, LogLevel.Error, $"Erreur lors de l'exécution de '{actionDescription}' : {ex.Message}"));
                ShowMessageBox(
                    string.Format(CultureInfo.CurrentCulture, _localizer["Errors.GenericOperationError"], actionDescription, ex.Message),
                    "Erreur",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        });
    }

    private DialogResult ShowMessageBox(
        string text,
        string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        if (IsDisposed || Disposing)
        {
            return DialogResult.None;
        }

        if (InvokeRequired)
        {
            return (DialogResult)Invoke(() => ShowMessageBox(text, caption, buttons, icon, defaultButton));
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        BringToFront();
        Activate();
        return MessageBox.Show(this, text, caption, buttons, icon, defaultButton);
    }

    private static Icon? LoadApplicationIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "cadran.ico");
        return File.Exists(iconPath) ? new Icon(iconPath) : null;
    }

    private static string ResolveLocalesDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var directLocales = Path.Combine(baseDir, "Resources", "Locales");
        if (Directory.Exists(directLocales))
        {
            return directLocales;
        }

        var sourceLocales = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Resources", "Locales"));
        if (Directory.Exists(sourceLocales))
        {
            return sourceLocales;
        }

        return directLocales;
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => $"{(bytes / (1024.0 * 1024.0)):F2} Mo",
            >= 1024 => $"{(bytes / 1024.0):F1} Ko",
            _ => $"{bytes} octets"
        };
    }
}
