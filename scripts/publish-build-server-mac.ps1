param(
    [ValidateSet("osx-arm64", "osx-x64")]
    [string]$Runtime = "osx-arm64",
    [string]$Configuration = "Release"
)

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "publish/build-server-$Runtime"

dotnet publish (Join-Path $root "BuildServer/BuildServer.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -o $output

Write-Host "BuildServer 已发布到: $output"
