using System.Net;

using System.Net.NetworkInformation;



using System.Net.Sockets;



using RemoteMonitor.Client.Config;



using RemoteMonitor.Client.Models;



using RemoteMonitor.Client.Networking;



using RemoteMonitor.Client.Services;







namespace RemoteMonitor.Client.Forms;







public sealed class MainForm : Form



{



    private const int DefaultApiPort = 5000;



    private const int DefaultRdpPort = 3389;







    private readonly RemotePcListService pcListService;



    private readonly RemoteMonitorApiClient apiClient = new();



    private readonly RdpAvailabilityService availabilityService = new();



    private readonly RdpConnectionService connectionService = new();



    private readonly List<RemotePcInfo> remotePcs = [];



    private readonly Dictionary<string, RemotePcRow> rowStates = [];



    private readonly Dictionary<string, int> statusRequestFailureCounts = [];



    private readonly HashSet<string> monitoringKeys = [];



    private readonly Dictionary<string, CancellationTokenSource> monitoringCancellationSources = [];



    private readonly DataGridView pcGrid = new();



    private readonly Button focusAnchorButton = new();



    private readonly Label pcNameValueLabel = new();



    private readonly Label ipValueLabel = new();



    private readonly System.Windows.Forms.Timer pollingTimer = new();

    private readonly System.Windows.Forms.Timer localPcInfoRefreshTimer = new();

    private readonly System.Windows.Forms.Timer automaticCheckStartTimer = new();



    private readonly ContextMenuStrip trayMenu = new();



    private readonly ContextMenuStrip remotePcDetailMenu = new();





    private readonly Dictionary<string, ToolStripMenuItem> trayRemotePcItems = [];



    private readonly NotifyIcon trayIcon;
    private readonly bool startInTray;



    private ToolStripMenuItem? trayCheckAllStartMenu;

    private ToolStripMenuItem? trayCheckAllStopMenu;



    private string? lastSelectedKey;




    private string? lastSelectedColumnName;



    private string? openDetailKey;



    private bool isPolling;

private bool isTrayStatusChecking;



    private bool keepTrayMenuOpenForStatusCheck;



    private bool suppressDetailMenuClosed;



    private bool ignoreNextExpandClick;







    public MainForm(bool startInTray = false)



    {
        this.startInTray = startInTray;



        Text = "RDP Client";



        Icon = GetApplicationIcon();



        ClientSize = new Size(1040, 620);



        MinimumSize = new Size(900, 560);



        StartPosition = FormStartPosition.CenterScreen;



        Font = new Font("Segoe UI", 10F);



        BackColor = Color.FromArgb(245, 247, 250);







        pcListService = new RemotePcListService(ClientOptions.Default);



        trayIcon = CreateTrayIcon();



        remotePcDetailMenu.Closed += RemotePcDetailMenuClosed;





        pollingTimer.Interval = 1000;



        pollingTimer.Tick += async (_, _) => await RefreshMonitoredRowsAsync();

        localPcInfoRefreshTimer.Interval = 3000;

        localPcInfoRefreshTimer.Tick += (_, _) => LoadLocalPcInfo();

        automaticCheckStartTimer.Interval = 5000;

        automaticCheckStartTimer.Tick += async (_, _) =>
        {
            automaticCheckStartTimer.Stop();
            await StartAllRemotePcsAsync();
        };







        BuildLayout();



        LoadRemotePcList();



        LoadLocalPcInfo();



    }







    protected override void OnLoad(EventArgs e)



