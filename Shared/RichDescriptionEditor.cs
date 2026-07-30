using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace RemoteMonitor.Shared.Forms;

internal sealed class RichDescriptionEditor : UserControl
{
    private const int DetailsMaxLength = 4000;
    private static readonly float[] FontSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72];

    private readonly RichTextBox editor = new FixedFontRichTextBox();
    private readonly ToolStripComboBox fontFamilyComboBox = new();
    private readonly ToolStripComboBox fontSizeComboBox = new();
    private readonly ToolStripButton boldButton;
    private readonly ToolStripButton italicButton;
    private readonly ToolStripButton underlineButton;
    private readonly ToolStripButton strikeoutButton;
    private readonly ToolStripButton bulletButton;
    private bool isUpdatingToolbar;

    public RichDescriptionEditor(string plainText, string rtfText)
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;

        editor.Dock = DockStyle.Fill;
        editor.AutoWordSelection = false;
        editor.BorderStyle = BorderStyle.FixedSingle;
        editor.AcceptsTab = true;
        editor.DetectUrls = true;
        editor.EnableAutoDragDrop = true;
        editor.HideSelection = false;
        editor.Font = new Font("맑은 고딕", 10F);
        editor.MaxLength = DetailsMaxLength;
        editor.ScrollBars = RichTextBoxScrollBars.Both;
        editor.WordWrap = true;

        var toolStrip = new ToolStrip
        {
            Dock = DockStyle.Fill,
            GripStyle = ToolStripGripStyle.Hidden,
            LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
            Padding = new Padding(2, 1, 2, 1),
            RenderMode = ToolStripRenderMode.System
        };

        ConfigureFontFamilyComboBox();
        ConfigureFontSizeComboBox();
        toolStrip.Items.Add(CreateButton("실행 취소", "↶", (_, _) =>
        {
            if (editor.CanUndo)
            {
                editor.Undo();
            }
        }));
        toolStrip.Items.Add(CreateButton("다시 실행", "↷", (_, _) =>
        {
            if (editor.CanRedo)
            {
                editor.Redo();
            }
        }));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(fontFamilyComboBox);
        toolStrip.Items.Add(fontSizeComboBox);
        toolStrip.Items.Add(CreateButton("글자 크기 줄이기 (Ctrl+Shift+<)", "−", (_, _) => ChangeFontSize(-1)));
        toolStrip.Items.Add(CreateButton("글자 크기 늘리기 (Ctrl+Shift+>)", "+", (_, _) => ChangeFontSize(1)));
        toolStrip.Items.Add(new ToolStripSeparator());

        boldButton = CreateToggleButton("굵게", "B", FontStyle.Bold);
        italicButton = CreateToggleButton("기울임", "I", FontStyle.Italic);
        underlineButton = CreateToggleButton("밑줄", "U", FontStyle.Underline);
        strikeoutButton = CreateToggleButton("취소선", "S", FontStyle.Strikeout);
        toolStrip.Items.Add(boldButton);
        toolStrip.Items.Add(italicButton);
        toolStrip.Items.Add(underlineButton);
        toolStrip.Items.Add(strikeoutButton);
        toolStrip.Items.Add(CreateButton("글자색", "글자색", (_, _) => ChooseTextColor()));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(CreateButton("왼쪽 정렬", "L", (_, _) => editor.SelectionAlignment = HorizontalAlignment.Left));
        toolStrip.Items.Add(CreateButton("가운데 정렬", "C", (_, _) => editor.SelectionAlignment = HorizontalAlignment.Center));
        toolStrip.Items.Add(CreateButton("오른쪽 정렬", "R", (_, _) => editor.SelectionAlignment = HorizontalAlignment.Right));
        bulletButton = CreateButton("글머리표", "•", (_, _) =>
        {
            editor.SelectionBullet = !editor.SelectionBullet;
            UpdateToolbarState();
        });
        bulletButton.CheckOnClick = false;
        toolStrip.Items.Add(bulletButton);
        toolStrip.Items.Add(CreateButton("내어쓰기", "←", (_, _) =>
        {
            editor.SelectionIndent = Math.Max(0, editor.SelectionIndent - 20);
        }));
        toolStrip.Items.Add(CreateButton("들여쓰기", "→", (_, _) =>
        {
            editor.SelectionIndent += 20;
        }));
        toolStrip.Items.Add(CreateButton("서식 지우기", "서식 지우기", (_, _) => ClearSelectionFormatting()));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(toolStrip, 0, 0);
        layout.Controls.Add(editor, 0, 1);
        Controls.Add(layout);

        LoadContents(plainText, rtfText);
        editor.SelectionChanged += (_, _) => UpdateToolbarState();
        editor.KeyDown += EditorKeyDown;
        UpdateToolbarState();
    }

    public string PlainText => editor.Text.Trim();

    public string RtfText => string.IsNullOrWhiteSpace(editor.Text) ? string.Empty : editor.Rtf ?? string.Empty;

    private void ConfigureFontFamilyComboBox()
    {
        fontFamilyComboBox.AutoSize = false;
        fontFamilyComboBox.Width = 132;
        fontFamilyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        fontFamilyComboBox.ToolTipText = "글꼴";

        using var installedFonts = new InstalledFontCollection();
        foreach (var familyName in installedFonts.Families
                     .Select(fontFamily => fontFamily.Name)
                     .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
        {
            fontFamilyComboBox.Items.Add(familyName);
        }

        fontFamilyComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (isUpdatingToolbar || fontFamilyComboBox.SelectedItem is not string selectedFamilyName)
            {
                return;
            }

            ApplySelectionFont(selectedFamilyName, null, null);
        };
    }

    private void ConfigureFontSizeComboBox()
    {
        fontSizeComboBox.AutoSize = false;
        fontSizeComboBox.Width = 55;
        fontSizeComboBox.ToolTipText = "글자 크기";
        fontSizeComboBox.Items.AddRange(FontSizes.Select(size => size.ToString("0.#")).ToArray());
        fontSizeComboBox.TextChanged += (_, _) =>
        {
            if (isUpdatingToolbar
                || !float.TryParse(fontSizeComboBox.Text, out var size)
                || size is < 6 or > 96)
            {
                return;
            }

            ApplySelectionFont(null, size, null);
        };
    }

    private ToolStripButton CreateToggleButton(string toolTip, string text, FontStyle style)
    {
        var button = CreateButton(toolTip, text, (_, _) =>
        {
            var currentStyle = (editor.SelectionFont ?? editor.Font).Style;
            ApplySelectionFont(null, null, currentStyle ^ style);
            UpdateToolbarState();
        });
        button.CheckOnClick = false;
        return button;
    }

    private static ToolStripButton CreateButton(string toolTip, string text, EventHandler clickHandler)
    {
        var button = new ToolStripButton
        {
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = text,
            ToolTipText = toolTip
        };
        button.Click += clickHandler;
        return button;
    }

    private void ApplySelectionFont(string? familyName, float? size, FontStyle? style)
    {
        var current = editor.SelectionFont ?? editor.Font;
        try
        {
            using var selectedFont = string.IsNullOrWhiteSpace(familyName)
                ? new Font(current.FontFamily, size ?? current.Size, style ?? current.Style)
                : new Font(familyName, size ?? current.Size, style ?? current.Style);
            editor.SelectionFont = selectedFont;
        }
        catch (ArgumentException)
        {
            // Some installed fonts do not support every style. Keep the current selection unchanged.
        }
    }

    private void ChooseTextColor()
    {
        using var dialog = new ColorDialog
        {
            Color = editor.SelectionColor,
            FullOpen = true
        };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            editor.SelectionColor = dialog.Color;
        }
    }

    private void ChangeFontSize(float delta)
    {
        var currentSize = editor.SelectionFont?.Size ?? editor.Font.Size;
        ApplySelectionFont(null, Math.Clamp(currentSize + delta, 6F, 96F), null);
        UpdateToolbarState();
    }

    private void EditorKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (!eventArgs.Control || !eventArgs.Shift)
        {
            return;
        }

        if (eventArgs.KeyCode == Keys.Oemcomma)
        {
            ChangeFontSize(-1);
        }
        else if (eventArgs.KeyCode == Keys.OemPeriod)
        {
            ChangeFontSize(1);
        }
        else
        {
            return;
        }

        eventArgs.Handled = true;
        eventArgs.SuppressKeyPress = true;
    }

    private void ClearSelectionFormatting()
    {
        editor.SelectionFont = editor.Font;
        editor.SelectionColor = editor.ForeColor;
        editor.SelectionAlignment = HorizontalAlignment.Left;
        editor.SelectionBullet = false;
        editor.SelectionIndent = 0;
        editor.SelectionHangingIndent = 0;
        editor.SelectionRightIndent = 0;
        UpdateToolbarState();
    }

    private void LoadContents(string plainText, string rtfText)
    {
        if (!string.IsNullOrWhiteSpace(rtfText))
        {
            try
            {
                editor.Rtf = rtfText;
                return;
            }
            catch (ArgumentException)
            {
                // Fall back to the compatible plain-text value when RTF is invalid.
            }
        }

        editor.Text = plainText ?? string.Empty;
    }

    private void UpdateToolbarState()
    {
        isUpdatingToolbar = true;
        try
        {
            var selectionFont = editor.SelectionFont;
            boldButton.Checked = selectionFont?.Bold == true;
            italicButton.Checked = selectionFont?.Italic == true;
            underlineButton.Checked = selectionFont?.Underline == true;
            strikeoutButton.Checked = selectionFont?.Strikeout == true;
            bulletButton.Checked = editor.SelectionBullet;

            var familyName = selectionFont?.FontFamily.Name ?? editor.Font.FontFamily.Name;
            var familyIndex = fontFamilyComboBox.Items.IndexOf(familyName);
            fontFamilyComboBox.SelectedIndex = familyIndex;
            fontSizeComboBox.Text = (selectionFont?.Size ?? editor.Font.Size).ToString("0.#");
        }
        finally
        {
            isUpdatingToolbar = false;
        }
    }

    private sealed class FixedFontRichTextBox : RichTextBox
    {
        private const int WmUser = 0x0400;
        private const int EmSetLanguageOptions = WmUser + 120;
        private const int EmGetLanguageOptions = WmUser + 121;
        private const int ImfAutoFont = 0x0002;
        private const int ImfAutoFontSizeAdjust = 0x0010;
        private const int ImfDualFont = 0x0080;

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);

            var options = SendMessage(
                Handle,
                EmGetLanguageOptions,
                IntPtr.Zero,
                IntPtr.Zero).ToInt32();
            options &= ~(ImfAutoFont | ImfAutoFontSizeAdjust | ImfDualFont);
            SendMessage(
                Handle,
                EmSetLanguageOptions,
                IntPtr.Zero,
                new IntPtr(options));
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(
            IntPtr windowHandle,
            int message,
            IntPtr wordParameter,
            IntPtr longParameter);
    }
}
