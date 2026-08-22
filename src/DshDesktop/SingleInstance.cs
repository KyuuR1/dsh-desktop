using System.IO.Pipes;
using System.Text;

namespace DshDesktop;

/// <summary>单实例互斥 + 命名管道：重复启动时通知已有实例显示主窗口。</summary>
public static class SingleInstance
{
    private const string MutexName = @"Local\DshDesktop_SingleInstance";
    private const string PipeName = "DshDesktop_ShowMainWindow";
    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        return createdNew;
    }

    public static void StartPipeListener(Action onShow)
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    server.WaitForConnection();
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    if (reader.ReadLine() == "show")
                        onShow();
                }
                catch
                {
                    // 监听失败时仅影响“唤醒”功能，不影响主程序
                }
            }
        })
        {
            IsBackground = true,
        };
        thread.Start();
    }

    public static void NotifyExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine("show");
        }
        catch
        {
            // 已有实例可能尚未就绪，忽略
        }
    }

    public static void Release() => _mutex?.Dispose();
}
