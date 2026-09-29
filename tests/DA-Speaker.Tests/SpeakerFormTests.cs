using DASpeaker;
using Xunit;

namespace DASpeaker.Tests;

public sealed class SpeakerFormTests
{
    [Fact]
    public void PlaybackButtonsPauseResumeAndUseConfirmedTransport() => RunSta(() =>
    {
        var input = new ControlledInput();
        using var form = CreatePlaybackForm(input);
        ShowOffscreen(form);
        var start = Get<Button>(form, "startScript");
        var editor = Get<RichTextBox>(form, "scriptEditor");
        Assert.True(start.Enabled);
        Assert.IsType<WindowsFormsSynchronizationContext>(SynchronizationContext.Current);
        start.PerformClick();
        Assert.True(editor.ReadOnly);
        Get<Button>(form, "pauseScript").PerformClick();
        input.FinishFirst.SetResult();
        PumpUntil(() => start.Text == "Resume");
        Assert.Equal(new[] { "First" }, input.Messages);
        Assert.True(start.Enabled);
        start.PerformClick();
        Assert.Equal("Start speaking", start.Text);
        PumpUntil(() => !editor.ReadOnly);
        Assert.Equal(new[] { "First", "Second" }, input.Messages);
        Assert.All(input.Methods, method => Assert.Equal(InputMethod.CtrlVDirect, method));
        var log = form.ActivityLog;
        Assert.Contains("MESSAGE 1/2 | source line 1 | 5 chars | First", log);
        Assert.Contains("MESSAGE 2/2 | source line 2 | 6 chars | Second", log);
        Assert.Contains("MESSAGE 2/2 dispatched; game receipt unconfirmed.", log);
        Assert.Contains("Timing: line=0 ms, paragraph=5000 ms, open=250 ms, paste=150 ms, gap=10 ms, wrap=False.", log);
    });

    [Fact]
    public void StopButtonWaitsForCurrentCleanupThenUnlocksEditor() => RunSta(() =>
    {
        var input = new ControlledInput();
        using var form = CreatePlaybackForm(input);
        ShowOffscreen(form);
        var editor = Get<RichTextBox>(form, "scriptEditor");
        Get<Button>(form, "startScript").PerformClick();
        Get<Button>(form, "stopScript").PerformClick();
        Assert.True(editor.ReadOnly);
        input.FinishFirst.SetResult();
        PumpUntil(() => !editor.ReadOnly);
        Assert.Equal(new[] { "First" }, input.Messages);
    });

    [Fact]
    public void InvalidTextBlocksStartAndErrorLinksSelectSource() => RunSta(() =>
    {
        using var form = new SpeakerForm(new AppSettings { Script = new string('x', 60) }, discoverClients: () => [new((nint)123, 456, "Test")]);
        ShowOffscreen(form);
        Assert.False(Get<Button>(form, "startScript").Enabled);
        Assert.Contains(form.Controls.Find("scriptProblem", true), c => c.Visible && c.Text.Contains("Word exceeds 59 characters"));
    });

    private static SpeakerForm CreatePlaybackForm(ControlledInput input) => new(new AppSettings { Script = "First\nSecond", AutoWrap = false, LineDelayMs = 0 },
        chatInput: input, discoverClients: () => [new((nint)123, 456, "Test")]);

    [Fact]
    public void WorkspaceKeepsDraftAndPlaybackAvailableWhenResized() => RunSta(() =>
    {
        using var form = new SpeakerForm(new AppSettings { Width = 1100, Height = 820, Script = "First\n\n[wait 5s]\n\nSecond" },
            discoverClients: () => [new((nint)123, 456, "Dark Ages")]);
        ShowOffscreen(form);
        var editor = Get<RichTextBox>(form, "scriptEditor");
        var cue = Assert.IsAssignableFrom<ListBox>(Assert.Single(form.Controls.Find("queuePreview", true)));
        Assert.True(editor.Visible);
        Assert.True(cue.Visible);
        Assert.Equal(3, cue.Items.Count);
        Assert.Contains("First", cue.Items[0].ToString());
        Assert.Contains("5000", cue.Items[1].ToString());
        form.Width = 800;
        Application.DoEvents();
        Assert.Equal("First\n\n[wait 5s]\n\nSecond", editor.Text);
        Get<Button>(form, "previewQueue").PerformClick();
        Assert.True(cue.Visible);
        Assert.False(editor.Visible);
        Assert.True(cue.GetItemRectangle(0).Height < 100, "A short message should keep compact spacing after resize.");
        Get<Button>(form, "showScript").PerformClick();
        Assert.True(editor.Visible);
        form.Width = 1100;
        Application.DoEvents();
        Assert.True(editor.Visible);
        Assert.True(cue.Visible);
        Assert.True(Get<Button>(form, "startScript").Visible);
        Assert.True(Get<Button>(form, "startScript").Enabled);
    });

