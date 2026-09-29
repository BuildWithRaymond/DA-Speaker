namespace DASpeaker;

internal sealed partial class SpeakerForm : DarkWindow
{
    private static readonly Color Background = SpeakerTheme.Background;
    private static readonly Color Surface = SpeakerTheme.Surface;
    private static readonly Color Ink = SpeakerTheme.Ink;
    private static readonly Color Muted = SpeakerTheme.Muted;
    private static readonly Color Accent = SpeakerTheme.Gold;
    private readonly WindowsWindowApi windows = new();
    private readonly SettingsStore? store;
    private AppSettings settings;
    private readonly IChatInput input;
    private readonly Func<IReadOnlyList<ClientTarget>> discoverClients;
    private readonly ScriptRunner runner;
    private readonly GlobalHotkeys hotkeys = new(new WindowsHotkeyApi());
    private readonly ComboBox clients = new() { Name = "clients", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, BackColor = Surface, ForeColor = Ink, DropDownWidth = 740 };
    private readonly Button refresh = Button("Refresh", "refreshClients", 95);
    private readonly Button open = Button("Open script", "openScript", 110);
    private readonly Button save = Button("Save script", "saveScript", 110);
    private readonly Button previewButton = Button("Preview queue", "previewQueue", 130);
    private readonly Button settingsButton = Button("Settings", "settings", 95);
    private readonly Button test = Button("Send test", "testChat", 112);
    private readonly Button start = Button("Start speaking", "startScript", 144);
    private readonly Button pause = Button("Pause", "pauseScript", 76);
    private readonly Button stop = Button("Stop", "stopScript", 70);
    private readonly RichTextBox editor = new() { Name = "scriptEditor", Dock = DockStyle.Fill, BackColor = SpeakerTheme.Panel, ForeColor = SpeakerTheme.Ink, Font = new Font("Segoe UI", 12.5f), BorderStyle = BorderStyle.None, DetectUrls = false, AcceptsTab = true, WordWrap = true };
    private readonly MessageCueList preview = new();
    private readonly Label counts = Label("", Muted);
    private readonly Label validation = Label("", Accent);
    private readonly Label status = Label("Choose a client, then paste or open your script.", Accent);
    private readonly Label progress = Label("Ready", Muted);
    private readonly Label hotkeyStatus = Label("Hotkeys off", Muted);
    private readonly CheckBox wrap = new AccentCheckBox() { Name = "autoWrap", Text = "Automatically wrap at 59 characters (whole words)", AutoSize = true, ForeColor = Ink };
    private readonly NumericUpDown lineDelay = Number("lineDelay", 86400, 1);
    private readonly NumericUpDown paragraphDelay = Number("paragraphDelay", 86400, 1);
    private readonly NumericUpDown chatOpen = Number("chatOpenDelay", 10000, 0);
    private readonly NumericUpDown submitDelay = Number("submitDelay", 10000, 0);
    private readonly System.Windows.Forms.Timer heartbeat = new() { Interval = 100 };
    private readonly System.Windows.Forms.Timer editDebounce = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer autosave = new() { Interval = 1500 };
    private readonly Queue<string> logs = [];
    private ScriptPlan plan = ScriptParser.Parse("", new());
    private bool testing;
    private bool closing;
    private bool manualRecovery;
    private bool preferencesDirty;
    private bool highlighting;
    private bool showingPlayback;
    private bool settingsOpen;
    private string? loggedPlaybackError;
    private string? filePath;

