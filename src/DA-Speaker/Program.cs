namespace DASpeaker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetColorMode(SystemColorMode.Dark);
        ApplicationConfiguration.Initialize();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DA Speaker", "settings.json");
        var store = new SettingsStore(path);
        var settings = store.Load(out var warning);
        Application.Run(new SpeakerForm(settings, store, warning));
    }
}
