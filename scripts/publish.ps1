<#
.SYNOPSIS
  发布 DshDesktop 为 win-x64 单文件 exe。
.EXAMPLE
  .\scripts\publish.ps1
  .\scripts\publish.ps1 -SelfContained   # 自包含（无需安装 .NET 运行时，体积更大）
  .\scripts\publish.ps1 -Output dist
#>
[CmdletBinding()]
param(
    [string]$Output = "artifacts",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\src\DshDesktop\DshDesktop.csproj"
$outDir = Join-Path $PSScriptRoot "..\$Output"

$dotnetArgs = @(
    "publish", $project,
    "-c", "Release",
    "-r", "win-x64",
    "-o", $outDir,
    "--self-contained", $(if ($SelfContained) { "true" } else { "false" }),
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true"
)

& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "发布完成: $outDir" -ForegroundColor Green
Write-Host "入口程序: $outDir\DshDesktop.exe" -ForegroundColor Green
