# DeepSeek Harness Desktop (dsh-desktop)

> 一个 Windows 桌面端托盘启动器：无控制台窗口、后台静默启动
> [DeepSeek Harness](https://github.com/deepseek-ai/deepseek-harness)（dsh），
> 并用 WebView2 将它的 Web UI 嵌入原生窗口，看起来就是一个桌面应用。

本项目与 DeepSeek 官方无任何关联，仅是对官方开源 dsh 的一层桌面封装。

## 功能特性

- 无 PowerShell / 控制台窗口：dsh 以隐藏子进程方式启动，stdout/stderr 写入日志文件
- 原生桌面窗口：WebView2 内嵌 dsh Web UI（默认 `http://127.0.0.1:3080`）
- 系统托盘：启动 / 停止 / 打开界面 / 查看日志 / 打开数据目录 / 开机自启 / 退出
- 自动就绪：服务启动后自动导航到 Web UI；重复启动应用只唤起已有窗口（单实例）
- 健康检查：dsh 意外退出时自动更新状态
- DeepSeek 鲸鱼图标：exe、任务栏、托盘、窗口统一使用（可一键替换，见下文）
- 一键发布：`scripts/publish.ps1` 产出单文件 exe

## 环境要求

- Windows 10/11（需安装 [Microsoft Edge WebView2 运行时](https://developer.microsoft.com/microsoft-edge/webview2/)，Windows 11 通常已自带）
- [.NET SDK 10.0](https://dotnet.microsoft.com/download)（构建）；若使用框架依赖发布，运行机器也需 .NET 10 Desktop Runtime
- [Node.js](https://nodejs.org/)（运行 dsh 必需，需在 PATH 中或通过配置指定完整路径）
- 已部署的 [deepseek-harness](https://github.com/deepseek-ai/deepseek-harness) 源码目录（`pnpm install && pnpm run build` 完成）

## 快速开始

```powershell
# 1. 构建
dotnet build src/DshDesktop/DshDesktop.csproj -c Release

# 2. 运行（开发模式）
dotnet run --project src/DshDesktop/DshDesktop.csproj

# 或发布为单文件 exe
.\scripts\publish.ps1
.\artifacts\DshDesktop.exe
```

## 使用方法

双击 `DshDesktop.exe`：

- 启动后自动在后台启动 dsh 服务，并打开嵌入 Web UI 的主窗口
- 关闭主窗口只隐藏到托盘，服务继续运行
- 右键托盘图标可启动/停止服务、查看日志、设置开机自启、退出
- 退出应用时默认同时停止 dsh（可在配置中关闭）
- 再次双击 exe（或开机自启的 `--tray` 模式）不会重复启动，只会唤起已有窗口

## 配置

配置文件默认位于 `%APPDATA%\DshDesktop\settings.json`，首次运行后自动生成：

```json
{
  "harnessPath": "D:\\deepseek-harness",
  "nodePath": "",
  "command": "web",
  "port": 3080,
  "url": "http://127.0.0.1:3080",
  "stopServerOnExit": true,
  "startupTimeoutSeconds": 90
}
```

常用项：

- `harnessPath`：dsh 仓库所在目录（迁移部署位置时修改）
- `nodePath`：`node.exe` 完整路径；留空则自动从 PATH 查找
- `port` / `url`：Web UI 监听端口与地址
- `stopServerOnExit`：退出应用时是否同时停止 dsh

数据目录可用环境变量 `DSH_DESKTOP_DATA_DIR` 覆盖（便携模式 / 调试）。

## 更换图标（预留的简单方式）

应用图标统一来自仓库根目录的 `assets\app.ico`（当前为 DeepSeek 鲸鱼图标，矢量源文件为
`assets\deepseek-icon.svg`）。两种替换方式：

### 方式一：替换后重新编译（推荐，全部生效）

```powershell
.\scripts\replace-icon.ps1 -IconPath 你的图标.ico
```

脚本会自动把图标复制为 `assets\app.ico` 并重新编译，之后 exe 图标、任务栏图标、
托盘图标、窗口图标全部使用新图标。也可以手动覆盖 `assets\app.ico` 后执行
`dotnet build`。

> 要求：目标必须是真的 `.ico` 文件，且包含多个尺寸（建议 16/32/48/256）。
> 任何图标编辑工具（IcoFX、GIMP + ico 插件、在线 ICO 生成器）都可从 PNG/SVG 导出。

### 方式二：免编译，仅运行时生效

把新的 `app.ico` 放到 exe 同目录的 `assets\` 子文件夹（例如 `artifacts\assets\app.ico`），
托盘图标与窗口图标立即使用新图标（exe 文件本身的图标仍是编译时的，需方式一才能更新）。

## 命令行参数

| 参数 | 说明 |
| --- | --- |
| `--tray` | 启动后仅驻留托盘（开机自启默认使用此模式） |
| `--selftest` | 无界面自检：启动 dsh → 等待 Web UI 就绪 → 停止；退出码 0 表示成功 |

## 目录结构

```
dsh-desktop/
├── .github/workflows/build.yml   # GitHub Actions CI
├── assets/                       # 图标资源（app.ico + 矢量源）
├── scripts/                      # 发布、换图标脚本
├── src/DshDesktop/               # 主程序
├── README.md
├── LICENSE                       # MIT
└── .gitignore
```

## 常见问题

**WebView2 初始化失败**：安装 [WebView2 运行时](https://developer.microsoft.com/microsoft-edge/webview2/)（Windows 11 一般已内置）。

**提示找不到 dsh 部署目录**：修改 `settings.json` 中的 `harnessPath`。

**启动后服务一直未就绪**：托盘右键 → 查看日志，`logs\dsh-desktop.log` 会记录 dsh 的完整输出。

**端口被其他程序占用**：本程序会在停止服务时自动结束占用 3080 端口的进程；若端口被无关程序占用，请先在配置中修改 `port`/`url`。

## License

[MIT](LICENSE)
