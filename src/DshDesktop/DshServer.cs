using System.Diagnostics;
using System.Net.Sockets;

namespace DshDesktop;

/// <summary>
/// dsh 服务进程管理：无窗口启动 node（等价于 pnpm dsh web）、
/// 轮询端口确认就绪、停止时结束整棵进程树。
/// </summary>
public sealed class DshServer : IDisposable
{
    private readonly AppConfig _config;
    private readonly object _gate = new();
    private Process? _process;
    private System.Threading.Timer? _healthTimer;
    private bool _disposed;

    public ServerState State { get; private set; } = ServerState.Stopped;

    public string? LastError { get; private set; }

    public event EventHandler<ServerState>? StateChanged;

    public DshServer(AppConfig config) => _config = config;

    public static bool IsPortOpen(int port, string host = "127.0.0.1")
    {
        using var client = new TcpClient();
        try
        {
            var task = client.ConnectAsync(host, port);
            return task.Wait(500) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public async Task StartAsync()
    {
        lock (_gate)
        {
            if (State is ServerState.Starting or ServerState.Running or ServerState.Stopping)
                return;
        }

        SetState(ServerState.Starting);

        if (IsPortOpen(_config.Port))
        {
            AppLog.Write("检测到 dsh 已在运行（端口已监听）。");
            SetState(ServerState.Running);
            return;
        }

        if (!Directory.Exists(_config.HarnessPath))
        {
            Fail($"未找到 deepseek-harness 部署目录: {_config.HarnessPath}");
            return;
        }

        var startInfo = BuildProcessStartInfo();
        AppLog.Write($"启动命令: {startInfo.FileName} {string.Join(' ', startInfo.ArgumentList)}");

        try
        {
            _process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Fail($"无法启动 dsh 进程: {ex.Message}");
            return;
        }

        if (_process is null)
        {
            Fail("进程启动失败（未返回进程对象）。");
            return;
        }

        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) =>
        {
            lock (_gate)
            {
                if (State is ServerState.Starting)
                    AppLog.Write($"dsh 进程提前退出（退出码 {_process?.ExitCode}），请查看日志。");
            }
        };

        if (startInfo.RedirectStandardOutput)
        {
            _process.OutputDataReceived += OnProcessOutput;
            _process.ErrorDataReceived += OnProcessOutput;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(10, _config.StartupTimeoutSeconds));
        while (DateTime.UtcNow < deadline)
        {
            if (State is not ServerState.Starting)
                return; // 启动过程中被 Stop() 打断

            await Task.Delay(700);

            var p = _process;
            if (p is not null && p.HasExited)
            {
                Fail($"dsh 进程提前退出（退出码 {p.ExitCode}），请查看日志。");
                return;
            }

            if (IsPortOpen(_config.Port))
            {
                SetState(ServerState.Running);
                StartHealthCheck();
                return;
            }
        }

        Fail($"等待 Web UI 超时（{_config.StartupTimeoutSeconds} 秒），请查看日志。");
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (State is ServerState.Stopped or ServerState.Stopping)
                return;
            SetState(ServerState.Stopping);
        }

        StopProcess();
        StopHealthCheck();

        // 等待端口释放；仍被占用则结束占用者（可能是外部启动的 dsh）
        for (var i = 0; i < 25; i++)
        {
            if (!IsPortOpen(_config.Port))
                break;
            Thread.Sleep(200);
        }
        if (IsPortOpen(_config.Port))
            KillPortOwner();

        SetState(ServerState.Stopped);
    }

    private ProcessStartInfo BuildProcessStartInfo()
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = _config.HarnessPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var node = ResolveNode();
        if (node is not null)
        {
            // 与 `pnpm dsh web` 等价，但直接启动 node，避免 cmd/pnpm 外壳
            psi.FileName = node;
            psi.ArgumentList.Add("--import");
            psi.ArgumentList.Add("tsx/esm");
            psi.ArgumentList.Add("apps/cli/src/bin.ts");
            psi.ArgumentList.Add(_config.Command);
        }
        else
        {
            // 兜底：通过 pnpm 启动（需要 pnpm 已安装并在 PATH 中）
            psi.FileName = "cmd.exe";
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add($"pnpm dsh {_config.Command}");
        }

        return psi;
    }

    private string? ResolveNode()
    {
        if (!string.IsNullOrWhiteSpace(_config.NodePath) && File.Exists(_config.NodePath))
            return _config.NodePath;

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), "node.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // 忽略无效路径
            }
        }
        return null;
    }

    private void OnProcessOutput(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
            AppLog.Write($"[dsh] {e.Data}");
    }

    private void StartHealthCheck()
    {
        StopHealthCheck();
        _healthTimer = new System.Threading.Timer(
            _ =>
            {
                if (State is ServerState.Running && !IsPortOpen(_config.Port))
                {
                    AppLog.Write("健康检查：dsh 已停止（端口不再监听）。");
                    StopProcess();
                    StopHealthCheck();
                    SetState(ServerState.Stopped);
                }
            },
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5));
    }

    private void StopHealthCheck()
    {
        _healthTimer?.Dispose();
        _healthTimer = null;
    }

    private void Fail(string message)
    {
        LastError = message;
        AppLog.Write($"[错误] {message}");
        StopProcess();
        SetState(ServerState.Error);
    }

    private void StopProcess()
    {
        lock (_gate)
        {
            var p = _process;
            _process = null;
            if (p is null)
                return;

            try
            {
                if (!p.HasExited)
                {
                    AppLog.Write($"正在停止 dsh 进程 (PID {p.Id})...");
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                AppLog.Write($"停止进程时出错: {ex.Message}");
            }
            finally
            {
                p.Dispose();
            }
        }
    }

    private void KillPortOwner()
    {
        try
        {
            var psi = new ProcessStartInfo("netstat", "-ano")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var netstat = Process.Start(psi);
            if (netstat is null)
                return;

            var output = netstat.StandardOutput.ReadToEnd();
            netstat.WaitForExit(3000);

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains($":{_config.Port}", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                {
                    var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length >= 5 && int.TryParse(tokens[^1], out var pid) && pid > 0)
                    {
                        try
                        {
                            Process.GetProcessById(pid).Kill(entireProcessTree: true);
                            AppLog.Write($"已结束占用端口 {_config.Port} 的进程 (PID {pid})");
                        }
                        catch
                        {
                            // 进程可能已退出
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"自动结束占用端口的进程失败: {ex.Message}");
        }
    }

    private void SetState(ServerState state)
    {
        State = state;
        AppLog.Write($"服务状态: {state}");
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
        _healthTimer?.Dispose();
    }
}
