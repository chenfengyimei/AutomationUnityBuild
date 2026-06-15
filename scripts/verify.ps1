param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "AutomationUnityBuildIOS.csproj"
$buildServerProject = Join-Path $repoRoot "BuildServer/BuildServer.csproj"
$verifyOutput = Join-Path $repoRoot "bin/Verify"
$buildServerVerifyOutput = Join-Path $repoRoot "bin/VerifyBuildServer"
$dll = Join-Path $verifyOutput "AutomationUnityBuildIOS.dll"
$sampleConfig = Join-Path $repoRoot "build-ios.sample.json"

dotnet build $project -c $Configuration -p:UseAppHost=false -o $verifyOutput
dotnet build $buildServerProject -c $Configuration -p:UseAppHost=false -o $buildServerVerifyOutput
dotnet $dll 00
dotnet $dll run --config $sampleConfig --dry-run --allow-non-mac --skip-git --skip-xcode
cmd /c "echo 0| dotnet ""$dll"" edit-config --config ""$sampleConfig"""
