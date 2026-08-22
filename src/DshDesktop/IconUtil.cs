using System.Drawing;
using System.Reflection;

namespace DshDesktop;

/// <summary>
/// 图标加载：优先使用 exe 同目录的 assets\app.ico（无需重新编译即可替换），
/// 找不到时回退到编译时嵌入的资源。
/// </summary>
public static class IconUtil
{
    public const string IconFileName = "app.ico";

    public static Icon? LoadAppIcon()
    {
        var sidecar = Path.Combine(AppContext.BaseDirectory, "assets", IconFileName);
        if (File.Exists(sidecar))
        {
            var icon = TryLoad(sidecar);
            if (icon is not null)
                return icon;
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("DshDesktop.app.ico");
        if (stream is not null)
        {
            var icon = TryLoad(stream);
            if (icon is not null)
                return icon;
        }

        return SystemIcons.Application;
    }

    private static Icon? TryLoad(string path)
    {
        try { return new Icon(path); } catch { return null; }
    }

    private static Icon? TryLoad(Stream stream)
    {
        try { return new Icon(stream); } catch { return null; }
    }
}
