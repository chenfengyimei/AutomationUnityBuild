param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "AutomationUnityBuildIOS.csproj"
$verifyOutput = Join-Path $repoRoot "bin/Verify"
$dll = Join-Path $verifyOutput "AutomationUnityBuildIOS.dll"
$sampleConfig = Join-Path $repoRoot "build-ios.sample.json"

dotnet build $project -c $Configuration -p:UseAppHost=false -o $verifyOutput
dotnet $dll 00
dotnet $dll run --config $sampleConfig --dry-run --allow-non-mac --skip-git --skip-xcode
cmd /c "echo 0| dotnet ""$dll"" edit-config --config ""$sampleConfig"""
