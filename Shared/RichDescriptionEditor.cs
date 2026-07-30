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
