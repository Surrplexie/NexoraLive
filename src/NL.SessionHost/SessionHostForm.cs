using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using NL.Core;
using NL.Core.Sp;
using NL.Moderation;
using NL.Server;

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
    private readonly CheckBox _antiCheatBox;
    private readonly CheckBox _joinGateBox;
    private readonly CheckBox _anomalyAutoModBox;
    private readonly CheckBox _replayBox;
    private readonly Button _startBtn;
    private readonly Button _stopBtn;
    private readonly RichTextBox _logBox;
    private readonly ToolStripStatusLabel _statusLabel;

    private CancellationTokenSource? _cts;
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

        LoadDefaultProfile();
        FormClosing += (_, e) =>
        {
            if (_runTask is { IsCompleted: false })
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

    private SessionProfileFile CaptureProfile() => new()
    {
        StreamerId = string.IsNullOrWhiteSpace(_streamerBox.Text) ? NlPaths.DefaultStreamerId : _streamerBox.Text.Trim(),
        Game = _gameBox.SelectedItem?.ToString() ?? "minecraft",
        ConfigPath = _configBox.Text.Trim(),
        SourcePath = _sourceBox.Text.Trim(),
        RconEndpoint = string.IsNullOrWhiteSpace(_rconBox.Text) ? null : _rconBox.Text.Trim(),
        BeamngCommandEndpoint = string.IsNullOrWhiteSpace(_beamngCmdBox.Text) ? null : _beamngCmdBox.Text.Trim(),
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
        if (_runTask is { IsCompleted: false })
        {
            return;
        }

        var profile = CaptureProfile();
        if (string.IsNullOrWhiteSpace(profile.ConfigPath) || !File.Exists(profile.ConfigPath))
        {
            MessageBox.Show(this, "Choose a valid .nle config path.", "NL Session Host", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.SourcePath))
        {
            MessageBox.Show(this, "Choose a source log / NDJSON path.", "NL Session Host", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_replayBox.Checked && !File.Exists(profile.SourcePath))
        {
            MessageBox.Show(this, "Replay requires an existing source file.", "NL Session Host", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_replayBox.Checked && !File.Exists(profile.SourcePath))
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
        _startBtn.Enabled = false;
        _stopBtn.Enabled = true;
        _statusLabel.Text = "Session running…";

        _cts = new CancellationTokenSource();
        var runner = new NlSessionRunner
        {
            Options = options,
            Log = line =>
            {
                if (IsDisposed)
                {
                    return;
                }

                BeginInvoke(() => AppendLog(line));
            },
        };

        _runTask = Task.Run(async () =>
        {
            try
            {
                await runner.RunAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                BeginInvoke(() => AppendLog($"ERROR: {ex.Message}"));
            }
            finally
            {
                BeginInvoke(() =>
                {
                    _startBtn.Enabled = true;
                    _stopBtn.Enabled = false;
                    _statusLabel.Text = "Session stopped.";
                });
            }
        }, CancellationToken.None);

        await Task.CompletedTask;
    }

    private void StopSession()
    {
        _cts?.Cancel();
        AppendLog("Stop requested…");
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
