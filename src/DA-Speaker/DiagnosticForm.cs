namespace DASpeaker;

internal sealed class DiagnosticForm : Form
{
    private static readonly Color Surface = Color.FromArgb(39, 42, 42);
    private static readonly Color Ink = Color.FromArgb(232, 230, 220);
    private static readonly Color Muted = Color.FromArgb(172, 179, 174);
    private static readonly Color Accent = Color.FromArgb(184, 158, 99);
    private readonly WindowsWindowApi windows = new();
    private readonly ComboBox clients = new() { Name = "clients", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 720 };
    private readonly Button refresh = MakeButton("Refresh", "refresh");
    private readonly Button test = MakeButton("TEST CHAT", "testChat");
    private readonly NumericUpDown chatOpen = MakeNumber("chatOpenDelay", 250, 10000);
    private readonly NumericUpDown paste = MakeNumber("pasteDelay", 150, 10000);
    private readonly NumericUpDown keyGap = MakeNumber("keyGap", 10, 1000);
    private readonly ComboBox inputMethod = new() { Name = "inputMethod", DropDownStyle = ComboBoxStyle.DropDownList, Width = 400, BackColor = Surface, ForeColor = Ink, Margin = new Padding(0, 5, 0, 0) };
    private readonly ComboBox testSample = new() { Name = "testSample", DropDownStyle = ComboBoxStyle.DropDownList, Width = 400, BackColor = Surface, ForeColor = Ink, Margin = new Padding(0, 5, 0, 0) };
    private readonly CheckBox countdown = new() { Text = "Wait 3 seconds before test (time to switch focus)", AutoSize = true };
    private readonly Label status = new() { Text = "Choose a client to begin.", Dock = DockStyle.Fill, AutoSize = true, ForeColor = Accent };
    private readonly TextBox log = new() { Name = "diagnosticLog", Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
    private readonly ChatInput input;
    private readonly List<string> logLines = [];
    private CancellationTokenSource? operation;
    private bool closing;
    private bool recoveryRequired;

    public DiagnosticForm()
    {
        Text = "DA Speaker — Input diagnostic";
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(27, 30, 30);
        ForeColor = Ink;
        ClientSize = new Size(750, 740);
        MinimumSize = new Size(720, 730);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(22);
        input = new(windows, new WindowsClipboard(), new InputDelay(TimeProvider.System), AppendLog);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 10, Margin = Padding.Empty };
        for (var i = 0; i < 9; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "DA Speaker", AutoSize = true, Font = new Font("Georgia", 23), ForeColor = Accent, Margin = new Padding(0, 0, 0, 3) }, 0, 0);
        layout.Controls.Add(new Label { Text = "INPUT DIAGNOSTIC 04  /  USDA 7.41", AutoSize = true, ForeColor = Muted, Margin = new Padding(0, 0, 0, 20) }, 0, 1);
        layout.Controls.Add(new Label { Text = "Dark Ages client", AutoSize = true, Margin = new Padding(0, 0, 0, 6) }, 0, 2);

