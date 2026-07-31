using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using RemoteMonitor.Server.Bridge;
using RemoteMonitor.Server.Config;
using RemoteMonitor.Server.Logging;
using RemoteMonitor.Server.Models;
using RemoteMonitor.Server.Networking;
using RemoteMonitor.Server.Services;
using RemoteMonitor.Shared;

namespace RemoteMonitor.Server.Forms;

public sealed class MainForm : Form
{
    private readonly NotifyIcon trayIcon;
    private readonly bool startInTray;
    private HttpApiServer apiServer;
    private readonly HttpClient serviceHttpClient = new() { Timeout = TimeSpan.FromSeconds(2) };
    private readonly BridgeService bridgeService;
    private readonly FileLogger logger;
    private readonly RdpSessionService sessionService;
    private readonly StartupRegistrationService startupRegistrationService = new();
    private readonly BindingList<RdpConnectionLogEntry> connectionLogs = [];
    private readonly Dictionary<int, RdpConnectionLogEntry> activeLogsBySessionId = [];
    private readonly DataGridView connectionLogGrid = new();
    private readonly System.Windows.Forms.Timer monitorTimer = new();
    private readonly Label statusLabel;
    private readonly Label bridgeDetailsLabel = new();
    private readonly Button bridgeToggleButton = new();
    private readonly NumericUpDown statusPortInput = new();
    private readonly Button statusPortApplyButton = new();
    private BridgePcListForm? bridgePcListForm;
    private ServerOptions currentOptions;
    private int apiPort;

private bool isMonitoring;
    private bool isServiceUiMode;

    public MainForm(bool startInTray = false)
    {
        this.startInTray = startInTray;
        Text = "RDP Server";
        Icon = GetApplicationIcon();
        Width = 860;
        Height = 520;
        StartPosition = FormStartPosition.CenterScreen;

        currentOptions = ServerOptions.Default;
        apiPort = currentOptions.Port;
        logger = new FileLogger(currentOptions.LogDirectory);
        sessionService = new RdpSessionService(logger);
        bridgeService = new BridgeService(logger);
        apiServer = new HttpApiServer(currentOptions, sessionService, bridgeService, logger);

        trayIcon = CreateTrayIcon();
        statusLabel = CreateStatusLabel(currentOptions.Port);
        Controls.Add(CreateLayout(currentOptions.Port));

        monitorTimer.Interval = 1000;
        monitorTimer.Tick += async (_, _) => await MonitorSessionsAsync();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        trayIcon.Visible = true;

        try
        {
            if (await TryEnterServiceUiModeAsync())
            {
                logger.Info("Server UI started in service UI mode.");
                monitorTimer.Start();
                await MonitorSessionsAsync();
                return;
            }

            await apiServer.StartAsync();
            logger.Info("Server started in standalone UI mode.");
            monitorTimer.Start();
            await MonitorSessionsAsync();
        }
        catch (Exception exception)
        {
            if (await TryEnterServiceUiModeAsync())
            {
                logger.Info("Server UI switched to service UI mode after standalone startup failed.");
                monitorTimer.Start();
                await MonitorSessionsAsync();
                return;
            }

            logger.Error("Server startup failed.", exception);
            statusLabel.Text = $"Server startup failed: {exception.Message}";
            MessageBox.Show(
                exception.Message,
                "RDP Server startup failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
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
            if (!isServiceUiMode)
            {
                apiServer.Dispose();
            }

            serviceHttpClient.Dispose();
            bridgeService.Dispose();
            trayIcon.Dispose();
            monitorTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private Label CreateStatusLabel(int port)
    {
        return new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = $"RDP Server is running on HTTP port {port}."
        };
    }

    private Control CreateLayout(int port)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(14)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 204));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        statusLabel.Text = $"HTTP API: http://localhost:{port}    Monitoring RDP sessions every 1 second.";

        var statusPanel = CreateStatusPanel();

        connectionLogGrid.Dock = DockStyle.Fill;
        connectionLogGrid.AutoGenerateColumns = false;
        connectionLogGrid.AllowUserToAddRows = false;
        connectionLogGrid.AllowUserToDeleteRows = false;
        connectionLogGrid.ReadOnly = true;
        connectionLogGrid.RowHeadersVisible = false;
        connectionLogGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        connectionLogGrid.BackgroundColor = Color.White;
        connectionLogGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        connectionLogGrid.DataSource = connectionLogs;
        connectionLogGrid.Columns.Add(CreateTextColumn("IpAddress", "IP", 20));
        connectionLogGrid.Columns.Add(CreateTextColumn("ClientName", "접속 PC 이름", 24));
        connectionLogGrid.Columns.Add(CreateTextColumn("StartedAtText", "접속 시작 시간", 28));
        connectionLogGrid.Columns.Add(CreateTextColumn("EndedAtText", "접속 종료 시간", 28));

