using System.Runtime.InteropServices;

namespace RemoteMonitor.Shared.Forms;

internal sealed class RichDescriptionEditor : UserControl
{
    private const int DetailsMaxLength = 4000;

    private readonly RichTextBox editor = new FixedFontRichTextBox();

    public RichDescriptionEditor(string plainText, string rtfText)
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;

        editor.Dock = DockStyle.Fill;
        editor.AutoWordSelection = false;
        editor.BorderStyle = BorderStyle.FixedSingle;
        editor.AcceptsTab = true;
        editor.DetectUrls = true;
        editor.EnableAutoDragDrop = false;
        editor.HideSelection = false;
        editor.Font = new Font("맑은 고딕", 10F);
        editor.MaxLength = DetailsMaxLength;
        editor.ScrollBars = RichTextBoxScrollBars.Both;
        editor.WordWrap = true;

        Controls.Add(editor);
        LoadContents(plainText, rtfText);
    }

    public string PlainText => editor.Text.Trim();

    public string RtfText => string.IsNullOrWhiteSpace(editor.Text)
        ? string.Empty
        : editor.Rtf ?? string.Empty;

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

    private sealed class FixedFontRichTextBox : RichTextBox
    {
        private const int WmUser = 0x0400;
        private const int EmSetLanguageOptions = WmUser + 120;
        private const int EmGetLanguageOptions = WmUser + 121;
        private const int ImfAutoFont = 0x0002;
        private const int ImfAutoFontSizeAdjust = 0x0010;
        private const int ImfDualFont = 0x0080;

        private bool isSelectingFromLineEnd;
        private int selectionAnchor;

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

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Left
                && TryGetTrailingLineEnd(eventArgs.Location, out var lineEnd))
            {
                Focus();
                selectionAnchor = lineEnd;
                Select(selectionAnchor, 0);
                isSelectingFromLineEnd = true;
                Capture = true;
                return;
            }

            isSelectingFromLineEnd = false;
            base.OnMouseDown(eventArgs);
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            if (!isSelectingFromLineEnd)
            {
                base.OnMouseMove(eventArgs);
                return;
            }

            if ((eventArgs.Button & MouseButtons.Left) == 0)
            {
                FinishLineEndSelection();
                return;
            }

            var currentIndex = GetCaretIndexFromPosition(eventArgs.Location);
            Select(
                Math.Min(selectionAnchor, currentIndex),
                Math.Abs(selectionAnchor - currentIndex));
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            if (isSelectingFromLineEnd && eventArgs.Button == MouseButtons.Left)
            {
                FinishLineEndSelection();
                return;
            }

            base.OnMouseUp(eventArgs);
        }

        protected override void OnMouseCaptureChanged(EventArgs eventArgs)
        {
            if (!Capture)
            {
                isSelectingFromLineEnd = false;
            }

            base.OnMouseCaptureChanged(eventArgs);
        }

        private bool TryGetTrailingLineEnd(Point point, out int lineEnd)
        {
            lineEnd = GetLineEndFromPosition(point);
            var endPosition = GetPositionFromCharIndex(lineEnd);
            return IsSameVisualLine(point, endPosition)
                && point.X >= endPosition.X;
        }

        private int GetCaretIndexFromPosition(Point point)
        {
            var lineEnd = GetLineEndFromPosition(point);
            var endPosition = GetPositionFromCharIndex(lineEnd);
            if (IsSameVisualLine(point, endPosition)
                && point.X >= endPosition.X)
            {
                return lineEnd;
            }

            var characterIndex = Math.Clamp(
                GetCharIndexFromPosition(point),
                0,
                TextLength);
            if (characterIndex >= lineEnd)
            {
                return lineEnd;
            }

            var characterStart = GetPositionFromCharIndex(characterIndex);
            var nextCharacterStart = GetPositionFromCharIndex(characterIndex + 1);
            if (characterStart.Y != nextCharacterStart.Y)
            {
                return point.X >= characterStart.X
                    ? characterIndex + 1
                    : characterIndex;
            }

            var characterMidpoint = characterStart.X
                + ((nextCharacterStart.X - characterStart.X) / 2);
            return point.X >= characterMidpoint
                ? characterIndex + 1
                : characterIndex;
        }

        private bool IsSameVisualLine(Point point, Point characterPosition)
        {
            return Math.Abs(point.Y - characterPosition.Y) <= Font.Height;
        }

        private int GetLineEndFromPosition(Point point)
        {
            var characterIndex = Math.Clamp(
                GetCharIndexFromPosition(point),
                0,
                TextLength);
            var lineIndex = GetLineFromCharIndex(characterIndex);
            var lineStart = GetFirstCharIndexFromLine(lineIndex);
            if (lineStart < 0 || lineIndex < 0 || lineIndex >= Lines.Length)
            {
                return TextLength;
            }

            return Math.Min(TextLength, lineStart + Lines[lineIndex].Length);
        }

        private void FinishLineEndSelection()
        {
            isSelectingFromLineEnd = false;
            Capture = false;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(
            IntPtr windowHandle,
            int message,
            IntPtr wordParameter,
            IntPtr longParameter);
    }
}
