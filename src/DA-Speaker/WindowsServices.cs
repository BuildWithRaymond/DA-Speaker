using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DASpeaker;

internal sealed class WindowsWindowApi : IWindowApi
{
    public IReadOnlyList<ClientTarget> FindClients()
    {
        var clients = new List<ClientTarget>();
        if (!Native.EnumWindows((handle, _) =>
        {
            if (GetClassName(handle) != "Darkages") return true;
            var pid = GetProcessId(handle);
            if (pid == 0) return true;
            var title = new StringBuilder(1024);
            Native.GetWindowText(handle, title, title.Capacity);
            clients.Add(new(handle, pid, title.ToString()));
            return true;
        }, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate client windows.");
        return clients.OrderBy(c => c.Title).ThenBy(c => c.ProcessId).ToArray();
    }

    public bool IsWindow(nint handle) => Native.IsWindow(handle);
    public string GetClassName(nint handle)
    {
        var name = new StringBuilder(256);
        return Native.GetClassName(handle, name, name.Capacity) == 0 ? "" : name.ToString();
    }
    public uint GetProcessId(nint handle)
    {
        Native.GetWindowThreadProcessId(handle, out var pid);
        return pid;
    }
    public uint MapScanCode(uint key) => Native.MapVirtualKey(key, 0);
    public short MapCharacter(nint handle, char character)
    {
        var thread = Native.GetWindowThreadProcessId(handle, out _);
        if (thread == 0) return -1;
        var layout = Native.GetKeyboardLayout(thread);
        return layout == 0 ? (short)-1 : Native.VkKeyScanEx(character, layout);
    }
    public bool Post(nint handle, KeyMessage message, out int error)
    {
        var result = Native.PostMessage(handle, message.Message, message.WParam, message.LParam);
        error = result ? 0 : Marshal.GetLastWin32Error();
        return result;
    }
    public bool Send(nint handle, KeyMessage message, out int error)
    {
        Marshal.SetLastSystemError(0);
        // BLOCK | ABORTIFHUNG | ERRORONEXIT. Never broadcast; caller supplies one validated HWND.
        var result = Native.SendMessageTimeout(handle, message.Message, message.WParam, message.LParam, 0x23, 1000, out _);
        error = result != 0 ? 0 : Marshal.GetLastWin32Error();
        return result != 0;
    }
}

internal sealed class WindowsClipboard : IClipboardService
{
    private sealed record Backup(DataObject? Data);

    internal static DataObject Materialize(IDataObject source)
    {
        var result = new DataObject();
        Type[] allowed = [typeof(string), typeof(string[]), typeof(byte[]), typeof(MemoryStream), typeof(Bitmap), typeof(int)];
        foreach (var format in source.GetFormats(autoConvert: false))
        {
            object? value;
            bool read;
            if (format == DataFormats.UnicodeText || format == DataFormats.Text || format == DataFormats.OemText ||
                format == DataFormats.Html || format == DataFormats.Rtf || format == DataFormats.CommaSeparatedValue || format == DataFormats.StringFormat)
            {
                read = source.TryGetData<string>(format, false, out var text);
                value = text;
            }
            else if (format == DataFormats.FileDrop || format is "FileName" or "FileNameW")
            {
                read = source.TryGetData<string[]>(format, false, out var paths);
                value = paths;
            }
            else if (format == DataFormats.Bitmap)
            {
                read = source.TryGetData<Bitmap>(format, false, out var bitmap);
                value = bitmap;
            }
            else
            {
                read = source.TryGetData<object>(format, name => allowed.FirstOrDefault(t => t.FullName == name.FullName), false, out value);
            }
            if (!read)
                throw new InvalidOperationException($"Cannot save clipboard format '{format}'. Copy plain text, then retry TEST CHAT.");
            object copy = value switch
            {
                string text => text,
                string[] paths => paths.Clone(),
                byte[] bytes => bytes.Clone(),
                MemoryStream stream => new MemoryStream(stream.ToArray()),
                Bitmap bitmap => bitmap.Clone(),
                int number => number,
                _ => throw new InvalidOperationException($"Cannot preserve clipboard format '{format}'. Copy plain text, then retry TEST CHAT.")
            };
            result.SetData(format, autoConvert: false, copy);
        }
        return result;
    }

    public async Task<object> CaptureAsync(CancellationToken cancellationToken)
    {
        Backup? backup = null;
        await RetryAsync(() =>
        {
            var sequence = Native.GetClipboardSequenceNumber();
            var data = Clipboard.GetDataObject();
            var saved = data is null ? null : Materialize(data);
            if (sequence != Native.GetClipboardSequenceNumber())
                throw new ExternalException("Clipboard changed while it was being saved.");
            backup = new(saved);
        }, "save", cancellationToken);
        return backup!;
    }

    public Task SetTextAsync(string text, CancellationToken cancellationToken) => RetryAsync(() =>
    {
        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        Clipboard.SetDataObject(data, copy: true, retryTimes: 0, retryDelay: 0);
    }, "set", cancellationToken);

    public Task RestoreAsync(object snapshot) => RetryAsync(() =>
    {
        var backup = (Backup)snapshot;
        if (backup.Data is null || backup.Data.GetFormats(false).Length == 0) Clipboard.Clear();
        else Clipboard.SetDataObject(backup.Data, copy: true, retryTimes: 0, retryDelay: 0);
    }, "restore", CancellationToken.None);

    private static async Task RetryAsync(Action operation, string verb, CancellationToken ct)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("Clipboard access must run on the STA UI thread.");
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { operation(); return; }
            catch (ExternalException ex)
            {
                if (attempt == 4) throw new InvalidOperationException($"Could not {verb} clipboard after 5 attempts. Close applications holding the clipboard and retry.", ex);
                await Task.Delay(50, ct);
            }
        }
    }
}

internal static class Native
{
    internal delegate bool EnumWindowsCallback(nint handle, nint parameter);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint handle);
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(nint handle, StringBuilder name, int maxCount);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(nint handle, StringBuilder text, int maxCount);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(nint handle, out uint processId);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    internal static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll")]
    internal static extern nint GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll", EntryPoint = "VkKeyScanExW", CharSet = CharSet.Unicode)]
    internal static extern short VkKeyScanEx(char character, nint keyboardLayout);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint handle, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    internal static extern nint SendMessageTimeout(nint handle, uint message, nuint wParam, nint lParam, uint flags, uint timeoutMs, out nuint result);
    [DllImport("user32.dll")]
    internal static extern uint GetClipboardSequenceNumber();
}
