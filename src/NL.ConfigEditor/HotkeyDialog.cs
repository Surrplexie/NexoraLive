using System.Windows.Forms;
using NL.NleEditor.Model;

namespace NL.ConfigEditor;

/// <summary>
/// A small modal dialog for adding or editing a single <see cref="HotkeyEntry"/>.
/// </summary>
internal sealed class HotkeyDialog : Form
{
    private readonly TextBox _comboBox;
    private readonly TextBox _actionBox;
    private readonly Button _okButton;

    public HotkeyEntry Result { get; private set; } = new();

    public HotkeyDialog(HotkeyEntry? existing = null)
    {
        Text = existing is null ? "Add Hotkey Binding" : "Edit Hotkey Binding";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(380, 148);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 3,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        layout.Controls.Add(new Label { Text = "Combo:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
        _comboBox = new TextBox { Text = existing?.Combo ?? "", Dock = DockStyle.Fill, PlaceholderText = "e.g. Ctrl+Alt+0" };
        layout.Controls.Add(_comboBox, 1, 0);

        layout.Controls.Add(new Label { Text = "Action:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
        _actionBox = new TextBox { Text = existing?.Action ?? "", Dock = DockStyle.Fill, PlaceholderText = "e.g. toggleMic" };
        layout.Controls.Add(_actionBox, 1, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };

        layout.SetColumnSpan(buttonPanel, 2);

        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        _okButton = new Button { Text = "OK", Width = 80 };
        _okButton.Click += OnOK;

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(_okButton);

        layout.Controls.Add(buttonPanel, 0, 2);

        Controls.Add(layout);

        AcceptButton = _okButton;
        CancelButton = cancelButton;

        _comboBox.TextChanged += (_, _) => UpdateOK();
        _actionBox.TextChanged += (_, _) => UpdateOK();
        UpdateOK();
    }

    private void UpdateOK()
    {
        _okButton.Enabled = _comboBox.Text.Trim().Length > 0 && _actionBox.Text.Trim().Length > 0;
    }

    private void OnOK(object? sender, EventArgs e)
    {
        Result = new HotkeyEntry
        {
            Combo  = _comboBox.Text.Trim(),
            Action = _actionBox.Text.Trim(),
        };
        DialogResult = DialogResult.OK;
    }
}