    [Fact]
    public void SettingsPreserveAdvancedTimingsAndStayReadOnlyDuringPlayback() => RunSta(() =>
    {
        using var dialog = new SpeakerSettingsDialog(new AppSettings { KeyGapMs = 180 }, 250, 150, "Example activity", true, () => { });
        ShowOffscreen(dialog);
        Assert.Equal(250, dialog.ChatOpenMs);
        Assert.Equal(150, dialog.SubmitMs);
        Assert.Equal(180, dialog.KeyGapMs);
        Assert.False(Get<NumericUpDown>(dialog, "settingsChatOpen").Enabled);
        Assert.False(Get<Button>(dialog, "applySettings").Enabled);
        Get<Button>(dialog, "troubleshooting").PerformClick();
        Assert.True(Get<TextBox>(dialog, "diagnostics").Visible);
        Assert.Equal("Example activity", Get<TextBox>(dialog, "diagnostics").Text);
    });

    [Fact]
    public void EditingAfterPlaybackClearsTheOldCompletionState() => RunSta(() =>
    {
        var input = new ControlledInput();
        input.FinishFirst.SetResult();
        using var form = CreatePlaybackForm(input);
        form.Width = 800;
        ShowOffscreen(form);
        Get<Button>(form, "startScript").PerformClick();
        PumpUntil(() => Get<Label>(form, "playbackStatus").Text == "Script finished");
        Get<RichTextBox>(form, "scriptEditor").Text = "A new speech";
        Get<Button>(form, "previewQueue").PerformClick();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        PumpUntil(() => elapsed.ElapsedMilliseconds > 200);
        Assert.Equal("Ready to start", Get<Label>(form, "playbackStatus").Text);
        Assert.Equal("Close local chat before starting.", Get<Label>(form, "playbackProgress").Text);
    });

    [Fact]
    public void VisualStatesKeepControlsReachableAndCapturePreviews() => RunSta(() =>
    {
        const string script = "Aislings, gather beneath the gentle light of Glioca.\nTonight we honor compassion, mercy, and patience.\n\n[wait 5s]\n\nMay kindness guide us.";
        var input = new ControlledInput();
        using var form = new SpeakerForm(new AppSettings { Script = script }, chatInput: input,
            discoverClients: () => [new((nint)123, 456, "Dark Ages")]);
        ShowOffscreen(form);
        Capture(form, "moonlit-ready");
        Get<Button>(form, "startScript").PerformClick();
        Capture(form, "moonlit-running");
        Get<Button>(form, "pauseScript").PerformClick();
        input.FinishFirst.SetResult();
        PumpUntil(() => Get<Button>(form, "startScript").Text == "Resume");
        Capture(form, "moonlit-paused");
        Get<Button>(form, "stopScript").PerformClick();
        PumpUntil(() => !Get<RichTextBox>(form, "scriptEditor").ReadOnly);
        form.Width = 760;
        form.Height = 740;
        Application.DoEvents();
        Capture(form, "moonlit-compact-script");
        Get<Button>(form, "previewQueue").PerformClick();
        Capture(form, "moonlit-compact-preview");
        AssertVisibleWithin(form, Get<Button>(form, "startScript"));
        AssertVisibleWithin(form, Get<Button>(form, "stopScript"));
        Get<Button>(form, "showScript").PerformClick();
        Get<RichTextBox>(form, "scriptEditor").Text = new string('x', 60);
        Get<Button>(form, "previewQueue").PerformClick();
        Capture(form, "moonlit-invalid");
        Assert.False(Get<Button>(form, "startScript").Enabled);
        Get<RichTextBox>(form, "scriptEditor").Clear();
        Get<Button>(form, "showScript").PerformClick();
        PumpUntil(() => Get<Label>(form, "playbackStatus").Text == "Make room for your words");
        Capture(form, "moonlit-empty");

        using var settings = new SpeakerSettingsDialog(new AppSettings(), 250, 150, form.ActivityLog, false, () => { });
        ShowOffscreen(settings);
        Capture(settings, "moonlit-settings");
        AssertVisibleWithin(settings, Get<Button>(settings, "applySettings"));
        Get<Button>(settings, "troubleshooting").PerformClick();
        Capture(settings, "moonlit-troubleshooting");
    });

    private static void AssertVisibleWithin(Form form, Control control)
    {
        Assert.True(control.Visible);
        var rect = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
        Assert.True(form.ClientRectangle.Contains(rect), $"{control.Name} is clipped: {rect}");
    }

    private static void Capture(Form form, string name)
    {
        Application.DoEvents();
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "DA-Speaker.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root.FullName, "artifacts", "design");
        Directory.CreateDirectory(directory);
        image.Save(Path.Combine(directory, name + ".png"));
    }

    [Fact]
    public void CustomCloseWaitsForCurrentMessageCleanup() => RunSta(() =>
    {
        var input = new ControlledInput();
        using var form = CreatePlaybackForm(input);
        ShowOffscreen(form);
        Get<Button>(form, "startScript").PerformClick();
        Get<Button>(form, "closeWindow").PerformClick();
        Assert.False(form.IsDisposed);
        Assert.True(Get<RichTextBox>(form, "scriptEditor").ReadOnly);
        input.FinishFirst.SetResult();
        PumpUntil(() => form.IsDisposed);
        Assert.Equal(new[] { "First" }, input.Messages);
    });

