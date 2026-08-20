param(
    [ValidateSet("osx-arm64", "osx-x64")]
    [string]$Runtime = "osx-arm64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$publishScript = Join-Path $PSScriptRoot "publish-build-server.ps1"
& $publishScript -Runtime $Runtime -Configuration $Configuration
