using RemoteMonitor.Shared.Forms;

namespace RemoteMonitor.Client.Forms;

public sealed class RemotePcDescriptionForm : Form
{
    private const int SummaryMaxLength = 100;
    private readonly TextBox summaryTextBox = new();
    private readonly RichDescriptionEditor detailsEditor;

    public string DescriptionSummary => summaryTextBox.Text.Trim();

    public string DescriptionDetails => detailsEditor.PlainText;

    public string DescriptionDetailsRtf => detailsEditor.RtfText;

    public Func<bool>? CompletionValidator { get; set; }

    public RemotePcDescriptionForm(string remotePcName, string summary, string details, string detailsRtf)
    {
        Text = "원격 PC 설명";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 540);

        summaryTextBox.Text = summary ?? string.Empty;
        summaryTextBox.MaxLength = SummaryMaxLength;
        summaryTextBox.Dock = DockStyle.Fill;

        detailsEditor = new RichDescriptionEditor(details, detailsRtf);

        Controls.Add(CreateLayout(remotePcName));
    }

    private Control CreateLayout(string remotePcName)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var pcNameLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(remotePcName) ? "원격 PC" : remotePcName.Trim(),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font, FontStyle.Bold),
            AutoEllipsis = true
        };

        layout.Controls.Add(pcNameLabel, 0, 0);
        layout.Controls.Add(CreateLabel("요약 (목록에 표시되는 한 줄 설명)"), 0, 1);
        layout.Controls.Add(summaryTextBox, 0, 2);
        layout.Controls.Add(CreateLabel("상세 설명"), 0, 3);
        layout.Controls.Add(detailsEditor, 0, 4);
        layout.Controls.Add(CreateButtons(), 0, 5);
        return layout;
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

    private Control CreateButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };

        var saveButton = CreateDialogButton("완료");
        saveButton.Click += (_, _) =>
        {
            if (CompletionValidator is not null && !CompletionValidator())
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = CreateDialogButton("취소");
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        panel.Controls.Add(saveButton);
        panel.Controls.Add(cancelButton);
        return panel;
    }

    private static Button CreateDialogButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 90,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };
    }
}
