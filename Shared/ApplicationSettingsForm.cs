namespace RemoteMonitor.Shared;

internal sealed class ApplicationSettingsOptions
{
    public required string ProgramName { get; init; }
    public required string Version { get; init; }
    public required string InstallPath { get; init; }
    public required string StartupDescription { get; init; }
    public required Func<bool> IsStartupEnabled { get; init; }
    public required Func<bool, Task> SetStartupEnabledAsync { get; init; }
    public required string ShortcutName { get; init; }
    public required string ExecutablePath { get; init; }
}

internal sealed class ApplicationSettingsForm : Form
{
    private static readonly Color BorderColor = Color.FromArgb(150, 150, 150);
    private static readonly Color SuccessColor = Color.FromArgb(30, 120, 65);
    private static readonly Color WarningColor = Color.FromArgb(190, 45, 45);

    private readonly ApplicationSettingsOptions options;
    private readonly WindowsShortcutService shortcutService;
    private readonly CheckBox startupCheckBox = new();
    private readonly Label startupStatusLabel = new();
    private readonly Label desktopStatusLabel = new();
    private readonly Label startMenuStatusLabel = new();
    private readonly Button desktopCreateButton = new();
    private readonly Button startMenuCreateButton = new();
    private readonly Button desktopDeleteButton = new();
    private readonly Button startMenuDeleteButton = new();

    public ApplicationSettingsForm(ApplicationSettingsOptions options)
    {
        this.options = options;
        shortcutService = new WindowsShortcutService(
            options.ShortcutName,
            options.ExecutablePath,
            $"{options.ProgramName} 실행");

        Text = $"{options.ProgramName} 설정";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(540, 360);
        Font = new Font("맑은 고딕", 9F);
        BackColor = Color.White;

        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(16, 6)
        };
        tabControl.TabPages.Add(CreateGeneralPage());
        tabControl.TabPages.Add(CreateShortcutPage());
        tabControl.TabPages.Add(CreateInformationPage());

