using DASpeaker;
using Xunit;

namespace DASpeaker.Tests;

public sealed class InputTests
{
    private static readonly ClientTarget Target = new((nint)123, 456, "Test client");

    [Fact]
    public void KeyMessagesUseScanCodesAndSignedKeyUpBits()
    {
        var down = KeyboardMessage.Create(0x0D, 0x1C, false);
        var up = KeyboardMessage.Create(0x0D, 0x1C, true);
        Assert.Equal(new KeyMessage(0x100, 0x0D, (nint)0x001C0001), down);
        Assert.Equal(new KeyMessage(0x101, 0x0D, (nint)unchecked((int)0xC01C0001)), up);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(256u)]
    public void InvalidScanCodeRejected(uint scanCode) => Assert.Throws<ArgumentOutOfRangeException>(() => KeyboardMessage.Create(13, scanCode, false));

    [Fact]
    public async Task SendsExactSequenceWithClipboardRestoration()
    {
        var env = new Environment();
        await env.Input.SendAsync(Target, "GLIOCA TEST", new(), default);
        Assert.Equal(new[] { "capture", "down:13", "wait:10", "up:13", "wait:10", "wait:250", "text:GLIOCA TEST", "wait:50", "down:17", "wait:10", "down:86", "wait:10", "up:86", "wait:10", "up:17", "wait:10", "wait:150", "down:13", "wait:10", "up:13", "wait:10", "wait:100", "restore" }, env.Events);
        Assert.Equal("original", env.Clipboard);
        Assert.All(env.Posts, p => Assert.Equal(Target.Handle, p.Handle));
    }

