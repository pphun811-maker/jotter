using Microsoft.Win32;

namespace Jotter;

public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Jotter";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) as string == QuotedExePath();
    }

    public static void Set(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enable && Environment.ProcessPath is string exe)
            key.SetValue(ValueName, QuotedExePath());
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string QuotedExePath() => $"\"{Environment.ProcessPath}\"";
}
