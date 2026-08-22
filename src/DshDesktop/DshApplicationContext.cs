using System.Diagnostics;
using System.Windows.Forms;

namespace DshDesktop;

/// <summary>
/// 应用上下文：负责托盘图标、服务生命周期与主窗口之间的协调。
/// </summary>
public sealed class DshApplicationContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly DshServer _server;
    private readonly MainForm _form;
    private readonly NotifyIcon _trayIcon;
    private ToolStripMenuItem _startItem = null!;
    private ToolStripMenuItem _stopItem = null!;
    private ToolStripMenuItem _autoStartItem = null!;
    private bool _exiting;

    public DshApplicationContext(AppConfig config, bool trayMode)
    {
        _config = config;
        _server = new DshServer(config);
        _form = new MainForm(config);
        _form.FormClosed += (_, _) => OnFormClosed();

        _trayIcon = new NotifyIcon
        {
            Icon = IconUtil.LoadAppIcon(),
            Text = "DeepSeek Harness",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _trayIcon.DoubleClick += (_, _) => _form.ShowMainWindow();

        _server.StateChanged += (_, state) => OnServerStateChanged(state);

        if (!trayMode)
            _form.ShowMainWindow();

        _ = StartServerAsync();
    }

    public void ShowMainWindow() => _form.ShowMainWindow();

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("打开界面", null, (_, _) => _form.ShowMainWindow());
        _startItem = new ToolStripMenuItem("启动服务", null, async (_, _) => await StartServerAsync());
        _stopItem = new ToolStripMenuItem("停止服务", null, (_, _) => _server.Stop());
        _autoStartItem = new ToolStripMenuItem("开机自启", null, (_, _) => ToggleAutoStart())
        {
            Checked = AutoStart.IsEnabled(),
        };
        var logItem = new ToolStripMenuItem("查看日志", null, (_, _) => OpenLog());
        var dataItem = new ToolStripMenuItem("打开数据目录", null, (_, _) => OpenDataDir());
        var exitItem = new ToolStripMenuItem("退出", null, (_, _) => ExitApplication());

        menu.Items.AddRange(new ToolStripItem[]
        {
            openItem,
            _startItem,
            _stopItem,
            new ToolStripSeparator(),
            _autoStartItem,
            logItem,
            dataItem,
            new ToolStripSeparator(),
            exitItem,
        });

        UpdateMenu(ServerState.Stopped);
        return menu;
    }

    private void OnServerStateChanged(ServerState state)
    {
        _form.SetServerState(state);
        UpdateMenu(state);

        _trayIcon.Text = state switch
        {
            ServerState.Running => "DeepSeek Harness - 运行中",
            ServerState.Starting => "DeepSeek Harness - 启动中",
            _ => "DeepSeek Harness - 未运行",
        };

        if (state == ServerState.Running)
            _form.NavigateToApp();
    }

    private void UpdateMenu(ServerState state)
    {
        _startItem.Enabled = state is ServerState.Stopped or ServerState.Error;
        _stopItem.Enabled = state is ServerState.Starting or ServerState.Running;
        _autoStartItem.Checked = AutoStart.IsEnabled();
    }

    private async Task StartServerAsync()
    {
        try
        {
            await _server.StartAsync();
            if (_server.State == ServerState.Error)
            {
                _trayIcon.ShowBalloonTip(
                    5000, "DeepSeek Harness",
                    _server.LastError ?? "启动失败，请查看日志。",
                    ToolTipIcon.Error);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"启动服务异常: {ex}");
            _trayIcon.ShowBalloonTip(5000, "DeepSeek Harness", ex.Message, ToolTipIcon.Error);
        }
    }

    private void ToggleAutoStart()
    {
        var enable = !_autoStartItem.Checked;
        try
        {
            AutoStart.SetEnabled(enable);
            _autoStartItem.Checked = enable;
            AppLog.Write($"开机自启: {(enable ? "开启" : "关闭")}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"设置开机自启失败: {ex.Message}",
                "DeepSeek Harness",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void OpenLog()
    {
        try
        {
            Directory.CreateDirectory(AppConfig.LogDir);
            if (!File.Exists(AppConfig.LogFile))
                File.WriteAllText(AppConfig.LogFile, $"日志文件已创建: {DateTime.Now}{Environment.NewLine}");
            Process.Start(new ProcessStartInfo(AppConfig.LogFile) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Write($"打开日志失败: {ex.Message}");
        }
    }

    private void OpenDataDir()
    {
        try
        {
            Directory.CreateDirectory(AppConfig.DataDir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppConfig.DataDir}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLog.Write($"打开数据目录失败: {ex.Message}");
        }
    }

    private void ExitApplication()
    {
        if (_exiting)
            return;
        _exiting = true;

        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        if (_config.StopServerOnExit)
            _server.Stop();

        _form.ExitRequested = true;
        _form.Close();
    }

    private void OnFormClosed()
    {
        if (!_exiting)
        {
            // 系统关机/任务管理器结束进程时走到这里
            _exiting = true;
            if (_config.StopServerOnExit)
                _server.Stop();
        }

        _server.Dispose();
        ExitThread();
    }
}
