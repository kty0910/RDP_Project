using RemoteMonitor.Client.Models;

namespace RemoteMonitor.Client.Forms;

public sealed class RemotePcEditForm : Form
{
    private readonly RemotePcInfo original;
    private readonly bool allowDelete;
    private readonly PersistentPlaceholderTextBox nameTextBox = new();
    private readonly PersistentPlaceholderTextBox hostTextBox = new();
    private readonly NumericUpDown statusPortInput = new();
    private readonly PersistentPlaceholderTextBox userIdTextBox = new();
    private readonly PersistentPlaceholderTextBox passwordTextBox = new();
    private readonly PersistentPlaceholderTextBox descriptionSummaryTextBox = new();
    private readonly CheckBox useBridgeCheckBox = new();
    private readonly PersistentPlaceholderTextBox bridgeHostTextBox = new();
    private readonly NumericUpDown bridgeApiPortInput = new();
#if BRIDGE_TOKEN_REQUIRED
    private readonly PersistentPlaceholderTextBox bridgeTokenTextBox = new();
#endif
    private bool deleteRequested;

#if BRIDGE_TOKEN_REQUIRED
    private const int BridgeSettingsRowCount = 10;
    private const int ButtonsRowIndex = 10;
#else
    private const int BridgeSettingsRowCount = 9;
    private const int ButtonsRowIndex = 9;
#endif

    private string descriptionDetails = string.Empty;

    public RemotePcInfo RemotePc { get; private set; }

    public bool IsDeleteRequested => deleteRequested;

    public RemotePcEditForm(RemotePcInfo remotePc, bool allowDelete = true)
    {
        original = remotePc;
        this.allowDelete = allowDelete;
        RemotePc = remotePc;

        Text = allowDelete ? "원격 PC 정보 수정" : "원격 PC 정보 추가";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
#if BRIDGE_TOKEN_REQUIRED
        ClientSize = new Size(520, 430);
#else
        ClientSize = new Size(520, 396);
#endif

        nameTextBox.Text = remotePc.Name;
        hostTextBox.Text = FormatHostForInput(remotePc.Host, remotePc.RdpPort);
        statusPortInput.Minimum = 1;
        statusPortInput.Maximum = 65535;
        statusPortInput.Value = remotePc.Port <= 0 ? 5000 : remotePc.Port;
        userIdTextBox.Text = remotePc.UserId;
        passwordTextBox.Text = remotePc.Password;
        descriptionSummaryTextBox.Text = remotePc.DescriptionSummary;
        descriptionDetails = remotePc.DescriptionDetails;
        useBridgeCheckBox.Checked = remotePc.UseBridge;
        bridgeHostTextBox.Text = remotePc.BridgeHost;
        bridgeApiPortInput.Minimum = 1;
        bridgeApiPortInput.Maximum = 65535;
        bridgeApiPortInput.Value = remotePc.BridgeApiPort <= 0 ? 5000 : remotePc.BridgeApiPort;
#if BRIDGE_TOKEN_REQUIRED
        bridgeTokenTextBox.Text = remotePc.BridgeToken;
#endif

        BuildLayout();
        SetBridgeInputsEnabled();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = ButtonsRowIndex + 1,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var index = 0; index < BridgeSettingsRowCount; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        ConfigureTextBox(nameTextBox);
        ConfigureTextBox(hostTextBox);
        ConfigureTextBox(userIdTextBox);
        ConfigureTextBox(passwordTextBox);
        ConfigureTextBox(descriptionSummaryTextBox);
        ConfigureTextBox(bridgeHostTextBox);
#if BRIDGE_TOKEN_REQUIRED
        ConfigureTextBox(bridgeTokenTextBox);
#endif
        nameTextBox.PersistentPlaceholder = "ex. V5";
        hostTextBox.PersistentPlaceholder = "ex. 127.0.0.1";
        userIdTextBox.PersistentPlaceholder = "원격 PC ID";
        passwordTextBox.PersistentPlaceholder = "원격 PC PW";
        descriptionSummaryTextBox.PersistentPlaceholder = "이 PC의 목적을 한 줄로 입력";
        descriptionSummaryTextBox.MaxLength = 100;
        bridgeHostTextBox.PersistentPlaceholder = "중개 PC IP";
#if BRIDGE_TOKEN_REQUIRED
        bridgeTokenTextBox.PersistentPlaceholder = "Token";
#endif
        passwordTextBox.UseSystemPasswordChar = true;
#if BRIDGE_TOKEN_REQUIRED
        bridgeTokenTextBox.UseSystemPasswordChar = true;
#endif
        statusPortInput.Dock = DockStyle.Left;
        statusPortInput.Width = 90;
        useBridgeCheckBox.Dock = DockStyle.Fill;
        useBridgeCheckBox.Text = "중개 PC 경유";
        useBridgeCheckBox.CheckedChanged += (_, _) => SetBridgeInputsEnabled();
        bridgeApiPortInput.Dock = DockStyle.Left;
        bridgeApiPortInput.Width = 90;

        layout.Controls.Add(CreateLabel("PC 이름"), 0, 0);
        layout.Controls.Add(nameTextBox, 1, 0);
        layout.Controls.Add(CreateLabel("IP"), 0, 1);
        layout.Controls.Add(hostTextBox, 1, 1);
        layout.Controls.Add(CreateLabel("Status Port"), 0, 2);
        layout.Controls.Add(CreateStatusPortPanel(), 1, 2);
        layout.Controls.Add(CreateLabel("ID"), 0, 3);
        layout.Controls.Add(userIdTextBox, 1, 3);
        layout.Controls.Add(CreateLabel("PW"), 0, 4);
        layout.Controls.Add(passwordTextBox, 1, 4);
        layout.Controls.Add(CreateLabel("부가 설명"), 0, 5);
        layout.Controls.Add(CreateDescriptionPanel(), 1, 5);
        layout.Controls.Add(useBridgeCheckBox, 0, 6);
        layout.SetColumnSpan(useBridgeCheckBox, 2);
        layout.Controls.Add(CreateLabel("중개 PC IP"), 0, 7);
        layout.Controls.Add(bridgeHostTextBox, 1, 7);
        layout.Controls.Add(CreateLabel("중개 PC Port"), 0, 8);
        layout.Controls.Add(CreateBridgePortPanel(), 1, 8);
#if BRIDGE_TOKEN_REQUIRED
        layout.Controls.Add(CreateLabel("Token"), 0, 9);
        layout.Controls.Add(bridgeTokenTextBox, 1, 9);
#endif

        var buttons = CreateButtons();
        layout.Controls.Add(buttons, 0, ButtonsRowIndex);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
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
        using var dialog = new RemotePcDescriptionForm(
            string.IsNullOrWhiteSpace(nameTextBox.Text) ? original.Name : nameTextBox.Text,
            descriptionSummaryTextBox.Text,
            descriptionDetails);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        descriptionSummaryTextBox.Text = dialog.DescriptionSummary;
        descriptionDetails = dialog.DescriptionDetails;
    }

