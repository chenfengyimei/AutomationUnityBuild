param(
    [ValidateSet("osx-arm64", "osx-x64")]
    [string]$Runtime = "osx-arm64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "publish/build-server-$Runtime"
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $root "publish"))
$outputFullPath = [System.IO.Path]::GetFullPath($output)
$stagingPath = Join-Path $publishRoot (".staging-build-server-$Runtime-" + [Guid]::NewGuid().ToString("N"))

if (-not $outputFullPath.StartsWith($publishRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish output must stay under publish directory: $outputFullPath"
}

try {
    dotnet publish (Join-Path $root "BuildServer/BuildServer.csproj") `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -o $stagingPath

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    dotnet publish (Join-Path $root "AutomationUnityBuildIOS.csproj") `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -o $stagingPath

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish AutomationUnityBuildIOS failed with exit code $LASTEXITCODE"
    }

    foreach ($requiredFile in @("BuildServer", "AutomationUnityBuildIOS", "appsettings.json", "wwwroot/index.html")) {
        if (-not (Test-Path -LiteralPath (Join-Path $stagingPath $requiredFile))) {
            throw "Published BuildServer file was not found: $requiredFile"
        }
    }

    if (Test-Path -LiteralPath $outputFullPath) {
        Remove-Item -LiteralPath $outputFullPath -Recurse -Force
    }
    Move-Item -LiteralPath $stagingPath -Destination $outputFullPath
    $stagingPath = ""
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($stagingPath) -and (Test-Path -LiteralPath $stagingPath)) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
}

Write-Host "BuildServer published to: $outputFullPath"
Write-Host "AutomationUnityBuildIOS CLI was included in the same directory."
