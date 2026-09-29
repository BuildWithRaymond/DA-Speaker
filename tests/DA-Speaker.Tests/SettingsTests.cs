using DASpeaker;
using Xunit;

namespace DASpeaker.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void SavesAndLoadsDraftAndPreferencesAtomically()
    {
        var path = Path.Combine(Path.GetTempPath(), $"da-speaker-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            var expected = new AppSettings { Script = "Hello\n[wait 5s]", LineDelayMs = 4200, HotkeysEnabled = true, LastHandle = 123, LastPid = 456, Left = -1200, Top = 40 };
            store.Save(expected);
            Assert.Equal(expected, store.Load(out var warning));
            Assert.Null(warning);
            store.Save(expected with { Script = "Changed" });
            Assert.True(File.Exists(path + ".bak"));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { File.Delete(path); File.Delete(path + ".bak"); File.Delete(path + ".tmp"); }
    }

    [Fact]
    public void CorruptSettingsFallBackWithWarning()
    {
        var path = Path.Combine(Path.GetTempPath(), $"da-speaker-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "not json");
            var settings = new SettingsStore(path).Load(out var warning);
            Assert.Equal(3500, settings.LineDelayMs);
            Assert.NotNull(warning);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void OutOfRangePreferencesAreClampedWithoutLosingDraft()
    {
        var path = Path.Combine(Path.GetTempPath(), $"da-speaker-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"Script\":\"Keep me\",\"LineDelayMs\":-99,\"KeyGapMs\":99999,\"Width\":2}");
            var settings = new SettingsStore(path).Load(out _);
            Assert.Equal("Keep me", settings.Script);
            Assert.Equal(0, settings.LineDelayMs);
            Assert.Equal(1000, settings.KeyGapMs);
            Assert.True(settings.Width >= 760);
        }
        finally { File.Delete(path); }
    }
}
