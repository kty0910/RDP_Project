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
    private readonly Dictionary<string, BridgePcEditForm> targetEditForms =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BridgePcDescriptionForm> targetDescriptionForms =
        new(StringComparer.OrdinalIgnoreCase);
    private BridgePcEditForm? addTargetForm;

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
            TextAlign = ContentAlignment.MiddleCenter
        };
        closeButton.Click += (_, _) => Close();
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
        if (addTargetForm is { IsDisposed: false })
        {
            RestoreAndActivate(addTargetForm);
            return;
        }

        var dialog = new BridgePcEditForm(new BridgeTarget(), allowDelete: false);
        addTargetForm = dialog;
        dialog.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(addTargetForm, dialog))
            {
                addTargetForm = null;
            }

            if (dialog.DialogResult != DialogResult.OK)
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
        };
        dialog.Show(this);
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
        var editorKey = GetTargetKey(original);
        if (targetEditForms.TryGetValue(editorKey, out var openEditForm)
            && !openEditForm.IsDisposed)
        {
            MessageBox.Show(
                "원격 PC 수정 창이 열려 있습니다.\n수정 창의 상세 버튼을 눌러 부가설명을 열어 주세요.",
                "원격 PC 설명",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            RestoreAndActivate(openEditForm);
            return;
        }

        targetEditForms.Remove(editorKey);
        if (TryActivateTargetDescription(editorKey))
        {
            return;
        }

        var dialog = new BridgePcDescriptionForm(
            original.Name,
            original.DescriptionSummary,
            original.DescriptionDetails,
            original.DescriptionDetailsRtf);
        RegisterTargetDescription(editorKey, dialog);
        dialog.FormClosed += (_, _) =>
        {
            if (dialog.DialogResult != DialogResult.OK)
            {
                return;
            }

            if (targetEditForms.TryGetValue(editorKey, out var editForm)
                && !editForm.IsDisposed)
            {
                editForm.UpdateDescriptionDraft(
                    dialog.DescriptionSummary,
                    dialog.DescriptionDetails,
                    dialog.DescriptionDetailsRtf);
                return;
            }

            var currentIndex = FindTargetIndex(editorKey);
            if (currentIndex < 0)
            {
                ShowTargetChangedWarning();
                return;
            }

            var updated = new BridgeTarget
            {
                Name = original.Name,
                Host = original.Host,
                DescriptionSummary = dialog.DescriptionSummary,
                DescriptionDetails = dialog.DescriptionDetails,
                DescriptionDetailsRtf = dialog.DescriptionDetailsRtf,
                ApiPort = original.ApiPort,
                RdpPort = original.RdpPort
            };
            targets[currentIndex] = updated;

            SaveTargets();
        };
        dialog.Show(this);
    }

    private void EditTarget(int index)
    {
        var original = targets[index];
        var editorKey = GetTargetKey(original);
        if (TryActivateTargetEdit(editorKey))
        {
            return;
        }

        var dialog = new BridgePcEditForm(
            original,
            allowDelete: true,
            descriptionFormProvider: () => GetOpenTargetDescription(editorKey),
            descriptionFormOpened: form => RegisterTargetDescription(editorKey, form));
        targetEditForms[editorKey] = dialog;
        dialog.FormClosed += (_, _) =>
        {
            if (targetEditForms.TryGetValue(editorKey, out var registered)
                && ReferenceEquals(registered, dialog))
            {
                targetEditForms.Remove(editorKey);
            }

            if (dialog.DialogResult != DialogResult.OK)
            {
                return;
            }

            var currentIndex = FindTargetIndex(editorKey);
            if (currentIndex < 0)
            {
                ShowTargetChangedWarning();
                return;
            }

            if (dialog.IsDeleteRequested)
            {
                targets.RemoveAt(currentIndex);
                SaveTargets();
                return;
            }

            if (ContainsDuplicate(dialog.Target, currentIndex))
            {
                ShowDuplicateWarning();
                return;
            }

            targets[currentIndex] = dialog.Target;
            SaveTargets();
        };
        dialog.Show(this);
    }

    private bool TryActivateTargetEdit(string editorKey)
    {
        if (!targetEditForms.TryGetValue(editorKey, out var form)
            || form.IsDisposed)
        {
            targetEditForms.Remove(editorKey);
            return false;
        }

        RestoreAndActivate(form);
        return true;
    }

    private bool TryActivateTargetDescription(string editorKey)
    {
        if (GetOpenTargetDescription(editorKey) is not { } form)
        {
            return false;
        }

        RestoreAndActivate(form);
        return true;
    }

    private BridgePcDescriptionForm? GetOpenTargetDescription(string editorKey)
    {
        if (!targetDescriptionForms.TryGetValue(editorKey, out var form)
            || form.IsDisposed)
        {
            targetDescriptionForms.Remove(editorKey);
            return null;
        }

        return form;
    }

    private void RegisterTargetDescription(
        string editorKey,
        BridgePcDescriptionForm form)
    {
        targetDescriptionForms[editorKey] = form;
        form.FormClosed += (_, _) =>
        {
            if (targetDescriptionForms.TryGetValue(editorKey, out var registered)
                && ReferenceEquals(registered, form))
            {
                targetDescriptionForms.Remove(editorKey);
            }
        };
    }

    private int FindTargetIndex(string editorKey)
    {
        for (var index = 0; index < targets.Count; index++)
        {
            if (GetTargetKey(targets[index]).Equals(
                    editorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetTargetKey(BridgeTarget target)
    {
        return $"{target.Name}|{target.Host}|{target.ApiPort}|{target.RdpPort}";
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

    private static void ShowTargetChangedWarning()
    {
        MessageBox.Show(
            "창이 열린 동안 원격 PC 목록이 변경되어 내용을 적용할 수 없습니다.\n목록에서 PC를 다시 선택해 주세요.",
            "원격 PC 정보 변경",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
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
