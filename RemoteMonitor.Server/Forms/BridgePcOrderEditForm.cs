using RemoteMonitor.Server.Bridge;

namespace RemoteMonitor.Server.Forms;

public sealed class BridgePcOrderEditForm : Form
{
    private readonly ListBox pcListBox = new();
    private readonly List<BridgeTarget> targets;
    private readonly Button moveUpButton = new();
    private readonly Button moveDownButton = new();

    public IReadOnlyList<BridgeTarget> Targets => targets;

    public BridgePcOrderEditForm(IEnumerable<BridgeTarget> sourceTargets)
    {
        targets = sourceTargets.ToList();

        Text = "원격 PC 목록 편집";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 480);
        Font = new Font("Segoe UI", 10F);

        BuildLayout();
        LoadListItems();
        UpdateMoveButtons();
    }

    private void BuildLayout()
    {
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14)
        };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));

        var title = new Label
        {
            Text = "PC 선택 후 위/아래 버튼을 클릭하여 순서 변경",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var listArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        listArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        listArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));

        pcListBox.Dock = DockStyle.Fill;
        pcListBox.IntegralHeight = false;
        pcListBox.HorizontalScrollbar = true;
        pcListBox.SelectedIndexChanged += (_, _) => UpdateMoveButtons();

        var orderButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(8, 0, 0, 0),
            Height = 80
        };
        orderButtons.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        orderButtons.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        ConfigureMoveButton(moveUpButton, "▲");
        moveUpButton.Click += (_, _) => MoveSelectedItem(-1);

        ConfigureMoveButton(moveDownButton, "▼");
        moveDownButton.Click += (_, _) => MoveSelectedItem(1);

        orderButtons.Controls.Add(moveUpButton, 0, 0);
        orderButtons.Controls.Add(moveDownButton, 0, 1);

        listArea.Controls.Add(pcListBox, 0, 0);
        listArea.Controls.Add(orderButtons, 1, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 14, 0, 0)
        };

        var okButton = CreateButton("확인");
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += (_, _) => ApplyListOrder();

        var cancelButton = CreateButton("취소");
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = okButton;
        CancelButton = cancelButton;
        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);

        container.Controls.Add(title, 0, 0);
        container.Controls.Add(listArea, 0, 1);
        container.Controls.Add(buttons, 0, 2);
        Controls.Add(container);
    }

    private static void ConfigureMoveButton(Button button, string text)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Height = 34;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Margin = new Padding(0, 0, 0, 4);
    }

    private static Button CreateButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 90,
            Height = 38,
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private void LoadListItems()
    {
        pcListBox.Items.Clear();
        foreach (var target in targets)
        {
            pcListBox.Items.Add(new TargetListItem(target));
        }

        if (pcListBox.Items.Count > 0)
        {
            pcListBox.SelectedIndex = 0;
        }
    }

    private void MoveSelectedItem(int direction)
    {
        var sourceIndex = pcListBox.SelectedIndex;
        var targetIndex = sourceIndex + direction;
        if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= pcListBox.Items.Count)
        {
            return;
        }

        var item = pcListBox.Items[sourceIndex];
        pcListBox.Items.RemoveAt(sourceIndex);
        pcListBox.Items.Insert(targetIndex, item);
        pcListBox.SelectedIndex = targetIndex;
        UpdateMoveButtons();
    }

    private void UpdateMoveButtons()
    {
        var selectedIndex = pcListBox.SelectedIndex;
        moveUpButton.Enabled = selectedIndex > 0;
        moveDownButton.Enabled = selectedIndex >= 0 && selectedIndex < pcListBox.Items.Count - 1;
    }

    private void ApplyListOrder()
    {
        targets.Clear();
        foreach (var item in pcListBox.Items)
        {
            if (item is TargetListItem targetItem)
            {
                targets.Add(targetItem.Target);
            }
        }
    }

    private sealed record TargetListItem(BridgeTarget Target)
    {
        public override string ToString()
        {
            return $"{Target.Name} ({Target.Host})";
        }
    }
}
