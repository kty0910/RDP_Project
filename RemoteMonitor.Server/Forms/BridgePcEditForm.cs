using RemoteMonitor.Server.Bridge;

namespace RemoteMonitor.Server.Forms;

public sealed class BridgePcEditForm : Form
{
    private readonly bool allowDelete;
    private readonly Func<BridgePcDescriptionForm?>? descriptionFormProvider;
    private readonly Action<BridgePcDescriptionForm>? descriptionFormOpened;
    private readonly TextBox nameTextBox = new();
    private readonly TextBox hostTextBox = new();
    private readonly NumericUpDown statusPortInput = new();
    private readonly NumericUpDown rdpPortInput = new();
    private readonly TextBox descriptionSummaryTextBox = new();
    private bool deleteRequested;
    private string descriptionDetails = string.Empty;
    private string descriptionDetailsRtf = string.Empty;
    private BridgePcDescriptionForm? descriptionForm;

    public void UpdateDescriptionDraft(string summary, string details, string detailsRtf)
    {
        descriptionSummaryTextBox.Text = summary ?? string.Empty;
        descriptionDetails = details ?? string.Empty;
        descriptionDetailsRtf = detailsRtf ?? string.Empty;
    }

    public BridgePcEditForm(
        BridgeTarget target,
        bool allowDelete,
        Func<BridgePcDescriptionForm?>? descriptionFormProvider = null,
        Action<BridgePcDescriptionForm>? descriptionFormOpened = null)
    {
        this.allowDelete = allowDelete;
        this.descriptionFormProvider = descriptionFormProvider;
        this.descriptionFormOpened = descriptionFormOpened;
        Target = target;

        Text = allowDelete ? "원격 PC 수정" : "원격 PC 추가";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 310);

        nameTextBox.Text = target.Name;
        nameTextBox.PlaceholderText = "ex. V5";
        nameTextBox.Dock = DockStyle.Fill;

        hostTextBox.Text = target.Host;
        hostTextBox.PlaceholderText = "ex. 192.168.250.3";
        hostTextBox.Dock = DockStyle.Fill;

        ConfigurePortInput(statusPortInput, target.ApiPort <= 0 ? 5000 : target.ApiPort);
        ConfigurePortInput(rdpPortInput, target.RdpPort <= 0 ? 3389 : target.RdpPort);

        descriptionSummaryTextBox.Text = target.DescriptionSummary;
        descriptionSummaryTextBox.PlaceholderText = "이 PC의 목적을 한 줄로 입력";
        descriptionSummaryTextBox.MaxLength = 100;
        descriptionSummaryTextBox.Dock = DockStyle.Fill;
        descriptionDetails = target.DescriptionDetails;
        descriptionDetailsRtf = target.DescriptionDetailsRtf;

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
            RowCount = 6,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
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
        layout.Controls.Add(CreateLabel("부가 설명"), 0, 4);
        layout.Controls.Add(CreateDescriptionPanel(), 1, 4);

        var buttons = CreateButtons();
        layout.Controls.Add(buttons, 0, 5);
        layout.SetColumnSpan(buttons, 2);
        return layout;
    }

    private Control CreateDescriptionPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));

        var detailButton = new Button
        {
            Text = "상세",
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
        detailButton.Click += (_, _) => OpenDescriptionEditor();

        panel.Controls.Add(descriptionSummaryTextBox, 0, 0);
        panel.Controls.Add(detailButton, 1, 0);
        return panel;
    }

    private void OpenDescriptionEditor()
    {
        if (descriptionForm is { IsDisposed: false })
        {
            RestoreAndActivate(descriptionForm);
            return;
        }

        if (descriptionFormProvider?.Invoke() is { IsDisposed: false } openDescriptionForm)
        {
            RestoreAndActivate(openDescriptionForm);
            return;
        }

        var dialog = new BridgePcDescriptionForm(
            nameTextBox.Text,
            descriptionSummaryTextBox.Text,
            descriptionDetails,
            descriptionDetailsRtf);
        AttachDescriptionForm(dialog);
        descriptionFormOpened?.Invoke(dialog);
        dialog.Show(this);
    }

    private void AttachDescriptionForm(BridgePcDescriptionForm dialog)
    {
        descriptionForm = dialog;

        dialog.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(descriptionForm, dialog))
            {
                descriptionForm = null;
            }

            if (IsDisposed || Disposing)
            {
                return;
            }

            if (dialog.DialogResult != DialogResult.OK)
            {
                return;
            }

            descriptionSummaryTextBox.Text = dialog.DescriptionSummary;
            descriptionDetails = dialog.DescriptionDetails;
            descriptionDetailsRtf = dialog.DescriptionDetailsRtf;
        };
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
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

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
        if (descriptionForm is { IsDisposed: false })
        {
            MessageBox.Show(
                "부가설명 수정이 아직 완료되지 않았습니다.\n부가설명 창에서 완료 또는 취소를 먼저 눌러 주세요.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            RestoreAndActivate(descriptionForm);
            return;
        }

        if (descriptionFormProvider?.Invoke() is { IsDisposed: false } openDescriptionForm)
        {
            MessageBox.Show(
                "메인 화면에서 연 부가설명 창이 아직 열려 있습니다.\n부가설명 창에서 완료 또는 취소를 먼저 눌러 주세요.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            RestoreAndActivate(openDescriptionForm);
            return;
        }

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
            DescriptionSummary = descriptionSummaryTextBox.Text.Trim(),
            DescriptionDetails = descriptionDetails,
            DescriptionDetailsRtf = descriptionDetailsRtf,
            ApiPort = (int)statusPortInput.Value,
            RdpPort = (int)rdpPortInput.Value
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void RestoreAndActivate(Form form)
    {
        if (form.WindowState == FormWindowState.Minimized)
        {
            form.WindowState = FormWindowState.Normal;
        }

        form.Show();
        form.Activate();
        form.BringToFront();
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
