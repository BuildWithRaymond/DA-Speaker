namespace DASpeaker;

internal sealed partial class SpeakerForm
{
    private readonly TableLayoutPanel workspace = new() { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = SpeakerTheme.Background, ColumnCount = 2, RowCount = 1 };
    private readonly TableLayoutPanel workspaceShell = new() { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = SpeakerTheme.Background, ColumnCount = 1, RowCount = 3 };
    private readonly Panel scriptPanel = new SurfacePanel() { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(22, 16, 22, 12), BackColor = SpeakerTheme.Panel };
    private readonly Panel cuePanel = new SurfacePanel() { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(20, 16, 16, 12), BackColor = SpeakerTheme.Preview };
    private readonly Button scriptButton = Button("Script", "showScript", 105);
    private readonly Label scriptTitle = Label("Your script", SpeakerTheme.Ink);
    private readonly Label previewEmpty = Label("Your messages will appear here.\n\nEach message fits Dark Ages'\n59-character chat limit.", SpeakerTheme.Muted);
    private readonly PlaybackTrack track = new() { Dock = DockStyle.Bottom, Margin = Padding.Empty };
    private readonly ToolTip help = new();
    private FlowLayoutPanel compactTabs = null!;
    private bool compact;
    private bool showingPreview;

    private void BuildLayout()
    {
        SuspendLayout();
        Content.Padding = new Padding(24, 16, 24, 20);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 1, RowCount = 5 };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 80));
        root.RowStyles.Add(new(SizeType.Absolute, 64));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 72));
        root.RowStyles.Add(new(SizeType.Absolute, 98));
        Content.Controls.Add(root);
        root.Controls.Add(new CeremonyHeading(), 0, 0);

        settingsButton.Width = 82;
        settingsButton.Height = 32;
        settingsButton.Font = new Font("Segoe UI", 9);
        settingsButton.Margin = new Padding(0, 4, 16, 0);
        settingsButton.BackColor = SpeakerTheme.Background;
        settingsButton.FlatAppearance.BorderSize = 0;
        CaptionActions.Controls.Add(settingsButton);

        var target = Grid(4);
        target.ColumnStyles.Add(new(SizeType.Absolute, 118));
        target.ColumnStyles.Add(new(SizeType.Percent, 100));
        target.ColumnStyles.Add(new(SizeType.Absolute, 94));
        target.ColumnStyles.Add(new(SizeType.Absolute, 112));
        var picker = new SurfacePanel { Dock = DockStyle.Top, Height = 42, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(8, 5, 6, 5), BackColor = SpeakerTheme.Surface };
        var targetCaption = Label("GAME WINDOW", SpeakerTheme.Gold);
        targetCaption.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        targetCaption.Margin = new Padding(0, 13, 0, 0);
        target.Controls.Add(targetCaption, 0, 0);
        clients.Margin = Padding.Empty;
        clients.Font = new Font("Segoe UI", 10);
        clients.AccessibleName = "Game window";
        clients.DrawMode = DrawMode.OwnerDrawFixed;
        clients.ItemHeight = 26;
        clients.FlatStyle = FlatStyle.Flat;
        clients.DrawItem += DrawClient;
        clients.SelectedIndexChanged += (_, _) => help.SetToolTip(clients, clients.SelectedItem?.ToString() ?? "Select the game window to receive your messages.");
        picker.Controls.Add(clients);
        target.Controls.Add(picker, 1, 0);
        refresh.Width = 84;
        refresh.Margin = new Padding(0, 0, 8, 0);
        test.Width = 112;
        test.Margin = Padding.Empty;
        target.Controls.Add(refresh, 2, 0);
        target.Controls.Add(test, 3, 0);
        help.SetToolTip(test, "Send GLIOCA TEST to the selected game window. Close local chat first.");
        root.Controls.Add(target, 0, 1);

        workspace.ColumnStyles.Add(new(SizeType.Percent, 60));
        workspace.ColumnStyles.Add(new(SizeType.Percent, 40));
        workspace.RowStyles.Add(new(SizeType.Percent, 100));
        workspace.Controls.Add(scriptPanel, 0, 0);
        workspace.Controls.Add(cuePanel, 1, 0);
        workspaceShell.ColumnStyles.Add(new(SizeType.Percent, 100));
        workspaceShell.RowStyles.Add(new(SizeType.Absolute, 0));
        workspaceShell.RowStyles.Add(new(SizeType.Percent, 100));
        workspaceShell.RowStyles.Add(new(SizeType.AutoSize));
        compactTabs = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(12, 4, 0, 0), WrapContents = false, Visible = false };
        SpeakerTheme.Quiet(scriptButton);
        SpeakerTheme.Quiet(previewButton);
        scriptButton.Height = previewButton.Height = 34;
        previewButton.Text = "Preview";
        compactTabs.Controls.AddRange([scriptButton, previewButton]);
        workspaceShell.Controls.Add(compactTabs, 0, 0);
        workspaceShell.Controls.Add(workspace, 0, 1);
        validation.ForeColor = Color.FromArgb(242, 161, 167);
        validation.Name = "scriptProblem";
        validation.BackColor = Color.FromArgb(54, 27, 32);
        validation.Padding = new Padding(14, 9, 14, 9);
        validation.Dock = DockStyle.Fill;
        validation.Cursor = Cursors.Hand;
        validation.AccessibleName = "Script problem";
        workspaceShell.Controls.Add(validation, 0, 2);
        root.Controls.Add(workspaceShell, 0, 2);

        var script = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        script.ColumnStyles.Add(new(SizeType.Percent, 100));
        script.RowStyles.Add(new(SizeType.Absolute, 66));
        script.RowStyles.Add(new(SizeType.Percent, 100));
        script.RowStyles.Add(new(SizeType.Absolute, 36));
        scriptPanel.Controls.Add(script);
        var scriptHeader = Grid(3);
        scriptHeader.ColumnStyles.Add(new(SizeType.Percent, 100));
        scriptHeader.ColumnStyles.Add(new(SizeType.Absolute, 60));
        scriptHeader.ColumnStyles.Add(new(SizeType.Absolute, 60));
        scriptTitle.Font = new Font("Georgia", 19);
        scriptTitle.AutoSize = false;
        scriptTitle.AutoEllipsis = true;
        scriptTitle.Dock = DockStyle.Top;
        scriptTitle.Height = 32;
        scriptTitle.TextAlign = ContentAlignment.TopLeft;
        scriptTitle.Padding = new Padding(0, 3, 0, 0);
        var scriptIdentity = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var scriptHint = Label("Compose your address", SpeakerTheme.Muted);
        scriptHint.Font = new Font("Segoe UI", 8.5f);
        scriptHint.Dock = DockStyle.Top;
        scriptIdentity.Controls.Add(scriptHint);
        scriptIdentity.Controls.Add(scriptTitle);
        scriptHeader.Controls.Add(scriptIdentity, 0, 0);
        open.Text = "Open";
        save.Text = "Save";
        foreach (var button in new[] { open, save }) { SpeakerTheme.Quiet(button); button.Width = 58; button.Height = 36; button.Margin = Padding.Empty; }
        scriptHeader.Controls.Add(open, 1, 0);
        scriptHeader.Controls.Add(save, 2, 0);
        script.Controls.Add(scriptHeader, 0, 0);
        editor.Margin = Padding.Empty;
        editor.HideSelection = false;
        editor.AccessibleName = "Your script";
        editor.AccessibleDescription = "Paste or write your speech. Blank lines separate paragraphs. Use [wait 5s] for a pause.";
        script.Controls.Add(editor, 0, 1);
        wrap.Text = "Split long lines automatically";
        wrap.ForeColor = SpeakerTheme.Muted;
        wrap.Font = new Font("Segoe UI", 9);
        wrap.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        wrap.Margin = Padding.Empty;
        help.SetToolTip(wrap, "Keep every message within 59 characters. Words stay intact. Blank lines separate paragraphs.");
        script.Controls.Add(wrap, 0, 2);

        var cueLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = Padding.Empty };
        cueLayout.ColumnStyles.Add(new(SizeType.Percent, 100));
        cueLayout.RowStyles.Add(new(SizeType.Absolute, 32));
        cueLayout.RowStyles.Add(new(SizeType.Absolute, 34));
        cueLayout.RowStyles.Add(new(SizeType.Percent, 100));
        cuePanel.Controls.Add(cueLayout);
        var cueTitle = Label("Message preview", SpeakerTheme.Ink);
        cueTitle.Font = new Font("Georgia", 16);
        cueLayout.Controls.Add(cueTitle, 0, 0);
        counts.ForeColor = SpeakerTheme.Muted;
        counts.Font = new Font("Segoe UI", 9);
        cueLayout.Controls.Add(counts, 0, 1);
        var previewHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        previewHost.Controls.Add(preview);
        previewEmpty.Dock = DockStyle.Fill;
        previewEmpty.Padding = new Padding(0, 20, 0, 0);
        previewEmpty.Font = new Font("Segoe UI", 10.5f);
        previewHost.Controls.Add(previewEmpty);
        cueLayout.Controls.Add(previewHost, 0, 2);
        preview.SelectedIndexChanged += (_, _) =>
        {
            if (preview.SelectedItem is not QueueStep step) return;
            var offset = editor.GetFirstCharIndexFromLine(step.SourceLine - 1);
            if (offset < 0) return;
            editor.Select(offset, editor.Lines.ElementAtOrDefault(step.SourceLine - 1)?.Length ?? 0);
            if (compact) ShowWorkspace(false);
            editor.ScrollToCaret();
        };

        var pace = Grid(3);
        pace.Padding = new Padding(2, 14, 0, 12);
        pace.ColumnStyles.Add(new(SizeType.Absolute, 64));
        pace.ColumnStyles.Add(new(SizeType.Percent, 50));
        pace.ColumnStyles.Add(new(SizeType.Percent, 50));
        var paceCaption = Label("PACE", SpeakerTheme.Gold);
        paceCaption.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        paceCaption.Margin = Padding.Empty;
        paceCaption.Anchor = AnchorStyles.Left;
        pace.Controls.Add(paceCaption, 0, 0);
        pace.Controls.Add(PaceField("Between messages", lineDelay), 1, 0);
        pace.Controls.Add(PaceField("Between paragraphs", paragraphDelay), 2, 0);
        help.SetToolTip(paragraphDelay, "Blank lines use this pause instead of the between-message pause.");
        root.Controls.Add(pace, 0, 3);

        var playback = new SurfacePanel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = SpeakerTheme.Panel, Padding = new Padding(18, 8, 18, 10) };
        var footer = Grid(2);
        footer.Padding = new Padding(0, 8, 0, 8);
        footer.ColumnStyles.Add(new(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new(SizeType.Absolute, 340));
        var text = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 0, 12, 0) };
        text.RowStyles.Add(new(SizeType.Percent, 40));
        text.RowStyles.Add(new(SizeType.Percent, 35));
        text.RowStyles.Add(new(SizeType.Percent, 25));
        text.ColumnStyles.Add(new(SizeType.Percent, 100));
        status.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        status.ForeColor = Ink;
        status.Name = "playbackStatus";
        status.AutoSize = false;
        status.Dock = DockStyle.Fill;
        status.AutoEllipsis = true;
        progress.Font = new Font("Segoe UI", 9);
        progress.Name = "playbackProgress";
        progress.AutoSize = false;
        progress.Dock = DockStyle.Fill;
        progress.AutoEllipsis = true;
        hotkeyStatus.Font = new Font("Segoe UI", 8);
        text.Controls.Add(status, 0, 0);
        text.Controls.Add(progress, 0, 1);
        text.Controls.Add(hotkeyStatus, 0, 2);
        footer.Controls.Add(text, 0, 0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, 10, 0, 0), WrapContents = false };
        start.Width = 160;
        pause.Width = 86;
        stop.Width = 78;
        start.Height = pause.Height = stop.Height = 46;
        start.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        stop.Margin = Padding.Empty;
        actions.Controls.AddRange([start, pause, stop]);
        footer.Controls.Add(actions, 1, 0);
        playback.Controls.Add(footer);
        playback.Controls.Add(track);
        root.Controls.Add(playback, 0, 4);

        scriptButton.Click += (_, _) => ShowWorkspace(false);
        SizeChanged += (_, _) => UpdateWorkspaceLayout();
        FormClosed += (_, _) => { help.Dispose(); chatOpen.Dispose(); submitDelay.Dispose(); };
        ResumeLayout(true);
        UpdateWorkspaceLayout();
    }

    private static TableLayoutPanel Grid(int columns)
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = 1, Margin = Padding.Empty };
        grid.RowStyles.Add(new(SizeType.Percent, 100));
        return grid;
    }

    private static Control PaceField(string title, NumericUpDown number)
    {
        var field = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 14, 0) };
        field.ColumnStyles.Add(new(SizeType.Percent, 100));
        field.ColumnStyles.Add(new(SizeType.Absolute, 92));
        field.ColumnStyles.Add(new(SizeType.Absolute, 21));
        field.RowStyles.Add(new(SizeType.Percent, 100));
        var label = Label(title, Ink);
        label.Font = new Font("Segoe UI", 9);
        label.Anchor = AnchorStyles.Left;
        field.Controls.Add(label, 0, 0);
        number.Width = 68;
        number.Font = new Font("Consolas", 10.5f);
        number.Anchor = AnchorStyles.Left;
        number.Margin = Padding.Empty;
        number.AccessibleName = title + " in seconds";
        field.Controls.Add(SpeakerTheme.InputField(number), 1, 0);
        var unit = Label("s", Muted);
        unit.Anchor = AnchorStyles.Left;
        unit.Margin = new Padding(6, 0, 0, 0);
        field.Controls.Add(unit, 2, 0);
        return field;
    }

    private void ShowWorkspace(bool previewSelected)
    {
        showingPreview = previewSelected;
        UpdateWorkspaceLayout();
    }

    private void UpdateWorkspaceLayout()
    {
        if (compactTabs is null) return;
        clients.ItemHeight = 26 * DeviceDpi / 96;
        compact = ClientSize.Width < 940 * DeviceDpi / 96f;
        workspace.SuspendLayout();
        workspaceShell.SuspendLayout();
        compactTabs.Visible = compact;
        workspaceShell.RowStyles[0].Height = compact ? 44 * DeviceDpi / 96f : 0;
        scriptPanel.Margin = new Padding(0, 0, compact ? 0 : 6, 0);
        cuePanel.Margin = new Padding(compact ? 0 : 6, 0, 0, 0);
        scriptPanel.Visible = !compact || !showingPreview;
        cuePanel.Visible = !compact || showingPreview;
        workspace.ColumnStyles[0].Width = compact ? showingPreview ? 0 : 100 : 60;
        workspace.ColumnStyles[1].Width = compact ? showingPreview ? 100 : 0 : 40;
        scriptButton.ForeColor = showingPreview ? SpeakerTheme.Muted : SpeakerTheme.Gold;
        previewButton.ForeColor = showingPreview ? SpeakerTheme.Gold : SpeakerTheme.Muted;
        validation.MaximumSize = new Size(Math.Max(200, workspaceShell.Width), 0);
        workspaceShell.ResumeLayout(true);
        workspace.ResumeLayout(true);
        preview.Refresh();
    }

    protected override void OnScaleAdjusted()
    {
        UpdateWorkspaceLayout();
        if (editDebounce.Enabled && !Busy) ValidateScript();
        UpdateEnabled();
    }

    private void DrawClient(object? sender, DrawItemEventArgs e)
    {
        using var brush = new SolidBrush(SpeakerTheme.Surface);
        e.Graphics.FillRectangle(brush, e.Bounds);
        var value = "Select a Dark Ages window";
        if (e.Index >= 0 && clients.Items[e.Index] is ClientTarget target)
        {
            value = string.IsNullOrWhiteSpace(target.Title) ? "Dark Ages" : target.Title;
            if (clients.Items.Cast<ClientTarget>().Count(c => c.Title == target.Title) > 1) value += $"  ·  {target.ProcessId}";
        }
        var bounds = Rectangle.Inflate(e.Bounds, -8, 0);
        TextRenderer.DrawText(e.Graphics, value, clients.Font, bounds, Ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        e.DrawFocusRectangle();
    }
}
