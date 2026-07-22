using System.Drawing;
using System.Windows.Forms;
using NL.NleEditor;
using NL.NleEditor.Model;
using NL.Core;

namespace NL.ConfigEditor;

/// <summary>
/// Main editor window.  Left side: hotkey bindings + event rules.
/// Right side: live Rule-Engine preview (type an event + properties, click Evaluate)
/// and a read-only view of the generated .nle text.
/// </summary>
internal sealed class EditorForm : Form
{
    // ── State ──────────────────────────────────────────────────────────────────
    private ConfigModel _model = new();
    private string? _filePath;
    private bool _isDirty;

    // ── Hotkey controls ────────────────────────────────────────────────────────
    private readonly ListView _hotkeyListView;
    private readonly Button _hkAddBtn, _hkEditBtn, _hkRemoveBtn, _hkUpBtn, _hkDnBtn;

    // ── Event controls ─────────────────────────────────────────────────────────
    private readonly ListBox _eventListBox;
    private readonly Button _evAddBtn, _evRemoveBtn, _evRenameBtn;

    // ── Statement controls ─────────────────────────────────────────────────────
    private readonly ListView _stmtListView;
    private readonly Button _stAddBtn, _stEditBtn, _stRemoveBtn, _stUpBtn, _stDnBtn;

    // ── Preview / raw controls ─────────────────────────────────────────────────
    private readonly TextBox _previewEventBox;
    private readonly TextBox _previewPropsBox;
    private readonly Panel _previewResultPanel;
    private readonly Label _previewResultLabel;
    private readonly RichTextBox _rawNleBox;

    // ── Status ─────────────────────────────────────────────────────────────────
    private readonly ToolStripStatusLabel _statusLabel;

    public EditorForm(string? initialPath = null)
    {
        Text = "NL Config Editor";
        MinimumSize = new Size(960, 620);
        Size = new Size(1080, 700);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;

        // Menu
        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("File");
        fileMenu.DropDownItems.Add("New",        null, (_, _) => NewConfig());
        fileMenu.DropDownItems.Add("Open…",      null, (_, _) => OpenFile());
        fileMenu.DropDownItems.Add("Save",       null, (_, _) => Save());
        fileMenu.DropDownItems.Add("Save As…",   null, (_, _) => SaveAs());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Exit",       null, (_, _) => Close());
        menu.Items.Add(fileMenu);

        var toolsMenu = new ToolStripMenuItem("Tools");
        toolsMenu.DropDownItems.Add("Open in Daemon (notify to reload)", null, (_, _) => OpenInDaemon());
        toolsMenu.DropDownItems.Add("Copy .nle to clipboard", null, (_, _) => CopyToClipboard());
        menu.Items.Add(toolsMenu);
        MainMenuStrip = menu;
        Controls.Add(menu);

        // ToolStrip
        var ts = new ToolStrip();
        ts.Items.Add(new ToolStripButton("New",  null, (_, _) => NewConfig())  { ToolTipText = "New config" });
        ts.Items.Add(new ToolStripButton("Open", null, (_, _) => OpenFile())   { ToolTipText = "Open .nle file" });
        ts.Items.Add(new ToolStripButton("Save", null, (_, _) => Save())       { ToolTipText = "Save" });
        ts.Items.Add(new ToolStripSeparator());
        ts.Items.Add(new ToolStripButton("Open hotkeys.nle", null, (_, _) => OpenDefaultConfig()) { ToolTipText = "Open %LOCALAPPDATA%\\NL\\hotkeys.nle" });
        Controls.Add(ts);

        // Status bar
        var status = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
        status.Items.Add(_statusLabel);
        Controls.Add(status);

        // ── Main split: left | right ───────────────────────────────────────────
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 420,
        };
        Controls.Add(mainSplit);

        // ── LEFT: hotkeys + events (stacked) ──────────────────────────────────
        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        mainSplit.Panel1.Controls.Add(leftLayout);

