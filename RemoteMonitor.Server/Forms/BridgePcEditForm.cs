using RemoteMonitor.Server.Bridge;

namespace RemoteMonitor.Server.Forms;

public sealed class BridgePcEditForm : Form
{
    private readonly bool allowDelete;
    private readonly TextBox nameTextBox = new();
    private readonly TextBox hostTextBox = new();
    private readonly NumericUpDown statusPortInput = new();
    private readonly NumericUpDown rdpPortInput = new();
    private bool deleteRequested;

    public BridgePcEditForm(BridgeTarget target, bool allowDelete)
    {
        this.allowDelete = allowDelete;
        Target = target;

        Text = allowDelete ? "원격 PC 수정" : "원격 PC 추가";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(430, 270);

        nameTextBox.Text = target.Name;
        nameTextBox.PlaceholderText = "ex. V5";
        nameTextBox.Dock = DockStyle.Fill;

        hostTextBox.Text = target.Host;
        hostTextBox.PlaceholderText = "ex. 192.168.250.3";
        hostTextBox.Dock = DockStyle.Fill;

        ConfigurePortInput(statusPortInput, target.ApiPort <= 0 ? 5000 : target.ApiPort);
        ConfigurePortInput(rdpPortInput, target.RdpPort <= 0 ? 3389 : target.RdpPort);

        Controls.Add(CreateLayout());
    }

    public BridgeTarget Target { get; private set; }

    public bool IsDeleteRequested => deleteRequested;

    private Control CreateLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CreateLabel("PC 이름"), 0, 0);
        layout.Controls.Add(nameTextBox, 1, 0);
        layout.Controls.Add(CreateLabel("IP"), 0, 1);
        layout.Controls.Add(hostTextBox, 1, 1);
        layout.Controls.Add(CreateLabel("Status Port"), 0, 2);
        layout.Controls.Add(statusPortInput, 1, 2);
        layout.Controls.Add(CreateLabel("RDP Port"), 0, 3);
        layout.Controls.Add(rdpPortInput, 1, 3);

        var buttons = CreateButtons();
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);
        return layout;
    }

    private Control CreateButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 12, 0, 0)
        };

        var saveButton = CreateButton("완료");
        saveButton.Click += (_, _) => Save();

        var cancelButton = CreateButton("취소");
        cancelButton.DialogResult = DialogResult.Cancel;

        panel.Controls.Add(saveButton);
        panel.Controls.Add(cancelButton);

        if (allowDelete)
        {
            var deleteButton = CreateButton("삭제");
            deleteButton.Click += (_, _) => Delete();
            panel.Controls.Add(deleteButton);
        }

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        return panel;
    }

    private void Save()
    {
        var name = nameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("PC 이름을 입력해 주세요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var host = hostTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            MessageBox.Show("원격 PC IP를 입력해 주세요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Target = new BridgeTarget
        {
            Name = name,
            Host = host,
            ApiPort = (int)statusPortInput.Value,
            RdpPort = (int)rdpPortInput.Value
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Delete()
    {
        if (MessageBox.Show(
            "선택한 원격 PC를 목록에서 삭제할까요?",
            "원격 PC 삭제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        deleteRequested = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void ConfigurePortInput(NumericUpDown input, int value)
    {
        input.Minimum = 1;
        input.Maximum = 65535;
        input.Value = Math.Clamp(value, 1, 65535);
        input.Width = 110;
        input.Dock = DockStyle.Left;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Button CreateButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 88,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };
    }
}
