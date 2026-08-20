param(
    [ValidateSet("win-x64", "osx-arm64", "osx-x64")]
    [string]$Runtime = "osx-arm64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $root "publish"))
$output = Join-Path $publishRoot "build-server-$Runtime"
$outputFullPath = [System.IO.Path]::GetFullPath($output)
$stagingPath = Join-Path $publishRoot (".staging-build-server-$Runtime-" + [Guid]::NewGuid().ToString("N"))
$executableSuffix = if ($Runtime.StartsWith("win", [System.StringComparison]::OrdinalIgnoreCase)) { ".exe" } else { "" }
$pathComparison = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    [System.StringComparison]::OrdinalIgnoreCase
} else {
    [System.StringComparison]::Ordinal
}

if (-not $outputFullPath.StartsWith($publishRoot + [System.IO.Path]::DirectorySeparatorChar, $pathComparison)) {
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
        throw "dotnet publish BuildServer failed with exit code $LASTEXITCODE"
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

    $requiredFiles = @(
        "BuildServer$executableSuffix",
        "AutomationUnityBuildIOS$executableSuffix",
        "appsettings.json",
        "wwwroot/index.html"
    )
    foreach ($requiredFile in $requiredFiles) {
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
