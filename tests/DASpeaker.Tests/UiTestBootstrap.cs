using System.Runtime.CompilerServices;

namespace DASpeaker.Tests;

internal static class UiTestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetColorMode(SystemColorMode.Dark);
        Application.EnableVisualStyles();
    }
}
