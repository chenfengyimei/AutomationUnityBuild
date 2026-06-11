param(
    [ValidateSet("osx-arm64", "osx-x64")]
    [string] $Runtime = "osx-arm64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "publish/$Runtime"

dotnet publish (Join-Path $root "AutomationUnityBuildIOS.csproj") `
    -c Release `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    -o $output

Write-Host "已发布 Mac 可执行文件到: $output"
