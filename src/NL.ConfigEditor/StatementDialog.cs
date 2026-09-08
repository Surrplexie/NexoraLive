using System.Windows.Forms;
using NL.NleEditor.Model;

namespace NL.ConfigEditor;

/// <summary>
/// Modal dialog for creating or editing a <see cref="StatementEntry"/> (allow, block, deny,
/// warn, or if with up to three and/or conditions plus then/else branches).
/// </summary>
internal sealed class StatementDialog : Form
{
    // ── Controls ──────────────────────────────────────────────────────────────
    private readonly ComboBox _typeCombo;

    // warn panel
    private readonly Panel _warnPanel;
    private readonly TextBox _warnMsgBox;

    // if panel
    private readonly Panel _ifPanel;
    // condition rows (up to 3)
    private readonly TextBox[] _leftBoxes = new TextBox[3];
    private readonly ComboBox[] _opCombos  = new ComboBox[3];
    private readonly TextBox[] _rightBoxes = new TextBox[3];
    private readonly ComboBox[] _joinCombos = new ComboBox[2];   // between row 0-1 and 1-2
    private readonly CheckBox[] _addRowChecks = new CheckBox[2]; // "+ second condition", "+ third"
    // then branch (assigned inside BuildIfPanel, cannot be readonly)
    private ComboBox _thenActionCombo = null!;
    private TextBox _thenWarnBox = null!;
    // else branch
    private CheckBox _hasElseCheck = null!;
    private ComboBox _elseActionCombo = null!;
    private TextBox _elseWarnBox = null!;

    public StatementEntry Result { get; private set; } = new();

    private static readonly string[] ActionItems = ["allow", "block", "deny", "warn…"];
    private static readonly string[] OpItems = [">", "<", ">=", "<=", "==", "!="];

    public StatementDialog(StatementEntry? existing = null)
    {
        Text = existing is null ? "Add Statement" : "Edit Statement";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(520, 480);
        AutoScroll = true;

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 1,
            AutoSize = true,
        };

        // ── Type row ──────────────────────────────────────────────────────────
        outer.Controls.Add(new Label { Text = "Statement type:", AutoSize = true });

