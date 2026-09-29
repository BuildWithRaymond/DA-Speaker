using System.Text.Json;

namespace DASpeaker;

internal sealed record AppSettings
{
    public string Script { get; init; } = "";
    public int LineDelayMs { get; init; } = 3500;
    public int ParagraphDelayMs { get; init; } = 5000;
    public int ChatOpenDelayMs { get; init; } = 250;
    public int SubmitDelayMs { get; init; } = 150;
    public int KeyGapMs { get; init; } = 10;
    public bool AutoWrap { get; init; } = true;
    public bool HotkeysEnabled { get; init; }
    public int Width { get; init; } = 1060;
    public int Height { get; init; } = 800;
    public int? Left { get; init; }
    public int? Top { get; init; }
    public long LastHandle { get; init; }
    public uint LastPid { get; init; }
    public string LastTitle { get; init; } = "";
}

internal sealed class SettingsStore(string path)
{
    public AppSettings Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(path)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
            return settings with
            {
                Script = settings.Script ?? "",
                LastTitle = settings.LastTitle ?? "",
                LineDelayMs = Math.Clamp(settings.LineDelayMs, 0, 86400000),
                ParagraphDelayMs = Math.Clamp(settings.ParagraphDelayMs, 0, 86400000),
                ChatOpenDelayMs = Math.Clamp(settings.ChatOpenDelayMs, 0, 10000),
                SubmitDelayMs = Math.Clamp(settings.SubmitDelayMs, 0, 10000),
                KeyGapMs = Math.Clamp(settings.KeyGapMs, 0, 1000),
                Width = Math.Clamp(settings.Width, 760, 2000),
                Height = Math.Clamp(settings.Height, 740, 2000)
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = "Settings could not be read. Defaults loaded: " + ex.Message;
            return new();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
    }
}
