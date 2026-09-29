namespace DASpeaker;

internal sealed class SpeakerSettingsDialog : DarkWindow
{
    private readonly CheckBox keys;
    private readonly NumericUpDown gap;
    private readonly NumericUpDown chat;
    private readonly NumericUpDown submit;
    public bool HotkeysEnabled => keys.Checked;
    public int KeyGapMs => (int)gap.Value;
    public int ChatOpenMs => (int)chat.Value;
    public int SubmitMs => (int)submit.Value;

    public SpeakerSettingsDialog(AppSettings settings, int chatOpenMs, int submitMs, string log, bool busy, Action saveLog) : base(false)
    {
        Text = "DA Speaker · Settings";
        Icon = SpeakerTheme.LoadIcon();
        Font = new Font("Segoe UI", 10);
        BackColor = SpeakerTheme.Background;
        ForeColor = SpeakerTheme.Ink;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(580, 660);
        MinimumSize = Size;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        Content.Padding = new Padding(24);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 70));
        root.RowStyles.Add(new(SizeType.Absolute, 54));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 36));
        root.RowStyles.Add(new(SizeType.Absolute, 64));
        Content.Controls.Add(root);
        var heading = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        heading.Controls.Add(new Label { Text = "Make the rhythm your own.", Font = new Font("Segoe UI", 9), AutoSize = true, ForeColor = SpeakerTheme.Muted, Location = new Point(0, 39) });
        heading.Controls.Add(new Label { Text = "Settings", Font = new Font("Georgia", 24), AutoSize = true, ForeColor = SpeakerTheme.Ink, Location = Point.Empty });
        root.Controls.Add(heading, 0, 0);
        var nav = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var preferences = SpeakerTheme.Action("Preferences", "preferences", 120);
        var troubleshooting = SpeakerTheme.Action("Troubleshooting", "troubleshooting", 154);
        nav.Controls.AddRange([preferences, troubleshooting]);
        root.Controls.Add(nav, 0, 1);
        var host = new SurfacePanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(1), BackColor = SpeakerTheme.Panel };
        root.Controls.Add(host, 0, 2);
        var options = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, Padding = new Padding(18), ForeColor = SpeakerTheme.Ink };
        options.ColumnStyles.Add(new(SizeType.Percent, 100));
        keys = new AccentCheckBox { Text = "Control playback from anywhere", Name = "settingsHotkeys", Checked = settings.HotkeysEnabled, AutoSize = true, Margin = new Padding(0, 0, 0, 8), Enabled = !busy };
        options.Controls.Add(keys);
        options.Controls.Add(Caption("F8 Start / Resume    ·    F9 Pause    ·    F10 Stop", 10));
        options.Controls.Add(Caption(busy ? "Finish playback before changing preferences." : "Shortcuts stay off until you enable them.", 18));
        options.Controls.Add(new Label { Text = "Advanced input timing", Font = new Font("Georgia", 13), AutoSize = true, Margin = new Padding(0, 8, 0, 5) });
        options.Controls.Add(Caption("Adjust only if messages are arriving incorrectly.", 12));
        chat = Timing("settingsChatOpen", chatOpenMs, 10000);
        submit = Timing("settingsSubmit", submitMs, 10000);
        gap = Timing("settingsKeyGap", settings.KeyGapMs, 1000);
        foreach (var number in new[] { chat, submit, gap }) number.Enabled = !busy;
        options.Controls.Add(TimingRow("Wait for chat to open", chat));
        options.Controls.Add(TimingRow("Wait before sending", submit));
        options.Controls.Add(TimingRow("Gap between key events", gap));
        host.Controls.Add(options);

        var logs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16), Visible = false };
        logs.ColumnStyles.Add(new(SizeType.Percent, 100));
        logs.RowStyles.Add(new(SizeType.Absolute, 42));
        logs.RowStyles.Add(new(SizeType.Percent, 100));
        logs.RowStyles.Add(new(SizeType.Absolute, 48));
        logs.Controls.Add(new Label { Text = "Activity details for troubleshooting.\nSending a message does not confirm it appeared in game.", ForeColor = SpeakerTheme.Muted, AutoSize = true, Font = new Font("Segoe UI", 9) }, 0, 0);
        logs.Controls.Add(new TextBox { Name = "diagnostics", AccessibleName = "Activity log", Text = log, Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, WordWrap = false, ScrollBars = ScrollBars.Both, BorderStyle = BorderStyle.None, BackColor = SpeakerTheme.Background, ForeColor = SpeakerTheme.Ink, Font = new Font("Consolas", 9) }, 0, 1);
        var export = SpeakerTheme.Action("Save log", "saveLog", 105);
        export.Margin = new Padding(0, 8, 0, 0);
        export.Click += (_, _) => saveLog();
        logs.Controls.Add(export, 0, 2);
        host.Controls.Add(logs);
        void SelectPage(bool showPreferences)
        {
            options.Visible = showPreferences;
            logs.Visible = !showPreferences;
            if (!showPreferences) logs.BringToFront();
            preferences.ForeColor = showPreferences ? SpeakerTheme.Gold : SpeakerTheme.Muted;
            troubleshooting.ForeColor = showPreferences ? SpeakerTheme.Muted : SpeakerTheme.Gold;
            preferences.FlatAppearance.BorderColor = showPreferences ? SpeakerTheme.GoldDim : SpeakerTheme.Border;
            troubleshooting.FlatAppearance.BorderColor = showPreferences ? SpeakerTheme.Border : SpeakerTheme.GoldDim;
            preferences.BackColor = showPreferences ? SpeakerTheme.Surface : SpeakerTheme.Background;
            troubleshooting.BackColor = showPreferences ? SpeakerTheme.Background : SpeakerTheme.Surface;
        }
        SelectPage(true);
        preferences.Click += (_, _) => SelectPage(true);
        troubleshooting.Click += (_, _) => SelectPage(false);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 16, 0, 0), Margin = Padding.Empty };
        var apply = SpeakerTheme.Action("Save changes", "applySettings", 135);
        apply.BackColor = SpeakerTheme.Gold;
        apply.ForeColor = SpeakerTheme.Background;
        apply.DialogResult = DialogResult.OK;
        apply.Enabled = !busy;
        var cancel = SpeakerTheme.Action(busy ? "Close" : "Cancel", "cancelSettings", 90);
        cancel.DialogResult = DialogResult.Cancel;
        actions.Controls.AddRange([apply, cancel]);
        var credit = new LinkLabel
        {
            Name = "designCredit", Text = "Design inspired by Aosda · darkagesbot.com", AutoSize = true,
            Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9),
            ForeColor = SpeakerTheme.Muted, LinkColor = SpeakerTheme.Gold,
            ActiveLinkColor = SpeakerTheme.Ink, VisitedLinkColor = SpeakerTheme.Gold,
            LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0, 10, 0, 0)
        };
        credit.Links.Clear();
        credit.Links.Add(19, credit.Text.Length - 19, "https://darkagesbot.com");
        credit.LinkClicked += (_, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo((string)e.Link!.LinkData!) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { MessageBox.Show(this, "Open darkagesbot.com in your browser.", "Aosda", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        };
        root.Controls.Add(credit, 0, 3);
        root.Controls.Add(actions, 0, 4);
        AcceptButton = busy ? cancel : apply;
        CancelButton = cancel;
    }

    private static Label Caption(string text, int bottom) => new() { Text = text, ForeColor = SpeakerTheme.Muted, AutoSize = true, Font = new Font("Segoe UI", 9), Margin = new Padding(0, 0, 0, bottom) };
    private static NumericUpDown Timing(string name, int value, int maximum) => new() { Name = name, Maximum = maximum, Value = value, Increment = 10, Width = 82, BorderStyle = BorderStyle.None, BackColor = SpeakerTheme.Surface, ForeColor = SpeakerTheme.Ink };
    private static Control TimingRow(string label, NumericUpDown number)
    {
        var row = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 3, Margin = new Padding(0, 0, 0, 8) };
        row.ColumnStyles.Add(new(SizeType.Percent, 100));
        row.ColumnStyles.Add(new(SizeType.Absolute, 102));
        row.ColumnStyles.Add(new(SizeType.Absolute, 30));
        row.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        number.AccessibleName = label + " in milliseconds";
        row.Controls.Add(SpeakerTheme.InputField(number), 1, 0);
        row.Controls.Add(new Label { Text = "ms", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SpeakerTheme.Muted }, 2, 0);
        return row;
    }
}
