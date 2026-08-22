using System.Text.Json;

namespace DshDesktop;

/// <summary>
/// 应用配置。默认位于 %APPDATA%\DshDesktop\settings.json。
/// 可用环境变量 DSH_DESKTOP_DATA_DIR 覆盖数据目录（便于便携使用或调试）。
/// </summary>
public sealed class AppConfig
{
    public string HarnessPath { get; set; } = @"D:\deepseek-harness";

    /// <summary>node.exe 的完整路径；留空时自动在 PATH 中查找。</summary>
    public string? NodePath { get; set; }

    /// <summary>dsh 子命令，默认 web。</summary>
    public string Command { get; set; } = "web";

    public int Port { get; set; } = 3080;

    public string Url { get; set; } = "http://127.0.0.1:3080";

    /// <summary>退出应用时是否同时停止 dsh 服务。</summary>
    public bool StopServerOnExit { get; set; } = true;

    public int StartupTimeoutSeconds { get; set; } = 90;

    public static string DataDir { get; } = ResolveDataDir();

    public static string LogDir => Path.Combine(DataDir, "logs");

    public static string LogFile => Path.Combine(LogDir, "dsh-desktop.log");

    public static string WebView2DataDir => Path.Combine(DataDir, "webview2");

    public static string ConfigPath => Path.Combine(DataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static string ResolveDataDir()
    {
        var overrideDir = Environment.GetEnvironmentVariable("DSH_DESKTOP_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
            return overrideDir;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DshDesktop");
    }

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (cfg is not null)
                    return cfg;
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"读取配置失败，使用默认配置: {ex.Message}");
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            AppLog.Write($"保存配置失败: {ex.Message}");
        }
    }
}