        layout.Controls.Add(statusPanel, 0, 0);
        layout.Controls.Add(connectionLogGrid, 0, 1);
        UpdateBridgeStatusUi();

        return layout;
    }

    private Control CreateStatusPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var statusPortPanel = CreateStatusPortPanel();

        var bridgeTogglePanel = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.None,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        var bridgeTitleLabel = new Label
        {
            AutoSize = true,
            Text = "Bridge:",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 7, 8, 0)
        };

        bridgeToggleButton.AutoSize = false;
        bridgeToggleButton.Width = 86;
        bridgeToggleButton.Height = 28;
        bridgeToggleButton.Margin = new Padding(0, 2, 0, 3);
        bridgeToggleButton.Click += (_, _) => ToggleBridgeEnabled();

        var bridgePcListButton = new Button
        {
            Text = "원격 PC 목록",
            Width = 110,
            Height = 28,
            Margin = new Padding(8, 2, 0, 3)
        };
        bridgePcListButton.Click += (_, _) => ShowBridgePcList();

        bridgeTogglePanel.Controls.Add(bridgeTitleLabel);
        bridgeTogglePanel.Controls.Add(bridgeToggleButton);
        bridgeTogglePanel.Controls.Add(bridgePcListButton);

        bridgeDetailsLabel.Dock = DockStyle.Fill;
        bridgeDetailsLabel.TextAlign = ContentAlignment.TopCenter;

        panel.Controls.Add(statusLabel, 0, 0);
        panel.Controls.Add(statusPortPanel, 0, 1);
        panel.Controls.Add(bridgeTogglePanel, 0, 2);
        panel.Controls.Add(bridgeDetailsLabel, 0, 3);
        return panel;
    }


    private Control CreateStatusPortPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.None,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var label = new Label
        {
            AutoSize = true,
            Text = "Status Port:",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 7, 8, 0)
        };

        statusPortInput.Minimum = 1;
        statusPortInput.Maximum = 65535;
        statusPortInput.Value = apiPort;
        statusPortInput.Width = 88;
        statusPortInput.Margin = new Padding(0, 3, 8, 0);

        statusPortApplyButton.Text = "적용";
        statusPortApplyButton.Width = 62;
        statusPortApplyButton.Height = 27;
        statusPortApplyButton.Margin = new Padding(0, 2, 0, 0);
        statusPortApplyButton.Click += async (_, _) => await ApplyStatusPortAsync();

        panel.Controls.Add(label);
        panel.Controls.Add(statusPortInput);
        panel.Controls.Add(statusPortApplyButton);
        return panel;
    }

    private async Task ApplyStatusPortAsync()
    {
        var newPort = (int)statusPortInput.Value;
        if (newPort == currentOptions.Port)
        {
            return;
        }

        var previousOptions = currentOptions;
        var newOptions = currentOptions.WithPort(newPort);

        if (isServiceUiMode)
        {
            var result = MessageBox.Show(
                "Status Port 변경을 적용하려면 Windows Service를 재시작해야 합니다. 지금 자동으로 재시작할까요?",
                "Status Port 변경",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (result != DialogResult.OK)
            {
                statusPortInput.Value = previousOptions.Port;
                return;
            }

            newOptions.Save();
            currentOptions = newOptions;
            await RestartServerServiceAsync(newPort);
            return;
        }

        newOptions.Save();

        try
        {
            apiServer.Dispose();
            apiServer = new HttpApiServer(newOptions, sessionService, bridgeService, logger);
            await apiServer.StartAsync();
            currentOptions = newOptions;
            apiPort = newPort;
            statusLabel.Text = $"HTTP API: http://localhost:{apiPort}    Monitoring RDP sessions every 1 second.";
            logger.Info($"Status Port changed to {apiPort}.");
        }
        catch (Exception exception)
        {
            logger.Error("Failed to change Status Port.", exception);
            previousOptions.Save();
            currentOptions = previousOptions;
            apiPort = previousOptions.Port;
            statusPortInput.Value = apiPort;
            apiServer.Dispose();
            apiServer = new HttpApiServer(previousOptions, sessionService, bridgeService, logger);
            await apiServer.StartAsync();

            MessageBox.Show(
                exception.Message,
                "Status Port 변경 실패",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
    private async Task RestartServerServiceAsync(int newPort)
    {
        try
        {
            statusPortApplyButton.Enabled = false;
            statusPortApplyButton.Text = "재시작";
            logger.Info($"Restarting Windows Service for Status Port {newPort}.");

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C sc stop RemoteMonitor.Server.Service >NUL 2>&1 & timeout /t 2 /nobreak >NUL & sc start RemoteMonitor.Server.Service",
                UseShellExecute = true,
                Verb = "runas"
            });

            if (process is null)
            {
                throw new InvalidOperationException("Windows Service 재시작을 시작할 수 없습니다.");
            }

            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Windows Service 재시작에 실패했습니다.");
            }

            apiPort = newPort;
            statusPortInput.Value = newPort;
            await Task.Delay(1500);
            await TryEnterServiceUiModeAsync();
            MessageBox.Show(
                "Windows Service를 재시작했습니다.",
                "Status Port",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Win32Exception)
        {
            MessageBox.Show(
                "Windows Service 재시작이 취소되었습니다. 수동으로 재시작하면 적용됩니다.",
                "Status Port",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            logger.Error("Failed to restart Windows Service.", exception);
            MessageBox.Show(
                exception.Message,
                "Windows Service 재시작 실패",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            statusPortApplyButton.Text = "적용";
            statusPortApplyButton.Enabled = true;
        }
    }
    private static DataGridViewTextBoxColumn CreateTextColumn(string propertyName, string headerText, float fillWeight)
    {
        return new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = headerText,
            FillWeight = fillWeight,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
    }

    private static Icon GetApplicationIcon()
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("RemoteMonitor.Server.AppIconR");
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }
    private NotifyIcon CreateTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowFromTray());
        menu.Items.Add("Refresh Status", null, async (_, _) => await RefreshStatusAsync());

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("설정", null, (_, _) => ShowSettings());

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitFromTray());

        var icon = new NotifyIcon
        {
            Icon = GetApplicationIcon(),
            Text = "RDP Server",
            ContextMenuStrip = menu
        };
        icon.MouseDoubleClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                ShowFromTray();
            }
        };
        return icon;
    }

    private void ShowSettings()
    {
        using var settingsForm = new ApplicationSettingsForm(
            new ApplicationSettingsOptions
            {
                ProgramName = "RDP Server",
                Version = $"v{typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "1.1.1"}",
                InstallPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                StartupDescription = "Windows 부팅 시 자동 실행",
                IsStartupEnabled = startupRegistrationService.IsRegistered,
                SetStartupEnabledAsync = startupRegistrationService.SetRegisteredAsync,
                ShortcutName = "Remote Monitor Server",
                ExecutablePath = Application.ExecutablePath
            })
        {
            Icon = Icon
        };

        if (Visible)
        {
            settingsForm.ShowDialog(this);
            return;
        }

        settingsForm.ShowDialog();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        trayIcon.Visible = false;
        Application.Exit();
    }

    private async Task<bool> TryEnterServiceUiModeAsync()
    {
        try
        {
            using var response = await serviceHttpClient.GetAsync($"http://127.0.0.1:{apiPort}/health");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            isServiceUiMode = true;
            statusLabel.Text = $"Service UI mode: using background service on HTTP port {apiPort}.";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<ServerStatusResponse> GetCurrentStatusAsync()
    {
        if (!isServiceUiMode)
        {
            return await sessionService.RefreshStatusAsync();
        }

        var status = await serviceHttpClient.GetFromJsonAsync<ServerStatusResponse>($"http://127.0.0.1:{apiPort}/status");
        return status ?? new ServerStatusResponse();
    }

    private async Task<BridgeStatus?> GetCurrentBridgeStatusAsync()
    {
        if (!isServiceUiMode)
        {
            return bridgeService.GetStatus();
        }

        try
        {
            return await serviceHttpClient.GetFromJsonAsync<BridgeStatus>($"http://127.0.0.1:{apiPort}/bridge/status");
        }
        catch (Exception exception)
        {
            logger.Error("Failed to refresh bridge status from service.", exception);
            return bridgeService.GetStatus();
        }
    }
    private async Task RefreshStatusAsync()
    {
        await MonitorSessionsAsync();
    }

    private async Task MonitorSessionsAsync()
    {
        if (isMonitoring)
        {
            return;
        }

        isMonitoring = true;

        try
        {
            var status = await GetCurrentStatusAsync();
            UpdateConnectionLog(status.Sessions);

            statusLabel.Text = status.HasActiveRdpSession
                ? $"Active RDP sessions: {status.ActiveRdpSessionCount}{Environment.NewLine}Last checked: {status.CheckedAt:T}"
                : $"No active RDP sessions.{Environment.NewLine}Last checked: {status.CheckedAt:T}";
            UpdateBridgeStatusUi(await GetCurrentBridgeStatusAsync());
        }
        catch (Exception exception)
        {
            logger.Error("Failed to refresh status.", exception);
            statusLabel.Text = $"Status refresh failed: {exception.Message}";
        }
        finally
        {
            isMonitoring = false;
        }
    }

    private void ToggleBridgeEnabled()
    {
        try
        {
            var currentStatus = bridgeService.GetStatus();
            bridgeService.SetEnabled(!currentStatus.Enabled);
            UpdateBridgeStatusUi();
        }
        catch (Exception exception)
        {
            logger.Error("Failed to change bridge status.", exception);
            MessageBox.Show(
                exception.Message,
                "Bridge 상태 변경 실패",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ShowBridgePcList()
    {
        if (bridgePcListForm is { IsDisposed: false })
        {
            RestoreAndActivate(bridgePcListForm);
            return;
        }

        var dialog = new BridgePcListForm(logger);
        bridgePcListForm = dialog;
        dialog.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(bridgePcListForm, dialog))
            {
                bridgePcListForm = null;
            }

            UpdateBridgeStatusUi();
        };
        dialog.Show(this);
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

    private void UpdateBridgeStatusUi(BridgeStatus? bridgeStatus = null)
    {
        bridgeStatus ??= bridgeService.GetStatus();
        bridgeToggleButton.Text = bridgeStatus.Enabled ? "enabled" : "disabled";
        bridgeToggleButton.BackColor = bridgeStatus.Enabled ? Color.FromArgb(220, 245, 220) : SystemColors.Control;
        bridgeDetailsLabel.Text = FormatBridgeDetails(bridgeStatus);
    }

    private static string FormatBridgeDetails(BridgeStatus bridgeStatus)
    {
        if (!bridgeStatus.Enabled)
        {
            return $"PCs {bridgeStatus.AllowedTargetCount}, active forwarders {bridgeStatus.ActiveForwarderCount}, " +
                $"RDP ports {bridgeStatus.RdpPortStart}-{bridgeStatus.RdpPortEnd}";
        }

        return $"PCs {bridgeStatus.AllowedTargetCount}, " +
            $"active forwarders {bridgeStatus.ActiveForwarderCount}, " +
            $"RDP ports {bridgeStatus.RdpPortStart}-{bridgeStatus.RdpPortEnd}";
    }

    private void UpdateConnectionLog(IReadOnlyList<RdpSessionInfo> sessions)
    {
        var activeRdpSessions = sessions
            .Where(session => session.IsActive && session.IsRemoteDesktop)
            .ToArray();
        var activeSessionIds = activeRdpSessions
            .Select(session => session.SessionId)
            .ToHashSet();

        foreach (var session in activeRdpSessions)
        {
            if (activeLogsBySessionId.ContainsKey(session.SessionId))
            {
                continue;
            }

            var entry = new RdpConnectionLogEntry
            {
                SessionId = session.SessionId,
                IpAddress = string.IsNullOrWhiteSpace(session.ClientAddress) ? "Unknown" : session.ClientAddress,
                ClientName = string.IsNullOrWhiteSpace(session.ClientName) ? "Unknown" : session.ClientName,
                StartedAt = DateTime.Now
            };

            activeLogsBySessionId[session.SessionId] = entry;
            connectionLogs.Insert(0, entry);
            logger.Info($"RDP connected: session={entry.SessionId}, clientPc={entry.ClientName}, ip={entry.IpAddress}, started={entry.StartedAtText}");
        }

        foreach (var sessionId in activeLogsBySessionId.Keys.ToArray())
        {
            if (activeSessionIds.Contains(sessionId))
            {
                continue;
            }

            var entry = activeLogsBySessionId[sessionId];
            entry.EndedAt = DateTime.Now;
            activeLogsBySessionId.Remove(sessionId);
            logger.Info($"RDP disconnected: session={entry.SessionId}, clientPc={entry.ClientName}, ip={entry.IpAddress}, ended={entry.EndedAtText}");
        }
    }

}





