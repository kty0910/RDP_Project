using System.ComponentModel;
using RemoteMonitor.Server.Bridge;
using RemoteMonitor.Server.Logging;

namespace RemoteMonitor.Server.Forms;

public sealed class BridgePcListForm : Form
{
    private const int DescriptionButtonWidth = 38;

    private readonly FileLogger logger;
    private readonly BindingList<BridgeTarget> targets;
    private readonly DataGridView grid = new();

    public BridgePcListForm(FileLogger logger)
    {
        this.logger = logger;
        targets = new BindingList<BridgeTarget>(BridgeOptions.Load(logger).AllowedTargets.ToList());

        Text = "원격 PC 목록";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 390);

        Controls.Add(CreateLayout());
    }

    private Control CreateLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        header.Controls.Add(new Label
        {
            Text = "등록된 원격 PC",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font, FontStyle.Bold)
        }, 0, 0);

        var editOrderButton = new Button
        {
            Text = "목록 편집",
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 2, 4, 4)
        };
        editOrderButton.Click += (_, _) => EditTargetOrder();
        header.Controls.Add(editOrderButton, 1, 0);

        var addButton = new Button
        {
            Text = "PC 추가",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 4)
        };
        addButton.Click += (_, _) => AddTarget();
        header.Controls.Add(addButton, 2, 0);

        ConfigureGrid();

        var closePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 4)
        };
        var closeButton = new Button
        {
            Text = "닫기",
            Width = 92,
            Height = 36,
            Margin = new Padding(3),
            TextAlign = ContentAlignment.MiddleCenter,
            DialogResult = DialogResult.OK
        };
        closePanel.Controls.Add(closeButton);
        AcceptButton = closeButton;

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(closePanel, 0, 2);
        return layout;
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.BackgroundColor = Color.White;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DataSource = targets;
        grid.Columns.Add(CreateColumn(nameof(BridgeTarget.Name), "PC 이름", 20));
        grid.Columns.Add(CreateColumn(nameof(BridgeTarget.DescriptionSummary), "부가 설명", 30));
        grid.Columns.Add(CreateColumn(nameof(BridgeTarget.Host), "IP", 25));
        grid.Columns.Add(CreateColumn(nameof(BridgeTarget.ApiPort), "Status Port", 13));
        grid.Columns.Add(CreateColumn(nameof(BridgeTarget.RdpPort), "RDP Port", 12));
        grid.CellPainting += GridCellPainting;
        grid.CellMouseClick += GridCellMouseClick;
        grid.CellMouseDoubleClick += GridCellMouseDoubleClick;
    }

    private void GridCellMouseClick(object? sender, DataGridViewCellMouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left
            || eventArgs.RowIndex < 0
            || eventArgs.ColumnIndex < 0
            || !IsDescriptionColumn(eventArgs.ColumnIndex)
            || !IsDescriptionButtonHit(eventArgs))
        {
            return;
        }

        EditTargetDescription(eventArgs.RowIndex);
    }

    private void GridCellMouseDoubleClick(object? sender, DataGridViewCellMouseEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 || eventArgs.ColumnIndex < 0)
        {
            return;
        }

        if (IsDescriptionColumn(eventArgs.ColumnIndex) && IsDescriptionButtonHit(eventArgs))
        {
            return;
        }

        EditTarget(eventArgs.RowIndex);
    }

    private void GridCellPainting(object? sender, DataGridViewCellPaintingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0
            || eventArgs.ColumnIndex < 0
            || !IsDescriptionColumn(eventArgs.ColumnIndex)
            || eventArgs.Graphics is null
            || eventArgs.CellStyle is null)
        {
            return;
        }

        var isSelected = (eventArgs.State & DataGridViewElementStates.Selected) != 0;
        eventArgs.PaintBackground(eventArgs.CellBounds, isSelected);
        eventArgs.Paint(eventArgs.CellBounds, DataGridViewPaintParts.Border);

        var buttonBounds = GetDescriptionButtonBounds(eventArgs.CellBounds);
        var textBounds = new Rectangle(
            eventArgs.CellBounds.Left + 6,
            eventArgs.CellBounds.Top + 1,
            Math.Max(0, eventArgs.CellBounds.Width - DescriptionButtonWidth - 12),
            Math.Max(0, eventArgs.CellBounds.Height - 2));
        var textColor = isSelected
            ? eventArgs.CellStyle.SelectionForeColor
            : eventArgs.CellStyle.ForeColor;

        TextRenderer.DrawText(
            eventArgs.Graphics,
            Convert.ToString(eventArgs.FormattedValue) ?? string.Empty,
            eventArgs.CellStyle.Font ?? grid.Font,
            textBounds,
            textColor,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.PreserveGraphicsClipping);

        var centerX = buttonBounds.Left + (buttonBounds.Width / 2);
        var centerY = buttonBounds.Top + (buttonBounds.Height / 2);
        using var plusPen = new Pen(Color.Black, 2F);
        eventArgs.Graphics.DrawLine(plusPen, centerX - 6, centerY, centerX + 6, centerY);
        eventArgs.Graphics.DrawLine(plusPen, centerX, centerY - 6, centerX, centerY + 6);

        eventArgs.Handled = true;
    }

    private bool IsDescriptionColumn(int columnIndex)
    {
        return grid.Columns[columnIndex].DataPropertyName == nameof(BridgeTarget.DescriptionSummary);
    }

    private bool IsDescriptionButtonHit(DataGridViewCellMouseEventArgs eventArgs)
    {
        var columnWidth = grid.Columns[eventArgs.ColumnIndex].Width;
        return eventArgs.X >= Math.Max(0, columnWidth - DescriptionButtonWidth);
    }

    private static Rectangle GetDescriptionButtonBounds(Rectangle cellBounds)
    {
        return new Rectangle(
            cellBounds.Right - DescriptionButtonWidth,
            cellBounds.Top,
            DescriptionButtonWidth,
            cellBounds.Height);
    }

    private void AddTarget()
    {
        using var dialog = new BridgePcEditForm(new BridgeTarget(), allowDelete: false);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (ContainsDuplicate(dialog.Target, -1))
        {
            ShowDuplicateWarning();
            return;
        }

        targets.Add(dialog.Target);
        SaveTargets();
    }

    private void EditTargetOrder()
    {
        using var dialog = new BridgePcOrderEditForm(targets);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        targets.RaiseListChangedEvents = false;
        targets.Clear();
        foreach (var target in dialog.Targets)
        {
            targets.Add(target);
        }

        targets.RaiseListChangedEvents = true;
        targets.ResetBindings();
        SaveTargets();
    }

    private void EditTargetDescription(int index)
    {
        var original = targets[index];
        using var dialog = new BridgePcDescriptionForm(
            original.Name,
            original.DescriptionSummary,
            original.DescriptionDetails);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        targets[index] = new BridgeTarget
        {
            Name = original.Name,
            Host = original.Host,
            DescriptionSummary = dialog.DescriptionSummary,
            DescriptionDetails = dialog.DescriptionDetails,
            ApiPort = original.ApiPort,
            RdpPort = original.RdpPort
        };
        SaveTargets();
    }

    private void EditTarget(int index)
    {
        var original = targets[index];
        using var dialog = new BridgePcEditForm(original, allowDelete: true);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (dialog.IsDeleteRequested)
        {
            targets.RemoveAt(index);
            SaveTargets();
            return;
        }

        if (ContainsDuplicate(dialog.Target, index))
        {
            ShowDuplicateWarning();
            return;
        }

        targets[index] = dialog.Target;
        SaveTargets();
    }

    private bool ContainsDuplicate(BridgeTarget candidate, int excludedIndex)
    {
        return targets
            .Where((_, index) => index != excludedIndex)
            .Any(target => target.Name.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase)
                || (target.Host.Equals(candidate.Host, StringComparison.OrdinalIgnoreCase)
                    && target.ApiPort == candidate.ApiPort
                    && target.RdpPort == candidate.RdpPort));
    }

    private void SaveTargets()
    {
        var options = BridgeOptions.Load(logger).WithTargets(targets.ToArray());
        BridgeOptions.Save(options);
        logger.Info($"Bridge PC list updated: count={targets.Count}.");
    }

    private static void ShowDuplicateWarning()
    {
        MessageBox.Show(
            "동일한 PC 이름 또는 연결 정보가 이미 등록되어 있습니다.",
            "원격 PC 목록",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static DataGridViewTextBoxColumn CreateColumn(string propertyName, string headerText, float fillWeight)
    {
        return new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = headerText,
            FillWeight = fillWeight,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
    }
}
