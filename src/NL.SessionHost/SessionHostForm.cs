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
/// Thin Windows shell to start/stop one NLServer session profile (Minecraft-first):
/// .nle + log path + RCON + join gate + anti-cheat + shared %LOCALAPPDATA%\NL data paths.
/// Opens Config Editor / Moderation Console as companion tools.
/// </summary>
internal sealed class SessionHostForm : Form
{
    private readonly TextBox _streamerBox;
    private readonly ComboBox _gameBox;
    private readonly TextBox _configBox;
    private readonly TextBox _sourceBox;
    private readonly TextBox _rconBox;
    private readonly TextBox _beamngCmdBox;
    private readonly TextBox _nlActionBox;
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
        _gameBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _gameBox.Items.AddRange(new object[] { "minecraft", "generic" });
        _gameBox.SelectedIndex = 0;
        fields.Controls.Add(new Label { Text = "Game", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
        fields.Controls.Add(_gameBox, 1, 1);

        _configBox = AddBrowseRow(fields, 2, "Config (.nle)", "NLEvents (*.nle)|*.nle|All files (*.*)|*.*");
        _sourceBox = AddBrowseRow(fields, 3, "Source (log/ndjson)", "Logs/NDJSON (*.log;*.ndjson)|*.log;*.ndjson|All files (*.*)|*.*");
        _rconBox = AddRow(fields, 4, "RCON", new TextBox { PlaceholderText = "host:port:password (Minecraft; optional)" });
        _beamngCmdBox = AddRow(fields, 5, "BeamNG UDP", new TextBox
        {
            PlaceholderText = "127.0.0.1:27022 (NL_BeamNGBridge; optional)",
        });
        _nlActionBox = AddRow(fields, 6, "NL action", new TextBox
        {
            PlaceholderText = "auto (ws) or tcp://host:port",
        });
        _useSessionBusBox = new CheckBox { Text = "Use session bus (ws://)", AutoSize = true, Dock = DockStyle.Fill };
        fields.Controls.Add(new Label { Text = "Session bus", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 7);
        fields.Controls.Add(_useSessionBusBox, 1, 7);
        fields.SetColumnSpan(_useSessionBusBox, 2);

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

        LoadDefaultProfile();
        FormClosing += (_, _) =>
        {
            if (_sessions.IsRunning)
            {
                StopSession();
            }
        };
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

    private void LoadDefaultProfile()
    {
        if (File.Exists(NlPaths.SessionProfile))
        {
            ApplyProfile(NlSessionRunner.LoadProfile(NlPaths.SessionProfile));
        }
    }

    private void ApplyProfile(SessionProfileFile profile)
    {
        _streamerBox.Text = profile.StreamerId;
        _gameBox.SelectedItem = profile.Game is "generic" or "minecraft" ? profile.Game : "minecraft";
        _configBox.Text = profile.ConfigPath;
        _sourceBox.Text = profile.SourcePath;
        _rconBox.Text = profile.RconEndpoint ?? "";
        _beamngCmdBox.Text = profile.BeamngCommandEndpoint ?? "";
        _nlActionBox.Text = profile.NlActionEndpoint ?? "";
        _useSessionBusBox.Checked = profile.UseSessionBus;
        _antiCheatBox.Checked = profile.AntiCheat;
        _joinGateBox.Checked = profile.JoinGate;
        _anomalyAutoModBox.Checked = profile.AnomalyAutoMod;
    }

    private void LoadBeamngDefaults()
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

        var repoSample = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "configs", "beamng.nle"));
        if (!File.Exists(repoSample))
        {
            repoSample = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples", "configs", "beamng.nle"));
        }

        _gameBox.SelectedItem = "generic";
        _configBox.Text = File.Exists(repoSample) ? repoSample : _configBox.Text;
        _sourceBox.Text = NlPaths.BeamngEvents;
        _rconBox.Text = "";
        _beamngCmdBox.Text = $"127.0.0.1:{NlPaths.BeamngCommandPort}";
        _antiCheatBox.Checked = true;
        _joinGateBox.Checked = false; // solo freeroam; enable under BeamMP
        _anomalyAutoModBox.Checked = false;
        _replayBox.Checked = false;
        _statusLabel.Text = "BeamNG freeroam defaults loaded (join gate off until BeamMP).";
        AppendLog($"BeamNG defaults: source={NlPaths.BeamngEvents}, cmd=127.0.0.1:{NlPaths.BeamngCommandPort}");
    }

    private void LoadBusDefaults()
    {
        var repoSample = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "configs", "generic.nle"));
        if (!File.Exists(repoSample))
        {
            repoSample = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples", "configs", "generic.nle"));
        }

        var bus = NlSessionBusHelper.CreateBusInfo(
            NlSessionBusDefaults.DefaultBindHost,
            NlSessionBusDefaults.HttpPort,
            NlSessionBusDefaults.WebSocketPort,
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N")[..12]);

        var profile = CaptureProfile();
        NlSessionBusHelper.ApplyBusSource(profile, bus);
        if (File.Exists(repoSample))
        {
            profile.ConfigPath = repoSample;
        }

        ApplyProfile(profile);
        _statusLabel.Text = $"Session bus defaults (bridge URL includes token).";
        AppendLog($"Bus defaults: {bus.BridgeConnectUrl}");
    }

    private SessionProfileFile CaptureProfile() => new()
    {
        StreamerId = string.IsNullOrWhiteSpace(_streamerBox.Text) ? NlPaths.DefaultStreamerId : _streamerBox.Text.Trim(),
        Game = _gameBox.SelectedItem?.ToString() ?? "minecraft",
        ConfigPath = _configBox.Text.Trim(),
        SourcePath = _sourceBox.Text.Trim(),
        RconEndpoint = string.IsNullOrWhiteSpace(_rconBox.Text) ? null : _rconBox.Text.Trim(),
        BeamngCommandEndpoint = string.IsNullOrWhiteSpace(_beamngCmdBox.Text) ? null : _beamngCmdBox.Text.Trim(),
        NlActionEndpoint = string.IsNullOrWhiteSpace(_nlActionBox.Text) ? null : _nlActionBox.Text.Trim(),
        UseSessionBus = _useSessionBusBox.Checked,
        AntiCheat = _antiCheatBox.Checked,
        JoinGate = _joinGateBox.Checked,
        AnomalyAutoMod = _anomalyAutoModBox.Checked,
        UseDefaultDataPaths = true,
    };

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
            // Live follow waits for the file (BeamNG bridge creates/appends it).
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
            // artifacts/publish layout: SessionHost/../ModerationConsole/NL.ModerationConsole.exe
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
