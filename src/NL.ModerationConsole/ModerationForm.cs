using System.Drawing;
using System.Windows.Forms;
using NL.Core;
using NL.Core.Sp;
using NL.Moderation;
using NL.Moderation.Core;

namespace NL.ModerationConsole;

/// <summary>
/// Basic admin/mod dashboard for Phase 4 — ROADMAP.md: "Build basic admin/mod views for
/// recent actions, issuing warnings/bans, and viewing SP offense history." Reads/writes the
/// same JSON-Lines moderation log and JSON SP-profile file that <c>NL.Server</c> writes to.
/// </summary>
internal sealed class ModerationForm : Form
{
    private ModerationService _moderation;
    private string _moderationLogPath;
    private string _spStorePath;

    private readonly TextBox _streamerBox;
    private readonly TextBox _modIdBox;
    private readonly Label _pathsLabel;

    // Recent actions
    private readonly ListView _recentListView;
    private readonly NumericUpDown _recentCountBox;

    // Offense history + actions
    private readonly TextBox _playerIdBox;
    private readonly TextBox _reasonBox;
    private readonly Label _standingLabel;
    private readonly ListView _offenseListView;

    private readonly ToolStripStatusLabel _statusLabel;

    public ModerationForm()
    {
        Text = "NL Moderation Console";
        MinimumSize = new Size(920, 620);
        Size = new Size(1040, 720);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;

        NlPaths.EnsureRoot();
        _moderationLogPath = NlPaths.ModerationLog;
        _spStorePath = NlPaths.SpProfiles;
        _moderation = BuildService(_moderationLogPath, _spStorePath);

        // ── Menu ─────────────────────────────────────────────────────────────
        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("File");
        fileMenu.DropDownItems.Add("Open Moderation Log…", null, (_, _) => OpenModerationLog());
        fileMenu.DropDownItems.Add("Open SP Profile Store…", null, (_, _) => OpenSpStore());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Exit", null, (_, _) => Close());
        menu.Items.Add(fileMenu);
        MainMenuStrip = menu;
        Controls.Add(menu);

        // ── Top bar: streamer / mod id ─────────────────────────────────────
        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            ColumnCount = 5,
            Padding = new Padding(6, 4, 6, 4),
        };
        topPanel.Controls.Add(new Label { Text = "Streamer:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
        _streamerBox = new TextBox { Text = NlPaths.DefaultStreamerId, Dock = DockStyle.Fill };
        topPanel.Controls.Add(_streamerBox, 1, 0);
        topPanel.Controls.Add(new Label { Text = "Issued by (mod id):", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 2, 0);
        _modIdBox = new TextBox { Text = "mod-console", Dock = DockStyle.Fill };
        topPanel.Controls.Add(_modIdBox, 3, 0);
        var refreshTopBtn = new Button { Text = "Refresh", Dock = DockStyle.Fill };
        refreshTopBtn.Click += (_, _) => RefreshRecentActions();
        topPanel.Controls.Add(refreshTopBtn, 4, 0);
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(topPanel);

        _pathsLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Padding = new Padding(6, 0, 6, 0),
            ForeColor = SystemColors.GrayText,
            Text = PathsSummary(),
        };
        Controls.Add(_pathsLabel);

        // ── Status bar ───────────────────────────────────────────────────────
        var status = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        status.Items.Add(_statusLabel);
        Controls.Add(status);

        // ── Main split: recent actions | offense history + actions ─────────
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 520,
        };
        Controls.Add(mainSplit);

        // ── LEFT: Recent Actions ────────────────────────────────────────────
        var recentGroup = new GroupBox { Text = "Recent Actions", Dock = DockStyle.Fill, Padding = new Padding(6) };
        var recentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        recentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var recentTopRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        recentTopRow.Controls.Add(new Label { Text = "Show up to:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        _recentCountBox = new NumericUpDown { Minimum = 10, Maximum = 5000, Value = 100, Width = 70 };
        recentTopRow.Controls.Add(_recentCountBox);
        var recentRefreshBtn = new Button { Text = "Refresh", AutoSize = true };
        recentRefreshBtn.Click += (_, _) => RefreshRecentActions();
        recentTopRow.Controls.Add(recentRefreshBtn);
        recentLayout.Controls.Add(recentTopRow, 0, 0);

        _recentListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
        };
        _recentListView.Columns.Add("Time (UTC)", 130);
        _recentListView.Columns.Add("Kind", 90);
        _recentListView.Columns.Add("Player", 110);
        _recentListView.Columns.Add("Event", 90);
        _recentListView.Columns.Add("Decision", 70);
        _recentListView.Columns.Add("By", 90);
        _recentListView.Columns.Add("Message", 260);
        _recentListView.SelectedIndexChanged += (_, _) => OnRecentActionSelected();
        recentLayout.Controls.Add(_recentListView, 0, 1);
        recentGroup.Controls.Add(recentLayout);
        mainSplit.Panel1.Controls.Add(recentGroup);

        // ── RIGHT: Offense history + issue action ───────────────────────────
        var rightLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        mainSplit.Panel2.Controls.Add(rightLayout);

        var historyGroup = new GroupBox { Text = "SP Offense History", Dock = DockStyle.Fill, Padding = new Padding(6) };
        var historyLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        historyLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lookupRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        lookupRow.Controls.Add(new Label { Text = "Player id:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        _playerIdBox = new TextBox { Width = 160 };
        lookupRow.Controls.Add(_playerIdBox);
        var lookupBtn = new Button { Text = "Look Up", AutoSize = true };
        lookupBtn.Click += (_, _) => RefreshOffenseHistory();
        lookupRow.Controls.Add(lookupBtn);
        historyLayout.Controls.Add(lookupRow, 0, 0);

        _standingLabel = new Label { AutoSize = true, Dock = DockStyle.Top, Font = new Font(Font, FontStyle.Bold) };
        historyLayout.Controls.Add(_standingLabel, 0, 1);

        _offenseListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
        };
        _offenseListView.Columns.Add("Issued (UTC)", 120);
        _offenseListView.Columns.Add("By", 90);
        _offenseListView.Columns.Add("Reason", 180);
        _offenseListView.Columns.Add("Game", 80);
        _offenseListView.Columns.Add("Active?", 60);
        historyLayout.Controls.Add(_offenseListView, 0, 2);
        historyGroup.Controls.Add(historyLayout);
        rightLayout.Controls.Add(historyGroup, 0, 0);

        var actionGroup = new GroupBox { Text = "Issue Action", Dock = DockStyle.Fill, Padding = new Padding(6) };
        var actionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        actionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var reasonRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        reasonRow.Controls.Add(new Label { Text = "Reason:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        _reasonBox = new TextBox { Width = 300 };
        reasonRow.Controls.Add(_reasonBox);
        actionLayout.Controls.Add(reasonRow, 0, 0);

        var buttonRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        var warnBtn = new Button { Text = "Issue Warning", AutoSize = true };
        warnBtn.Click += async (_, _) => await IssueActionAsync(ModerationActionKind.Warning);
        var banBtn = new Button { Text = "Issue Ban", AutoSize = true, ForeColor = Color.DarkRed };
        banBtn.Click += async (_, _) => await IssueActionAsync(ModerationActionKind.Ban);
        var graylistBtn = new Button { Text = "Graylist Hold", AutoSize = true };
        graylistBtn.Click += async (_, _) => await IssueActionAsync(ModerationActionKind.GraylistHold);
        var clearBtn = new Button { Text = "Clear Standing", AutoSize = true };
        clearBtn.Click += async (_, _) => await IssueActionAsync(ModerationActionKind.StandingCleared);
        buttonRow.Controls.Add(warnBtn);
        buttonRow.Controls.Add(banBtn);
        buttonRow.Controls.Add(graylistBtn);
        buttonRow.Controls.Add(clearBtn);
        actionLayout.Controls.Add(buttonRow, 0, 1);

        var createRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        var createBtn = new Button { Text = "Create Profile If Missing", AutoSize = true };
        createBtn.Click += (_, _) => CreateProfileIfMissing();
        createRow.Controls.Add(createBtn);
        actionLayout.Controls.Add(createRow, 0, 2);

        actionGroup.Controls.Add(actionLayout);
        rightLayout.Controls.Add(actionGroup, 0, 1);

        Load += (_, _) => RefreshRecentActions();
    }

    private static ModerationService BuildService(string moderationLogPath, string spStorePath)
    {
        var store = new JsonlModerationStore(moderationLogPath);
        var profiles = new JsonFileSpProfileRepository(spStorePath);
        return new ModerationService(store, profiles);
    }

    private string PathsSummary() => $"Log: {_moderationLogPath}    |    SP store: {_spStorePath}";

    private void OpenModerationLog()
    {
        using var dialog = new OpenFileDialog { Filter = "Moderation log (*.jsonl)|*.jsonl|All files (*.*)|*.*", CheckFileExists = false };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _moderationLogPath = dialog.FileName;
            _moderation = BuildService(_moderationLogPath, _spStorePath);
            _pathsLabel.Text = PathsSummary();
            RefreshRecentActions();
        }
    }

    private void OpenSpStore()
    {
        using var dialog = new OpenFileDialog { Filter = "SP profile store (*.json)|*.json|All files (*.*)|*.*", CheckFileExists = false };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _spStorePath = dialog.FileName;
            _moderation = BuildService(_moderationLogPath, _spStorePath);
            _pathsLabel.Text = PathsSummary();
            RefreshOffenseHistory();
        }
    }

    private string StreamerId =>
        string.IsNullOrWhiteSpace(_streamerBox.Text) ? NlPaths.DefaultStreamerId : _streamerBox.Text.Trim();

    private async void RefreshRecentActions()
    {
        try
        {
            var records = await _moderation.GetRecentActionsAsync(StreamerId, (int)_recentCountBox.Value);
            _recentListView.BeginUpdate();
            _recentListView.Items.Clear();
            foreach (var record in records)
            {
                var item = new ListViewItem(record.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(record.Kind.ToString());
                item.SubItems.Add(record.PlayerName ?? record.PlayerId ?? "?");
                item.SubItems.Add(record.EventName);
                item.SubItems.Add(record.Decision?.ToString() ?? "-");
                item.SubItems.Add(record.IssuedBy ?? "-");
                item.SubItems.Add(record.Message ?? "");
                item.Tag = record;
                _recentListView.Items.Add(item);
            }

            _recentListView.EndUpdate();
            _statusLabel.Text = $"Loaded {records.Count} recent action(s) for '{StreamerId}'.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed to load recent actions: {ex.Message}";
        }
    }

    private void OnRecentActionSelected()
    {
        if (_recentListView.SelectedItems.Count == 0)
        {
            return;
        }

        if (_recentListView.SelectedItems[0].Tag is ModerationRecord record && record.PlayerId is not null)
        {
            _playerIdBox.Text = record.PlayerId;
            RefreshOffenseHistory();
        }
    }

    private void RefreshOffenseHistory()
    {
        var playerId = _playerIdBox.Text.Trim();
        if (playerId.Length == 0)
        {
            _statusLabel.Text = "Enter a player id to look up.";
            return;
        }

        var history = _moderation.GetOffenseHistory(StreamerId, playerId);
        _offenseListView.Items.Clear();
        if (history is null)
        {
            _standingLabel.Text = $"Unknown SP '{playerId}'.";
            _standingLabel.ForeColor = SystemColors.GrayText;
            _statusLabel.Text = $"No profile found for '{playerId}'. Use \"Create Profile If Missing\" to add one.";
            return;
        }

        _standingLabel.Text = $"Standing: {history.Standing}   (active offenses: {history.ActiveOffenseCount})";
        _standingLabel.ForeColor = history.Standing switch
        {
            SpStanding.Banned => Color.DarkRed,
            SpStanding.Graylist => Color.DarkOrange,
            _ => Color.DarkGreen,
        };

        var now = DateTimeOffset.UtcNow;
        foreach (var offense in history.Offenses)
        {
            var item = new ListViewItem(offense.IssuedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            item.SubItems.Add(offense.IssuedBy);
            item.SubItems.Add(offense.Reason);
            item.SubItems.Add(offense.Game ?? "-");
            item.SubItems.Add(offense.IsActive(now) ? "Yes" : "Archived");
            _offenseListView.Items.Add(item);
        }

        _statusLabel.Text = $"Loaded {history.Offenses.Count} offense(s) for '{playerId}'.";
    }

    private void CreateProfileIfMissing()
    {
        var playerId = _playerIdBox.Text.Trim();
        if (playerId.Length == 0)
        {
            _statusLabel.Text = "Enter a player id first.";
            return;
        }

        _moderation.GetOrCreateProfile(playerId, playerId);
        _statusLabel.Text = $"Profile '{playerId}' ready.";
        RefreshOffenseHistory();
    }

    private async Task IssueActionAsync(ModerationActionKind kind)
    {
        var playerId = _playerIdBox.Text.Trim();
        var reason = _reasonBox.Text.Trim();
        var issuedBy = string.IsNullOrWhiteSpace(_modIdBox.Text) ? "mod-console" : _modIdBox.Text.Trim();

        if (playerId.Length == 0)
        {
            _statusLabel.Text = "Enter a player id first.";
            return;
        }

        if (kind != ModerationActionKind.StandingCleared && reason.Length == 0)
        {
            _statusLabel.Text = "Enter a reason first.";
            return;
        }

        try
        {
            switch (kind)
            {
                case ModerationActionKind.Warning:
                    await _moderation.IssueWarningAsync(StreamerId, playerId, issuedBy, reason);
                    break;
                case ModerationActionKind.Ban:
                    await _moderation.IssueBanAsync(StreamerId, playerId, issuedBy, reason);
                    break;
                case ModerationActionKind.GraylistHold:
                    await _moderation.IssueGraylistHoldAsync(StreamerId, playerId, issuedBy, reason);
                    break;
                case ModerationActionKind.StandingCleared:
                    await _moderation.ClearStandingAsync(StreamerId, playerId, issuedBy, reason.Length == 0 ? null : reason);
                    break;
            }

            _statusLabel.Text = $"{kind} applied to '{playerId}'.";
            RefreshOffenseHistory();
            RefreshRecentActions();
        }
        catch (InvalidOperationException ex)
        {
            _statusLabel.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Unknown SP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
