using System.Runtime.InteropServices;

namespace DASpeaker;

internal interface IHotkeyApi
{
    bool Register(nint window, int id, uint key, out int error);
    void Unregister(nint window, int id);
}

internal sealed class GlobalHotkeys(IHotkeyApi api)
{
    public const int StartId = 0x5318, PauseId = 0x5319, StopId = 0x5320;
    public bool Enabled { get; private set; }
    private readonly List<int> registered = [];
    public string? Enable(nint window)
    {
        if (Enabled) return null;
        (int Id, uint Key, string Name)[] keys = [(StartId, 0x77, "F8"), (PauseId, 0x78, "F9"), (StopId, 0x79, "F10")];
        foreach (var key in keys)
        {
            if (!api.Register(window, key.Id, key.Key, out var error))
            {
                Disable(window);
                return $"Could not register {key.Name} (Win32 error {error}). Another application may use it. Global hotkeys are disabled.";
            }
            registered.Add(key.Id);
        }
        Enabled = true;
        return null;
    }
    public void Disable(nint window)
    {
        foreach (var id in registered) api.Unregister(window, id);
        registered.Clear();
        Enabled = false;
    }
}

internal sealed class WindowsHotkeyApi : IHotkeyApi
{
    public bool Register(nint window, int id, uint key, out int error)
    {
        var result = RegisterHotKey(window, id, 0x4000, key); // MOD_NOREPEAT
        error = result ? 0 : Marshal.GetLastWin32Error();
        return result;
    }
    public void Unregister(nint window, int id) => UnregisterHotKey(window, id);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
}
