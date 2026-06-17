param(
    [string]$Runtime = "linux-x64",
    [string]$Configuration = "Release",
    [string]$Output = "publish/linux-gateway"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

dotnet publish .\LinuxGateway\LinuxGateway.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $Output

Write-Host "LinuxGateway published to $Output"