    internal SpeakerForm(AppSettings settings, SettingsStore? store = null, string? startupWarning = null,
        IChatInput? chatInput = null, Func<IReadOnlyList<ClientTarget>>? discoverClients = null)
    {
        this.settings = settings;
        this.store = store;
        Text = "DA Speaker";
        Icon = SpeakerTheme.LoadIcon();
        Font = new Font("Segoe UI", 10);
        BackColor = Background;
        ForeColor = Ink;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(760, 740);
        Size = new Size(settings.Width, settings.Height);
        StartPosition = FormStartPosition.CenterScreen;
        if (settings.Left is { } left && settings.Top is { } top)
        {
            var remembered = new Rectangle(left, top, Width, Height);
            if (Screen.AllScreens.Any(screen => Rectangle.Intersect(screen.WorkingArea, remembered) is { Width: >= 200, Height: >= 100 }))
            {
                StartPosition = FormStartPosition.Manual;
                Location = remembered.Location;
            }
        }
        input = chatInput ?? new ChatInput(windows, new WindowsClipboard(), new InputDelay(TimeProvider.System), Log);
        this.discoverClients = discoverClients ?? windows.FindClients;
        runner = new(input, TimeProvider.System);
        runner.Changed += UpdatePlayback;
        runner.Trace += Log;

        BuildLayout();

        editor.Text = settings.Script;
        wrap.Checked = settings.AutoWrap;
        lineDelay.Value = settings.LineDelayMs / 1000m;
        paragraphDelay.Value = settings.ParagraphDelayMs / 1000m;
        chatOpen.Value = settings.ChatOpenDelayMs;
        submitDelay.Value = settings.SubmitDelayMs;
        editor.TextChanged += (_, _) => ScheduleValidation();
        wrap.CheckedChanged += (_, _) => ScheduleValidation();
        foreach (var number in new[] { lineDelay, paragraphDelay, chatOpen, submitDelay }) number.ValueChanged += (_, _) => ScheduleValidation();
        clients.SelectedIndexChanged += (_, _) => { preferencesDirty = true; if (!Busy) UpdateReadyStatus(); UpdateEnabled(); };
        refresh.Click += (_, _) => RefreshClients();
        open.Click += (_, _) => OpenScript();
        save.Click += (_, _) => SaveScript();
        previewButton.Click += (_, _) => { if (!Busy) ValidateScript(); ShowWorkspace(true); };
        settingsButton.Click += (_, _) => ShowSettings();
        test.Click += async (_, _) => await TestChatAsync();
        start.Click += async (_, _) => await StartOrResumeAsync();
        pause.Click += (_, _) => runner.Pause();
        stop.Click += (_, _) => runner.Stop();
        validation.Click += (_, _) => ShowFirstError();
        editDebounce.Tick += (_, _) => { editDebounce.Stop(); ValidateScript(); };
        heartbeat.Tick += (_, _) => UpdateProgress();
        autosave.Tick += (_, _) => { if (preferencesDirty && !runner.IsActive && !testing) SavePreferences(); };
        ResizeEnd += (_, _) => preferencesDirty = true;
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            if (Height > area.Height) Height = area.Height;
            if (Width > area.Width) Width = area.Width;
            RefreshClients();
            ApplyHotkeys();
            heartbeat.Start();
            autosave.Start();
        };
        FormClosing += OnClosing;
        FormClosed += (_, _) => { heartbeat.Dispose(); editDebounce.Dispose(); autosave.Dispose(); };
        Log("DA Speaker 1.1.0. Direct Ctrl+V; posted Enter; 100 ms after submit before clipboard restore. Messages limited to 59 characters.");
        if (startupWarning is not null) Log(startupWarning);
        ValidateScript();
        UpdateEnabled();
    }

    internal string ActivityLog => string.Join(System.Environment.NewLine, logs);

    private bool Busy => runner.IsActive || testing;
    private InputTimings Timings => new((int)chatOpen.Value, (int)submitDelay.Value, settings.KeyGapMs, InputMethod.CtrlVDirect);

    private void ScheduleValidation()
    {
        if (highlighting) return;
        preferencesDirty = true;
        editDebounce.Stop();
        editDebounce.Start();
        if (!Busy) start.Enabled = false;
    }

    private void ValidateScript()
    {
        editDebounce.Stop();
        plan = ScriptParser.Parse(editor.Text, new(wrap.Checked, (int)(lineDelay.Value * 1000), (int)(paragraphDelay.Value * 1000)));
        counts.Text = $"{plan.MessageCount} {(plan.MessageCount == 1 ? "message" : "messages")} · 59 characters per message";
        validation.Text = plan.Errors.Count > 0 ? $"{plan.Errors.Count} error(s). Line {plan.Errors[0].Line}: {plan.Errors[0].Message}  Click to locate." : "";
        validation.Visible = plan.Errors.Count > 0;
        HighlightErrors();
        preview.SetPlan(plan);
        previewEmpty.Visible = plan.Steps.Count == 0;
        previewEmpty.Text = plan.Errors.Count > 0 ? "Fix the highlighted text\nto see your message preview." : "Your messages will appear here.\n\nEach message fits Dark Ages'\n59-character chat limit.";
        if (!Busy)
        {
            showingPlayback = false;
            UpdateReadyStatus();
            track.Value = 0;
        }
        UpdateEnabled();
    }

    private void UpdateReadyStatus()
    {
        status.Text = plan.Errors.Count > 0 ? "Your script needs a small fix" : plan.MessageCount == 0 ? "Make room for your words" : clients.SelectedItem is ClientTarget ? "Ready to start" : clients.Items.Count == 0 ? "Open Dark Ages, then refresh" : "Choose your game window";
        progress.Text = plan.Errors.Count > 0 ? "Select the highlighted problem to fix it." : plan.MessageCount == 0 ? "Paste your words, or open a script." : "Close local chat before starting.";
    }

    private void HighlightErrors()
    {
        var position = editor.SelectionStart;
        var length = editor.SelectionLength;
        highlighting = true;
        try
        {
            editor.SelectAll();
            editor.SelectionBackColor = editor.BackColor;
            foreach (var error in plan.Errors)
            {
                editor.Select(error.Offset, error.Length);
                editor.SelectionBackColor = Color.FromArgb(92, 39, 47);
            }
            editor.Select(position, length);
            if (length == 0) editor.SelectionBackColor = editor.BackColor;
        }
        finally { highlighting = false; }
    }

    private void ShowFirstError()
    {
        if (plan.Errors.Count == 0) return;
        ShowWorkspace(false);
        var error = plan.Errors[0];
        editor.Focus();
        editor.Select(error.Offset, error.Length);
        editor.ScrollToCaret();
    }

    private void RefreshClients()
    {
        if (Busy) return;
        var previous = clients.SelectedItem as ClientTarget;
        try
        {
            var found = discoverClients();
            clients.Items.Clear();
            clients.Items.AddRange(found.Cast<object>().ToArray());
            var match = previous is null
                ? found.FirstOrDefault(c => (long)c.Handle == settings.LastHandle && c.ProcessId == settings.LastPid && c.Title == settings.LastTitle)
                : found.FirstOrDefault(c => c.Handle == previous.Handle && c.ProcessId == previous.ProcessId);
            clients.SelectedIndex = -1;
            if (match is not null) clients.SelectedItem = match;
            else if (found.Count == 1) clients.SelectedIndex = 0;
            UpdateReadyStatus();
            Log($"Found {found.Count} Darkages window(s).");
        }
        catch (Exception ex) { Report(ex); }
        UpdateEnabled();
    }

    private async Task StartOrResumeAsync()
    {
        if (testing || closing || settingsOpen) return;
        try
        {
            if (runner.IsActive)
            {
                if (runner.State != PlaybackState.Paused) return;
                if (runner.RecoveryRequired && !ConfirmRecovery()) return;
                runner.Resume(acknowledgeRecovery: true);
                return;
            }
            ValidateScript();
            if (!plan.IsValid) { ShowFirstError(); return; }
            if (clients.SelectedItem is not ClientTarget target) return;
            if (manualRecovery && !ConfirmRecovery()) return;
            manualRecovery = false;
            SavePreferences();
            Log($"START: {plan.MessageCount} messages to {target}.");
            Log($"Timing: line={(int)(lineDelay.Value * 1000)} ms, paragraph={(int)(paragraphDelay.Value * 1000)} ms, open={Timings.ChatOpenDelayMs} ms, paste={Timings.PasteDelayMs} ms, gap={Timings.KeyGapMs} ms, wrap={wrap.Checked}.");
            await runner.StartAsync(plan, target, Timings);
            manualRecovery |= runner.RecoveryRequired;
            Log($"Playback {runner.State}. {runner.SentCount} message(s) dispatched.");
        }
        catch (Exception ex) { Report(ex); }
        finally { UpdateEnabled(); CloseWhenReady(); }
    }

    private async Task TestChatAsync()
    {
        if (Busy || closing || clients.SelectedItem is not ClientTarget target) return;
        if (manualRecovery && !ConfirmRecovery()) return;
        testing = true;
        manualRecovery = false;
        UpdateEnabled();
        status.Text = "Sending GLIOCA TEST...";
        try
        {
            await input.SendAsync(target, "GLIOCA TEST", Timings, CancellationToken.None);
            status.Text = "Test sent";
            progress.Text = "Check for GLIOCA TEST in game.";
        }
        catch (Exception ex) { manualRecovery = true; Report(ex); }
        finally { testing = false; UpdateEnabled(); CloseWhenReady(); }
    }

    private bool ConfirmRecovery() => MessageBox.Show(this,
        "Check the selected client. Clear or close any pending chat input. The failed message will be retried and might already have appeared in game. Ready to continue?",
        "Recover interrupted chat", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    private void UpdatePlayback()
    {
        showingPlayback = true;
        if (runner.LastError is { } failure && failure != loggedPlaybackError) Log("Playback input failed: " + failure);
        loggedPlaybackError = runner.LastError;
        status.Text = runner.State switch
        {
            PlaybackState.Running => "Speaking...",
            PlaybackState.Pausing => "Finishing this message...",
            PlaybackState.Paused => runner.LastError is not null ? "Paused - check your game window" : "Paused, ready when you are",
            PlaybackState.Stopping => "Finishing this message...",
            PlaybackState.Completed => "Script finished",
            _ => "Stopped - ready from the beginning"
        };
        UpdateProgress();
        UpdateEnabled();
    }

    private void UpdateProgress()
    {
        if (!showingPlayback) return;
        if (!runner.IsActive && runner.State != PlaybackState.Completed) { track.Value = 0; preview.SetActiveStep(-1); progress.Text = "Queue reset. Start begins again."; return; }
        var remaining = runner.RemainingDelay.TotalSeconds;
        progress.Text = runner.State == PlaybackState.Completed ? "Check your messages in game." : runner.RecoveryRequired ? "Clear pending chat, then Resume. Details in Settings." : $"{runner.SentCount} of {runner.TotalMessages} messages" + (remaining > 0 ? $"  /  {(runner.State == PlaybackState.Paused ? "Wait remaining" : "Next in")} {remaining:0.0}s" : "");
        track.Value = runner.TotalMessages == 0 ? 0 : (float)runner.SentCount / runner.TotalMessages;
        preview.SetActiveStep(runner.IsActive ? runner.StepIndex : -1);
        help.SetToolTip(status, runner.LastError ?? status.Text);
    }

    private void UpdateEnabled()
    {
        var idle = !Busy && !closing;
        clients.Enabled = refresh.Enabled = open.Enabled = wrap.Enabled = idle;
        settingsButton.Enabled = !closing;
        lineDelay.Enabled = paragraphDelay.Enabled = chatOpen.Enabled = submitDelay.Enabled = idle;
        editor.ReadOnly = !idle;
        test.Enabled = idle && clients.SelectedItem is ClientTarget;
        start.Text = runner.State == PlaybackState.Paused ? "Resume" : "Start speaking";
        start.Enabled = !closing && !testing && (runner.State == PlaybackState.Paused || idle && plan.IsValid && clients.SelectedItem is ClientTarget);
        pause.Enabled = runner.IsActive && runner.State == PlaybackState.Running;
        stop.Enabled = runner.IsActive && runner.State != PlaybackState.Stopping;
        start.BackColor = start.Enabled ? SpeakerTheme.Gold : Surface;
        start.ForeColor = start.Enabled ? SpeakerTheme.Background : Muted;
        pause.BackColor = pause.Enabled ? SpeakerTheme.Gold : Surface;
        pause.ForeColor = pause.Enabled ? SpeakerTheme.Background : Muted;
        start.AccessibleName = start.Text;
        help.SetToolTip(start, start.Enabled ? start.Text : Busy ? "A script is in progress." : !plan.IsValid ? "Add a script and fix any highlighted problems." : "Select a game window first.");
    }

    private void OpenScript()
    {
        if (Busy) return;
        using var dialog = new OpenFileDialog { Filter = "Text scripts (*.txt)|*.txt|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { editor.Text = File.ReadAllText(dialog.FileName); filePath = dialog.FileName; ValidateScript(); ShowWorkspace(false); UpdateScriptTitle(); }
        catch (Exception ex) { Report(ex); }
    }

    private void SaveScript()
    {
        using var dialog = new SaveFileDialog { Filter = "Text scripts (*.txt)|*.txt", FileName = filePath is null ? "ceremony.txt" : Path.GetFileName(filePath) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllText(dialog.FileName, editor.Text); filePath = dialog.FileName; Log($"Script saved: {filePath}"); UpdateScriptTitle(); }
        catch (Exception ex) { Report(ex); }
    }

    private void UpdateScriptTitle()
    {
        scriptTitle.Text = filePath is null ? "Your script" : Path.GetFileName(filePath);
        help.SetToolTip(scriptTitle, filePath ?? "Your script");
    }

    private void ShowSettings()
    {
        if (settingsOpen) return;
        settingsOpen = true;
        try
        {
            using var dialog = new SpeakerSettingsDialog(settings, (int)chatOpen.Value, (int)submitDelay.Value, ActivityLog, Busy, SaveLog);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            settings = settings with { HotkeysEnabled = dialog.HotkeysEnabled, KeyGapMs = dialog.KeyGapMs };
            chatOpen.Value = dialog.ChatOpenMs;
            submitDelay.Value = dialog.SubmitMs;
            ApplyHotkeys();
            preferencesDirty = true;
            SavePreferences();
        }
        finally { settingsOpen = false; }
    }

    private void ApplyHotkeys()
    {
        hotkeys.Disable(Handle);
        if (settings.HotkeysEnabled && hotkeys.Enable(Handle) is { } error)
        {
            settings = settings with { HotkeysEnabled = false };
            Log(error);
            MessageBox.Show(this, error, "Hotkeys unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        hotkeyStatus.Text = hotkeys.Enabled ? "F8 Start / Resume   /   F9 Pause   /   F10 Stop" : "";
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && hotkeys.Enabled)
        {
            switch ((int)m.WParam)
            {
                case GlobalHotkeys.StartId: BeginInvoke((Action)(async () => await StartOrResumeAsync())); break;
                case GlobalHotkeys.PauseId: runner.Pause(); break;
                case GlobalHotkeys.StopId: runner.Stop(); break;
            }
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        hotkeys.Disable(Handle);
        base.OnHandleDestroyed(e);
    }

    private void SavePreferences()
    {
        if (store is null) return;
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        var target = clients.SelectedItem as ClientTarget;
        settings = settings with
        {
            Script = editor.Text, AutoWrap = wrap.Checked, LineDelayMs = (int)(lineDelay.Value * 1000), ParagraphDelayMs = (int)(paragraphDelay.Value * 1000),
            ChatOpenDelayMs = (int)chatOpen.Value, SubmitDelayMs = (int)submitDelay.Value, Width = bounds.Width, Height = bounds.Height,
            Left = bounds.Left, Top = bounds.Top,
            LastHandle = target is null ? settings.LastHandle : (long)target.Handle, LastPid = target?.ProcessId ?? settings.LastPid, LastTitle = target?.Title ?? settings.LastTitle
        };
        try { store.Save(settings); preferencesDirty = false; }
        catch (Exception ex) { Log("Could not save settings: " + ex.Message); preferencesDirty = false; }
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (Busy)
        {
            closing = true;
            e.Cancel = true;
            runner.Stop();
            status.Text = "Finishing this message...";
            UpdateEnabled();
            return;
        }
        SavePreferences();
        heartbeat.Stop();
        autosave.Stop();
        editDebounce.Stop();
    }

    private void CloseWhenReady() { if (closing && !Busy && !IsDisposed) BeginInvoke((Action)Close); }
    private void Report(Exception ex) { status.Text = "Something interrupted this action"; progress.Text = "Check Settings / Troubleshooting for details."; help.SetToolTip(status, ex.Message); Log(ex.ToString()); }

    private void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff}  {message}";
        logs.Enqueue(line);
        if (logs.Count > 2000)
        {
            while (logs.Count > 1800) logs.Dequeue();
        }
    }

    private void SaveLog()
    {
        using var dialog = new SaveFileDialog { Filter = "Text log (*.txt)|*.txt", FileName = $"DA-Speaker-{DateTime.Now:yyyyMMdd-HHmmss}.txt" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllLines(dialog.FileName, logs); } catch (Exception ex) { Report(ex); }
    }

    private static Label Label(string text, Color color) => new() { Text = text, AutoSize = true, ForeColor = color, Margin = Padding.Empty };
    private static Button Button(string text, string name, int width) => SpeakerTheme.Action(text, name, width);
    private static NumericUpDown Number(string name, int maximum, int decimals) => new() { Name = name, Maximum = maximum, Minimum = 0, DecimalPlaces = decimals, Increment = decimals == 0 ? 10 : 0.1m, Width = 130, BackColor = Surface, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 5, 10, 0) };
}
