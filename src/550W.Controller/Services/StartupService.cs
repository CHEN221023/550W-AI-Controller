using Microsoft.Win32;

namespace Controller550W.Controller.Services;

public static class StartupService
{
    const string Name = "550W AI Controller";
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue(Name, $"\"{Environment.ProcessPath}\" --background");
        else key.DeleteValue(Name, false);
    }
}