    [Theory]
    [InlineData(false, "Darkages", 456u)]
    [InlineData(true, "Notepad", 456u)]
    [InlineData(true, "darkages", 456u)]
    [InlineData(true, "Darkages", 999u)]
    public async Task InvalidTargetNeverTouchesClipboardOrPosts(bool exists, string className, uint pid)
    {
        var env = new Environment { Exists = exists, ClassName = className, Pid = pid };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => env.Input.SendAsync(Target, "GLIOCA TEST", new(), default));
        Assert.Contains("Dark Ages client is no longer available.", error.Message);
        Assert.Empty(env.Events);
    }

    [Fact]
    public async Task TargetLostDuringWaitStopsAndRestoresClipboard()
    {
        var env = new Environment();
        env.OnWait = ms => { if (ms == 150) env.Exists = false; };
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Input.SendAsync(Target, "GLIOCA TEST", new(), default));
        Assert.Equal(2, env.Posts.Count);
        Assert.Equal("original", env.Clipboard);
    }

    [Fact]
    public async Task DefaultInputSendsCtrlVInOrderAndKeepsEnterOnProvenPath()
    {
        var env = new Environment();
        await env.Input.SendAsync(Target, "GLIOCA TEST", new(), default);
        Assert.Equal(4, env.Posts.Count);
        Assert.All(env.Posts, p => Assert.Equal((nuint)13, p.Message.WParam));
        Assert.Equal(new uint[] { 17, 86, 86, 17 }, env.Sends.Select(p => (uint)p.Message.WParam));
        Assert.Equal(new uint[] { 0x100, 0x100, 0x101, 0x101 }, env.Sends.Select(p => p.Message.Message));
        Assert.All(env.Sends, p => Assert.Equal(Target.Handle, p.Handle));
        Assert.Equal("original", env.Clipboard);
    }

    [Fact]
    public async Task HoldsMessageClipboardFor100MsAfterSubmitEnter()
    {
        var env = new Environment();
        var observed = false;
        env.OnWait = ms =>
        {
            if (ms != 100) return;
            observed = true;
            Assert.Equal("GLIOCA TEST", env.Clipboard);
            Assert.Equal(4, env.Posts.Count);
            Assert.Equal((uint)0x101, env.Posts[^1].Message.Message);
            Assert.Equal((nuint)13, env.Posts[^1].Message.WParam);
            Assert.DoesNotContain("restore", env.Events);
        };
        await env.Input.SendAsync(Target, "GLIOCA TEST", new(), default);
        Assert.True(observed, "Missing delay after submit Enter.");
        Assert.Equal(new[] { "up:13", "wait:10", "wait:100", "restore" }, env.Events.TakeLast(4));
        Assert.Equal("original", env.Clipboard);
    }

    [Fact]
    public async Task TargetLostDuringSubmitSettleStillRestoresClipboard()
    {
        var env = new Environment();
        env.OnWait = ms => { if (ms == 100) env.Exists = false; };
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Input.SendAsync(Target, "GLIOCA TEST", new(), default));
        Assert.Equal("original", env.Clipboard);
        Assert.Equal("restore", env.Events.Last());
    }

    [Fact]
    public async Task DirectPasteFailureReleasesControlAndRestoresClipboard()
    {
        var env = new Environment { FailSend = 2 };
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => env.Input.SendAsync(Target, "GLIOCA TEST", new(), default));
        Assert.Contains("SendMessageTimeoutW", failure.Message);
        Assert.Contains("up:17", env.Events);
        Assert.All(env.Posts, p => Assert.Equal((nuint)13, p.Message.WParam));
        Assert.Equal("original", env.Clipboard);
    }

    [Fact]
    public async Task FailedSubmitRestoresClipboardAndReportsNativeError()
    {
        var env = new Environment { FailPost = 3 };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => env.Input.SendAsync(Target, "GLIOCA TEST", new(), default));
        Assert.Contains("PostMessageW", error.Message);
        Assert.Contains("5", error.Message);
        Assert.Contains("elevation", error.Message);
        Assert.Contains("up:17", env.Events);
        Assert.Equal("original", env.Clipboard);
        Assert.Equal("restore", env.Events.Last());
    }

    [Fact]
    public async Task CancellationAfterControlDownReleasesControlAndRestoresClipboard()
    {
        using var cts = new CancellationTokenSource();
        var env = new Environment();
        env.OnWait = _ => { if (env.Events.Contains("down:17")) cts.Cancel(); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => env.Input.SendAsync(Target, "GLIOCA TEST", new(), cts.Token));
        Assert.Contains("up:17", env.Events);
        Assert.DoesNotContain("down:86", env.Events);
        Assert.Equal("original", env.Clipboard);
    }

    [Fact]
    public async Task CaptureFailureDoesNotOpenChat()
    {
        var env = new Environment { CaptureFails = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Input.SendAsync(Target, "GLIOCA TEST", new(), default));
        Assert.Empty(env.Posts);
    }

    [Fact]
    public async Task MappingFailureDoesNotOpenChat()
    {
        var env = new Environment { ScanCode = 0 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Input.SendAsync(Target, "GLIOCA TEST", new(), default));
        Assert.Empty(env.Events);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello\nworld")]
    [InlineData("café")]
    [InlineData("123456789012345678901234567890123456789012345678901234567890")]
    public async Task InvalidChatTextRejectedBeforeInput(string text)
    {
        var env = new Environment();
        await Assert.ThrowsAsync<ArgumentException>(() => env.Input.SendAsync(Target, text, new(), default));
        Assert.Empty(env.Events);
    }

    [Fact]
    public async Task RestoreFailureIsNotReportedAsSuccess()
    {
        var env = new Environment { RestoreFails = true };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => env.Input.SendAsync(Target, "GLIOCA TEST", new(), default));
        Assert.Contains("restore", error.Message);
    }

    private sealed class Environment : IWindowApi, IClipboardService, IInputDelay
    {
        public List<string> Events { get; } = [];
        public List<(nint Handle, KeyMessage Message)> Posts { get; } = [];
        public List<(nint Handle, KeyMessage Message)> Sends { get; } = [];
        public bool Exists = true;
        public string ClassName = "Darkages";
        public uint Pid = 456;
        public uint ScanCode = 28;
        public int FailPost;
        public int FailSend;
        public bool CaptureFails;
        public bool RestoreFails;
        public string Clipboard = "original";
        public Action<int>? OnWait;
        public ChatInput Input { get; }
        public Environment() => Input = new(this, this, this, _ => { });
        public bool IsWindow(nint h) => Exists;
        public string GetClassName(nint h) => ClassName;
        public uint GetProcessId(nint h) => Pid;
        public uint MapScanCode(uint key) => ScanCode;
        public bool Post(nint h, KeyMessage m, out int error)
        {
            Posts.Add((h, m));
            Events.Add($"{(m.Message == 0x100 ? "down" : "up")}:{m.WParam}");
            error = Posts.Count == FailPost ? 5 : 0;
            return error == 0;
        }
        public bool Send(nint h, KeyMessage m, out int error)
        {
            Sends.Add((h, m));
            Events.Add($"{(m.Message == 0x100 ? "down" : "up")}:{m.WParam}");
            error = Sends.Count == FailSend ? 5 : 0;
            return error == 0;
        }
        public Task<object> CaptureAsync(CancellationToken ct)
        {
            if (CaptureFails) throw new InvalidOperationException("clipboard capture failed");
            Events.Add("capture");
            return Task.FromResult<object>(Clipboard);
        }
        public Task SetTextAsync(string text, CancellationToken ct)
        {
            Events.Add($"text:{text}");
            Clipboard = text;
            return Task.CompletedTask;
        }
        public Task RestoreAsync(object snapshot)
        {
            Events.Add("restore");
            if (RestoreFails) throw new InvalidOperationException("clipboard restore failed");
            Clipboard = (string)snapshot;
            return Task.CompletedTask;
        }
        public Task WaitAsync(int ms, CancellationToken ct)
        {
            Events.Add($"wait:{ms}");
            OnWait?.Invoke(ms);
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
