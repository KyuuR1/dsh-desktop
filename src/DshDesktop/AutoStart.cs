using Microsoft.Win32;

namespace DshDesktop;

/// <summary>通过 HKCU 注册表 Run 键实现“登录时以 --tray 模式启动”。</summary>
public static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DshDesktop";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
                return;

            if (enable)
            {
                var exe = Environment.ProcessPath
                          ?? Path.Combine(AppContext.BaseDirectory, "DshDesktop.exe");
                key.SetValue(ValueName, $"\"{exe}\" --tray");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"设置开机自启失败: {ex.Message}");
            throw;
        }
    }
}