        var picker = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 18) };
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        clients.BackColor = Surface;
        clients.ForeColor = Ink;
        clients.Margin = new Padding(0, 3, 10, 0);
        refresh.Dock = DockStyle.Fill;
        picker.Controls.Add(clients, 0, 0);
        picker.Controls.Add(refresh, 1, 0);
        layout.Controls.Add(picker, 0, 3);

        var timing = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 15) };
        for (var i = 0; i < 3; i++) timing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        AddTiming(timing, 0, "Chat open · ms", chatOpen);
        AddTiming(timing, 1, "Before submit · ms", paste);
        AddTiming(timing, 2, "Key gap · ms", keyGap);
        var triggerLabel = new Label { Text = "Input method", AutoSize = true, ForeColor = Muted, Margin = new Padding(0, 12, 0, 0) };
        timing.Controls.Add(triggerLabel, 0, 2);
        timing.SetColumnSpan(triggerLabel, 3);
        inputMethod.Items.AddRange(["Ctrl+V direct — avoid translated V", "Ctrl+V posted — previous test added v", "ASCII key pairs — case failed", "Ctrl only — previous test was empty"]);
        inputMethod.SelectedIndex = 0;
        timing.Controls.Add(inputMethod, 0, 3);
        timing.SetColumnSpan(inputMethod, 3);
        var sampleLabel = new Label { Text = "Test text", AutoSize = true, ForeColor = Muted, Margin = new Padding(0, 12, 0, 0) };
        timing.Controls.Add(sampleLabel, 0, 4);
        timing.SetColumnSpan(sampleLabel, 3);
        testSample.Items.AddRange(["GLIOCA TEST", "glioca test", "Aa Zz 09 !?:\"'.,-"]);
        testSample.SelectedIndex = 0;
        timing.Controls.Add(testSample, 0, 5);
        timing.SetColumnSpan(testSample, 3);
        layout.Controls.Add(timing, 0, 4);
        layout.Controls.Add(new Label
        {
            Text = "Log in and leave local chat closed with no pending text.\nDirect Ctrl+V test preserves your clipboard. Check case and punctuation.",
            AutoSize = true, ForeColor = Muted, Margin = new Padding(0, 0, 0, 12)
        }, 0, 5);
        countdown.Margin = new Padding(0, 0, 0, 12);
        layout.Controls.Add(countdown, 0, 6);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 12) };
        test.Width = 160;
        test.BackColor = Color.FromArgb(71, 81, 62);
        test.Enabled = false;
        var save = MakeButton("Save log…", "saveLog");
        save.Width = 120;
        actions.Controls.Add(test);
        actions.Controls.Add(save);
        layout.Controls.Add(actions, 0, 7);
        status.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(status, 0, 8);
        log.BackColor = Color.FromArgb(20, 23, 23);
        log.ForeColor = Muted;
        log.Font = new Font("Consolas", 9);
        log.Margin = Padding.Empty;
        layout.Controls.Add(log, 0, 9);

        Shown += (_, _) => RefreshClients();
        refresh.Click += (_, _) => RefreshClients();
        clients.SelectedIndexChanged += (_, _) => UpdateEnabled();
        test.Click += async (_, _) => await TestAsync();
        save.Click += (_, _) => SaveLog();
        FormClosing += OnClosing;
        AppendLog("Diagnostic only. No script playback until live input is verified.");
        AppendLog("Build 04: Ctrl/V use SendMessageTimeoutW; Enter keeps original PostMessageW path.");
        AppendLog("Direct sends have a 1000 ms timeout. Clipboard settle: 50 ms. No focus changes.");
    }

    private void RefreshClients()
    {
        var previous = clients.SelectedItem as ClientTarget;
        try
        {
            var found = windows.FindClients();
            clients.Items.Clear();
            foreach (var client in found) clients.Items.Add(client);
            clients.SelectedIndex = -1;
            var match = found.FirstOrDefault(c => c.Handle == previous?.Handle && c.ProcessId == previous?.ProcessId);
            if (match is not null) clients.SelectedItem = match;
            else if (found.Count == 1) clients.SelectedIndex = 0;
            status.Text = found.Count switch
            {
                0 => "No Darkages windows found. Start the client, then Refresh.",
                1 => "Client found. Ready for TEST CHAT.",
                _ => "Multiple clients found. Select the intended client."
            };
            AppendLog($"Found {found.Count} window(s) with exact class Darkages.");
            foreach (var client in found) AppendLog(client.ToString());
        }
        catch (Exception ex) { status.Text = "Client discovery failed. See log."; AppendLog(ex.Message); }
        UpdateEnabled();
    }

    private async Task TestAsync()
    {
        if (operation is not null || clients.SelectedItem is not ClientTarget target) return;
        if (recoveryRequired && MessageBox.Show(this,
            "The previous test may have left chat open or text pending. Close or clear that chat input before retrying. Is the client ready?",
            "Check Dark Ages chat", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        recoveryRequired = false;
        using var cts = new CancellationTokenSource();
        operation = cts;
        UpdateEnabled();
        try
        {
            AppendLog($"TEST CHAT target: {target}");
            var trigger = inputMethod.SelectedIndex switch { 0 => InputMethod.CtrlVDirect, 1 => InputMethod.CtrlV, 2 => InputMethod.AsciiKeyPairs, _ => InputMethod.CtrlOnly };
            var sample = (string)testSample.SelectedItem!;
            var timings = new InputTimings((int)chatOpen.Value, (int)paste.Value, (int)keyGap.Value, trigger);
            AppendLog($"Timing: open={timings.ChatOpenDelayMs} ms, paste={timings.PasteDelayMs} ms, gap={timings.KeyGapMs} ms.");
            AppendLog($"Selected input method: {trigger}. Expected text: {sample}");
            if (countdown.Checked)
                for (var remaining = 3; remaining > 0; remaining--)
                {
                    status.Text = $"Test in {remaining} seconds — switch focus now if needed.";
                    await Task.Delay(1000, cts.Token);
                }
            status.Text = $"Posting {sample}…";
            await input.SendAsync(target, sample, timings, cts.Token);
            status.Text = "Input dispatched. Compare game text with sample, including case and symbols.";
            // Even successful posting cannot confirm that both Enter presses were consumed.
            recoveryRequired = true;
        }
        catch (OperationCanceledException)
        {
            recoveryRequired = true;
            status.Text = "Cancelled. Check local chat before another test.";
            AppendLog(status.Text);
        }
        catch (Exception ex)
        {
            recoveryRequired = true;
            status.Text = "Test failed. Check chat state; see diagnostics below.";
            AppendLog(ex.ToString());
        }
        finally
        {
            operation = null;
            UpdateEnabled();
            if (closing) BeginInvoke(Close);
        }
    }

    private void UpdateEnabled()
    {
        var idle = operation is null;
        test.Enabled = idle && clients.SelectedItem is ClientTarget;
        refresh.Enabled = clients.Enabled = chatOpen.Enabled = paste.Enabled = keyGap.Enabled = inputMethod.Enabled = testSample.Enabled = countdown.Enabled = idle;
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (operation is null) return;
        e.Cancel = true;
        closing = true;
        operation.Cancel();
        status.Text = "Finishing key and clipboard cleanup…";
    }

    private void AppendLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff}  {message}";
        logLines.Add(line);
        log.AppendText(line + System.Environment.NewLine);
    }

    private void SaveLog()
    {
        using var dialog = new SaveFileDialog { Filter = "Text log (*.txt)|*.txt", FileName = $"DA-Speaker-test-{DateTime.Now:yyyyMMdd-HHmmss}.txt" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllLines(dialog.FileName, logLines); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save log", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static Button MakeButton(string text, string name)
    {
        var button = new Button { Text = text, Name = name, Height = 36, BackColor = Surface, ForeColor = Ink, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 10, 0), UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderColor = Color.FromArgb(80, 86, 79);
        return button;
    }

    private static NumericUpDown MakeNumber(string name, int value, int maximum) => new()
    {
        Name = name, Minimum = 0, Maximum = maximum, Value = value, Increment = 10,
        BackColor = Surface, ForeColor = Ink, Width = 150, BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(0, 5, 12, 0), AccessibleName = name
    };

    private static void AddTiming(TableLayoutPanel table, int column, string label, NumericUpDown input)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = Muted, Margin = Padding.Empty }, column, 0);
        table.Controls.Add(input, column, 1);
    }
}
