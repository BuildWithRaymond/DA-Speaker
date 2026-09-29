using System.Drawing.Drawing2D;

namespace DASpeaker;

internal static class SpeakerTheme
{
    public static readonly Color Background = Color.FromArgb(16, 17, 19);
    public static readonly Color Ink = Color.FromArgb(239, 234, 223);
    public static readonly Color Surface = Color.FromArgb(37, 37, 38);
    public static readonly Color Panel = Color.FromArgb(24, 25, 27);
    public static readonly Color Preview = Color.FromArgb(21, 22, 24);
    public static readonly Color Muted = Color.FromArgb(166, 165, 160);
    public static readonly Color Gold = Color.FromArgb(216, 184, 120);
    public static readonly Color Border = Color.FromArgb(57, 56, 52);
    public static readonly Color GoldDim = Color.FromArgb(91, 78, 53);

    public static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Icon LoadIcon()
    {
        using var stream = typeof(SpeakerTheme).Assembly.GetManifestResourceStream("DASpeaker.Speaker.ico")!;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    public static Button Action(string text, string name, int width = 100) => new SpeakerButton
    {
        Text = text, Name = name, AccessibleName = text, Width = width, Height = 40,
        FlatStyle = FlatStyle.Flat, BackColor = Surface, ForeColor = Ink,
        UseVisualStyleBackColor = false, Cursor = Cursors.Hand,
        Margin = new Padding(0, 0, 8, 0),
        FlatAppearance = { BorderSize = 1, BorderColor = Border, MouseOverBackColor = Color.FromArgb(49, 47, 42), MouseDownBackColor = Color.FromArgb(62, 54, 39) }
    };

    public static void Quiet(Button button)
    {
        button.BackColor = Panel;
        button.ForeColor = Muted;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Surface;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 49, 36);
    }

    public static Panel InputField(NumericUpDown number, int width = 92)
    {
        number.BorderStyle = BorderStyle.None;
        number.Dock = DockStyle.Fill;
        number.Margin = Padding.Empty;
        number.BackColor = Surface;
        number.ForeColor = Ink;
        // Retain the native spinner's input and accessibility; paint only its chrome.
        foreach (Control child in number.Controls)
        {
            if (!child.GetType().Name.Contains("UpDownButtons", StringComparison.Ordinal)) continue;
            child.Paint += (_, e) =>
            {
                e.Graphics.Clear(Surface);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var scale = (number.FindForm()?.DeviceDpi ?? number.DeviceDpi) / 96f;
                using var pen = new Pen(number.Enabled ? Gold : Muted, scale);
                var x = child.Width / 2f;
                var top = child.Height / 4f;
                var bottom = child.Height * .75f;
                e.Graphics.DrawLines(pen, [new PointF(x - 3 * scale, top + scale), new PointF(x, top - 2 * scale), new PointF(x + 3 * scale, top + scale)]);
                e.Graphics.DrawLines(pen, [new PointF(x - 3 * scale, bottom - scale), new PointF(x, bottom + 2 * scale), new PointF(x + 3 * scale, bottom - scale)]);
            };
        }
        var field = new SurfacePanel { Width = width, Height = 36, BackColor = Surface, Padding = new Padding(10, 8, 6, 5), Margin = Padding.Empty, Anchor = AnchorStyles.Left };
        field.Controls.Add(number);
        return field;
    }
}

internal sealed class SpeakerComboBox : ComboBox
{
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is not (0x000F or 0x0317 or 0x0318) || !IsHandleCreated || Width < 4 || Height < 4) return;
        using var graphics = m.Msg == 0x000F ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(m.WParam);
        var scale = (FindForm()?.DeviceDpi ?? DeviceDpi) / 96f;
        using var border = new Pen(BackColor, 3 * scale);
        graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        var arrowWidth = SystemInformation.VerticalScrollBarWidth;
        using var fill = new SolidBrush(BackColor);
        graphics.FillRectangle(fill, Width - arrowWidth - 2, 1, arrowWidth + 1, Height - 2);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var chevron = new Pen(Enabled ? SpeakerTheme.Gold : SpeakerTheme.Muted, scale);
        var x = Width - arrowWidth / 2f - 2;
        var y = Height / 2f;
        graphics.DrawLines(chevron, [new PointF(x - 3 * scale, y - scale), new PointF(x, y + 2 * scale), new PointF(x + 3 * scale, y - scale)]);
    }
}