        var closePanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            BackColor = Color.FromArgb(247, 247, 247),
            Padding = new Padding(0, 10, 14, 10)
        };

        var closeButton = CreateButton("닫기", 88);
        closeButton.Dock = DockStyle.Right;
        closeButton.DialogResult = DialogResult.OK;
        closePanel.Controls.Add(closeButton);

        AcceptButton = closeButton;
        CancelButton = closeButton;
        Controls.Add(tabControl);
        Controls.Add(closePanel);

        Shown += (_, _) =>
        {
            RefreshStartupState();
            RefreshShortcutState();
        };
    }

    private TabPage CreateGeneralPage()
    {
        var page = CreateTabPage("일반");

        var title = CreateSectionTitle("자동 실행");
        title.Location = new Point(24, 24);

        startupCheckBox.Text = options.StartupDescription;
        startupCheckBox.AutoSize = true;
        startupCheckBox.AutoCheck = false;
        startupCheckBox.Location = new Point(28, 68);
        startupCheckBox.Click += async (_, _) => await ToggleStartupAsync();

        startupStatusLabel.AutoSize = false;
        startupStatusLabel.Location = new Point(48, 100);
        startupStatusLabel.Size = new Size(440, 42);
        startupStatusLabel.ForeColor = Color.DimGray;

        page.Controls.Add(title);
        page.Controls.Add(startupCheckBox);
        page.Controls.Add(startupStatusLabel);
        return page;
    }

    private TabPage CreateShortcutPage()
    {
        var page = CreateTabPage("바로가기");

        var title = CreateSectionTitle("바로가기 생성");
        title.Location = new Point(24, 20);
        page.Controls.Add(title);

        AddShortcutRow(
            page,
            62,
            "바탕화면 바로가기",
            desktopStatusLabel,
            desktopCreateButton,
            desktopDeleteButton,
            shortcutService.CreateDesktopShortcut,
            shortcutService.RemoveDesktopShortcut);

        AddShortcutRow(
            page,
            118,
            "시작메뉴 바로가기",
            startMenuStatusLabel,
            startMenuCreateButton,
            startMenuDeleteButton,
            shortcutService.CreateStartMenuShortcut,
            shortcutService.RemoveStartMenuShortcut);

        var warningLabel = new Label
        {
            AutoSize = false,
            Location = new Point(24, 186),
            Size = new Size(466, 48),
            ForeColor = WarningColor,
            Text = "※ 설정에서 만든 바로가기는 삭제할 수 있으며 중복 생성되지 않습니다.",
            TextAlign = ContentAlignment.MiddleLeft
        };
        page.Controls.Add(warningLabel);
        return page;
    }

    private TabPage CreateInformationPage()
    {
        var page = CreateTabPage("정보");
        var title = CreateSectionTitle("프로그램 정보");
        title.Location = new Point(24, 20);
        page.Controls.Add(title);

        var table = new TableLayoutPanel
        {
            Location = new Point(24, 62),
            Size = new Size(466, 150),
            ColumnCount = 2,
            RowCount = 3,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddInformationRow(table, 0, "프로그램", options.ProgramName);
        AddInformationRow(table, 1, "버전", options.Version);
        AddInformationRow(table, 2, "설치 경로", options.InstallPath);
        page.Controls.Add(table);
        return page;
    }

    private void AddShortcutRow(
        Control parent,
        int top,
        string labelText,
        Label statusLabel,
        Button createButton,
        Button deleteButton,
        Action createShortcut,
        Action removeShortcut)
    {
        var label = new Label
        {
            Text = labelText,
            Location = new Point(28, top + 8),
            Size = new Size(180, 28),
            TextAlign = ContentAlignment.MiddleLeft
        };

        statusLabel.Location = new Point(195, top + 8);
        statusLabel.Size = new Size(86, 28);
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;

        ConfigureButton(deleteButton, "삭제", 82);
        deleteButton.Location = new Point(298, top + 6);
        deleteButton.Click += (_, _) =>
        {
            try
            {
                removeShortcut();
                RefreshShortcutState();
            }
            catch (OperationCanceledException)
            {
                // 관리자 권한 요청을 취소하면 기존 바로가기를 유지합니다.
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"바로가기를 삭제하지 못했습니다.{Environment.NewLine}{exception.Message}",
                    options.ProgramName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };

        ConfigureButton(createButton, "생성", 82);
        createButton.Location = new Point(390, top + 6);
        createButton.Click += (_, _) =>
        {
            try
            {
                createShortcut();
                RefreshShortcutState();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"바로가기를 생성하지 못했습니다.{Environment.NewLine}{exception.Message}",
                    options.ProgramName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };

        parent.Controls.Add(label);
        parent.Controls.Add(statusLabel);
        parent.Controls.Add(deleteButton);
        parent.Controls.Add(createButton);
    }

    private async Task ToggleStartupAsync()
    {
        var enabled = !options.IsStartupEnabled();
        startupCheckBox.Enabled = false;

        try
        {
            await options.SetStartupEnabledAsync(enabled);
        }
        catch (OperationCanceledException)
        {
            // 관리자 권한 요청을 취소하면 기존 설정을 유지한다.
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"자동 실행 설정을 변경하지 못했습니다.{Environment.NewLine}{exception.Message}",
                options.ProgramName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            startupCheckBox.Enabled = true;
            RefreshStartupState();
        }
    }

    private void RefreshStartupState()
    {
        var enabled = options.IsStartupEnabled();
        startupCheckBox.Checked = enabled;
        startupStatusLabel.Text = enabled
            ? "현재 Windows 자동 실행이 설정되어 있습니다."
            : "현재 Windows 자동 실행이 해제되어 있습니다.";
        startupStatusLabel.ForeColor = enabled ? SuccessColor : Color.DimGray;
    }

    private void RefreshShortcutState()
    {
        UpdateShortcutState(
            shortcutService.DesktopShortcutExists,
            shortcutService.IsDesktopShortcutUserCreated,
            shortcutService.CanRemoveDesktopShortcut,
            desktopStatusLabel,
            desktopCreateButton,
            desktopDeleteButton);
        UpdateShortcutState(
            shortcutService.StartMenuShortcutExists,
            shortcutService.IsStartMenuShortcutUserCreated,
            shortcutService.CanRemoveStartMenuShortcut,
            startMenuStatusLabel,
            startMenuCreateButton,
            startMenuDeleteButton);
    }

    private static void UpdateShortcutState(
        bool exists,
        bool userCreated,
        bool canRemove,
        Label statusLabel,
        Button createButton,
        Button deleteButton)
    {
        statusLabel.Text = userCreated ? "생성됨" : exists ? "설치됨" : "미생성";
        statusLabel.ForeColor = exists ? SuccessColor : Color.DimGray;
        createButton.Text = exists ? "생성됨" : "생성";
        createButton.Enabled = !exists;
        deleteButton.Enabled = canRemove;
    }

    private static TabPage CreateTabPage(string text)
    {
        return new TabPage
        {
            Text = text,
            BackColor = Color.White,
            Padding = Padding.Empty
        };
    }

    private static Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Size = new Size(466, 30),
            Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Button CreateButton(string text, int width)
    {
        var button = new Button();
        ConfigureButton(button, text, width);
        return button;
    }

    private static void ConfigureButton(Button button, string text, int width)
    {
        button.Text = text;
        button.Size = new Size(width, 32);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = Color.White;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.UseVisualStyleBackColor = false;
    }

    private static void AddInformationRow(TableLayoutPanel table, int row, string name, string value)
    {
        table.Controls.Add(
            new Label
            {
                Text = name,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 245),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty
            },
            0,
            row);

        table.Controls.Add(
            new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 8, 0),
                AutoEllipsis = true,
                Margin = Padding.Empty
            },
            1,
            row);
    }
}
