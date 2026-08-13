# publish-single-exe.ps1 — 一键发布「单文件自包含 exe」
# 用法：在 HourlyNotes 项目目录（含 .csproj）执行：  .\publish-single-exe.ps1
param(
    [ValidateSet("win-x64", "win-arm64")][string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "[错误] 未找到 dotnet，请先安装 .NET 8 SDK: winget install Microsoft.DotNet.SDK.8" -ForegroundColor Red
    exit 1
}

Write-Host "==> 发布单文件自包含版本 ($Runtime) ..." -ForegroundColor Cyan
dotnet publish -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$out = Join-Path (Get-Location) "bin\Release\net8.0-windows\$Runtime\publish"
Write-Host ""
Write-Host "==> 完成！exe 在: " -ForegroundColor Green
Write-Host "    $out\HourlyNotes.exe " -ForegroundColor Green
Write-Host "    把这个 exe 单独发给别人即可（目标机器无需安装 .NET）。 " -ForegroundColor Green
