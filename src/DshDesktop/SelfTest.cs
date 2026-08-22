namespace DshDesktop;

/// <summary>
/// 无界面自检模式（--selftest）：
/// 启动 dsh、等待 Web UI 就绪、停止服务；退出码 0 表示成功。
/// 详情见数据目录下的日志文件。
/// </summary>
public static class SelfTest
{
    public static int Run()
    {
        var config = AppConfig.Load();
        AppLog.Write("=== 自检开始 ===");

        using var server = new DshServer(config);
        try
        {
            server.StartAsync().GetAwaiter().GetResult();
            if (server.State != ServerState.Running)
                throw new InvalidOperationException(server.LastError ?? "服务未能进入运行状态");

            AppLog.Write("自检通过：Web UI 已就绪");
            server.Stop();
            AppLog.Write("=== 自检结束（成功） ===");
            return 0;
        }
        catch (Exception ex)
        {
            AppLog.Write($"自检失败: {ex}");
            server.Stop();
            return 1;
        }
    }
}