        _typeCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 160,
        };
        _typeCombo.Items.AddRange(["allow", "block", "deny", "warn \"…\"", "if … :"]);
        _typeCombo.SelectedIndex = 0;
        _typeCombo.SelectedIndexChanged += OnTypeChanged;
        outer.Controls.Add(_typeCombo);

        // ── Warn panel ────────────────────────────────────────────────────────
        _warnPanel = new Panel { Width = 490, Height = 34, Visible = false };
        _warnMsgBox = new TextBox { Left = 110, Top = 6, Width = 370, PlaceholderText = "warn message" };
        _warnPanel.Controls.Add(new Label { Text = "Message:", Top = 8, Left = 0, AutoSize = true });
        _warnPanel.Controls.Add(_warnMsgBox);
        outer.Controls.Add(_warnPanel);

        // ── If panel ──────────────────────────────────────────────────────────
        _ifPanel = new Panel { Width = 490, Height = 290, Visible = false };
        BuildIfPanel();
        outer.Controls.Add(_ifPanel);

        // ── OK / Cancel ───────────────────────────────────────────────────────
        var btnPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Width = 490, Height = 34,
        };
        var cancelBtn = new Button { Text = "Cancel", Width = 80, DialogResult = DialogResult.Cancel };
        var okBtn = new Button { Text = "OK", Width = 80 };
        okBtn.Click += OnOK;
        btnPanel.Controls.Add(cancelBtn);
        btnPanel.Controls.Add(okBtn);
        outer.Controls.Add(btnPanel);

        Controls.Add(outer);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        // Load existing
        if (existing is not null)
        {
            LoadFrom(existing);
        }
    }

    // ── Build if panel ────────────────────────────────────────────────────────

    private void BuildIfPanel()
    {
        var y = 0;

        // Condition group box
        var condGroup = new GroupBox { Text = "Condition", Left = 0, Top = y, Width = 490, Height = 188 };
        y += condGroup.Height + 4;

        var cy = 18;
        for (var i = 0; i < 3; i++)
        {
            var row = i;
            if (i > 0)
            {
                // Join combo (and/or) between rows
                var joinCombo = new ComboBox
                {
                    Left = 8, Top = cy, Width = 60, DropDownStyle = ComboBoxStyle.DropDownList,
                };
                joinCombo.Items.AddRange(["and", "or"]);
                joinCombo.SelectedIndex = 0;
                _joinCombos[i - 1] = joinCombo;
                condGroup.Controls.Add(joinCombo);

                // Checkbox to enable this row
                var addCheck = new CheckBox
                {
                    Text = $"Add {Ordinal(i + 1)} condition",
                    Left = 76, Top = cy + 2, AutoSize = true,
                };
                addCheck.CheckedChanged += (_, _) =>
                {
                    _leftBoxes[row].Enabled  = addCheck.Checked;
                    _opCombos[row].Enabled   = addCheck.Checked;
                    _rightBoxes[row].Enabled = addCheck.Checked;
                    joinCombo.Enabled        = addCheck.Checked;
                };
                _addRowChecks[i - 1] = addCheck;
                condGroup.Controls.Add(addCheck);

                cy += 26;

                _leftBoxes[row].Enabled  = false;
                _opCombos[row].Enabled   = false;
                _rightBoxes[row].Enabled = false;
                joinCombo.Enabled        = false;
            }

            var leftBox = new TextBox { Left = 8, Top = cy, Width = 160, PlaceholderText = "player.health" };
            var opCombo = new ComboBox { Left = 174, Top = cy, Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
            opCombo.Items.AddRange(OpItems);
            opCombo.SelectedIndex = 0;
            var rightBox = new TextBox { Left = 250, Top = cy, Width = 80, PlaceholderText = "0" };

            _leftBoxes[i]  = leftBox;
            _opCombos[i]   = opCombo;
            _rightBoxes[i] = rightBox;

            condGroup.Controls.Add(leftBox);
            condGroup.Controls.Add(opCombo);
            condGroup.Controls.Add(rightBox);

            cy += 30;
        }

        _ifPanel.Controls.Add(condGroup);

        // Then group
        var thenGroup = new GroupBox { Text = "Then", Left = 0, Top = y, Width = 490, Height = 64 };
        y += thenGroup.Height + 4;

        _thenActionCombo = new ComboBox { Left = 8, Top = 20, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        _thenActionCombo.Items.AddRange(ActionItems);
        _thenActionCombo.SelectedIndex = 0;
        _thenActionCombo.SelectedIndexChanged += (_, _) =>
            _thenWarnBox.Visible = _thenActionCombo.Text == "warn…";

        _thenWarnBox = new TextBox { Left = 116, Top = 20, Width = 360, Visible = false, PlaceholderText = "warn message" };
        thenGroup.Controls.Add(_thenActionCombo);
        thenGroup.Controls.Add(_thenWarnBox);
        _ifPanel.Controls.Add(thenGroup);

        // Else group
        var elseGroup = new GroupBox { Text = "Else (optional)", Left = 0, Top = y, Width = 490, Height = 64 };

        _hasElseCheck = new CheckBox { Text = "Add else branch", Left = 8, Top = 18, AutoSize = true };
        _hasElseCheck.CheckedChanged += (_, _) =>
        {
            _elseActionCombo.Enabled = _hasElseCheck.Checked;
            _elseWarnBox.Enabled     = _hasElseCheck.Checked;
        };

        _elseActionCombo = new ComboBox { Left = 134, Top = 18, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
        _elseActionCombo.Items.AddRange(ActionItems);
        _elseActionCombo.SelectedIndex = 0;
        _elseActionCombo.SelectedIndexChanged += (_, _) =>
            _elseWarnBox.Visible = _elseActionCombo.Enabled && _elseActionCombo.Text == "warn…";

        _elseWarnBox = new TextBox { Left = 242, Top = 18, Width = 234, Visible = false, Enabled = false, PlaceholderText = "warn message" };
        elseGroup.Controls.Add(_hasElseCheck);
        elseGroup.Controls.Add(_elseActionCombo);
        elseGroup.Controls.Add(_elseWarnBox);
        _ifPanel.Controls.Add(elseGroup);
    }

    private static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };

    // ── Load existing entry ───────────────────────────────────────────────────

    private void LoadFrom(StatementEntry e)
    {
        _typeCombo.SelectedIndex = e.Type switch
        {
            StatementType.Allow => 0,
            StatementType.Block => 1,
            StatementType.Deny  => 2,
            StatementType.Warn  => 3,
            StatementType.If    => 4,
            _                   => 0,
        };

        if (e.Type == StatementType.Warn)
        {
            _warnMsgBox.Text = e.WarnMessage ?? "";
        }

        if (e.Type == StatementType.If)
        {
            // Condition
            if (e.Condition is { } cond)
            {
                for (var i = 0; i < Math.Min(cond.Parts.Count, 3); i++)
                {
                    _leftBoxes[i].Text = cond.Parts[i].Left;
                    var opIdx = Array.IndexOf(OpItems, cond.Parts[i].Op);
                    _opCombos[i].SelectedIndex = opIdx >= 0 ? opIdx : 0;
                    _rightBoxes[i].Text = cond.Parts[i].Right;

                    if (i > 0)
                    {
                        _addRowChecks[i - 1].Checked = true;
                        if (i - 1 < cond.Joins.Count)
                        {
                            _joinCombos[i - 1].SelectedIndex = cond.Joins[i - 1] == "or" ? 1 : 0;
                        }
                    }
                }
            }

            // Then branch
            LoadBranchInto(e.ThenBody, _thenActionCombo, _thenWarnBox);

            // Else branch
            if (e.ElseBody is { Count: > 0 })
            {
                _hasElseCheck.Checked = true;
                LoadBranchInto(e.ElseBody, _elseActionCombo, _elseWarnBox);
            }
        }
    }

    private static void LoadBranchInto(List<StatementEntry> body, ComboBox actionCombo, TextBox warnBox)
    {
        if (body.Count == 0) return;
        var first = body[0];
        switch (first.Type)
        {
            case StatementType.Allow: actionCombo.SelectedIndex = 0; break;
            case StatementType.Block: actionCombo.SelectedIndex = 1; break;
            case StatementType.Deny:  actionCombo.SelectedIndex = 2; break;
            case StatementType.Warn:
                actionCombo.SelectedIndex = 3;
                warnBox.Text = first.WarnMessage ?? "";
                warnBox.Visible = true;
                break;
        }
    }

    // ── Type visibility toggle ────────────────────────────────────────────────

    private void OnTypeChanged(object? sender, EventArgs e)
    {
        _warnPanel.Visible = _typeCombo.SelectedIndex == 3;
        _ifPanel.Visible   = _typeCombo.SelectedIndex == 4;

        // Expand form to fit if panel
        Height = _ifPanel.Visible ? 520 : 180;
    }

    // ── Build result ──────────────────────────────────────────────────────────

    private void OnOK(object? sender, EventArgs e)
    {
        Result = _typeCombo.SelectedIndex switch
        {
            0 => new StatementEntry { Type = StatementType.Allow },
            1 => new StatementEntry { Type = StatementType.Block },
            2 => new StatementEntry { Type = StatementType.Deny },
            3 => new StatementEntry { Type = StatementType.Warn, WarnMessage = _warnMsgBox.Text },
            4 => BuildIfEntry(),
            _ => new StatementEntry { Type = StatementType.Allow },
        };
        DialogResult = DialogResult.OK;
    }

    private StatementEntry BuildIfEntry()
    {
        var cond = new ConditionEntry();
        cond.Parts.Add(new SimpleConditionEntry
        {
            Left  = _leftBoxes[0].Text.Trim().Length > 0 ? _leftBoxes[0].Text.Trim() : "true",
            Op    = OpItems[_opCombos[0].SelectedIndex],
            Right = _rightBoxes[0].Text.Trim().Length > 0 ? _rightBoxes[0].Text.Trim() : "true",
        });

        for (var i = 1; i < 3; i++)
        {
            if (!_addRowChecks[i - 1].Checked) break;
            cond.Joins.Add(_joinCombos[i - 1].SelectedIndex == 1 ? "or" : "and");
            cond.Parts.Add(new SimpleConditionEntry
            {
                Left  = _leftBoxes[i].Text.Trim().Length > 0 ? _leftBoxes[i].Text.Trim() : "true",
                Op    = OpItems[_opCombos[i].SelectedIndex],
                Right = _rightBoxes[i].Text.Trim().Length > 0 ? _rightBoxes[i].Text.Trim() : "true",
            });
        }

        return new StatementEntry
        {
            Type      = StatementType.If,
            Condition = cond,
            ThenBody  = [BuildBranchStatement(_thenActionCombo, _thenWarnBox)],
            ElseBody  = _hasElseCheck.Checked
                ? [BuildBranchStatement(_elseActionCombo, _elseWarnBox)]
                : null,
        };
    }

    private static StatementEntry BuildBranchStatement(ComboBox actionCombo, TextBox warnBox) =>
        actionCombo.SelectedIndex switch
        {
            0 => new StatementEntry { Type = StatementType.Allow },
            1 => new StatementEntry { Type = StatementType.Block },
            2 => new StatementEntry { Type = StatementType.Deny },
            3 => new StatementEntry { Type = StatementType.Warn, WarnMessage = warnBox.Text },
            _ => new StatementEntry { Type = StatementType.Allow },
        };
}
