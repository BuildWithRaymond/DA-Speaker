using System.Drawing.Drawing2D;

namespace DASpeaker;

internal static class SpeakerTheme
{
    public static readonly Color Background = Color.FromArgb(12, 12, 14);
    public static readonly Color Ink = Color.FromArgb(233, 233, 237);
    public static readonly Color Surface = Color.FromArgb(29, 29, 33);
    public static readonly Color Panel = Color.FromArgb(19, 19, 22);
    public static readonly Color Preview = Color.FromArgb(22, 22, 25);
    public static readonly Color Muted = Color.FromArgb(153, 162, 177);
    public static readonly Color Gold = Color.FromArgb(213, 167, 70);
    public static readonly Color Border = Color.FromArgb(43, 44, 49);
    public static readonly Color Green = Color.FromArgb(104, 199, 143);

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
        FlatAppearance = { BorderSize = 1, BorderColor = Border, MouseOverBackColor = Color.FromArgb(42, 42, 47), MouseDownBackColor = Color.FromArgb(55, 49, 36) }
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
        var field = new SurfacePanel { Width = width, Height = 34, BackColor = Surface, Padding = new Padding(8, 6, 5, 4), Margin = Padding.Empty, Anchor = AnchorStyles.Left };
        field.Controls.Add(number);
        return field;
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
        var color = Enabled && pressed ? primary ? Color.FromArgb(185, 139, 48) : FlatAppearance.MouseDownBackColor : Enabled && hover ? primary ? Color.FromArgb(234, 187, 88) : FlatAppearance.MouseOverBackColor : BackColor;
        var bounds = new RectangleF(1, 1, Width - 2, Height - 2);
        var diameter = 12 * scale;
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var fill = new SolidBrush(color);
        e.Graphics.FillPath(fill, path);
        if (FlatAppearance.BorderSize > 0 && !primary)
        {
            using var border = new Pen(FlatAppearance.BorderColor, scale);
            e.Graphics.DrawPath(border, path);
        }
        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(SpeakerTheme.Gold, 2 * scale);
            e.Graphics.DrawPath(focus, path);
        }
        var ink = Enabled ? ForeColor : SpeakerTheme.Muted;
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink,
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
        using var pen = new Pen(SpeakerTheme.Gold, 1.7f) { LineJoin = LineJoin.Round };
        e.Graphics.DrawPolygon(pen, [new(5, 7), new(37, 7), new(37, 29), new(19, 29), new(10, 37), new(10, 29), new(5, 29)]);
        e.Graphics.DrawLine(pen, 13, 15, 29, 15);
        e.Graphics.DrawLine(pen, 13, 21, 24, 21);
    }
}

internal sealed class SurfacePanel : Panel
{
    public SurfacePanel() => DoubleBuffered = true;
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? SpeakerTheme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var diameter = 16 * (FindForm()?.DeviceDpi ?? DeviceDpi) / 96f;
        var rect = new RectangleF(.5f, .5f, Width - 1, Height - 1);
        if (rect.Width < diameter || rect.Height < diameter) return;
        using var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var fill = new SolidBrush(BackColor);
        using var pen = new Pen(SpeakerTheme.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class AccentCheckBox : CheckBox
{
    public AccentCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
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
