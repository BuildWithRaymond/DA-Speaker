namespace DASpeaker;

internal sealed record ClientTarget(nint Handle, uint ProcessId, string Title)
{
    public override string ToString() => $"{(string.IsNullOrWhiteSpace(Title) ? "Dark Ages" : Title)} / PID {ProcessId} / HWND 0x{Handle:X}";
}

internal sealed record InputTimings(int ChatOpenDelayMs = 250, int PasteDelayMs = 150, int KeyGapMs = 10);
internal readonly record struct KeyMessage(uint Message, nuint WParam, nint LParam);

internal interface IWindowApi
{
    bool IsWindow(nint handle);
    string GetClassName(nint handle);
    uint GetProcessId(nint handle);
    uint MapScanCode(uint key);
    bool Post(nint handle, KeyMessage message, out int error);
    bool Send(nint handle, KeyMessage message, out int error);
}

internal interface IClipboardService
{
    Task<object> CaptureAsync(CancellationToken cancellationToken);
    Task SetTextAsync(string text, CancellationToken cancellationToken);
    Task RestoreAsync(object snapshot);
}

internal interface IInputDelay
{
    Task WaitAsync(int milliseconds, CancellationToken cancellationToken);
}

internal interface IChatInput
{
    Task SendAsync(ClientTarget target, string text, InputTimings timings, CancellationToken cancellationToken);
}

internal static class KeyboardMessage
{
    // Input-message reference: ewrogers/SleepHunter4. See THIRD_PARTY_NOTICES.md.
    public static KeyMessage Create(uint key, uint scanCode, bool keyUp)
    {
        if (scanCode is 0 or > 255) throw new ArgumentOutOfRangeException(nameof(scanCode));
        var bits = 1u | (scanCode << 16);
        if (keyUp) bits |= (1u << 30) | (1u << 31);
        return new(keyUp ? 0x0101u : 0x0100u, key, new nint(unchecked((int)bits)));
    }
}

internal sealed class ChatInput(IWindowApi windows, IClipboardService clipboard, IInputDelay delay, Action<string> log) : IChatInput
{
    private int active;

    public async Task SendAsync(ClientTarget target, string text, InputTimings timings, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
            throw new InvalidOperationException("An input operation is already running.");
        try { await SendCoreAsync(target, text, timings, cancellationToken); }
        finally { Volatile.Write(ref active, 0); }
    }

    private async Task SendCoreAsync(ClientTarget target, string text, InputTimings timings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 59 || text.Any(c => c is < ' ' or > '~'))
            throw new ArgumentException("Chat text must contain 1–59 printable ASCII characters on one line.", nameof(text));
        if (timings.ChatOpenDelayMs is < 0 or > 10000 || timings.PasteDelayMs is < 0 or > 10000 || timings.KeyGapMs is < 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(timings));
        ct.ThrowIfCancellationRequested();
        Validate(target);

        uint[] keys = [0x0D, 0x11, 0x56];
        var scans = keys.ToDictionary(k => k, k => windows.MapScanCode(k));
        if (scans.Values.Any(s => s is 0 or > 255))
            throw new InvalidOperationException("Keyboard scan-code mapping failed. No input was posted.");

        var held = new List<uint>();
        var snapshot = await clipboard.CaptureAsync(ct);
        Exception? failure = null;
        try
        {
            log("Clipboard saved. Opening local chat.");
            await Key(0x0D, false);
            await Key(0x0D, true);
            await Wait(timings.ChatOpenDelayMs);
            await clipboard.SetTextAsync(text, ct);
            log("Clipboard set to current message.");
            await Wait(50);
            log("Pasting with direct Ctrl+V.");
            await Key(0x11, false);
            await Key(0x56, false);
            await Key(0x56, true);
            await Key(0x11, true);
            await Wait(timings.PasteDelayMs);
            await Key(0x0D, false);
            await Key(0x0D, true);
            log("Submit posted. Game delivery requires visual verification.");
            log("Waiting 100 ms after submit Enter before clipboard restore.");
            await Wait(100);
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            // Cleanup does not use the cancelled token. Never release into a reused HWND.
            foreach (var key in held.AsEnumerable().Reverse())
            {
                try
                {
                    Validate(target);
                    await DispatchAsync(target, KeyboardMessage.Create(key, scans[key], true), key != 0x0D);
                }
                catch (Exception ex)
                {
                    log($"Key release failed: {ex.Message}");
                    failure = Combine(failure, ex);
                }
            }
            try
            {
                await clipboard.RestoreAsync(snapshot);
                log("Clipboard restored.");
            }
            catch (Exception ex)
            {
                log($"Clipboard restore failed: {ex.Message}");
                failure = Combine(failure, ex);
            }
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();

        async Task Wait(int milliseconds)
        {
            await delay.WaitAsync(milliseconds, ct);
            ct.ThrowIfCancellationRequested();
            Validate(target);
        }

        async Task Key(uint key, bool up)
        {
            ct.ThrowIfCancellationRequested();
            Validate(target);
            // A timed-out SendMessage may already have reached the receiver.
            // Track down keys before attempting dispatch so cleanup also covers that case.
            if (!up) held.Add(key);
            await DispatchAsync(target, KeyboardMessage.Create(key, scans[key], up), key != 0x0D);
            if (up) held.Remove(key);
            await Wait(timings.KeyGapMs);
        }
    }

    private void Validate(ClientTarget target)
    {
        if (target.Handle == 0 || target.ProcessId == 0 || !windows.IsWindow(target.Handle) ||
            windows.GetProcessId(target.Handle) != target.ProcessId || windows.GetClassName(target.Handle) != "Darkages")
            throw new InvalidOperationException("Dark Ages client is no longer available.");
    }

    private async Task DispatchAsync(ClientTarget target, KeyMessage message, bool direct)
    {
        bool succeeded;
        int error;
        if (direct)
        {
            // Keep the STA UI/clipboard thread responsive during the bounded native wait.
            (succeeded, error) = await Task.Run(() =>
            {
                Validate(target);
                var result = windows.Send(target.Handle, message, out var code);
                return (result, code);
            });
        }
        else succeeded = windows.Post(target.Handle, message, out error);
        var api = direct ? "SendMessageTimeoutW" : "PostMessageW";
        if (!succeeded)
        {
            var hint = error == 5 ? " DASpeaker likely needs the same elevation level as Dark Ages." : "";
            var reason = error == 0 ? "No error code supplied; timeout or generic failure." : $"Win32 error {error} ({new System.ComponentModel.Win32Exception(error).Message}).";
            var detail = $"{api} failed: {reason}{hint}";
            log(detail);
            throw new InvalidOperationException(detail);
        }
        log($"{(direct ? "SEND" : "POST")} {(message.Message == 0x100 ? "DOWN" : "UP")} VK=0x{message.WParam:X2} lParam=0x{unchecked((uint)(long)message.LParam):X8} HWND=0x{target.Handle:X}");
    }

    private static Exception Combine(Exception? first, Exception next) => first is null ? next : new AggregateException(first, next);
}

internal sealed class InputDelay(TimeProvider timeProvider) : IInputDelay
{
    public Task WaitAsync(int milliseconds, CancellationToken cancellationToken) => Task.Delay(TimeSpan.FromMilliseconds(milliseconds), timeProvider, cancellationToken);
}
