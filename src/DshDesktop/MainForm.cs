using System.Diagnostics;
using System.Drawing;
using System.ComponentModel;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DshDesktop;

/// <summary>主窗口：WebView2 嵌入 dsh 的 Web UI。</summary>
public sealed class MainForm : Form
{
    private readonly AppConfig _config;
    private readonly WebView2 _webView = new();
    private readonly Label _loadingLabel = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripStatusLabel _stateLabel = new();
    private bool _webViewReady;

    /// <summary>为 true 时允许窗口真正关闭（退出应用）；否则关闭仅隐藏到托盘。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ExitRequested { get; set; }

    public MainForm(AppConfig config)
    {
        _config = config;
        BuildUi();
    }

    private void BuildUi()
    {
        Text = "DeepSeek Harness";
        Icon = IconUtil.LoadAppIcon();
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(720, 480);
        StartPosition = FormStartPosition.CenterScreen;

        _webView.Dock = DockStyle.Fill;

        _loadingLabel.Dock = DockStyle.Fill;
        _loadingLabel.TextAlign = ContentAlignment.MiddleCenter;
        _loadingLabel.Font = new Font("Microsoft YaHei UI", 12f);
        _loadingLabel.Text = "正在启动 DeepSeek Harness...";

        _statusLabel.Text = "就绪";
        _statusLabel.Spring = true;
        _stateLabel.Text = "服务：未运行";

        _statusStrip.Items.Add(_statusLabel);
        _statusStrip.Items.Add(_stateLabel);

        Controls.Add(_webView);
        Controls.Add(_loadingLabel);
        Controls.Add(_statusStrip);

        Shown += async (_, _) => await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: AppConfig.WebView2DataDir);

            await _webView.EnsureCoreWebView2Async(env);
            _webViewReady = true;

            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;

            _webView.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                if (e.IsSuccess)
                    BeginInvoke(() => _loadingLabel.Visible = false);
            };

            // 外部链接交给系统默认浏览器
            _webView.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                try
                {
                    if (!string.IsNullOrEmpty(e.Uri))
                        Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
                }
                catch
                {
                    // 忽略无法打开的链接
                }
            };

            NavigateToApp();
        }
        catch (Exception ex)
        {
            AppLog.Write($"WebView2 初始化失败: {ex}");
            SetStatus("WebView2 初始化失败，请安装 WebView2 运行时（见 README）。");
            _loadingLabel.Text = "WebView2 初始化失败。\r\n请安装 Microsoft Edge WebView2 运行时后重试。\r\n详情见 README.md";
        }
    }

    public void NavigateToApp()
    {
        if (!_webViewReady)
        {
            SetStatus("等待 WebView2 初始化...");
            return;
        }

        try
        {
            _webView.CoreWebView2.Navigate(_config.Url);
            SetStatus($"已连接 {_config.Url}");
        }
        catch (Exception ex)
        {
            SetStatus($"导航失败: {ex.Message}");
        }
    }

    public void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(message));
            return;
        }
        _statusLabel.Text = message;
    }

    public void SetServerState(ServerState state)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetServerState(state));
            return;
        }

        _stateLabel.Text = state switch
        {
            ServerState.Starting => "服务：启动中",
            ServerState.Running => "服务：运行中",
            ServerState.Stopping => "服务：停止中",
            ServerState.Error => "服务：启动失败",
            _ => "服务：未运行",
        };
    }

    public void ShowMainWindow()
    {
        if (InvokeRequired)
        {
            BeginInvoke(ShowMainWindow);
            return;
        }
        if (IsDisposed)
            return;

        Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing)
        {
            base.OnFormClosing(e);
            return;
        }

        if (!ExitRequested)
        {
            // 点“×”只隐藏到托盘
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }
}
