<#
.SYNOPSIS
  一键替换应用图标并重新编译。

.DESCRIPTION
  将任意 .ico 复制为 assets\app.ico 后重新编译，使 exe 图标、
  任务栏图标、托盘图标、窗口图标全部生效。

  要求目标 .ico 为多尺寸图标（建议包含 16/32/48/256 像素），
  可用任意图标编辑工具（如 IcoFX、GIMP+ico 插件、在线 ICO 生成器）从 PNG/SVG 导出。

.EXAMPLE
  .\scripts\replace-icon.ps1 -IconPath C:\my-icons\my.ico
  .\scripts\replace-icon.ps1 -IconPath C:\my-icons\my.ico -SkipBuild
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$IconPath,

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$source = (Resolve-Path -LiteralPath $IconPath).Path
$dest = Join-Path $PSScriptRoot "..\assets\app.ico"

Copy-Item -LiteralPath $source -Destination $dest -Force
Write-Host "图标已替换: $dest" -ForegroundColor Green

if (-not $SkipBuild) {
    $project = Join-Path $PSScriptRoot "..\src\DshDesktop\DshDesktop.csproj"
    & dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "重新编译完成，exe 图标已生效。" -ForegroundColor Green
}
