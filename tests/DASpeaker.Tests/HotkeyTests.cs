using DASpeaker;
using Xunit;

namespace DASpeaker.Tests;

public sealed class HotkeyTests
{
    [Fact]
    public void ConflictUnregistersEarlierKeysAndReportsKey()
    {
        var api = new FakeApi { FailId = GlobalHotkeys.PauseId };
        var keys = new GlobalHotkeys(api);
        Assert.Contains("F9", keys.Enable(123));
        Assert.False(keys.Enabled);
        Assert.Contains(GlobalHotkeys.StartId, api.Removed);
    }
    [Fact]
    public void DisableReleasesAllRegisteredKeys()
    {
        var api = new FakeApi();
        var keys = new GlobalHotkeys(api);
        Assert.Null(keys.Enable(123));
        Assert.True(keys.Enabled);
        keys.Disable(123);
        Assert.Equal(new[] { 0x77u, 0x78u, 0x79u }, api.Keys);
        Assert.Equal(3, api.Removed.Count);
        Assert.False(keys.Enabled);
    }
    private sealed class FakeApi : IHotkeyApi
    {
        public int FailId;
        public List<int> Removed { get; } = [];
        public List<uint> Keys { get; } = [];
        public bool Register(nint w, int id, uint key, out int error) { Keys.Add(key); error = id == FailId ? 1409 : 0; return error == 0; }
        public void Unregister(nint w, int id) => Removed.Add(id);
    }
}