    {



        base.OnLoad(e);



        NetworkChange.NetworkAddressChanged += NetworkAddressChanged;

        NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChanged;

        trayIcon.Visible = true;



        ActiveControl = focusAnchorButton;



        focusAnchorButton.Focus();



        pollingTimer.Start();

        localPcInfoRefreshTimer.Start();

        automaticCheckStartTimer.Start();



    }







    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
    }







    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (startInTray)
        {
            BeginInvoke(Hide);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        trayIcon.Visible = false;
        base.OnFormClosing(e);
    }







    protected override void Dispose(bool disposing)



    {



        if (disposing)



        {



            NetworkChange.NetworkAddressChanged -= NetworkAddressChanged;

            NetworkChange.NetworkAvailabilityChanged -= NetworkAvailabilityChanged;

            ClearMonitoringCancellationSources();



            trayIcon.Dispose();



            trayMenu.Dispose();



            remotePcDetailMenu.Dispose();



            pollingTimer.Dispose();

            localPcInfoRefreshTimer.Dispose();

            automaticCheckStartTimer.Dispose();



        }







        base.Dispose(disposing);



    }







    private void BuildLayout()



    {



        var root = new TableLayoutPanel



        {



            Dock = DockStyle.Fill,



            ColumnCount = 1,



            RowCount = 2,



            Padding = new Padding(10),



            BackColor = Color.FromArgb(245, 247, 250)



        };



        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));



        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));







        root.Controls.Add(CreateTopPanel(), 0, 0);



        root.Controls.Add(CreateRemotePcPanel(), 0, 1);



        Controls.Add(root);



        Controls.Add(focusAnchorButton);



        focusAnchorButton.Size = new Size(1, 1);



        focusAnchorButton.Location = new Point(-100, -100);



        focusAnchorButton.TabStop = false;



    }







    private Control CreateTopPanel()



    {



        var panel = new TableLayoutPanel



        {



            Dock = DockStyle.Fill,



            ColumnCount = 3,



            RowCount = 1,



            Margin = Padding.Empty



        };



        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));



        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));



        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));







        panel.Controls.Add(CreateLocalInfoPanel(), 0, 0);



        panel.Controls.Add(CreateBackupPanel(), 1, 0);



        panel.Controls.Add(CreateAddPcPanel(), 2, 0);



        return panel;



    }







    private Control CreateLocalInfoPanel()



    {



        var container = CreateSectionPanel("내 PC 정보", rightMargin: 6);



        var table = new TableLayoutPanel



        {



            Dock = DockStyle.Fill,



            ColumnCount = 2,



            RowCount = 2,



            Padding = new Padding(8, 4, 8, 8)



        };



        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));



        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));



        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));



        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));







        table.Controls.Add(CreateFieldLabel("PC 이름"), 0, 0);



        table.Controls.Add(CreateValueLabel(pcNameValueLabel), 1, 0);



        table.Controls.Add(CreateFieldLabel("IP 주소"), 0, 1);



        table.Controls.Add(CreateValueLabel(ipValueLabel), 1, 1);







        container.Controls.Add(table, 0, 1);



        return container;



    }







    private Control CreateBackupPanel()



    {



        var container = CreateSectionPanel("원격 목록 백업", rightMargin: 6);



        var panel = new TableLayoutPanel



        {



            Dock = DockStyle.Fill,



            ColumnCount = 1,



            RowCount = 2,



            Padding = new Padding(14, 8, 14, 10)



        };



        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));



        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));



        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));







        var exportButton = CreateSecondaryButton("내보내기");



        exportButton.Dock = DockStyle.Fill;



        exportButton.Margin = new Padding(0, 0, 0, 4);



        exportButton.Click += (_, _) => ExportRemotePcList();







        var importButton = CreateSecondaryButton("가져오기");



        importButton.Dock = DockStyle.Fill;



        importButton.Margin = new Padding(0, 4, 0, 0);



        importButton.Click += (_, _) => ImportRemotePcList();







        panel.Controls.Add(exportButton, 0, 0);



        panel.Controls.Add(importButton, 0, 1);







        container.Controls.Add(panel, 0, 1);



        return container;



    }







    private Control CreateAddPcPanel()



    {



        var container = CreateSectionPanel("원격 PC 추가", rightMargin: 0);



        var panel = new Panel



        {



            Dock = DockStyle.Fill,



            Padding = new Padding(10, 8, 10, 10)



        };







        var addButton = CreatePrimaryButton("원격 PC 추가");



        addButton.Dock = DockStyle.Fill;



        addButton.Margin = Padding.Empty;



        addButton.Click += (_, _) => AddRemotePcWithDialog();







        panel.Controls.Add(addButton);







        container.Controls.Add(panel, 0, 1);



        return container;



    }







    private Control CreateRemotePcPanel()



    {



        var container = new TableLayoutPanel



        {



            Dock = DockStyle.Fill,



            ColumnCount = 1,



            RowCount = 2



        };



        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));



        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));







        var headerPanel = new TableLayoutPanel



        {



            Dock = DockStyle.Fill,



            ColumnCount = 2,



            RowCount = 1,



            Margin = Padding.Empty



        };



        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));



        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));







        var title = new Label



        {



            Text = "원격 PC 목록",



            Dock = DockStyle.None,



            AutoSize = false,



            Width = 220,



            Height = 24,



            Font = new Font(Font.FontFamily, 11.5F, FontStyle.Bold),



            Margin = new Padding(0, 12, 0, 0),



            TextAlign = ContentAlignment.MiddleLeft



        };







        var checkAllButtonPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        var editOrderButton = CreateSecondaryButton("목록 편집");
        editOrderButton.Dock = DockStyle.None;
        editOrderButton.Location = new Point(0, 8);
        editOrderButton.Width = 104;
        editOrderButton.Height = 32;
        editOrderButton.TextAlign = ContentAlignment.MiddleCenter;
        editOrderButton.Click += (_, _) => OpenRemotePcOrderEditor();

        var leftCheckAllButton = CreateSecondaryButton("전체 체크 시작");
        leftCheckAllButton.Dock = DockStyle.None;
        leftCheckAllButton.Location = new Point(112, 8);
        leftCheckAllButton.Width = 154;
        leftCheckAllButton.Height = 32;
        leftCheckAllButton.TextAlign = ContentAlignment.MiddleCenter;
        leftCheckAllButton.Click += async (_, _) => await StartAllRemotePcsAsync();

        var checkAllButton = CreateSecondaryButton("전체 체크 종료");
        checkAllButton.Dock = DockStyle.None;
        checkAllButton.Location = new Point(276, 8);
        checkAllButton.Width = 154;
        checkAllButton.Height = 32;
        checkAllButton.TextAlign = ContentAlignment.MiddleCenter;
        checkAllButton.Click += (_, _) => StopAllRemotePcs();

        checkAllButtonPanel.Controls.Add(editOrderButton);
        checkAllButtonPanel.Controls.Add(leftCheckAllButton);
        checkAllButtonPanel.Controls.Add(checkAllButton);

        headerPanel.Controls.Add(title, 0, 0);

        headerPanel.Controls.Add(checkAllButtonPanel, 1, 0);







        ConfigurePcGrid();



        container.Controls.Add(headerPanel, 0, 0);



        container.Controls.Add(pcGrid, 0, 1);



        return container;



    }







    private TableLayoutPanel CreateSectionPanel(string title, int rightMargin = 6)



    {



        var container = new TableLayoutPanel



        {



            Dock = DockStyle.Fill,



            ColumnCount = 1,



            RowCount = 2,



            Margin = new Padding(0, 6, rightMargin, 0),



            Padding = new Padding(1),



            BackColor = Color.White



        };



        container.Paint += DrawSectionBorder;



        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));



        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));



        container.Controls.Add(new Label



        {



            Text = title,



            Dock = DockStyle.Fill,



            Padding = new Padding(6, 0, 0, 0),



            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),



            TextAlign = ContentAlignment.MiddleLeft



        }, 0, 0);



        return container;



    }







    private static void DrawSectionBorder(object? sender, PaintEventArgs e)



    {



        if (sender is not Control control)



        {



            return;



        }







        var bounds = control.ClientRectangle;



        bounds.Width -= 1;



        bounds.Height -= 1;



        e.Graphics.DrawRectangle(Pens.Black, bounds);



    }







    private static void ConfigureInput(TextBox textBox, string placeholder)



    {



        textBox.Dock = DockStyle.Fill;



        textBox.PlaceholderText = placeholder;



        textBox.Margin = new Padding(0, 3, 0, 3);



    }







    private static Label CreateFieldLabel(string text)



    {



        return new Label



        {



            Text = text,



            Dock = DockStyle.Fill,



            ForeColor = Color.FromArgb(80, 86, 94),



            TextAlign = ContentAlignment.MiddleLeft



        };



    }







    private static Label CreateValueLabel(Label label)



    {



        label.Dock = DockStyle.Fill;



        label.ForeColor = Color.FromArgb(20, 24, 30);



        label.TextAlign = ContentAlignment.MiddleLeft;



        return label;



    }







    private void ConfigurePcGrid()



    {



        pcGrid.Dock = DockStyle.Fill;



        pcGrid.Margin = new Padding(0, 4, 0, 0);



        pcGrid.AllowUserToAddRows = false;



        pcGrid.AllowUserToDeleteRows = false;



        pcGrid.AllowUserToResizeColumns = true;



        pcGrid.AllowUserToResizeRows = false;




        pcGrid.AutoGenerateColumns = false;



        pcGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;



        pcGrid.BackgroundColor = Color.White;



        pcGrid.BorderStyle = BorderStyle.FixedSingle;



        pcGrid.ReadOnly = true;



        pcGrid.RowHeadersVisible = false;



        pcGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;



        pcGrid.ShowCellToolTips = false;



        pcGrid.EnableHeadersVisualStyles = false;



        pcGrid.DefaultCellStyle.SelectionBackColor = Color.White;



        pcGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 24, 30);



        pcGrid.RowsDefaultCellStyle.SelectionBackColor = Color.White;



        pcGrid.RowsDefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 24, 30);



        pcGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SystemColors.Control;



        pcGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 24, 30);



        pcGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;



        pcGrid.ColumnHeadersDefaultCellStyle.Padding = Padding.Empty;



        pcGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;



        pcGrid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;



        pcGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;



        pcGrid.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;



        pcGrid.AlternatingRowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;



        pcGrid.RowTemplate.Height = 40;



        pcGrid.CellClick += PcGridCellClick;



        pcGrid.SelectionChanged += PcGridSelectionChanged;



        pcGrid.CellContentClick += PcGridCellContentClick;



        pcGrid.CellPainting += PcGridCellPainting;



        pcGrid.CellDoubleClick += PcGridCellDoubleClick;








        pcGrid.Columns.Add(CreateButtonColumn("ExpandToggle", string.Empty, "ExpandButtonText", 34));



        pcGrid.Columns.Add(CreateTextColumn("Name", "원격 PC 이름", 24));



        pcGrid.Columns.Add(CreateTextColumn("DescriptionSummary", "부가 설명", 30));



        pcGrid.Columns.Add(CreateTextColumn("ConnectionText", "연결 상태", 18));



        pcGrid.Columns.Add(CreateTextColumn("OccupancyText", "접속 인원", 20));



        pcGrid.Columns.Add(CreateButtonColumn("StatusToggle", "상태 체크", "StatusButtonText", 92));



        pcGrid.Columns.Add(CreateButtonColumn("Connect", "원격 접속", "ConnectButtonText", 92));



    }







    private static Icon GetApplicationIcon()



    {



        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("RemoteMonitor.Client.AppIconR");



        return stream is null ? SystemIcons.Application : new Icon(stream);



    }



    private NotifyIcon CreateTrayIcon()



    {



        trayMenu.Opening += (_, _) => UpdateTrayMenu();



        trayMenu.Closing += TrayMenuClosing;







        var notifyIcon = new NotifyIcon



        {



            Icon = GetApplicationIcon(),



            Text = "RDP Client",



            ContextMenuStrip = trayMenu



        };



        notifyIcon.DoubleClick += (_, _) => ShowDetailedMode();



        return notifyIcon;



    }







    private void UpdateTrayMenu()



    {



        trayMenu.Items.Clear();



        trayRemotePcItems.Clear();







        if (remotePcs.Count == 0)



        {



            var emptyMenu = new ToolStripMenuItem("등록된 원격 PC 없음")



            {



                Enabled = false



            };



            trayMenu.Items.Add(emptyMenu);



        }



        else



        {



            foreach (var remotePc in remotePcs)



            {



                var key = GetRemotePcKey(remotePc);



                rowStates.TryGetValue(key, out var row);







                var item = new ToolStripMenuItem(CreateTrayRemotePcText(remotePc, row))



                {



                    ForeColor = GetTrayRemotePcColor(row),



                    Enabled = row is not null && row.IsMonitoring



                };



                item.Click += async (_, _) => await ConnectFromTrayAsync(remotePc);



                trayRemotePcItems[key] = item;



                trayMenu.Items.Add(item);



            }



        }







        trayMenu.Items.Add(new ToolStripSeparator());



        trayCheckAllStartMenu = new ToolStripMenuItem("전체 체크 시작");

        trayCheckAllStartMenu.MouseDown += (_, _) => keepTrayMenuOpenForStatusCheck = true;

        trayCheckAllStartMenu.Click += async (_, _) => await CheckAllFromTrayAsync();

        trayMenu.Items.Add(trayCheckAllStartMenu);

        trayCheckAllStopMenu = new ToolStripMenuItem("전체 체크 종료");

        trayCheckAllStopMenu.MouseDown += (_, _) => keepTrayMenuOpenForStatusCheck = true;

        trayCheckAllStopMenu.Click += (_, _) => StopAllRemotePcs();

        trayMenu.Items.Add(trayCheckAllStopMenu);



        trayMenu.Items.Add("상세모드 열기", null, (_, _) => ShowDetailedMode());



        trayMenu.Items.Add(new ToolStripSeparator());



        trayMenu.Items.Add("종료", null, (_, _) => ExitFromTray());







        RefreshTrayMenuState();



    }







    private void RefreshTrayMenuState()



    {



        if (trayCheckAllStartMenu is not null)
        {
            trayCheckAllStartMenu.Text = isTrayStatusChecking ? "전체 체크 시작 중..." : "전체 체크 시작";
            trayCheckAllStartMenu.Enabled = !isTrayStatusChecking && remotePcs.Count > 0;
        }

        if (trayCheckAllStopMenu is not null)
        {
            trayCheckAllStopMenu.Enabled =
                automaticCheckStartTimer.Enabled ||
                monitoringKeys.Count > 0 ||
                monitoringCancellationSources.Count > 0;
        }







        foreach (var remotePc in remotePcs)



        {



            var key = GetRemotePcKey(remotePc);







            if (!trayRemotePcItems.TryGetValue(key, out var item))



            {



                continue;



            }







            rowStates.TryGetValue(key, out var row);



            item.Text = CreateTrayRemotePcText(remotePc, row);



            item.ForeColor = GetTrayRemotePcColor(row);



            item.Enabled = row is not null && row.IsMonitoring;



        }



    }







    private static string CreateTrayRemotePcText(RemotePcInfo remotePc, RemotePcRow? row)



    {



        var displayName = string.IsNullOrWhiteSpace(remotePc.Name) ? remotePc.Host : remotePc.Name;







        if (row is null || !row.IsMonitoring)



        {



            return $"{displayName} ({remotePc.Host}) - 미확인";



        }







        if (row.HasActiveSession)



        {



            return $"{displayName} ({remotePc.Host}) - 접속자 있음";



        }







        if (row.CanConfirmOccupancy)



        {



            return $"{displayName} ({remotePc.Host}) - 접속자 없음";



        }







        return $"{displayName} ({remotePc.Host}) - {row.ConnectionText}";



    }







    private static Color GetTrayRemotePcColor(RemotePcRow? row)



    {



        if (row is null || !row.IsMonitoring)



        {



            return SystemColors.GrayText;



        }







        if (row.HasActiveSession)



        {



            return Color.Firebrick;



        }







        if (row.CanConfirmOccupancy)



        {



            return Color.ForestGreen;



        }







        return row.IsReachable ? Color.DarkGoldenrod : SystemColors.GrayText;



    }







    private void ShowDetailedMode()



    {



        CloseTrayMenus();



        Show();



        WindowState = FormWindowState.Normal;



        Activate();



    }







    private void ExitFromTray()



    {






        trayIcon.Visible = false;



        Application.Exit();



    }







    private void TrayMenuClosing(object? sender, ToolStripDropDownClosingEventArgs e)



    {



        if (keepTrayMenuOpenForStatusCheck && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)



        {



            e.Cancel = true;



            keepTrayMenuOpenForStatusCheck = false;



        }



    }







    private void CloseTrayMenus()



    {



        trayMenu.Close();



    }







    private static DataGridViewTextBoxColumn CreateTextColumn(string propertyName, string headerText, float fillWeight)



    {



        var column = new DataGridViewTextBoxColumn



        {



            DataPropertyName = propertyName,



            HeaderText = headerText,



            FillWeight = fillWeight,



            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,



            SortMode = DataGridViewColumnSortMode.NotSortable,



            ToolTipText = string.Empty,



            DefaultCellStyle =



            {



                Alignment = DataGridViewContentAlignment.MiddleCenter



            }



        };



        column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;



        column.HeaderCell.Style.Padding = Padding.Empty;



        return column;



    }







    private static DataGridViewButtonColumn CreateButtonColumn(string name, string headerText, string propertyName, int width)



    {



        var column = new DataGridViewButtonColumn



        {



            Name = name,



            HeaderText = headerText,



            DataPropertyName = propertyName,



            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,



            Width = width,



            MinimumWidth = width,



            UseColumnTextForButtonValue = false,



            SortMode = DataGridViewColumnSortMode.NotSortable,



            FlatStyle = FlatStyle.Flat,



            ToolTipText = string.Empty,



            DefaultCellStyle =



            {



                Alignment = DataGridViewContentAlignment.MiddleCenter,



                Padding = Padding.Empty



            }



        };



        column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;



        column.HeaderCell.Style.Padding = Padding.Empty;



        return column;



    }







    private static Button CreatePrimaryButton(string text)



    {



        var button = new Button



        {



            Text = text,



            BackColor = Color.FromArgb(36, 96, 200),



            ForeColor = Color.White,



            FlatStyle = FlatStyle.Flat,



            Height = 64,



            Width = 120



        };



        button.FlatAppearance.BorderColor = Color.FromArgb(32, 85, 178);



        return button;



    }







    private static Button CreateSecondaryButton(string text)



    {



        var button = new Button



        {



            Text = text,



            BackColor = Color.White,



            ForeColor = Color.FromArgb(20, 24, 30),



            FlatStyle = FlatStyle.Flat,



            Height = 34



        };



        button.FlatAppearance.BorderColor = Color.FromArgb(120, 130, 145);



        return button;



    }







    private void LoadRemotePcList()



    {



        remotePcs.Clear();



        remotePcs.AddRange(pcListService.Load());



        NormalizeRemotePcList(saveIfChanged: true);



        ResetRowStates();



        BindGrid();



    }







    private void NormalizeRemotePcList(bool saveIfChanged)
    {
        var uniqueRemotePcs = new List<RemotePcInfo>();

        foreach (var remotePc in remotePcs)
        {
            if (uniqueRemotePcs.Any(existing => IsSameRemotePc(existing, remotePc)))
            {
                continue;
            }

            uniqueRemotePcs.Add(remotePc);
        }

        if (uniqueRemotePcs.Count == remotePcs.Count)
        {
            return;
        }

        remotePcs.Clear();
        remotePcs.AddRange(uniqueRemotePcs);

        if (saveIfChanged)
        {
            pcListService.Save(remotePcs);
        }
    }

    private void ResetRowStates()



    {



        rowStates.Clear();



        monitoringKeys.Clear();



        ClearMonitoringCancellationSources();







        foreach (var remotePc in remotePcs)



        {



            rowStates[GetRemotePcKey(remotePc)] = RemotePcRow.Pending(remotePc);



        }



    }







    private CancellationToken GetMonitoringToken(string key)



    {



        return monitoringCancellationSources.TryGetValue(key, out var cancellationSource)



            ? cancellationSource.Token



            : CancellationToken.None;



    }







    private void StartMonitoringCancellationSource(string key)



    {



        RemoveMonitoringCancellationSource(key);



        monitoringCancellationSources[key] = new CancellationTokenSource();



    }







    private void RemoveMonitoringCancellationSource(string key)



    {



        if (!monitoringCancellationSources.Remove(key, out var cancellationSource))



        {



            return;



        }







        cancellationSource.Cancel();



        cancellationSource.Dispose();



    }







    private void ClearMonitoringCancellationSources()



    {



        foreach (var cancellationSource in monitoringCancellationSources.Values)



        {



            cancellationSource.Cancel();



            cancellationSource.Dispose();



        }







        monitoringCancellationSources.Clear();



    }











    private void BindGrid(bool rebuildTrayMenu = true)



    {



        string? selectedKey = lastSelectedKey;







        if (selectedKey is null && GetSelectedRow() is { } selectedRow)



        {



            selectedKey = GetRemotePcKey(selectedRow.RemotePc);



        }







        var selectedColumnName = lastSelectedColumnName ?? pcGrid.CurrentCell?.OwningColumn?.Name;



        var firstDisplayedRowIndex = GetFirstDisplayedRowIndex();







        foreach (var item in rowStates)



        {



            item.Value.SetDetailOpen(item.Key == openDetailKey);



        }







        var orderedRows = new List<RemotePcRow>(remotePcs.Count);

        foreach (var remotePc in remotePcs)
        {
            if (rowStates.TryGetValue(GetRemotePcKey(remotePc), out var row))
            {
                orderedRows.Add(row);
            }
        }

        pcGrid.DataSource = orderedRows;







        RestoreGridPosition(selectedKey, selectedColumnName, firstDisplayedRowIndex);



        if (rebuildTrayMenu)



        {



            UpdateTrayMenu();



        }



        else



        {



            RefreshTrayMenuState();



        }



    }







    private void RestoreGridPosition(string? selectedKey, string? selectedColumnName, int firstDisplayedRowIndex)



    {



        if (pcGrid.Rows.Count == 0)



        {



            return;



        }







        var rowIndex = 0;







        if (!string.IsNullOrWhiteSpace(selectedKey))



        {



            for (var index = 0; index < pcGrid.Rows.Count; index++)



            {



                if (pcGrid.Rows[index].DataBoundItem is RemotePcRow row



                    && GetRemotePcKey(row.RemotePc).Equals(selectedKey, StringComparison.OrdinalIgnoreCase))



                {



                    rowIndex = index;



                    break;



                }



            }



        }







        var columnIndex = 0;







        if (!string.IsNullOrWhiteSpace(selectedColumnName)



            && pcGrid.Columns.Contains(selectedColumnName))



        {



            columnIndex = pcGrid.Columns[selectedColumnName].Index;



        }







        pcGrid.ClearSelection();



        pcGrid.Rows[rowIndex].Selected = true;



        pcGrid.CurrentCell = pcGrid.Rows[rowIndex].Cells[columnIndex];



        RememberGridPosition(rowIndex, columnIndex);







        if (firstDisplayedRowIndex >= 0 && firstDisplayedRowIndex < pcGrid.Rows.Count)



        {



            pcGrid.FirstDisplayedScrollingRowIndex = firstDisplayedRowIndex;



        }



    }







    private int GetFirstDisplayedRowIndex()



    {



        try



        {



            return pcGrid.Rows.Count == 0 ? 0 : pcGrid.FirstDisplayedScrollingRowIndex;



        }



        catch



        {



            return 0;



        }



    }







    private void ReorderRowStatesByRemotePcOrder()
    {
        var existingRows = rowStates.ToDictionary(item => item.Key, item => item.Value);
        rowStates.Clear();

        foreach (var remotePc in remotePcs)
        {
            var key = GetRemotePcKey(remotePc);
            rowStates[key] = existingRows.TryGetValue(key, out var existingRow)
                ? existingRow
                : RemotePcRow.Pending(remotePc);
        }
    }

    private void PcGridCellClick(object? sender, DataGridViewCellEventArgs e)



    {



        if (e.RowIndex < 0 || e.ColumnIndex < 0)



        {



            return;



        }







        RememberGridPosition(e.RowIndex, e.ColumnIndex);



    }







    private void PcGridCellDoubleClick(object? sender, DataGridViewCellEventArgs e)



    {



        if (e.RowIndex < 0 || e.ColumnIndex < 0)



        {



            return;



        }







        var columnName = pcGrid.Columns[e.ColumnIndex].Name;







        if (columnName is "StatusToggle" or "Connect" or "ExpandToggle")



        {



            return;



        }







        EditSelectedRemotePc();



    }







    private void PcGridCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)



    {



        if (e.RowIndex < 0)



        {



            return;



        }







        if (pcGrid.Columns[e.ColumnIndex].Name == "Connect"



            && pcGrid.Rows[e.RowIndex].DataBoundItem is RemotePcRow { IsMonitoring: false }



            && e.CellStyle is not null)



        {



            e.CellStyle.ForeColor = Color.FromArgb(180, 180, 180);



            e.CellStyle.BackColor = Color.White;



            e.CellStyle.SelectionForeColor = Color.FromArgb(180, 180, 180);



            e.CellStyle.SelectionBackColor = Color.White;



        }







        e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.Focus);



        e.Handled = true;



    }







    private void PcGridSelectionChanged(object? sender, EventArgs e)



    {



        if (pcGrid.CurrentCell is null)



        {



            return;



        }







        RememberGridPosition(pcGrid.CurrentCell.RowIndex, pcGrid.CurrentCell.ColumnIndex);



    }







    private void RememberGridPosition(int rowIndex, int columnIndex)



    {



        if (rowIndex < 0 || columnIndex < 0 || rowIndex >= pcGrid.Rows.Count || columnIndex >= pcGrid.Columns.Count)



        {



            return;



        }







        if (pcGrid.Rows[rowIndex].DataBoundItem is RemotePcRow row)



        {



            lastSelectedKey = GetRemotePcKey(row.RemotePc);



            lastSelectedColumnName = pcGrid.Columns[columnIndex].Name;



        }



    }







    private void NetworkAddressChanged(object? sender, EventArgs e)
    {
        ScheduleLocalPcInfoRefresh();
    }

    private void NetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        ScheduleLocalPcInfoRefresh();
    }

    private void ScheduleLocalPcInfoRefresh()
    {
        _ = RefreshLocalPcInfoAfterNetworkChangeAsync();
    }

    private async Task RefreshLocalPcInfoAfterNetworkChangeAsync()
    {
        foreach (var delay in new[] { 0, 1500, 4000 })
        {
            if (delay > 0)
            {
                await Task.Delay(delay);
            }

            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke((Action)LoadLocalPcInfo);
            }
            catch
            {
                return;
            }
        }
    }

    private void LoadLocalPcInfo()
    {
        pcNameValueLabel.Text = Environment.MachineName;

        var localIpAddress = GetLocalIpAddress(GetPreferredNetworkTargetHost());
        if (string.IsNullOrWhiteSpace(localIpAddress))
        {
            ipValueLabel.Text = "네트워크 연결 확인";
            ipValueLabel.ForeColor = Color.Firebrick;
            return;
        }

        ipValueLabel.Text = localIpAddress;
        ipValueLabel.ForeColor = Color.Black;
    }







    private string? GetPreferredNetworkTargetHost()
    {
        return remotePcs
            .Select(remotePc => remotePc.UseBridge && !string.IsNullOrWhiteSpace(remotePc.BridgeHost)
                ? remotePc.BridgeHost
                : remotePc.Host)
            .FirstOrDefault(host => !string.IsNullOrWhiteSpace(host));
    }

    private static string? GetLocalIpAddress(string? preferredTargetHost)
    {
        try
        {
            var activeInterfaces = GetEligibleNetworkInterfaces().ToArray();
            if (activeInterfaces.Length == 0)
            {
                return null;
            }

            var targetRouteIpAddress = GetRouteLocalIpAddress(preferredTargetHost);
            if (IPAddress.TryParse(targetRouteIpAddress, out var routeAddress)
                && IsAddressOnInterfaces(routeAddress, activeInterfaces))
            {
                return routeAddress.ToString();
            }

            return GetFirstIpv4Address(activeInterfaces
                    .Where(networkInterface => networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                ?? GetFirstIpv4Address(activeInterfaces
                    .Where(networkInterface => networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet));
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<NetworkInterface> GetEligibleNetworkInterfaces()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up
                && networkInterface.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet
                && !IsVirtualOrNonPhysicalInterface(networkInterface)
                && HasUsableGateway(networkInterface)
                && GetFirstIpv4Address(new[] { networkInterface }) is not null);
    }

    private static bool IsVirtualOrNonPhysicalInterface(NetworkInterface networkInterface)
    {
        var text = $"{networkInterface.Name} {networkInterface.Description}";
        return text.Contains("virtual", StringComparison.OrdinalIgnoreCase)
            || text.Contains("vmware", StringComparison.OrdinalIgnoreCase)
            || text.Contains("virtualbox", StringComparison.OrdinalIgnoreCase)
            || text.Contains("hyper-v", StringComparison.OrdinalIgnoreCase)
            || text.Contains("bluetooth", StringComparison.OrdinalIgnoreCase)
            || text.Contains("loopback", StringComparison.OrdinalIgnoreCase)
            || text.Contains("tap", StringComparison.OrdinalIgnoreCase)
            || text.Contains("tun", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasUsableGateway(NetworkInterface networkInterface)
    {
        return networkInterface.GetIPProperties().GatewayAddresses
            .Any(gateway => IsUsableIpv4Address(gateway.Address));
    }

    private static bool IsAddressOnInterfaces(IPAddress address, IEnumerable<NetworkInterface> networkInterfaces)
    {
        return networkInterfaces.Any(networkInterface => networkInterface.GetIPProperties().UnicastAddresses
            .Any(unicastAddress => unicastAddress.Address.Equals(address)));
    }

    private static string? GetRouteLocalIpAddress(string? targetHost)
    {
        if (string.IsNullOrWhiteSpace(targetHost))
        {
            return null;
        }

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(targetHost, 65530);
            if (socket.LocalEndPoint is not IPEndPoint localEndPoint || !IsUsableIpv4Address(localEndPoint.Address))
            {
                return null;
            }

            return localEndPoint.Address.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetFirstIpv4Address(IEnumerable<NetworkInterface> networkInterfaces)
    {
        foreach (var networkInterface in networkInterfaces)
        {
            foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
            {
                var address = unicastAddress.Address;
                if (IsUsableIpv4Address(address))
                {
                    return address.ToString();
                }
            }
        }

        return null;
    }

    private static bool IsUsableIpv4Address(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes.Length < 2 || bytes[0] != 169 || bytes[1] != 254;
    }







    private async Task RefreshMonitoredRowsAsync()



    {







        if (isPolling || monitoringKeys.Count == 0)



        {



            return;



        }







        isPolling = true;







        try



        {



            foreach (var key in monitoringKeys.ToArray())



            {



                if (rowStates.TryGetValue(key, out var row))



                {



                    var token = GetMonitoringToken(key);



                    var refreshedRow = await BuildRowAsync(row.RemotePc, isMonitoring: true, token);







                    if (monitoringKeys.Contains(key) && !token.IsCancellationRequested)



                    {



                        rowStates[key] = refreshedRow;



                    }



                }



            }







            BindGrid();



        }



        finally



        {



            isPolling = false;



        }



    }







    private async Task CheckAllFromTrayAsync()



    {



        await StartAllRemotePcsAsync();



    }







    private async Task StartAllRemotePcsAsync()



    {



        automaticCheckStartTimer.Stop();

        if (isTrayStatusChecking)



        {



            return;



        }







        isTrayStatusChecking = true;



        RefreshTrayMenuState();







        try



        {



            ClearMonitoringCancellationSources();



            monitoringKeys.Clear();







            foreach (var remotePc in remotePcs)



            {



                var key = GetRemotePcKey(remotePc);



                statusRequestFailureCounts.Remove(key);



                monitoringKeys.Add(key);



                StartMonitoringCancellationSource(key);



                rowStates[key] = RemotePcRow.Pending(remotePc, isMonitoring: true);



            }







            BindGrid(rebuildTrayMenu: false);



            RefreshTrayMenuState();







            foreach (var remotePc in remotePcs)



            {



                var key = GetRemotePcKey(remotePc);



                var token = GetMonitoringToken(key);



                RemotePcRow refreshedRow;



                try

                {

                    refreshedRow = await BuildRowAsync(remotePc, isMonitoring: true, token);

                }

                catch (OperationCanceledException)

                {

                    continue;

                }







                if (monitoringKeys.Contains(key) && !token.IsCancellationRequested)



                {



                    rowStates[key] = refreshedRow;



                    BindGrid(rebuildTrayMenu: false);



                    RefreshTrayMenuState();



                }



            }







            BindGrid(rebuildTrayMenu: false);



        }



        finally



        {



            isTrayStatusChecking = false;



            RefreshTrayMenuState();



        }



    }







    private async Task ConnectFromTrayAsync(RemotePcInfo remotePc)



    {



        var key = GetRemotePcKey(remotePc);







        if (!rowStates.TryGetValue(key, out var row) || !row.IsMonitoring)



        {



            MessageBox.Show(



                "전체 상태 체크를 먼저 실행해야 원격 접속할 수 있습니다.",



                "원격 접속",



                MessageBoxButtons.OK,



                MessageBoxIcon.Information);



            return;



        }







        CloseTrayMenus();



        await ConnectAsync(row);



    }







    private async Task<RemotePcRow> BuildRowAsync(RemotePcInfo remotePc, bool isMonitoring, CancellationToken cancellationToken = default)



    {



        if (remotePc.UseBridge)



        {



            try



            {



                var bridgedStatus = await apiClient.GetStatusAsync(remotePc, cancellationToken);



                var bridgedUsers = bridgedStatus.Sessions



                    .Where(session => session.IsActive)



                    .Select(session => session.ClientName)



                    .Where(clientName => !string.IsNullOrWhiteSpace(clientName))



                    .Distinct(StringComparer.OrdinalIgnoreCase)



                    .ToArray();



                var bridgedClientIps = bridgedStatus.Sessions



                    .Where(session => session.IsActive)



                    .Select(session => session.ClientAddress)



                    .Where(clientAddress => !string.IsNullOrWhiteSpace(clientAddress))



                    .Distinct(StringComparer.OrdinalIgnoreCase)



                    .ToArray();







                ResetStatusRequestFailureCount(remotePc);
                return RemotePcRow.Available(remotePc, bridgedStatus.HasActiveRdpSession, bridgedUsers, bridgedClientIps, isMonitoring);



            }



            catch



            {



                return BuildStatusRequestFailedRow(remotePc, isMonitoring);



            }



        }







        if (!await availabilityService.CanConnectAsync(remotePc, cancellationToken))



        {



            ResetStatusRequestFailureCount(remotePc);
            return RemotePcRow.Unavailable(remotePc, isMonitoring);



        }







        try



        {



            var status = await apiClient.GetStatusAsync(remotePc, cancellationToken);



            var users = status.Sessions



                .Where(session => session.IsActive)



                .Select(session => session.ClientName)



                .Where(clientName => !string.IsNullOrWhiteSpace(clientName))



                .Distinct(StringComparer.OrdinalIgnoreCase)



                .ToArray();



            var clientIps = status.Sessions



                .Where(session => session.IsActive)



                .Select(session => session.ClientAddress)



                .Where(clientAddress => !string.IsNullOrWhiteSpace(clientAddress))



                .Distinct(StringComparer.OrdinalIgnoreCase)



                .ToArray();







            ResetStatusRequestFailureCount(remotePc);
            return RemotePcRow.Available(remotePc, status.HasActiveRdpSession, users, clientIps, isMonitoring);



        }



        catch



        {



            return BuildStatusRequestFailedRow(remotePc, isMonitoring);



        }



    }









    private RemotePcRow BuildStatusRequestFailedRow(RemotePcInfo remotePc, bool isMonitoring)
    {
        var key = GetRemotePcKey(remotePc);
        statusRequestFailureCounts.TryGetValue(key, out var failureCount);
        failureCount++;
        statusRequestFailureCounts[key] = failureCount;

        if (failureCount >= 3)
        {
            return RemotePcRow.AvailableWithoutMonitor(remotePc, isMonitoring);
        }

        return rowStates.TryGetValue(key, out var existingRow)
            ? existingRow
            : RemotePcRow.Pending(remotePc, isMonitoring);
    }

    private void ResetStatusRequestFailureCount(RemotePcInfo remotePc)
    {
        statusRequestFailureCounts.Remove(GetRemotePcKey(remotePc));
    }

    private void OpenRemotePcOrderEditor()
    {
        using var dialog = new RemotePcOrderEditForm(remotePcs);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        remotePcs.Clear();
        remotePcs.AddRange(dialog.RemotePcs);
        NormalizeRemotePcList(saveIfChanged: false);
        ReorderRowStatesByRemotePcOrder();
        pcListService.Save(remotePcs);
        BindGrid();
    }

    private void AddRemotePcWithDialog()



    {



        var remotePc = new RemotePcInfo



        {



            Port = DefaultApiPort,



            RdpPort = DefaultRdpPort,



            BridgeApiPort = DefaultApiPort



        };







        using var dialog = new RemotePcEditForm(remotePc, allowDelete: false);







        if (dialog.ShowDialog(this) != DialogResult.OK)



        {



            return;



        }







        if (remotePcs.Any(remotePc => IsSameRemotePc(remotePc, dialog.RemotePc)))
        {
            MessageBox.Show(
                "이미 같은 원격 PC 정보가 등록되어 있습니다.",
                "원격 PC 정보 추가",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        remotePcs.Add(dialog.RemotePc);



        pcListService.Save(remotePcs);



        rowStates[GetRemotePcKey(dialog.RemotePc)] = RemotePcRow.Pending(dialog.RemotePc);



        BindGrid();



    }







    private void ExportRemotePcList()



    {



        using var passwordDialog = new PasswordPromptForm("원격 PC 정보 내보내기", requireConfirmation: true);







        if (passwordDialog.ShowDialog(this) != DialogResult.OK)



        {



            return;



        }







        using var dialog = new SaveFileDialog



        {



            Title = "원격 PC 정보 내보내기",



            Filter = "RDP Monitor Backup (*.rmpbak)|*.rmpbak|All files (*.*)|*.*",



            FileName = $"RemotePcList_{DateTime.Now:yyyyMMdd}.rmpbak",



            OverwritePrompt = true



        };







        if (dialog.ShowDialog(this) != DialogResult.OK)



        {



            return;



        }







        try



        {



            pcListService.ExportBackup(dialog.FileName, remotePcs, passwordDialog.Password);



            MessageBox.Show(



                "원격 PC 정보를 내보냈습니다.",



                "내보내기 완료",



                MessageBoxButtons.OK,



                MessageBoxIcon.Information);



        }



        catch (Exception exception)



        {



            MessageBox.Show(



                exception.Message,



                "내보내기 실패",



                MessageBoxButtons.OK,



                MessageBoxIcon.Error);



        }



    }







    private void ImportRemotePcList()



    {



        using var dialog = new OpenFileDialog



        {



            Title = "원격 PC 정보 가져오기",



            Filter = "RDP Monitor Backup (*.rmpbak)|*.rmpbak|All files (*.*)|*.*",



            CheckFileExists = true



        };







        if (dialog.ShowDialog(this) != DialogResult.OK)



        {



            return;



        }







        using var passwordDialog = new PasswordPromptForm("원격 PC 정보 가져오기", requireConfirmation: false);







        if (passwordDialog.ShowDialog(this) != DialogResult.OK)



        {



            return;



        }







        try



        {



            var importedRemotePcs = pcListService.ImportBackup(dialog.FileName, passwordDialog.Password);



            remotePcs.Clear();



            remotePcs.AddRange(importedRemotePcs);



            NormalizeRemotePcList(saveIfChanged: false);



            pcListService.Save(remotePcs);



            ResetRowStates();



            BindGrid();



            MessageBox.Show(



                "원격 PC 정보를 가져왔습니다.",



                "가져오기 완료",



                MessageBoxButtons.OK,



                MessageBoxIcon.Information);



        }



        catch (Exception)



        {



            MessageBox.Show(



                "비밀번호를 다시 확인해 주세요.",



                "가져오기 실패",



                MessageBoxButtons.OK,



                MessageBoxIcon.Error);



        }



    }







    private void EditSelectedRemotePc()



    {



        if (GetSelectedRow() is not { } row)



        {



            return;



        }







        var original = remotePcs.FirstOrDefault(pc => IsSameRemotePc(pc, row.RemotePc));







        if (original is null)



        {



            MessageBox.Show(



                "선택한 원격 PC 정보를 찾을 수 없습니다.",



                "원격 PC 정보 수정",



                MessageBoxButtons.OK,



                MessageBoxIcon.Warning);



            return;



        }







        using var dialog = new RemotePcEditForm(original);







        if (dialog.ShowDialog(this) != DialogResult.OK)



        {



            return;



        }







        var oldKey = GetRemotePcKey(original);







        if (dialog.IsDeleteRequested)



        {



            remotePcs.Remove(original);



            monitoringKeys.Remove(oldKey);



            RemoveMonitoringCancellationSource(oldKey);



            rowStates.Remove(oldKey);



            pcListService.Save(remotePcs);



            BindGrid();



            return;



        }







        var index = remotePcs.IndexOf(original);



        remotePcs[index] = dialog.RemotePc;



        pcListService.Save(remotePcs);







        var wasMonitoring = monitoringKeys.Remove(oldKey);



        RemoveMonitoringCancellationSource(oldKey);



        rowStates.Remove(oldKey);



        var newKey = GetRemotePcKey(dialog.RemotePc);



        rowStates[newKey] = RemotePcRow.Pending(dialog.RemotePc, wasMonitoring);







        if (wasMonitoring)



        {



            monitoringKeys.Add(newKey);



            StartMonitoringCancellationSource(newKey);



        }







        BindGrid();



    }







    private async void PcGridCellContentClick(object? sender, DataGridViewCellEventArgs e)



    {



        if (e.RowIndex < 0)



        {



            return;



        }







        if (pcGrid.Rows[e.RowIndex].DataBoundItem is not RemotePcRow row)



        {



            return;



        }







        if (pcGrid.Columns[e.ColumnIndex].Name == "ExpandToggle")



        {



            if (ignoreNextExpandClick)



            {



                ignoreNextExpandClick = false;



                return;



            }







            ToggleRemotePcDetail(row, e.RowIndex, e.ColumnIndex);



            return;



        }







        if (pcGrid.Columns[e.ColumnIndex].Name == "StatusToggle")



        {



            await ToggleStatusMonitoringAsync(row);



            return;



        }







        if (pcGrid.Columns[e.ColumnIndex].Name == "Connect")



        {



            if (!row.IsMonitoring)



            {



                MessageBox.Show(



                    "상태 체크를 시작해야 원격 접속할 수 있습니다.",



                    "원격 접속",



                    MessageBoxButtons.OK,



                    MessageBoxIcon.Information);



                return;



            }







            await ConnectAsync(row);



        }



    }







    private void ToggleRemotePcDetail(RemotePcRow row, int rowIndex, int columnIndex)



    {



        var key = GetRemotePcKey(row.RemotePc);



        if (remotePcDetailMenu.Visible && openDetailKey == key)



        {



            CloseRemotePcDetail();



            return;



        }







        CloseRemotePcDetail();



        openDetailKey = key;



        row.SetDetailOpen(true);



        RefreshExpandCell(rowIndex);



        ShowRemotePcDetail(row, rowIndex, columnIndex);



    }







    private void ShowRemotePcDetail(RemotePcRow row, int rowIndex, int columnIndex)



    {



        remotePcDetailMenu.Items.Clear();



        AddDetailMenuText("---- 원격 PC IP ----");



        AddDetailMenuText(row.RemotePc.Host);



        remotePcDetailMenu.Items.Add(new ToolStripSeparator());



        AddDetailMenuText("---- 접속자 PC ----");



        AddDetailMenuText(row.UsersText);



        remotePcDetailMenu.Items.Add(new ToolStripSeparator());



        AddDetailMenuText("---- 접속자 IP ----");



        AddDetailMenuText(row.ClientIpText);







        var cellBounds = pcGrid.GetCellDisplayRectangle(columnIndex, rowIndex, true);



        remotePcDetailMenu.Show(pcGrid, new Point(cellBounds.Left, cellBounds.Bottom));



    }







    private void CloseRemotePcDetail(bool updateGrid = true)



    {



        if (remotePcDetailMenu.Visible)



        {



            suppressDetailMenuClosed = true;



            remotePcDetailMenu.Close();



            suppressDetailMenuClosed = false;



        }







        if (updateGrid)



        {



            ClearOpenDetailState();



        }



    }







    private void RemotePcDetailMenuClosed(object? sender, ToolStripDropDownClosedEventArgs e)



    {



        if (suppressDetailMenuClosed)



        {



            return;



        }







        ignoreNextExpandClick = IsMouseOverExpandColumn();



        ClearOpenDetailState();



    }







    private bool IsMouseOverExpandColumn()



    {



        var clientPoint = pcGrid.PointToClient(Control.MousePosition);



        var hit = pcGrid.HitTest(clientPoint.X, clientPoint.Y);







        if (hit.RowIndex < 0 || hit.ColumnIndex < 0)



        {



            return false;



        }







        return pcGrid.Columns[hit.ColumnIndex].Name == "ExpandToggle";



    }







    private void ClearOpenDetailState()



    {



        if (openDetailKey is null)



        {



            return;



        }







        var detailKey = openDetailKey;

        if (rowStates.TryGetValue(detailKey, out var row))



        {



            row.SetDetailOpen(false);



        }







        var expandColumnIndex = pcGrid.Columns["ExpandToggle"].Index;

        foreach (DataGridViewRow gridRow in pcGrid.Rows)
        {
            if (gridRow.DataBoundItem is not RemotePcRow displayedRow
                || !GetRemotePcKey(displayedRow.RemotePc).Equals(detailKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            displayedRow.SetDetailOpen(false);
            pcGrid.UpdateCellValue(expandColumnIndex, gridRow.Index);
            pcGrid.InvalidateCell(expandColumnIndex, gridRow.Index);
            break;
        }

        openDetailKey = null;



        pcGrid.Refresh();



    }







    private void RefreshExpandCell(int rowIndex)



    {



        if (rowIndex < 0 || rowIndex >= pcGrid.Rows.Count)



        {



            return;



        }







        var columnIndex = pcGrid.Columns["ExpandToggle"].Index;



        pcGrid.InvalidateCell(columnIndex, rowIndex);



    }







    private void AddDetailMenuText(string text)



    {



        remotePcDetailMenu.Items.Add(new ToolStripMenuItem(text)



        {



            Enabled = false



        });



    }







    private async Task ToggleStatusMonitoringAsync(RemotePcRow row)



    {



        var key = GetRemotePcKey(row.RemotePc);







        if (monitoringKeys.Contains(key))



        {



            monitoringKeys.Remove(key);



            statusRequestFailureCounts.Remove(key);



            RemoveMonitoringCancellationSource(key);



            rowStates[key] = RemotePcRow.Pending(row.RemotePc, isMonitoring: false);



            BindGrid();



            return;



        }







        statusRequestFailureCounts.Remove(key);



        statusRequestFailureCounts.Remove(key);



        monitoringKeys.Add(key);



        StartMonitoringCancellationSource(key);



        rowStates[key] = RemotePcRow.Pending(row.RemotePc, isMonitoring: true);



        BindGrid();







        var token = GetMonitoringToken(key);



        var refreshedRow = await BuildRowAsync(row.RemotePc, isMonitoring: true, token);







        if (!monitoringKeys.Contains(key) || token.IsCancellationRequested)



        {



            return;



        }







        rowStates[key] = refreshedRow;



        BindGrid();



    }







    private void StopAllRemotePcs()

    {

        automaticCheckStartTimer.Stop();

        if (monitoringKeys.Count == 0 && monitoringCancellationSources.Count == 0)

        {
            RefreshTrayMenuState();

            return;

        }



        foreach (var remotePc in remotePcs)

        {

            var key = GetRemotePcKey(remotePc);

            if (monitoringKeys.Contains(key) || rowStates.TryGetValue(key, out var row) && row.IsMonitoring)

            {

                rowStates[key] = RemotePcRow.Pending(remotePc, isMonitoring: false);

            }

        }



        monitoringKeys.Clear();

        ClearMonitoringCancellationSources();

        isTrayStatusChecking = false;

        BindGrid(rebuildTrayMenu: false);

        RefreshTrayMenuState();

    }



    private async Task ConnectAsync(RemotePcRow row)



    {



        if (row.IsMonitoring && !row.CanConfirmOccupancy)



        {



            var confirmation = MessageBox.Show(



                "현재 연결이 안되고 있거나 접속자 상태를 확인할 수 없습니다." + Environment.NewLine +



                "접속자가 있는지 없는지 알 수 없는 상태입니다. 그래도 원격 접속을 진행할까요?",



                "원격 접속",



                MessageBoxButtons.YesNo,



                MessageBoxIcon.Warning);







            if (confirmation != DialogResult.Yes)



            {



                return;



            }



        }







        if (row.HasActiveSession)



        {



            var result = MessageBox.Show(



                "이미 접속 중인 사용자가 있습니다. 그래도 원격 접속을 진행할까요?",



                "접속자 있음",



                MessageBoxButtons.YesNo,



                MessageBoxIcon.Warning);







            if (result != DialogResult.Yes)



            {



                return;



            }



        }







        try



        {



            if (row.RemotePc.UseBridge)



            {



                var bridgeRdp = await apiClient.StartBridgeRdpAsync(row.RemotePc);



                connectionService.Connect(row.RemotePc, $"{row.RemotePc.BridgeHost}:{bridgeRdp.BridgeRdpPort}");



                return;



            }







            connectionService.Connect(row.RemotePc);



        }



        catch (Exception exception)



        {



            MessageBox.Show(



                exception.Message,



                "원격 접속 실패",



                MessageBoxButtons.OK,



                MessageBoxIcon.Error);



        }



    }







    private RemotePcRow? GetSelectedRow()



    {



        return pcGrid.CurrentRow?.DataBoundItem as RemotePcRow;



    }







    private static bool IsSameRemotePc(RemotePcInfo first, RemotePcInfo second)



    {



        return first.Host.Equals(second.Host, StringComparison.OrdinalIgnoreCase)



            && first.Port == second.Port



            && first.Name.Equals(second.Name, StringComparison.OrdinalIgnoreCase);



    }







    private static string GetRemotePcKey(RemotePcInfo remotePc)



    {



        return $"{remotePc.Name}|{remotePc.Host}|{remotePc.Port}|{remotePc.RdpPort}";



    }







    private sealed class RemotePcRow
    {
        private RemotePcRow(RemotePcInfo remotePc)
        {
            RemotePc = remotePc;
        }

        public RemotePcInfo RemotePc { get; }

        public string Name { get; private init; } = string.Empty;

        public string DescriptionSummary => RemotePc.DescriptionSummary;

        public string ExpandButtonText { get; private set; } = "+";

        public string ConnectionText { get; private init; } = string.Empty;

        public string OccupancyText { get; private init; } = string.Empty;

        public string UsersText { get; private init; } = string.Empty;

        public string ClientIpText { get; private init; } = string.Empty;

        public bool IsReachable { get; private init; }

        public bool HasActiveSession { get; private init; }

        public bool CanConfirmOccupancy { get; private init; }

        public bool IsMonitoring { get; private init; }

        public string StatusButtonText => IsMonitoring ? "종료" : "시작";

        public string ConnectButtonText => "접속";

        public void SetDetailOpen(bool isOpen)
        {
            ExpandButtonText = isOpen ? "-" : "+";
        }

        public static RemotePcRow Pending(RemotePcInfo remotePc, bool isMonitoring = false)
        {
            return new RemotePcRow(remotePc)
            {
                Name = remotePc.Name,
                ConnectionText = isMonitoring ? "확인 중" : "대기",
                OccupancyText = "-",
                UsersText = "-",
                ClientIpText = "-",
                IsReachable = false,
                CanConfirmOccupancy = false,
                IsMonitoring = isMonitoring
            };
        }

        public static RemotePcRow Available(
            RemotePcInfo remotePc,
            bool hasActiveSession,
            IReadOnlyList<string> users,
            IReadOnlyList<string> clientIps,
            bool isMonitoring)
        {
            return new RemotePcRow(remotePc)
            {
                Name = remotePc.Name,
                ConnectionText = "연결 가능",
                OccupancyText = hasActiveSession ? $"접속자 있음 ({users.Count}명)" : "접속자 없음",
                UsersText = users.Count == 0 ? "-" : string.Join(", ", users),
                ClientIpText = clientIps.Count == 0 ? "-" : string.Join(", ", clientIps),
                IsReachable = true,
                HasActiveSession = hasActiveSession,
                CanConfirmOccupancy = true,
                IsMonitoring = isMonitoring
            };
        }

        public static RemotePcRow AvailableWithoutMonitor(RemotePcInfo remotePc, bool isMonitoring)
        {
            return new RemotePcRow(remotePc)
            {
                Name = remotePc.Name,
                ConnectionText = "연결 가능",
                OccupancyText = "확인 불가",
                UsersText = "-",
                ClientIpText = "-",
                IsReachable = true,
                HasActiveSession = false,
                CanConfirmOccupancy = false,
                IsMonitoring = isMonitoring
            };
        }

        public static RemotePcRow Unavailable(RemotePcInfo remotePc, bool isMonitoring)
        {
            return new RemotePcRow(remotePc)
            {
                Name = remotePc.Name,
                ConnectionText = "연결 불가",
                OccupancyText = "-",
                UsersText = "-",
                ClientIpText = "-",
                IsReachable = false,
                CanConfirmOccupancy = false,
                IsMonitoring = isMonitoring
            };
        }
    }

}




























