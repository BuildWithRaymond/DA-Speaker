namespace DASpeaker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetColorMode(SystemColorMode.Dark);
        ApplicationConfiguration.Initialize();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = Path.Combine(appData, "DASpeaker", "settings.json");
        var legacyPath = Path.Combine(appData, "DA Speaker", "settings.json");
        var store = new SettingsStore(path, legacyPath);
        var settings = store.Load(out var warning);
        Application.Run(new SpeakerForm(settings, store, warning));
    }
}
