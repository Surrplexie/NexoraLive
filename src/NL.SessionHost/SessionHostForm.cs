using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using NL.Core;
using NL.Core.Sp;
using NL.Moderation;
using NL.Server;
using NL.Server.Core.Integration;

namespace NL.SessionHost;

/// <summary>
/// Thin Windows shell to start/stop one NLServer session profile:
/// pick a game preset, enter one action port, then Start.
/// </summary>
internal sealed class SessionHostForm : Form
{
    private readonly TextBox _streamerBox;
    private readonly ComboBox _gamePresetBox;
    private readonly Label _actionPortLabel;
    private readonly TextBox _actionPortBox;
    private readonly Panel _advancedPanel;
    private readonly TextBox _configBox;
    private readonly TextBox _sourceBox;
    private readonly CheckBox _useSessionBusBox;
    private readonly CheckBox _antiCheatBox;
    private readonly CheckBox _joinGateBox;
    private readonly CheckBox _anomalyAutoModBox;
    private readonly CheckBox _replayBox;
    private readonly Button _startBtn;
    private readonly Button _stopBtn;
    private readonly RichTextBox _logBox;
    private readonly ToolStripStatusLabel _statusLabel;

    private readonly SessionHostService _sessions = new();
    private Task? _runTask;
    private bool _loadingProfile;

