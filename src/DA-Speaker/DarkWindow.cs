using System.Runtime.InteropServices;

namespace DASpeaker;

internal class DarkWindow : Form
{
    protected Panel Content { get; } = new() { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = SpeakerTheme.Background };
    protected FlowLayoutPanel CaptionActions { get; } = new() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty };
    private readonly bool resizeWindow;

    protected DarkWindow(bool resizable = true)
    {
        resizeWindow = resizable;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = SpeakerTheme.Background;
        Padding = new Padding(1);
        DoubleBuffered = true;
        var caption = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 3, RowCount = 1, BackColor = SpeakerTheme.Background, Padding = new Padding(12, 0, 0, 1), Margin = Padding.Empty };
        caption.ColumnStyles.Add(new(SizeType.Absolute, 32));
        caption.ColumnStyles.Add(new(SizeType.Percent, 100));
        caption.ColumnStyles.Add(new(SizeType.AutoSize));
        caption.RowStyles.Add(new(SizeType.Percent, 100));
        var mark = new SpeechMark { Width = 26, Height = 26, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        var title = new Label { Text = Text, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Georgia", 12, FontStyle.Bold), ForeColor = SpeakerTheme.Ink, Margin = Padding.Empty };
        TextChanged += (_, _) => title.Text = Text;
        caption.Controls.Add(mark, 0, 0);
        caption.Controls.Add(title, 1, 0);
        caption.Controls.Add(CaptionActions, 2, 0);
        var close = CaptionButton("×", "closeWindow", "Close");
        close.FlatAppearance.MouseOverBackColor = Color.FromArgb(117, 40, 49);
        close.Click += (_, _) => Close();
        CaptionActions.Controls.Add(close);
        if (resizable)
        {
            var maximize = CaptionButton("□", "maximizeWindow", "Maximize or restore");
            maximize.Click += (_, _) => ToggleMaximize();
            var minimize = CaptionButton("−", "minimizeWindow", "Minimize");
            minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
            CaptionActions.Controls.AddRange([maximize, minimize]);
        }
        foreach (Control control in new Control[] { caption, title, mark })
        {
            control.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessageW(Handle, 0x00A1, 2, 0); // Native caption dragging and snap.
            };
            control.DoubleClick += (_, _) => { if (resizeWindow) ToggleMaximize(); };
        }
        caption.Paint += (_, e) =>
        {
            using var pen = new Pen(SpeakerTheme.Border);
            e.Graphics.DrawLine(pen, 0, caption.Height - 1, caption.Width, caption.Height - 1);
        };
        Controls.Add(Content);
        Controls.Add(caption);
    }

    private static Button CaptionButton(string text, string name, string accessibleName)
    {
        var button = SpeakerTheme.Action(text, name, 42);
        button.Height = 40;
        button.Font = new Font("Segoe UI", 12);
        button.BackColor = SpeakerTheme.Background;
        button.ForeColor = SpeakerTheme.Muted;
        button.FlatAppearance.BorderSize = 0;
        button.Margin = Padding.Empty;
        button.AccessibleName = accessibleName;
        button.TabStop = false;
        return button;
    }

    private void ToggleMaximize()
    {
        MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var value = base.CreateParams;
            value.Style |= 0x00080000; // System menu, including Alt+F4.
            if (resizeWindow) value.Style |= 0x00040000 | 0x00020000 | 0x00010000;
            return value;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x02E0)
        {
            var oldDpi = DeviceDpi;
            var fonts = ExplicitFonts(this).Select(control => (Control: control, Font: control.Font)).ToArray();
            base.WndProc(ref m);
            if (oldDpi != DeviceDpi)
            {
                SuspendLayout();
                foreach (var entry in fonts)
                    entry.Control.Font = new Font(entry.Font.FontFamily, entry.Font.SizeInPoints * DeviceDpi / oldDpi, entry.Font.Style);
                ResumeLayout(true);
                OnScaleAdjusted();
            }
            return;
        }
        if (m.Msg == 0x0083 && m.WParam != 0) { m.Result = 0; return; } // Custom non-client frame.
        if (m.Msg == 0x0084 && resizeWindow && WindowState == FormWindowState.Normal)
        {
            var packed = (long)m.LParam;
            var point = PointToClient(new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
            var edge = Math.Max(5, 6 * DeviceDpi / 96);
            var left = point.X < edge;
            var right = point.X >= ClientSize.Width - edge;
            var top = point.Y < edge;
            var bottom = point.Y >= ClientSize.Height - edge;
            var hit = top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 0;
            if (hit != 0) { m.Result = hit; return; }
        }
        base.WndProc(ref m);
    }

    private static IEnumerable<Control> ExplicitFonts(Control root)
    {
        if (System.ComponentModel.TypeDescriptor.GetProperties(root)[nameof(Font)]!.ShouldSerializeValue(root)) yield return root;
        foreach (Control child in root.Controls)
            foreach (var item in ExplicitFonts(child)) yield return item;
    }

    protected virtual void OnScaleAdjusted() { }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(SpeakerTheme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint SendMessageW(nint window, uint message, nint wParam, nint lParam);
}
