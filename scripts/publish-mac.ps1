param(
    [ValidateSet("osx-arm64", "osx-x64")]
    [string] $Runtime = "osx-arm64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "publish/$Runtime"
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $root "publish"))
$outputFullPath = [System.IO.Path]::GetFullPath($output)
$stagingPath = Join-Path $publishRoot (".staging-$Runtime-" + [Guid]::NewGuid().ToString("N"))

if (-not $outputFullPath.StartsWith($publishRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish output must stay under publish directory: $outputFullPath"
}

try {
    dotnet publish (Join-Path $root "AutomationUnityBuildIOS.csproj") `
        -c Release `
        -r $Runtime `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:PublishTrimmed=false `
        -o $stagingPath

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    $stagingExecutablePath = Join-Path $stagingPath "AutomationUnityBuildIOS"
    if (-not (Test-Path -LiteralPath $stagingExecutablePath)) {
        throw "Published executable was not found: $stagingExecutablePath"
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

Write-Host "Mac executable published to: $outputFullPath"