    [Theory]
    [InlineData(144)]
    [InlineData(192)]
    public void DpiChangeKeepsWorkspaceAndPlaybackReachable(int dpi) => RunSta(() =>
    {
        using var form = new SpeakerForm(new AppSettings { Script = new string('W', 59) },
            discoverClients: () => [new((nint)123, 456, "Dark Ages")]);
        ShowOffscreen(form);
        var bounds = form.Bounds;
        var originalFontSize = Get<RichTextBox>(form, "scriptEditor").Font.Size;
        var target = new NativeRectangle { Left = bounds.Left, Top = bounds.Top, Right = bounds.Left + bounds.Width * dpi / 96, Bottom = bounds.Top + bounds.Height * dpi / 96 };
        var memory = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf<NativeRectangle>());
        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(target, memory, false);
            SendMessageW(form.Handle, 0x02E0, (nint)(dpi | dpi << 16), memory);
            Application.DoEvents();
            Assert.Equal(dpi, form.DeviceDpi);
            Assert.Equal(originalFontSize * dpi / 96, Get<RichTextBox>(form, "scriptEditor").Font.Size, 1);
            AssertVisibleWithin(form, Get<Button>(form, "startScript"));
            AssertVisibleWithin(form, Get<Button>(form, "stopScript"));
            AssertVisibleWithin(form, Get<RichTextBox>(form, "scriptEditor"));
            Capture(form, $"dark-dpi-{dpi}");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(memory); }
    });

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRectangle { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint SendMessageW(nint window, uint message, nint wParam, nint lParam);

    private static T Get<T>(Control form, string name) where T : Control => Assert.IsAssignableFrom<T>(Assert.Single(form.Controls.Find(name, true)));
    private static void ShowOffscreen(Form form)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-20000, -20000);
        form.Show();
        Application.DoEvents();
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(3)) { Application.DoEvents(); Thread.Yield(); }
        Assert.True(condition(), "UI did not reach expected state.");
    }
    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
            host.Shown += (_, _) => host.BeginInvoke((Action)(() =>
            {
                try { action(); } catch (Exception ex) { failure = ex; }
                finally { host.Close(); }
            }));
            Application.Run(host);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private sealed class ControlledInput : IChatInput
    {
        public TaskCompletionSource FinishFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Messages { get; } = [];
        public List<InputMethod> Methods { get; } = [];
        public async Task SendAsync(ClientTarget target, string text, InputTimings timings, CancellationToken cancellationToken)
        {
            Messages.Add(text);
            Methods.Add(timings.InputMethod);
            if (Messages.Count == 1) await FinishFirst.Task;
        }
    }

    [Fact]
    public void EditorPreviewAndControlsUseSavedSettingsWithoutSendingInput()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var form = new SpeakerForm(new AppSettings { Script = "Aislings, gather beneath the gentle light of Glioca.\nTonight we honor compassion, mercy, and patience.\n\n[wait 5s]\n\nMay kindness guide us.", LineDelayMs = 3500 });
                Assert.Equal("DA Speaker", form.Text);
                var editor = Assert.IsType<RichTextBox>(Assert.Single(form.Controls.Find("scriptEditor", true)));
                Assert.Contains("Glioca", editor.Text);
                Assert.Equal(3.5m, Assert.IsType<NumericUpDown>(Assert.Single(form.Controls.Find("lineDelay", true))).Value);
                Assert.True(Assert.IsAssignableFrom<CheckBox>(Assert.Single(form.Controls.Find("autoWrap", true))).Checked);
                Assert.False(Assert.IsAssignableFrom<Button>(Assert.Single(form.Controls.Find("startScript", true))).Enabled);
                Assert.False(Assert.IsAssignableFrom<Button>(Assert.Single(form.Controls.Find("pauseScript", true))).Enabled);
                var preview = Assert.IsType<MessageCueList>(Assert.Single(form.Controls.Find("queuePreview", true)));
                Assert.Contains(preview.Items.Cast<QueueStep>(), s => !s.IsMessage && s.DelayMs == 5000);
                Assert.All(preview.Items.Cast<QueueStep>().Where(s => s.IsMessage), s => Assert.InRange(s.Text!.Length, 1, 59));
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show();
                Application.DoEvents();
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                var root = new DirectoryInfo(AppContext.BaseDirectory);
                while (root is not null && !File.Exists(Path.Combine(root.FullName, "DA-Speaker.slnx"))) root = root.Parent;
                Assert.NotNull(root);
                Directory.CreateDirectory(Path.Combine(root.FullName, "artifacts"));
                image.Save(Path.Combine(root.FullName, "artifacts", "speaker-preview.png"));
                form.Width = 800;
                Get<Button>(form, "previewQueue").PerformClick();
                Application.DoEvents();
                using var compactImage = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(compactImage, new Rectangle(Point.Empty, form.Size));
                compactImage.Save(Path.Combine(root.FullName, "artifacts", "queue-preview.png"));
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
