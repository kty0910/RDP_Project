namespace RemoteMonitor.Client.Forms;

public sealed class PasswordPromptForm : Form
{
    private readonly TextBox passwordTextBox = new();
    private readonly TextBox confirmPasswordTextBox = new();
    private readonly bool requireConfirmation;

    public string Password => passwordTextBox.Text;

    public PasswordPromptForm(string title, bool requireConfirmation)
    {
        this.requireConfirmation = requireConfirmation;

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, requireConfirmation ? 178 : 136);
        Font = new Font("Segoe UI", 10F);

        BuildLayout();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = requireConfirmation ? 3 : 2,
            Padding = new Padding(18, 16, 18, 14)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        passwordTextBox.Dock = DockStyle.Fill;
        passwordTextBox.UseSystemPasswordChar = true;
        layout.Controls.Add(CreateLabel("비밀번호"), 0, 0);
        layout.Controls.Add(passwordTextBox, 1, 0);

        var buttonRowIndex = 1;

        if (requireConfirmation)
        {
            confirmPasswordTextBox.Dock = DockStyle.Fill;
            confirmPasswordTextBox.UseSystemPasswordChar = true;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.Controls.Add(CreateLabel("확인"), 0, 1);
            layout.Controls.Add(confirmPasswordTextBox, 1, 1);
            buttonRowIndex = 2;
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        var buttons = CreateButtons();
        layout.Controls.Add(buttons, 0, buttonRowIndex);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
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
            Padding = new Padding(0, 12, 0, 2),
            WrapContents = false
        };

        var okButton = CreateButton("확인");
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += (_, _) => ValidateInput();

        var cancelButton = CreateButton("취소");
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = okButton;
        CancelButton = cancelButton;

        panel.Controls.Add(okButton);
        panel.Controls.Add(cancelButton);
        return panel;
    }

    private static Button CreateButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 88,
            Height = 38,
            Margin = new Padding(6, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private void ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(passwordTextBox.Text))
        {
            MessageBox.Show("비밀번호를 입력해 주세요.", "비밀번호", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (requireConfirmation && passwordTextBox.Text != confirmPasswordTextBox.Text)
        {
            MessageBox.Show("비밀번호가 일치하지 않습니다.", "비밀번호", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
        }
    }
}
