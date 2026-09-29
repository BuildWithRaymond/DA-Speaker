using System.Runtime.InteropServices;

namespace DASpeaker;

internal sealed class MessageCueList : ListBox
{
    private int activeStep = -1;
    private Font numberFont = new("Consolas", 9);
    private Font detailFont = new("Segoe UI", 9);
    private int fontDpi = 96;
    private readonly ToolTip tip = new();
    private int hovered = -1;
    private int[] messageNumbers = [];

    public MessageCueList()
    {
        Name = "queuePreview";
        AccessibleName = "Message preview";
        AccessibleDescription = "Exact messages and pauses in speaking order. Select a message to locate it in your script.";
        Dock = DockStyle.Fill;
        BorderStyle = BorderStyle.None;
        BackColor = SpeakerTheme.Preview;
        ForeColor = SpeakerTheme.Ink;
        Font = new Font("Segoe UI", 10.5f);
        DrawMode = DrawMode.OwnerDrawVariable;
        IntegralHeight = false;
        HorizontalScrollbar = false;
        FormattingEnabled = true;
        Format += (_, e) =>
        {
            if (e.ListItem is QueueStep step)
                e.Value = step.IsMessage ? $"{step.Text} ({step.Text!.Length} of 59 characters)" : $"Pause for {step.DelayMs / 1000.0:0.###} seconds";
        };
    }

    public void SetPlan(ScriptPlan plan)
    {
        BeginUpdate();
        var number = 0;
        messageNumbers = plan.Steps.Select(step => step.IsMessage ? ++number : 0).ToArray();
        Items.Clear();
        Items.AddRange(plan.Steps.Cast<object>().ToArray());
        SelectedIndex = -1;
        activeStep = -1;
        EndUpdate();
        MeasureRows();
        Invalidate();
    }

    public void SetActiveStep(int index)
    {
        if (index == activeStep) return;
        activeStep = index;
        if (index >= 0 && index < Items.Count)
        {
            var rect = GetItemRectangle(index);
            if (rect.Top < 0 || rect.Bottom > ClientSize.Height) TopIndex = index;
        }
        Invalidate();
    }

    private int DrawingDpi => FindForm()?.DeviceDpi ?? DeviceDpi;
    private int U(int value) => (int)Math.Round(value * DrawingDpi / 96f);
    private int TextWidth => Math.Max(U(80), ClientSize.Width - U(62));

    protected override void OnMeasureItem(MeasureItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        e.ItemHeight = MeasureRow(e.Index);
    }

    private int MeasureRow(int index)
    {
        var step = (QueueStep)Items[index];
        var height = step.IsMessage
            ? TextRenderer.MeasureText(DisplayText(step.Text!), Font, new Size(TextWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + U(42)
            : U(30);
        // The native owner-drawn list box limits a single item's height to 255 pixels.
        return Math.Min(255, height);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var step = (QueueStep)Items[e.Index];
        if (fontDpi != DrawingDpi)
        {
            numberFont.Dispose();
            detailFont.Dispose();
            numberFont = new Font("Consolas", 9 * DrawingDpi / 96f);
            detailFont = new Font("Segoe UI", 9 * DrawingDpi / 96f);
            fontDpi = DrawingDpi;
        }
        var active = activeStep == e.Index;
        var selected = (e.State & DrawItemState.Selected) != 0;
        var background = active ? Color.FromArgb(49, 43, 32) : selected ? Color.FromArgb(37, 37, 36) : BackColor;
        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, e.Bounds);
        var x = e.Bounds.Left;
        var y = e.Bounds.Top;
        using var rail = new Pen(SpeakerTheme.Border, U(1));
        e.Graphics.DrawLine(rail, x + U(15), y, x + U(15), e.Bounds.Bottom);
        if (step.IsMessage)
        {
            var messageNumber = messageNumbers[e.Index];
            using var dot = new SolidBrush(active ? SpeakerTheme.Gold : SpeakerTheme.Surface);
            e.Graphics.FillEllipse(dot, x + U(3), y + U(13), U(24), U(24));
            TextRenderer.DrawText(e.Graphics, messageNumber.ToString("00"), numberFont,
                new Rectangle(x + U(2), y + U(13), U(26), U(24)), active ? SpeakerTheme.Background : SpeakerTheme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            var textBounds = new Rectangle(x + U(40), y + U(8), TextWidth, e.Bounds.Height - U(36));
            TextRenderer.DrawText(e.Graphics, DisplayText(step.Text!), Font, textBounds, SpeakerTheme.Ink,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics, $"{step.Text!.Length} / 59 characters", detailFont,
                new Rectangle(x + U(40), e.Bounds.Bottom - U(27), TextWidth, U(22)), SpeakerTheme.Muted,
                TextFormatFlags.NoPrefix);
            using var meterBase = new SolidBrush(SpeakerTheme.Border);
            using var meterFill = new SolidBrush(active ? SpeakerTheme.Gold : SpeakerTheme.GoldDim);
            var meterWidth = U(34);
            e.Graphics.FillRectangle(meterBase, e.Bounds.Right - meterWidth - U(8), e.Bounds.Bottom - U(15), meterWidth, U(2));
            e.Graphics.FillRectangle(meterFill, e.Bounds.Right - meterWidth - U(8), e.Bounds.Bottom - U(15), meterWidth * step.Text.Length / 59f, U(2));
        }
        else
        {
            using var dot = new SolidBrush(active ? SpeakerTheme.Gold : SpeakerTheme.Preview);
            e.Graphics.FillEllipse(dot, x + U(11), y + U(11), U(8), U(8));
            e.Graphics.DrawEllipse(rail, x + U(11), y + U(11), U(8), U(8));
            TextRenderer.DrawText(e.Graphics, $"{step.DelayMs / 1000.0:0.###} second pause", detailFont,
                new Rectangle(x + U(40), y, TextWidth, e.Bounds.Height), SpeakerTheme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        if (Focused && selected) e.DrawFocusRectangle();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        MeasureRows();
    }

    private void MeasureRows()
    {
        if (!IsHandleCreated) return;
        for (var i = 0; i < Items.Count; i++) SendMessageW(Handle, 0x01A0, (nint)i, (nint)MeasureRow(i)); // LB_SETITEMHEIGHT
        Invalidate();
    }

    private string DisplayText(string text)
    {
        // DrawText's word wrapping does not break a long single word. Keep the
        // actual queue text intact while allowing every character to be seen.
        return System.Text.RegularExpressions.Regex.Replace(text, @"\S+", match =>
        {
            if (TextRenderer.MeasureText(match.Value, Font).Width <= TextWidth) return match.Value;
            var output = new System.Text.StringBuilder();
            var line = "";
            foreach (var letter in match.Value)
            {
                if (line.Length > 0 && TextRenderer.MeasureText(line + letter, Font).Width > TextWidth)
                {
                    output.Append(line).Append('\n');
                    line = "";
                }
                line += letter;
            }
            return output.Append(line).ToString();
        });
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        MeasureRows();
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint SendMessageW(nint handle, uint message, nint wParam, nint lParam);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var index = IndexFromPoint(e.Location);
        if (index == hovered) return;
        hovered = index;
        tip.SetToolTip(this, index >= 0 ? $"Source line {((QueueStep)Items[index]).SourceLine}. Select to locate in script." : "");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { numberFont.Dispose(); detailFont.Dispose(); tip.Dispose(); }
        base.Dispose(disposing);
    }
}
