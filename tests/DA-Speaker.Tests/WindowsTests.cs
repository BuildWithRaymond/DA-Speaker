using DASpeaker;
using System.Runtime.InteropServices;
using Xunit;

namespace DASpeaker.Tests;

public sealed class WindowsTests
{
    [Fact]
    public void PostedVGeneratesCharacterButDirectVDoesNot() => Sta(() =>
    {
        using var probe = new TranslationProbe();
        var api = new WindowsWindowApi();
        var down = KeyboardMessage.Create(0x56, api.MapScanCode(0x56), false);
        var up = KeyboardMessage.Create(0x56, api.MapScanCode(0x56), true);
        Assert.True(api.Post(probe.Handle, down, out _));
        Assert.True(api.Post(probe.Handle, up, out _));
        PumpProbe(probe.Handle);
        Assert.Contains(0x100, probe.Messages);
        Assert.Contains(0x102, probe.Messages);
        probe.Messages.Clear();
        Assert.True(api.Send(probe.Handle, down, out _));
        Assert.True(api.Send(probe.Handle, up, out _));
        PumpProbe(probe.Handle);
        Assert.Equal(new[] { 0x100, 0x101 }, probe.Messages);
    });

    private sealed class TranslationProbe : NativeWindow, IDisposable
    {
        public List<int> Messages { get; } = [];
        public TranslationProbe() => CreateHandle(new CreateParams { Caption = "DA Speaker translation test", Parent = (nint)(-3) });
        protected override void WndProc(ref Message m)
        {
            if (m.Msg is 0x100 or 0x101 or 0x102) Messages.Add(m.Msg);
            base.WndProc(ref m);
        }
        public void Dispose() => DestroyHandle();
    }

    private static void PumpProbe(nint handle)
    {
        // A conventional native message loop, isolated to this private window.
        while (PeekMessage(out var message, handle, 0, 0, 1))
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Handle;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X, Y;
        public uint Private;
    }
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out NativeMessage message, nint handle, uint min, uint max, uint remove);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static extern nint DispatchMessage(ref NativeMessage message);

    [Fact]
    public void NativeGuardIdentifiesAnOrdinaryWindowWithoutSendingInput() => Sta(() =>
    {
        using var form = new Form();
        var api = new WindowsWindowApi();
        Assert.True(api.IsWindow(form.Handle));
        Assert.Equal((uint)System.Environment.ProcessId, api.GetProcessId(form.Handle));
        Assert.NotEqual("Darkages", api.GetClassName(form.Handle));
        Assert.Equal(0x1Cu, api.MapScanCode(0x0D));
        Assert.All(api.FindClients(), c =>
        {
            Assert.Equal("Darkages", api.GetClassName(c.Handle));
            Assert.Equal(c.ProcessId, api.GetProcessId(c.Handle));
        });
    });

    [Fact]
    public void ClipboardSnapshotPreservesMultipleFormatsWithoutTouchingSystemClipboard() => Sta(() =>
    {
        var original = new DataObject();
        original.SetData(DataFormats.UnicodeText, false, "Original text");
        original.SetData(DataFormats.Html, false, "<b>Original text</b>");
        using var sourceStream = new MemoryStream([1, 2, 3]);
        original.SetData("DA Speaker test bytes", false, sourceStream);
        var snapshot = WindowsClipboard.Materialize(original);
        sourceStream.WriteByte(9);
        Assert.True(snapshot.TryGetData<string>(DataFormats.UnicodeText, false, out var text));
        Assert.Equal("Original text", text);
        Assert.True(snapshot.TryGetData<string>(DataFormats.Html, false, out var html));
        Assert.Equal("<b>Original text</b>", html);
        Assert.True(snapshot.TryGetData<MemoryStream>("DA Speaker test bytes", false, out var copied));
        Assert.Equal(new byte[] { 1, 2, 3 }, copied.ToArray());
    });

    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA test timed out.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