        // Hotkeys group
        var hkGroup = new GroupBox { Text = "Hotkey Bindings", Dock = DockStyle.Fill, Padding = new Padding(6) };
        var hkLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        hkLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        hkLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _hotkeyListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
        };
        _hotkeyListView.Columns.Add("Combo",  160);
        _hotkeyListView.Columns.Add("Action", 160);
        _hotkeyListView.SelectedIndexChanged += (_, _) => UpdateHkButtons();
        hkLayout.Controls.Add(_hotkeyListView, 0, 0);

        var hkBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 2, 0, 0) };
        (_hkAddBtn    = Btn("+ Add"))   .Click += (_, _) => AddHotkey();
        (_hkEditBtn   = Btn("Edit"))    .Click += (_, _) => EditHotkey();
        (_hkRemoveBtn = Btn("Remove"))  .Click += (_, _) => RemoveHotkey();
        (_hkUpBtn     = Btn("↑"))       .Click += (_, _) => MoveHotkey(-1);
        (_hkDnBtn     = Btn("↓"))       .Click += (_, _) => MoveHotkey(1);
        hkBtnPanel.Controls.AddRange([_hkAddBtn, _hkEditBtn, _hkRemoveBtn, _hkUpBtn, _hkDnBtn]);
        hkLayout.Controls.Add(hkBtnPanel, 0, 1);

        hkGroup.Controls.Add(hkLayout);
        leftLayout.Controls.Add(hkGroup, 0, 0);

        // Events group (left: name list, right: statement list)
        var evGroup = new GroupBox { Text = "Event Rules", Dock = DockStyle.Fill, Padding = new Padding(6) };
        var evSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 150 };

        // Event name list
        var evLeftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        evLeftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        evLeftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _eventListBox = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _eventListBox.SelectedIndexChanged += OnEventSelected;
        evLeftLayout.Controls.Add(_eventListBox, 0, 0);

        var evBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 2, 0, 0) };
        (_evAddBtn    = Btn("+ Add"))  .Click += (_, _) => AddEvent();
        (_evRemoveBtn = Btn("Remove")) .Click += (_, _) => RemoveEvent();
        (_evRenameBtn = Btn("Rename")) .Click += (_, _) => RenameEvent();
        evBtnPanel.Controls.AddRange([_evAddBtn, _evRemoveBtn, _evRenameBtn]);
        evLeftLayout.Controls.Add(evBtnPanel, 0, 1);
        evSplit.Panel1.Controls.Add(evLeftLayout);

        // Statement list
        var stLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        stLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        stLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _stmtListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.None,
        };
        _stmtListView.Columns.Add("Statement", -2);
        _stmtListView.SelectedIndexChanged += (_, _) => UpdateStmtButtons();
        stLayout.Controls.Add(_stmtListView, 0, 0);

        var stBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 2, 0, 0) };
        (_stAddBtn    = Btn("+ Add"))  .Click += (_, _) => AddStatement();
        (_stEditBtn   = Btn("Edit"))   .Click += (_, _) => EditStatement();
        (_stRemoveBtn = Btn("Remove")) .Click += (_, _) => RemoveStatement();
        (_stUpBtn     = Btn("↑"))      .Click += (_, _) => MoveStatement(-1);
        (_stDnBtn     = Btn("↓"))      .Click += (_, _) => MoveStatement(1);
        stBtnPanel.Controls.AddRange([_stAddBtn, _stEditBtn, _stRemoveBtn, _stUpBtn, _stDnBtn]);
        stLayout.Controls.Add(stBtnPanel, 0, 1);
        evSplit.Panel2.Controls.Add(stLayout);

        evGroup.Controls.Add(evSplit);
        leftLayout.Controls.Add(evGroup, 0, 1);

        // ── RIGHT: rule preview (top) + raw .nle (bottom) ─────────────────────
        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        mainSplit.Panel2.Controls.Add(rightLayout);

        // Rule preview group
        var pvGroup = new GroupBox { Text = "Rule Engine Preview", Dock = DockStyle.Fill, Padding = new Padding(8) };
        var pvLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
        };
        pvLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        pvLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pvLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        pvLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        pvLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        pvLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        pvLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        pvLayout.Controls.Add(RLabel("Event name:"), 0, 0);
        _previewEventBox = new TextBox { Dock = DockStyle.Fill };
        pvLayout.Controls.Add(_previewEventBox, 1, 0);

        pvLayout.Controls.Add(RLabel("Properties:"), 0, 1);
        _previewPropsBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "name = 40  (one per line)",
            Multiline = true,
        };
        pvLayout.SetRowSpan(_previewPropsBox, 2);
        pvLayout.Controls.Add(_previewPropsBox, 1, 1);

        var evalBtn = new Button { Text = "▶  Evaluate", Dock = DockStyle.Left, Width = 110 };
        evalBtn.Click += (_, _) => RunPreview();
        pvLayout.SetColumnSpan(evalBtn, 2);
        pvLayout.Controls.Add(evalBtn, 0, 3);

        _previewResultPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.WhiteSmoke };
        _previewResultLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
        };
        _previewResultPanel.Controls.Add(_previewResultLabel);
        pvLayout.SetColumnSpan(_previewResultPanel, 2);
        pvLayout.Controls.Add(_previewResultPanel, 0, 4);

        pvGroup.Controls.Add(pvLayout);
        rightLayout.Controls.Add(pvGroup, 0, 0);

        // Raw .nle group
        var rawGroup = new GroupBox { Text = "Generated .nle  (read-only — reflects current editor state)", Dock = DockStyle.Fill, Padding = new Padding(6) };
        var rawLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        rawLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rawLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _rawNleBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(212, 212, 212),
            Font = new Font("Consolas", 9.5f),
            WordWrap = false,
        };
        rawLayout.Controls.Add(_rawNleBox, 0, 0);

        var rawBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 2, 0, 0) };
        var copyBtn = Btn("📋 Copy");
        copyBtn.Click += (_, _) => CopyToClipboard();
        var saveNowBtn = Btn("💾 Save to hotkeys.nle");
        saveNowBtn.Click += (_, _) => SaveToDefault();
        rawBtnPanel.Controls.AddRange([copyBtn, saveNowBtn]);
        rawLayout.Controls.Add(rawBtnPanel, 0, 1);

        rawGroup.Controls.Add(rawLayout);
        rightLayout.Controls.Add(rawGroup, 0, 1);

        // ── Initial state ──────────────────────────────────────────────────────
        UpdateHkButtons();
        UpdateStmtButtons();

        if (initialPath is not null && File.Exists(initialPath))
        {
            LoadFile(initialPath);
        }
        else
        {
            SetStatus("New config (unsaved)");
        }

        FormClosing += OnFormClosing;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Hotkey list operations ──────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private void AddHotkey()
    {
        using var dlg = new HotkeyDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _model.Hotkeys.Add(dlg.Result);
        MarkDirty();
        RefreshHotkeyList();
        _hotkeyListView.Items[^1].Selected = true;
    }

    private void EditHotkey()
    {
        var idx = SelectedHkIndex();
        if (idx < 0) return;
        using var dlg = new HotkeyDialog(_model.Hotkeys[idx]);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _model.Hotkeys[idx] = dlg.Result;
        MarkDirty();
        RefreshHotkeyList();
        if (idx < _hotkeyListView.Items.Count) _hotkeyListView.Items[idx].Selected = true;
    }

    private void RemoveHotkey()
    {
        var idx = SelectedHkIndex();
        if (idx < 0) return;
        _model.Hotkeys.RemoveAt(idx);
        MarkDirty();
        RefreshHotkeyList();
    }

    private void MoveHotkey(int delta)
    {
        var idx = SelectedHkIndex();
        var newIdx = idx + delta;
        if (idx < 0 || newIdx < 0 || newIdx >= _model.Hotkeys.Count) return;
        (_model.Hotkeys[idx], _model.Hotkeys[newIdx]) = (_model.Hotkeys[newIdx], _model.Hotkeys[idx]);
        MarkDirty();
        RefreshHotkeyList();
        _hotkeyListView.Items[newIdx].Selected = true;
    }

    private void RefreshHotkeyList()
    {
        _hotkeyListView.BeginUpdate();
        _hotkeyListView.Items.Clear();
        foreach (var h in _model.Hotkeys)
        {
            var item = new ListViewItem(h.Combo);
            item.SubItems.Add(h.Action);
            _hotkeyListView.Items.Add(item);
        }
        _hotkeyListView.EndUpdate();
        UpdatePreview();
        UpdateHkButtons();
    }

    private void UpdateHkButtons()
    {
        var idx = SelectedHkIndex();
        _hkEditBtn.Enabled   = idx >= 0;
        _hkRemoveBtn.Enabled = idx >= 0;
        _hkUpBtn.Enabled     = idx > 0;
        _hkDnBtn.Enabled     = idx >= 0 && idx < _model.Hotkeys.Count - 1;
    }

    private int SelectedHkIndex() =>
        _hotkeyListView.SelectedIndices.Count > 0 ? _hotkeyListView.SelectedIndices[0] : -1;

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Event list operations ───────────────────────────────────════════════
    // ═══════════════════════════════════════════════════════════════════════════

    private void AddEvent()
    {
        var name = Prompt("New event name:", "Add Event", "newEvent");
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_model.Events.Any(e => e.Name == name))
        {
            MessageBox.Show($"An event named '{name}' already exists.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _model.Events.Add(new EventEntry { Name = name, Statements = [new StatementEntry { Type = StatementType.Allow }] });
        MarkDirty();
        RefreshEventList();
        _eventListBox.SelectedIndex = _model.Events.Count - 1;
    }

    private void RemoveEvent()
    {
        var idx = _eventListBox.SelectedIndex;
        if (idx < 0) return;
        var name = _model.Events[idx].Name;
        if (MessageBox.Show($"Remove event '{name}' and all its rules?", "Confirm Remove",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _model.Events.RemoveAt(idx);
        MarkDirty();
        RefreshEventList();
    }

    private void RenameEvent()
    {
        var idx = _eventListBox.SelectedIndex;
        if (idx < 0) return;
        var newName = Prompt("New name:", "Rename Event", _model.Events[idx].Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == _model.Events[idx].Name) return;
        if (_model.Events.Any(e => e.Name == newName))
        {
            MessageBox.Show($"An event named '{newName}' already exists.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _model.Events[idx].Name = newName;
        // Keep a matching hotkey binding in sync if one exists
        var hk = _model.Hotkeys.FirstOrDefault(h => h.Action == _model.Events[idx].Name);
        if (hk is not null) hk.Action = newName;

        MarkDirty();
        RefreshEventList();
        _eventListBox.SelectedIndex = idx;
    }

    private void RefreshEventList()
    {
        var sel = _eventListBox.SelectedIndex;
        _eventListBox.BeginUpdate();
        _eventListBox.Items.Clear();
        foreach (var e in _model.Events)
            _eventListBox.Items.Add(e.Name);
        _eventListBox.EndUpdate();
        if (sel >= 0 && sel < _eventListBox.Items.Count)
            _eventListBox.SelectedIndex = sel;
        UpdatePreview();
    }

    private void OnEventSelected(object? sender, EventArgs e)
    {
        RefreshStatementList();
        UpdateStmtButtons();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Statement list operations ────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private EventEntry? CurrentEvent()
    {
        var idx = _eventListBox.SelectedIndex;
        return idx >= 0 ? _model.Events[idx] : null;
    }

    private void AddStatement()
    {
        var evt = CurrentEvent();
        if (evt is null) return;
        using var dlg = new StatementDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        evt.Statements.Add(dlg.Result);
        MarkDirty();
        RefreshStatementList();
        _stmtListView.Items[^1].Selected = true;
    }

    private void EditStatement()
    {
        var evt = CurrentEvent();
        var idx = SelectedStmtIndex();
        if (evt is null || idx < 0) return;
        using var dlg = new StatementDialog(evt.Statements[idx]);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        evt.Statements[idx] = dlg.Result;
        MarkDirty();
        RefreshStatementList();
        if (idx < _stmtListView.Items.Count) _stmtListView.Items[idx].Selected = true;
    }

    private void RemoveStatement()
    {
        var evt = CurrentEvent();
        var idx = SelectedStmtIndex();
        if (evt is null || idx < 0) return;
        evt.Statements.RemoveAt(idx);
        MarkDirty();
        RefreshStatementList();
    }

    private void MoveStatement(int delta)
    {
        var evt = CurrentEvent();
        var idx = SelectedStmtIndex();
        var newIdx = idx + delta;
        if (evt is null || idx < 0 || newIdx < 0 || newIdx >= evt.Statements.Count) return;
        (evt.Statements[idx], evt.Statements[newIdx]) = (evt.Statements[newIdx], evt.Statements[idx]);
        MarkDirty();
        RefreshStatementList();
        _stmtListView.Items[newIdx].Selected = true;
    }

    private void RefreshStatementList()
    {
        var evt = CurrentEvent();
        _stmtListView.BeginUpdate();
        _stmtListView.Items.Clear();
        if (evt is not null)
        {
            foreach (var s in evt.Statements)
                _stmtListView.Items.Add(new ListViewItem(s.ToDisplayString()));
        }
        _stmtListView.Columns[0].Width = -2; // auto-size to content
        _stmtListView.EndUpdate();
        UpdatePreview();
        UpdateStmtButtons();
    }

    private void UpdateStmtButtons()
    {
        var hasEvent = CurrentEvent() is not null;
        var idx = SelectedStmtIndex();
        var count = CurrentEvent()?.Statements.Count ?? 0;

        _stAddBtn.Enabled    = hasEvent;
        _stEditBtn.Enabled   = idx >= 0;
        _stRemoveBtn.Enabled = idx >= 0;
        _stUpBtn.Enabled     = idx > 0;
        _stDnBtn.Enabled     = idx >= 0 && idx < count - 1;
    }

    private int SelectedStmtIndex() =>
        _stmtListView.SelectedIndices.Count > 0 ? _stmtListView.SelectedIndices[0] : -1;

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Live preview ─────────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private void UpdatePreview()
    {
        try
        {
            _rawNleBox.Text = NleWriter.Write(_model);
        }
        catch (Exception ex)
        {
            _rawNleBox.Text = $"// NleWriter error: {ex.Message}";
        }
    }

    private void RunPreview()
    {
        var eventName = _previewEventBox.Text.Trim();
        if (eventName.Length == 0)
        {
            ShowPreviewResult("Enter an event name above, then click Evaluate.", Color.WhiteSmoke, Color.Black);
            return;
        }

        // Parse current model back through the engine
        string nleText;
        try
        {
            nleText = NleWriter.Write(_model);
        }
        catch (Exception ex)
        {
            ShowPreviewResult($"Writer error: {ex.Message}", Color.FromArgb(255, 220, 220), Color.DarkRed);
            return;
        }

        RuleEngine engine;
        try
        {
            var ast = Parser.Parse(nleText);
            engine = new RuleEngine(ast);
        }
        catch (Exception ex)
        {
            ShowPreviewResult($"Parse error: {ex.Message}", Color.FromArgb(255, 220, 220), Color.DarkRed);
            return;
        }

        // Parse properties
        var props = new Dictionary<string, double>();
        foreach (var line in _previewPropsBox.Lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            var eq = trimmed.IndexOf('=');
            if (eq < 0) continue;
            var key = trimmed[..eq].Trim();
            if (double.TryParse(trimmed[(eq + 1)..].Trim(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
            {
                props[key] = val;
            }
        }

        var gameEvent = new GameEvent(eventName, props);
        var result = engine.Evaluate(gameEvent);

        var isAllow = result.Decision == Decision.Allow;
        var color   = isAllow ? Color.FromArgb(220, 255, 220) : Color.FromArgb(255, 220, 220);
        var icon    = isAllow ? "✓ ALLOW" : "✗ BLOCK";
        var message = result.Message is not null ? $"\n{result.Message}" : "";
        ShowPreviewResult($"{icon}{message}", color, isAllow ? Color.DarkGreen : Color.DarkRed);
    }

    private void ShowPreviewResult(string text, Color bg, Color fg)
    {
        _previewResultPanel.BackColor = bg;
        _previewResultLabel.ForeColor = fg;
        _previewResultLabel.Text = text;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── File I/O ──────────────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private void NewConfig()
    {
        if (!ConfirmDiscard()) return;
        _model = new ConfigModel();
        _filePath = null;
        _isDirty = false;
        RefreshAll();
        SetStatus("New config (unsaved)");
    }

    private void OpenFile()
    {
        if (!ConfirmDiscard()) return;
        using var dlg = new OpenFileDialog
        {
            Title = "Open .nle config",
            Filter = "NLEvent configs (*.nle)|*.nle|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        LoadFile(dlg.FileName);
    }

    private void LoadFile(string path)
    {
        try
        {
            var src = File.ReadAllText(path);
            _model = NleLoader.Load(src);
            _filePath = path;
            _isDirty = false;
            RefreshAll();
            SetStatus($"Loaded: {path}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load:\n{ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Save()
    {
        if (_filePath is null) { SaveAs(); return; }
        WriteToDisk(_filePath);
    }

    private void SaveAs()
    {
        using var dlg = new SaveFileDialog
        {
            Title  = "Save .nle config",
            Filter = "NLEvent configs (*.nle)|*.nle|All files (*.*)|*.*",
            FileName = Path.GetFileName(_filePath) ?? "hotkeys.nle",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        WriteToDisk(dlg.FileName);
    }

    private void SaveToDefault()
    {
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NL", "hotkeys.nle");
        Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);
        WriteToDisk(defaultPath);
    }

    private void WriteToDisk(string path)
    {
        try
        {
            File.WriteAllText(path, NleWriter.Write(_model));
            _filePath = path;
            _isDirty = false;
            UpdateTitle();
            SetStatus($"Saved: {path}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Save failed:\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenDefaultConfig()
    {
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NL", "hotkeys.nle");
        if (!ConfirmDiscard()) return;
        if (File.Exists(defaultPath))
        {
            LoadFile(defaultPath);
        }
        else
        {
            if (MessageBox.Show($"Default config not found at:\n{defaultPath}\n\nCreate a new config there?",
                    "Not found", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _filePath = defaultPath;
                _model = new ConfigModel();
                MarkDirty();
                RefreshAll();
            }
        }
    }

    private void OpenInDaemon()
    {
        if (_isDirty)
        {
            var res = MessageBox.Show("Save changes before opening in daemon?", "Unsaved changes",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (res == DialogResult.Cancel) return;
            if (res == DialogResult.Yes) Save();
        }
        SetStatus("Daemon will auto-reload the file within ~1 second after saving.");
    }

    private void CopyToClipboard()
    {
        Clipboard.SetText(NleWriter.Write(_model));
        SetStatus("Copied to clipboard.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Helpers ──────────────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private void RefreshAll()
    {
        RefreshHotkeyList();
        RefreshEventList();
        _eventListBox.SelectedIndex = _model.Events.Count > 0 ? 0 : -1;
        RefreshStatementList();
        UpdatePreview();
        UpdateTitle();
    }

    private void MarkDirty()
    {
        _isDirty = true;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var name = _filePath is not null ? Path.GetFileName(_filePath) : "Untitled";
        Text = $"NL Config Editor — {name}{(_isDirty ? " *" : "")}";
    }

    private void SetStatus(string text) => _statusLabel.Text = text;

    private bool ConfirmDiscard()
    {
        if (!_isDirty) return true;
        var r = MessageBox.Show("You have unsaved changes. Discard them?", "Unsaved Changes",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
        return r == DialogResult.Yes;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_isDirty && !ConfirmDiscard())
            e.Cancel = true;
    }

    private static string? Prompt(string label, string title, string defaultValue = "")
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(360, 90),
        };
        var lbl = new Label { Left = 10, Top = 10, Width = 340, Text = label };
        var box = new TextBox { Left = 10, Top = 28, Width = 340, Text = defaultValue };
        var ok  = new Button { Text = "OK",     Left = 180, Top = 56, Width = 80, DialogResult = DialogResult.OK };
        var can = new Button { Text = "Cancel", Left = 270, Top = 56, Width = 80, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange([lbl, box, ok, can]);
        form.AcceptButton = ok;
        form.CancelButton = can;
        return form.ShowDialog() == DialogResult.OK ? box.Text.Trim() : null;
    }

    private static Button Btn(string text) => new() { Text = text, AutoSize = true };
    private static Label RLabel(string text) =>
        new() { Text = text, TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
}
