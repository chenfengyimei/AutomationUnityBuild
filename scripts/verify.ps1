param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed($LASTEXITCODE): $FilePath $($Arguments -join ' ')"
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "AutomationUnityBuildIOS.sln"
$project = Join-Path $repoRoot "AutomationUnityBuildIOS.csproj"
$buildServerProject = Join-Path $repoRoot "BuildServer/BuildServer.csproj"
$linuxGatewayProject = Join-Path $repoRoot "LinuxGateway/LinuxGateway.csproj"
$solutionVerifyOutput = Join-Path $repoRoot "bin/VerifySolution"
$verifyOutput = Join-Path $repoRoot "bin/Verify"
$buildServerVerifyOutput = Join-Path $repoRoot "bin/VerifyBuildServer"
$linuxGatewayVerifyOutput = Join-Path $repoRoot "bin/VerifyLinuxGateway"
$dll = Join-Path $verifyOutput "AutomationUnityBuildIOS.dll"
$sampleConfig = Join-Path $repoRoot "build-ios.sample.json"
$androidSampleConfig = Join-Path $repoRoot "build-android.sample.json"

Invoke-Native "dotnet" @("build", $solution, "-c", $Configuration, "-p:UseAppHost=false", "-p:OutDir=$solutionVerifyOutput\")
Invoke-Native "dotnet" @("build", $project, "-c", $Configuration, "-p:UseAppHost=false", "-o", $verifyOutput)
Invoke-Native "dotnet" @("build", $buildServerProject, "-c", $Configuration, "-p:UseAppHost=false", "-o", $buildServerVerifyOutput)
Invoke-Native "dotnet" @("build", $linuxGatewayProject, "-c", $Configuration, "-p:UseAppHost=false", "-o", $linuxGatewayVerifyOutput)
Invoke-Native "dotnet" @($dll, "00")
Invoke-Native "dotnet" @($dll, "run", "--config", $sampleConfig, "--dry-run", "--allow-non-mac", "--skip-git", "--skip-xcode")
Invoke-Native "dotnet" @($dll, "run", "--config", $androidSampleConfig, "--dry-run", "--allow-non-mac", "--skip-git")
Invoke-Native "cmd" @("/c", "echo 0| dotnet ""$dll"" edit-config --config ""$sampleConfig""")
