namespace DASpeaker;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetColorMode(SystemColorMode.Dark);
        ApplicationConfiguration.Initialize();
        if (args.Contains("--diagnostic", StringComparer.OrdinalIgnoreCase))
        {
            Application.Run(new DiagnosticForm());
            return;
        }
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DA Speaker", "settings.json");
        var store = new SettingsStore(path);
        var settings = store.Load(out var warning);
        Application.Run(new SpeakerForm(settings, store, warning));
    }
}
