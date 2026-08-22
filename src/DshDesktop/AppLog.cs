namespace DshDesktop;

/// <summary>简单的文本日志，写入数据目录下的 logs/dsh-desktop.log。</summary>
public static class AppLog
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppConfig.LogDir);
                File.AppendAllText(
                    AppConfig.LogFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
