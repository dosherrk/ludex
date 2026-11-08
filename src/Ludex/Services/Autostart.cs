using System;
using Microsoft.Win32;

// обращение к реестру всегда идёт через проверку OperatingSystem.IsWindows()

namespace Ludex.Services;

/// <summary>Автозапуск приложения при входе в Windows (ветка HKCU\...\Run).</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Ludex";
    private const string LegacyValueName = "GameVault";

    public static bool IsSupported => OperatingSystem.IsWindows();

#pragma warning disable CA1416
    public static bool IsEnabled()
    {
        if (!IsSupported) return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            if (key == null) return false;

            // запись прежней версии (GameVault) тоже считается включённым автозапуском
            return (key.GetValue(ValueName) is string value && value.Length > 0)
                || (key.GetValue(LegacyValueName) is string legacy && legacy.Length > 0);
        }
        catch (Exception ex)
        {
            AppPaths.Log("Autostart read failed: " + ex.Message);
            return false;
        }
    }

    public static bool Set(bool enabled)
    {
        if (!IsSupported) return false;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (key == null) return false;

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exe)) return false;

                key.SetValue(ValueName, $"\"{exe}\"");
                key.DeleteValue(LegacyValueName, false);
            }
            else
            {
                key.DeleteValue(ValueName, false);
                key.DeleteValue(LegacyValueName, false);
            }

            return IsEnabled() == enabled;
        }
        catch (Exception ex)
        {
            AppPaths.Log("Autostart write failed: " + ex.Message);
            return false;
        }
    }
#pragma warning restore CA1416
}