    public SessionHostForm()
    {
        Text = "NL Session Host";
        MinimumSize = new Size(720, 560);
        Size = new Size(820, 640);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;

        NlPaths.EnsureRoot();
        EnsureDefaultJoinRequirements();

        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("File");
        fileMenu.DropDownItems.Add("Load Profile…", null, (_, _) => LoadProfileDialog());
        fileMenu.DropDownItems.Add("Save Profile", null, (_, _) => SaveProfile(NlPaths.SessionProfile));
        fileMenu.DropDownItems.Add("Save Profile As…", null, (_, _) => SaveProfileDialog());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Exit", null, (_, _) => Close());
        menu.Items.Add(fileMenu);
        var toolsMenu = new ToolStripMenuItem("Tools");
        toolsMenu.DropDownItems.Add("Open Config Editor", null, (_, _) => LaunchSibling("NL.ConfigEditor"));
        toolsMenu.DropDownItems.Add("Open Moderation Console", null, (_, _) => LaunchSibling("NL.ModerationConsole"));
        toolsMenu.DropDownItems.Add("Open NL data folder", null, (_, _) =>
        {
            NlPaths.EnsureRoot();
            Process.Start(new ProcessStartInfo { FileName = NlPaths.Root, UseShellExecute = true });
        });
        toolsMenu.DropDownItems.Add("Load BeamNG freeroam defaults", null, (_, _) => LoadBeamngDefaults());
        toolsMenu.DropDownItems.Add("Load session bus defaults", null, (_, _) => LoadBusDefaults());
        menu.Items.Add(toolsMenu);
        MainMenuStrip = menu;
        Controls.Add(menu);

        var status = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel($"Data: {NlPaths.Root}") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        status.Items.Add(_statusLabel);
        Controls.Add(status);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(0, 24, 0, 0) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        _streamerBox = AddRow(fields, 0, "Streamer id", new TextBox { Text = NlPaths.DefaultStreamerId });

        _gamePresetBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        foreach (var preset in SessionHostGamePresetInfo.All)
        {
            _gamePresetBox.Items.Add(preset.DisplayName);
        }

        _gamePresetBox.SelectedIndex = 0;
        _gamePresetBox.SelectedIndexChanged += (_, _) => OnGamePresetChanged(userInitiated: true);
        fields.Controls.Add(new Label { Text = "Game", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
        fields.Controls.Add(_gamePresetBox, 1, 1);

        _actionPortLabel = new Label { Text = "RCON port", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        _actionPortBox = new TextBox { Dock = DockStyle.Fill };
        fields.Controls.Add(_actionPortLabel, 0, 2);
        fields.Controls.Add(_actionPortBox, 1, 2);
        fields.SetColumnSpan(_actionPortBox, 2);

        _configBox = AddBrowseRow(fields, 3, "Config (.nle)", "NLEvents (*.nle)|*.nle|All files (*.*)|*.*");
        _sourceBox = AddBrowseRow(fields, 4, "Source (log/ndjson)", "Logs/NDJSON (*.log;*.ndjson)|*.log;*.ndjson|All files (*.*)|*.*");

        _advancedPanel = new Panel { Dock = DockStyle.Top, AutoSize = true };
        var advancedFields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
        advancedFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        advancedFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        advancedFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        _useSessionBusBox = new CheckBox { Text = "Use session bus (ws://)", AutoSize = true, Dock = DockStyle.Fill };
        advancedFields.Controls.Add(new Label { Text = "Session bus", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        advancedFields.Controls.Add(_useSessionBusBox, 1, 0);
        advancedFields.SetColumnSpan(_useSessionBusBox, 2);
        _advancedPanel.Controls.Add(advancedFields);
        fields.Controls.Add(_advancedPanel, 0, 5);
        fields.SetColumnSpan(_advancedPanel, 3);

        layout.Controls.Add(fields, 0, 0);

        var flags = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        _antiCheatBox = new CheckBox { Text = "Anti-cheat", Checked = true, AutoSize = true };
        _joinGateBox = new CheckBox { Text = "Join gate", Checked = false, AutoSize = true };
        _anomalyAutoModBox = new CheckBox { Text = "Anomaly auto-mod", AutoSize = true };
        _replayBox = new CheckBox { Text = "Replay once (then stop)", AutoSize = true };
        flags.Controls.Add(_antiCheatBox);
        flags.Controls.Add(_joinGateBox);
        flags.Controls.Add(_anomalyAutoModBox);
        flags.Controls.Add(_replayBox);

        _startBtn = new Button { Text = "Start session", AutoSize = true };
        _startBtn.Click += async (_, _) => await StartSessionAsync();
        _stopBtn = new Button { Text = "Stop", AutoSize = true, Enabled = false };
        _stopBtn.Click += (_, _) => StopSession();
        flags.Controls.Add(_startBtn);
        flags.Controls.Add(_stopBtn);
        layout.Controls.Add(flags, 0, 1);

        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 9f),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.Gainsboro,
        };
        layout.Controls.Add(_logBox, 0, 2);

        _sessions.LogAppended += line =>
        {
            if (IsDisposed)
            {
                return;
            }

            BeginInvoke(() => AppendLog(line));
        };
        _sessions.StateChanged += () =>
        {
            if (IsDisposed)
            {
                return;
            }

            BeginInvoke(UpdateSessionButtons);
        };

        OnGamePresetChanged(userInitiated: false);
        LoadDefaultProfile();
        FormClosing += (_, _) =>
        {
            if (_sessions.IsRunning)
            {
                StopSession();
            }
        };
    }

    private SessionHostGamePresetInfo SelectedPreset =>
        SessionHostGamePresetInfo.Get((SessionHostGamePreset)_gamePresetBox.SelectedIndex);

    private void OnGamePresetChanged(bool userInitiated)
    {
        var preset = SelectedPreset;
        _actionPortLabel.Text = preset.ActionLabel;
        _actionPortBox.PlaceholderText = preset.ActionPlaceholder;
        _advancedPanel.Visible = preset.ShowAdvancedAction;

        if (userInitiated && !_loadingProfile)
        {
            _joinGateBox.Checked = preset.DefaultJoinGate;
            _anomalyAutoModBox.Checked = preset.DefaultAnomalyAutoMod;

            if (string.IsNullOrWhiteSpace(_actionPortBox.Text) && !string.IsNullOrWhiteSpace(preset.DefaultActionValue))
            {
                _actionPortBox.Text = preset.DefaultActionValue;
            }

            if (preset.Preset == SessionHostGamePreset.BeamNg)
            {
                ApplyBeamngPathDefaults();
            }
            else if (preset.Preset == SessionHostGamePreset.Minecraft)
            {
                ApplyMinecraftPathDefaults();
            }
            else if (preset.Preset == SessionHostGamePreset.Generic)
            {
                ApplyGenericPathDefaults();
            }
        }
    }

    private static TextBox AddRow(TableLayoutPanel fields, int row, string label, TextBox box)
    {
        fields.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, row);
        box.Dock = DockStyle.Fill;
        fields.Controls.Add(box, 1, row);
        fields.SetColumnSpan(box, 2);
        return box;
    }

    private static TextBox AddBrowseRow(TableLayoutPanel fields, int row, string label, string filter)
    {
        fields.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, row);
        var box = new TextBox { Dock = DockStyle.Fill };
        fields.Controls.Add(box, 1, row);
        var browse = new Button { Text = "…", Dock = DockStyle.Fill };
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                box.Text = dialog.FileName;
            }
        };
        fields.Controls.Add(browse, 2, row);
        return box;
    }

    private static void EnsureDefaultJoinRequirements()
    {
        if (!File.Exists(NlPaths.JoinRequirements))
        {
            JoinRequirementsStore.Save(NlPaths.JoinRequirements, JoinRequirements.None);
        }
    }

    private static string? ResolveRepoSample(params string[] parts)
    {
        var fromBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", Path.Combine(parts)));
        if (File.Exists(fromBase))
        {
            return fromBase;
        }

        var fromCwd = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts)));
        return File.Exists(fromCwd) ? fromCwd : null;
    }

    private void ApplyMinecraftPathDefaults()
    {
        if (string.IsNullOrWhiteSpace(_configBox.Text))
        {
            _configBox.Text = ResolveRepoSample("samples", "configs", "minecraft.nle") ?? _configBox.Text;
        }
    }

    private void ApplyGenericPathDefaults()
    {
        if (string.IsNullOrWhiteSpace(_configBox.Text))
        {
            _configBox.Text = ResolveRepoSample("samples", "configs", "generic.nle") ?? _configBox.Text;
        }
    }

    private void ApplyBeamngPathDefaults()
    {
        NlPaths.EnsureRoot();
        if (!File.Exists(NlPaths.BeamngEvents))
        {
            File.WriteAllText(NlPaths.BeamngEvents, "# NL BeamNG events — appended by NL_BeamNGBridge\n");
        }

        if (!File.Exists(NlPaths.BeamngKicks))
        {
            File.WriteAllText(NlPaths.BeamngKicks, "# NL BeamMP kick queue — appended by bridge; consumed by NL_Kick\n");
        }

        var sample = ResolveRepoSample("samples", "configs", "beamng.nle");
        if (string.IsNullOrWhiteSpace(_configBox.Text) && sample is not null)
        {
            _configBox.Text = sample;
        }

        if (string.IsNullOrWhiteSpace(_sourceBox.Text))
        {
            _sourceBox.Text = NlPaths.BeamngEvents;
        }
    }

    private void LoadDefaultProfile()
    {
        if (File.Exists(NlPaths.SessionProfile))
        {
            ApplyProfile(NlSessionRunner.LoadProfile(NlPaths.SessionProfile));
        }
    }

    private void ApplyProfile(SessionProfileFile profile)
    {
        _loadingProfile = true;
        try
        {
            _streamerBox.Text = profile.StreamerId;

            var preset = SessionHostGamePresetInfo.InferFromProfile(
                profile.Game,
                profile.RconEndpoint,
                profile.BeamngCommandEndpoint,
                profile.NlActionEndpoint);
            _gamePresetBox.SelectedIndex = (int)preset;
            OnGamePresetChanged(userInitiated: false);

            _actionPortBox.Text = preset switch
            {
                SessionHostGamePreset.Minecraft => profile.RconEndpoint ?? "",
                SessionHostGamePreset.BeamNg => profile.BeamngCommandEndpoint ?? "",
                _ => profile.NlActionEndpoint ?? "",
            };

            _configBox.Text = profile.ConfigPath;
            _sourceBox.Text = profile.SourcePath;
            _useSessionBusBox.Checked = profile.UseSessionBus;
            _antiCheatBox.Checked = profile.AntiCheat;
            _joinGateBox.Checked = profile.JoinGate;
            _anomalyAutoModBox.Checked = profile.AnomalyAutoMod;
        }
        finally
        {
            _loadingProfile = false;
        }
    }

    private void LoadBeamngDefaults()
    {
        _loadingProfile = true;
        try
        {
            _gamePresetBox.SelectedIndex = (int)SessionHostGamePreset.BeamNg;
            OnGamePresetChanged(userInitiated: false);
            ApplyBeamngPathDefaults();
            _actionPortBox.Text = $"127.0.0.1:{NlPaths.BeamngCommandPort}";
            _antiCheatBox.Checked = true;
            _joinGateBox.Checked = false;
            _anomalyAutoModBox.Checked = false;
            _replayBox.Checked = false;
            _useSessionBusBox.Checked = false;
        }
        finally
        {
            _loadingProfile = false;
        }

        _statusLabel.Text = "BeamNG freeroam defaults loaded (join gate off until BeamMP).";
        AppendLog($"BeamNG defaults: source={NlPaths.BeamngEvents}, cmd=127.0.0.1:{NlPaths.BeamngCommandPort}");
    }

    private void LoadBusDefaults()
    {
        var repoSample = ResolveRepoSample("samples", "configs", "generic.nle");

        var bus = NlSessionBusHelper.CreateBusInfo(
            NlSessionBusDefaults.DefaultBindHost,
            NlSessionBusDefaults.HttpPort,
            NlSessionBusDefaults.WebSocketPort,
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N")[..12]);

        var profile = CaptureProfile();
        NlSessionBusHelper.ApplyBusSource(profile, bus);
        if (repoSample is not null)
        {
            profile.ConfigPath = repoSample;
        }

        profile.UseSessionBus = true;
        ApplyProfile(profile);
        _gamePresetBox.SelectedIndex = (int)SessionHostGamePreset.Generic;
        OnGamePresetChanged(userInitiated: false);
        _statusLabel.Text = "Session bus defaults (bridge URL includes token).";
        AppendLog($"Bus defaults: {bus.BridgeConnectUrl}");
    }

    private SessionProfileFile CaptureProfile()
    {
        var preset = SelectedPreset;
        var actionPort = string.IsNullOrWhiteSpace(_actionPortBox.Text) ? null : _actionPortBox.Text.Trim();

        string? rcon = null;
        string? beamng = null;
        string? nlAction = null;

        switch (preset.ActionField)
        {
            case SessionHostActionField.Rcon:
                rcon = actionPort;
                break;
            case SessionHostActionField.BeamngUdp:
                beamng = actionPort;
                break;
            case SessionHostActionField.NlAction:
                nlAction = actionPort;
                break;
        }

        return new SessionProfileFile
        {
            StreamerId = string.IsNullOrWhiteSpace(_streamerBox.Text) ? NlPaths.DefaultStreamerId : _streamerBox.Text.Trim(),
            Game = preset.EngineGame,
            ConfigPath = _configBox.Text.Trim(),
            SourcePath = _sourceBox.Text.Trim(),
            RconEndpoint = rcon,
            BeamngCommandEndpoint = beamng,
            NlActionEndpoint = nlAction,
            UseSessionBus = _useSessionBusBox.Checked,
            AntiCheat = _antiCheatBox.Checked,
            JoinGate = _joinGateBox.Checked,
            AnomalyAutoMod = _anomalyAutoModBox.Checked,
            UseDefaultDataPaths = true,
        };
    }

    private void LoadProfileDialog()
    {
        using var dialog = new OpenFileDialog { Filter = "Session profile (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ApplyProfile(NlSessionRunner.LoadProfile(dialog.FileName));
            _statusLabel.Text = $"Loaded {dialog.FileName}";
        }
    }

    private void SaveProfileDialog()
    {
        using var dialog = new SaveFileDialog { Filter = "Session profile (*.json)|*.json", FileName = "session-profile.json" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SaveProfile(dialog.FileName);
        }
    }

    private void SaveProfile(string path)
    {
        NlSessionRunner.SaveProfile(path, CaptureProfile());
        _statusLabel.Text = $"Saved {path}";
        AppendLog($"Saved profile → {path}");
    }

    private async Task StartSessionAsync()
    {
        if (_sessions.IsRunning || _runTask is { IsCompleted: false })
        {
            return;
        }

        var profile = CaptureProfile();
        if (profile.UseSessionBus)
        {
            var token = profile.BusToken ?? Guid.NewGuid().ToString("N");
            var bus = NlSessionBusHelper.CreateBusInfo(
                NlSessionBusDefaults.DefaultBindHost,
                NlSessionBusDefaults.HttpPort,
                NlSessionBusDefaults.WebSocketPort,
                token,
                Guid.NewGuid().ToString("N")[..12]);
            NlSessionBusHelper.ApplyBusSource(profile, bus);
        }
        else if (string.IsNullOrWhiteSpace(profile.BusToken))
        {
            profile.BusToken = null;
        }

        if (string.IsNullOrWhiteSpace(profile.ConfigPath) || !File.Exists(profile.ConfigPath))
        {
            MessageBox.Show(this, "Choose a valid .nle config path.", "NL Session Host", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.SourcePath))
        {
            MessageBox.Show(this, "Choose a source log / NDJSON path, or use tcp:// / ws://.", "NL Session Host", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var networkSource = NlSessionBusHelper.IsNetworkSource(profile.SourcePath);
        if (_replayBox.Checked && !networkSource && !File.Exists(profile.SourcePath))
        {
            MessageBox.Show(this, "Replay requires an existing source file.", "NL Session Host", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_replayBox.Checked && !networkSource && !File.Exists(profile.SourcePath))
        {
            try
            {
                var dir = Path.GetDirectoryName(profile.SourcePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(profile.SourcePath, "");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not create source file: {ex.Message}", "NL Session Host", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        SaveProfile(NlPaths.SessionProfile);

        var options = profile.ToSessionOptions(replay: _replayBox.Checked);
        _logBox.Clear();
        AppendLog("Starting session…");
        UpdateSessionButtons();
        _statusLabel.Text = "Session running…";

        _runTask = Task.Run(async () =>
        {
            try
            {
                await _sessions.StartAsync(options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                BeginInvoke(() => AppendLog($"ERROR: {ex.Message}"));
            }
        }, CancellationToken.None);

        await Task.CompletedTask;
    }

    private void StopSession()
    {
        _sessions.Stop();
    }

    private void UpdateSessionButtons()
    {
        _startBtn.Enabled = !_sessions.IsRunning;
        _stopBtn.Enabled = _sessions.IsRunning;
        if (!_sessions.IsRunning)
        {
            _statusLabel.Text = "Session stopped.";
        }
    }

    private void AppendLog(string line)
    {
        _logBox.AppendText(line + Environment.NewLine);
        _logBox.ScrollToCaret();
    }

    private void LaunchSibling(string projectName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, $"{projectName}.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", projectName.Replace("NL.", ""), $"{projectName}.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", projectName, "bin", "Debug", "net8.0-windows", $"{projectName}.exe")),
        };

        foreach (var exe in candidates)
        {
            if (File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
                return;
            }
        }

        MessageBox.Show(this,
            $"Could not find {projectName}.exe. Build that project (or publish with scripts/publish.ps1).",
            "NL Session Host", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
