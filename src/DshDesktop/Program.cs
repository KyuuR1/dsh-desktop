namespace DshDesktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var selfTest = args.Contains("--selftest", StringComparer.OrdinalIgnoreCase);
        var trayMode = args.Contains("--tray", StringComparer.OrdinalIgnoreCase);

        // 无界面自检模式：启动 dsh、等待 Web UI 就绪、停止并返回退出码
        if (selfTest)
            return SelfTest.Run();

        if (!SingleInstance.TryAcquire())
        {
            // 已有实例在运行：通知它把主窗口带到前台
            SingleInstance.NotifyExistingInstance();
            return 0;
        }

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var config = AppConfig.Load();

        using var context = new DshApplicationContext(config, trayMode);
        SingleInstance.StartPipeListener(context.ShowMainWindow);
        Application.Run(context);

        SingleInstance.Release();
        return 0;
    }
}