internal sealed class SpeakerButton : Button
{
    private bool hover;
    private bool pressed;

    public SpeakerButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { pressed = false; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var scale = (FindForm()?.DeviceDpi ?? DeviceDpi) / 96f;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        var primary = BackColor == SpeakerTheme.Gold;
        var color = Enabled && pressed ? primary ? Color.FromArgb(182, 150, 87) : FlatAppearance.MouseDownBackColor : Enabled && hover ? primary ? Color.FromArgb(232, 206, 155) : FlatAppearance.MouseOverBackColor : BackColor;
        var bounds = new RectangleF(1, 1, Width - 2, Height - 2);
        using var path = SpeakerTheme.Rounded(bounds, 5 * scale);
        using var fill = new LinearGradientBrush(bounds, color, primary ? Color.FromArgb(Math.Max(0, color.R - 15), Math.Max(0, color.G - 16), Math.Max(0, color.B - 17)) : color, 90f);
        e.Graphics.FillPath(fill, path);
        if (FlatAppearance.BorderSize > 0 && !primary)
        {
            using var border = new Pen(FlatAppearance.BorderColor, scale);
            e.Graphics.DrawPath(border, path);
        }
        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(primary ? SpeakerTheme.Background : SpeakerTheme.Gold, scale);
            using var focusPath = SpeakerTheme.Rounded(new RectangleF(4 * scale, 4 * scale, Width - 8 * scale, Height - 8 * scale), 3 * scale);
            e.Graphics.DrawPath(focus, focusPath);
        }
        var ink = Enabled ? ForeColor : Color.FromArgb(112, 113, 111);
        var textBounds = ClientRectangle;
        var glyph = Name switch { "startScript" => "play", "pauseScript" => "pause", "stopScript" => "stop", _ => "" };
        if (glyph.Length > 0)
        {
            var textWidth = TextRenderer.MeasureText(Text, Font).Width;
            var x = Math.Max(10 * scale, (Width - textWidth - 20 * scale) / 2);
            var y = Height / 2f;
            using var glyphBrush = new SolidBrush(ink);
            if (glyph == "play") e.Graphics.FillPolygon(glyphBrush, [new PointF(x, y - 5 * scale), new PointF(x + 8 * scale, y), new PointF(x, y + 5 * scale)]);
            else if (glyph == "stop") e.Graphics.FillRectangle(glyphBrush, x, y - 4 * scale, 8 * scale, 8 * scale);
            else { e.Graphics.FillRectangle(glyphBrush, x, y - 5 * scale, 3 * scale, 10 * scale); e.Graphics.FillRectangle(glyphBrush, x + 5 * scale, y - 5 * scale, 3 * scale, 10 * scale); }
            textBounds.X += (int)(18 * scale);
            textBounds.Width -= (int)(18 * scale);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class SpeechMark : Control
{
    public SpeechMark()
    {
        Size = new Size(44, 44);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Width / 44f;
        e.Graphics.ScaleTransform(scale, scale);
        using var pen = new Pen(SpeakerTheme.Gold, 1.3f) { LineJoin = LineJoin.Round };
        using var rim = new Pen(SpeakerTheme.GoldDim, 1);
        e.Graphics.DrawPolygon(rim, [new(22, 1), new(42, 22), new(22, 43), new(2, 22)]);
        e.Graphics.DrawPolygon(pen, [new(12, 12), new(32, 12), new(32, 27), new(23, 27), new(17, 33), new(17, 27), new(12, 27)]);
        e.Graphics.DrawLine(pen, 17, 18, 27, 18);
        e.Graphics.DrawLine(pen, 17, 22, 24, 22);
    }
}

internal sealed class SurfacePanel : Panel
{
    public SurfacePanel() => DoubleBuffered = true;
    protected override void OnEnter(EventArgs e) { base.OnEnter(e); Invalidate(); }
    protected override void OnLeave(EventArgs e) { base.OnLeave(e); Invalidate(); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? SpeakerTheme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var diameter = 14 * (FindForm()?.DeviceDpi ?? DeviceDpi) / 96f;
        var rect = new RectangleF(.5f, .5f, Width - 1, Height - 1);
        if (rect.Width < diameter || rect.Height < diameter) return;
        using var path = SpeakerTheme.Rounded(rect, diameter / 2);
        using var fill = new SolidBrush(BackColor);
        using var pen = new Pen(ContainsFocus ? SpeakerTheme.GoldDim : SpeakerTheme.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class CeremonyHeading : Control
{
    public CeremonyHeading()
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var s = (FindForm()?.DeviceDpi ?? DeviceDpi) / 96f;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var title = new Font("Georgia", 23 * s);
        using var caption = new Font("Segoe UI", 8 * s, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, "D A R K   A G E S   /   S P E E C H   C O M P A N I O N", caption,
            new Rectangle(0, (int)(3 * s), Width, (int)(20 * s)), SpeakerTheme.Gold, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(e.Graphics, "Your words. Your pace.", title,
            new Rectangle(0, (int)(25 * s), Width, (int)(44 * s)), SpeakerTheme.Ink, TextFormatFlags.NoPadding);
        // A quiet engraved arch echoes the speech-mark seal without competing with the editor.
        if (Width < 850 * s) return;
        var cx = Width - 95 * s;
        var cy = 38 * s;
        using var pen = new Pen(SpeakerTheme.GoldDim, s);
        e.Graphics.DrawLine(pen, cx - 150 * s, cy, cx - 48 * s, cy);
        e.Graphics.DrawLine(pen, cx + 48 * s, cy, Width, cy);
        e.Graphics.DrawEllipse(pen, cx - 31 * s, cy - 31 * s, 62 * s, 62 * s);
        e.Graphics.DrawEllipse(pen, cx - 26 * s, cy - 26 * s, 52 * s, 52 * s);
        using var gold = new Pen(SpeakerTheme.Gold, s);
        e.Graphics.DrawPolygon(gold, [new PointF(cx, cy - 16 * s), new PointF(cx + 12 * s, cy), new PointF(cx, cy + 16 * s), new PointF(cx - 12 * s, cy)]);
        e.Graphics.DrawLine(gold, cx, cy - 7 * s, cx, cy + 7 * s);
    }
}

internal sealed class AccentCheckBox : CheckBox
{
    public AccentCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var scale = (FindForm()?.DeviceDpi ?? DeviceDpi) / 96f;
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(text.Width + (int)Math.Ceiling(24 * scale) + Padding.Horizontal,
            Math.Max(text.Height, (int)Math.Ceiling(17 * scale)) + Padding.Vertical);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = (FindForm()?.DeviceDpi ?? DeviceDpi) / 96f;
        var size = 13 * scale;
        var y = (Height - size) / 2;
        var rect = new RectangleF(1, y, size, size);
        using var fill = new SolidBrush(Checked && Enabled ? SpeakerTheme.Gold : SpeakerTheme.Surface);
        using var border = new Pen(Checked && Enabled ? SpeakerTheme.Gold : SpeakerTheme.Muted, scale);
        e.Graphics.FillRectangle(fill, rect);
        e.Graphics.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
        if (Checked)
        {
            using var tick = new Pen(Enabled ? SpeakerTheme.Background : SpeakerTheme.Muted, 1.8f * scale);
            e.Graphics.DrawLines(tick, new PointF[] { new(3 * scale, y + 7 * scale), new(6 * scale, y + 10 * scale), new(11 * scale, y + 4 * scale) });
        }
        var text = new Rectangle((int)(20 * scale), 0, Width - (int)(20 * scale), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, text, Enabled ? ForeColor : SpeakerTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, text, SpeakerTheme.Gold, BackColor);
    }
}

internal sealed class PlaybackTrack : Control
{
    private float value;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public float Value { get => value; set { this.value = Math.Clamp(value, 0, 1); Invalidate(); } }
    public PlaybackTrack()
    {
        Height = 3;
        TabStop = false;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(SpeakerTheme.Surface);
        using var brush = new SolidBrush(SpeakerTheme.Gold);
        e.Graphics.FillRectangle(brush, 0, 0, Width * value, Height);
    }
}
