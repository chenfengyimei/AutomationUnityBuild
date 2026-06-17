param(
    [string]$Urls = "http://127.0.0.1:5090"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

dotnet run --project .\LinuxGateway\LinuxGateway.csproj --urls $Urls