    private Control CreateStatusPortPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var description = new Label
        {
            Text = "Server의 Status Port 입력",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(2, 3, 0, 0),
            ForeColor = Color.FromArgb(100, 100, 100),
            AutoEllipsis = true
        };

        panel.Controls.Add(statusPortInput, 0, 0);
        panel.Controls.Add(description, 1, 0);
        return panel;
    }


    private Control CreateBridgePortPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var description = new Label
        {
            Text = "중개 PC의 Port 입력",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(2, 3, 0, 0),
            ForeColor = Color.FromArgb(100, 100, 100),
            AutoEllipsis = true
        };

        panel.Controls.Add(bridgeApiPortInput, 0, 0);
        panel.Controls.Add(description, 1, 0);
        return panel;
    }
    private static void ConfigureTextBox(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
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
        saveButton.DialogResult = DialogResult.OK;
        saveButton.Click += (_, _) => Save();

        var cancelButton = CreateDialogButton("취소");
        cancelButton.DialogResult = DialogResult.Cancel;

        var deleteButton = CreateDialogButton("삭제");
        deleteButton.Click += (_, _) => Delete();

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        panel.Controls.Add(saveButton);
        panel.Controls.Add(cancelButton);
        if (allowDelete)
        {
            panel.Controls.Add(deleteButton);
        }

        return panel;
    }

    private static Button CreateDialogButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 90,
            Height = 38,
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private void SetBridgeInputsEnabled()
    {
        var enabled = useBridgeCheckBox.Checked;
        bridgeHostTextBox.Enabled = enabled;
        bridgeApiPortInput.Enabled = enabled;
#if BRIDGE_TOKEN_REQUIRED
        bridgeTokenTextBox.Enabled = enabled;
#endif
    }

    private static string FormatHostForInput(string host, int rdpPort)
    {
        if (string.IsNullOrWhiteSpace(host) || rdpPort <= 0 || rdpPort == 3389)
        {
            return host;
        }

        return $"{host}:{rdpPort}";
    }

    private static bool TryParseHostAndRdpPort(string input, int fallbackRdpPort, out string host, out int rdpPort, out string errorMessage)
    {
        host = input.Trim();
        rdpPort = fallbackRdpPort > 0 ? fallbackRdpPort : 3389;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(host))
        {
            errorMessage = "IP를 입력해 주세요.";
            return false;
        }

        if (host.StartsWith("[", StringComparison.Ordinal))
        {
            var closeBracketIndex = host.IndexOf(']');
            if (closeBracketIndex < 0)
            {
                errorMessage = "IP 형식을 확인해 주세요.";
                return false;
            }

            var bracketHost = host[1..closeBracketIndex].Trim();
            var remainder = host[(closeBracketIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(bracketHost))
            {
                errorMessage = "IP를 입력해 주세요.";
                return false;
            }

            if (remainder.Length == 0)
            {
                host = bracketHost;
                return true;
            }

            if (!remainder.StartsWith(":", StringComparison.Ordinal)
                || !TryParseRdpPort(remainder[1..], out rdpPort))
            {
                errorMessage = "포트는 1~65535 사이 숫자로 입력해 주세요.";
                return false;
            }

            host = bracketHost;
            return true;
        }

        var firstColonIndex = host.IndexOf(':');
        if (firstColonIndex < 0)
        {
            return true;
        }

        if (firstColonIndex != host.LastIndexOf(':'))
        {
            return true;
        }

        var hostPart = host[..firstColonIndex].Trim();
        var portPart = host[(firstColonIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(hostPart))
        {
            errorMessage = "IP를 입력해 주세요.";
            return false;
        }

        if (!TryParseRdpPort(portPart, out rdpPort))
        {
            errorMessage = "포트는 1~65535 사이 숫자로 입력해 주세요.";
            return false;
        }

        host = hostPart;
        return true;
    }

    private static bool TryParseRdpPort(string portText, out int rdpPort)
    {
        return int.TryParse(portText.Trim(), out rdpPort) && rdpPort is >= 1 and <= 65535;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(nameTextBox.Text)
            || string.IsNullOrWhiteSpace(hostTextBox.Text)
            || string.IsNullOrWhiteSpace(userIdTextBox.Text)
            || string.IsNullOrWhiteSpace(passwordTextBox.Text))
        {
            MessageBox.Show(
                "PC 이름, IP, ID, PW를 모두 입력해 주세요.",
                allowDelete ? "원격 PC 정보 수정" : "원격 PC 정보 추가",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (useBridgeCheckBox.Checked
            && (string.IsNullOrWhiteSpace(bridgeHostTextBox.Text)
#if BRIDGE_TOKEN_REQUIRED
                || string.IsNullOrWhiteSpace(bridgeTokenTextBox.Text))
#else
                )
#endif
            )
        {
            MessageBox.Show(
#if BRIDGE_TOKEN_REQUIRED
                "중개 PC 경유에는 중개 PC IP와 Token을 모두 입력해 주세요.",
#else
                "중개 PC 경유에는 중개 PC IP를 입력해 주세요.",
#endif
                "중개 PC 정보",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (!TryParseHostAndRdpPort(hostTextBox.Text, 3389, out var parsedHost, out var parsedRdpPort, out var hostErrorMessage))
        {
            MessageBox.Show(
                hostErrorMessage,
                allowDelete ? "원격 PC 정보 수정" : "원격 PC 정보 추가",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        RemotePc = new RemotePcInfo
        {
            Name = nameTextBox.Text.Trim(),
            Host = parsedHost,
            UserId = userIdTextBox.Text.Trim(),
            Password = passwordTextBox.Text,
            DescriptionSummary = descriptionSummaryTextBox.Text.Trim(),
            DescriptionDetails = descriptionDetails,
            Port = (int)statusPortInput.Value,
            RdpPort = parsedRdpPort,
            UseBridge = useBridgeCheckBox.Checked,
            BridgeHost = bridgeHostTextBox.Text.Trim(),
            BridgeApiPort = (int)bridgeApiPortInput.Value,
#if BRIDGE_TOKEN_REQUIRED
            BridgeToken = bridgeTokenTextBox.Text
#else
            BridgeToken = string.Empty
#endif
        };
    }

    private void Delete()
    {
        var result = MessageBox.Show(
            "선택한 원격 PC 정보를 삭제할까요?",
            "원격 PC 삭제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        deleteRequested = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed class PersistentPlaceholderTextBox : TextBox
    {
        private const int WmPaint = 0x000F;
        private static readonly Color PlaceholderColor = Color.FromArgb(150, 150, 150);

        private string persistentPlaceholder = string.Empty;

        public string PersistentPlaceholder
        {
            get => persistentPlaceholder;
            set
            {
                persistentPlaceholder = value;
                Invalidate();
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            RedrawPlaceholderAfterNativePaint();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            RedrawPlaceholderAfterNativePaint();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            RedrawPlaceholderAfterNativePaint();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            RedrawPlaceholderAfterNativePaint();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            RedrawPlaceholderAfterNativePaint();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            RedrawPlaceholderAfterNativePaint();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            RedrawPlaceholderAfterNativePaint();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WmPaint)
            {
                DrawPlaceholder();
            }
        }

        private void RedrawPlaceholderAfterNativePaint()
        {
            BeginInvoke(new Action(Invalidate));
        }

        private void DrawPlaceholder()
        {
            if (TextLength > 0 || string.IsNullOrWhiteSpace(PersistentPlaceholder))
            {
                return;
            }

            using var graphics = Graphics.FromHwnd(Handle);
            var bounds = ClientRectangle;
            bounds.Inflate(-2, 0);
            TextRenderer.DrawText(
                graphics,
                PersistentPlaceholder,
                Font,
                bounds,
                PlaceholderColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
