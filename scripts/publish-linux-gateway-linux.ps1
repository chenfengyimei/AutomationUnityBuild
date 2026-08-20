param(
    [string]$Runtime = "linux-x64",
    [string]$Configuration = "Release",
    [string]$Output = "publish/linux-gateway",
    [switch]$AllowDirty
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "publish"))
$publishDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Output))
$pathComparison = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    [System.StringComparison]::OrdinalIgnoreCase
} else {
    [System.StringComparison]::Ordinal
}

if (-not $publishDir.StartsWith($publishRoot + [System.IO.Path]::DirectorySeparatorChar, $pathComparison)) {
    throw "LinuxGateway publish output must stay under publish directory: $publishDir"
}

# Prefer a Git tag/commit for the release version; fall back to the current date.
$gitTag = git describe --tags --always --dirty 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitTag)) {
    $gitTag = "v$(Get-Date -Format 'yyyy-MM-dd')"
}

if (-not $AllowDirty -and $gitTag.EndsWith("-dirty", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to publish from a dirty Git worktree. Commit/stash changes or pass -AllowDirty for a non-release validation build."
}

# Create a tar.gz asset for a Gitee or GitHub release.
$safeVersion = $gitTag -replace '[^A-Za-z0-9._-]', '-'
$tarGzName = "linux-gateway-$safeVersion.tar.gz"
$tarGzPath = Join-Path $publishRoot $tarGzName
$operationId = [Guid]::NewGuid().ToString("N")
$publishStaging = Join-Path $publishRoot ".linux-gateway-staging-$operationId"
$archiveStaging = Join-Path $publishRoot ".$tarGzName.$operationId.tmp"
$tempArchive = ""

try {
    dotnet publish .\LinuxGateway\LinuxGateway.csproj `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -o $publishStaging

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    foreach ($requiredFile in @("LinuxGateway", "appsettings.json", "wwwroot/index.html")) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishStaging $requiredFile))) {
            throw "Published LinuxGateway file was not found: $requiredFile"
        }
    }

    # SelfUpdateService reads this file to determine the installed version.
    $versionPath = Join-Path $publishStaging "VERSION"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($versionPath, $gitTag, $utf8NoBom)
    Write-Host "VERSION file written: $gitTag"

    # Copy published files while excluding runtime data directories.
    $tempArchive = Join-Path ([System.IO.Path]::GetTempPath()) ("lgw-archive-" + [System.IO.Path]::GetRandomFileName())
    New-Item -ItemType Directory -Path $tempArchive | Out-Null
    Get-ChildItem -LiteralPath $publishStaging -Recurse |
        Where-Object { $_.FullName -notmatch 'linuxgateway-data' -and $_.FullName -notmatch '\\data\\' } |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($publishStaging.Length).TrimStart('\', '/')
            $targetPath = Join-Path $tempArchive $relativePath
            if ($_.PSIsContainer) {
                New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
            } else {
                $targetDir = Split-Path $targetPath -Parent
                if (-not (Test-Path -LiteralPath $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
                Copy-Item -LiteralPath $_.FullName -Destination $targetPath
            }
        }

    tar -czf $archiveStaging -C $tempArchive .
    if ($LASTEXITCODE -ne 0) {
        throw "tar failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path -LiteralPath $archiveStaging)) {
        throw "Release package was not created: $archiveStaging"
    }

    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }
    Move-Item -LiteralPath $publishStaging -Destination $publishDir
    $publishStaging = ""

    if (Test-Path -LiteralPath $tarGzPath) {
        Remove-Item -LiteralPath $tarGzPath -Force
    }
    Move-Item -LiteralPath $archiveStaging -Destination $tarGzPath
    $archiveStaging = ""
} finally {
    if (-not [string]::IsNullOrWhiteSpace($tempArchive) -and (Test-Path -LiteralPath $tempArchive)) {
        Remove-Item -LiteralPath $tempArchive -Recurse -Force
    }
    if (-not [string]::IsNullOrWhiteSpace($publishStaging) -and (Test-Path -LiteralPath $publishStaging)) {
        Remove-Item -LiteralPath $publishStaging -Recurse -Force
    }
    if (-not [string]::IsNullOrWhiteSpace($archiveStaging) -and (Test-Path -LiteralPath $archiveStaging)) {
        Remove-Item -LiteralPath $archiveStaging -Force
    }
}

Write-Host ""
Write-Host "LinuxGateway published to $publishDir"
Write-Host "Version: $gitTag"
Write-Host "Release package: $tarGzPath"
Write-Host ""
Write-Host "Upload $tarGzName to Gitee Release as an asset."
Write-Host "  Gitee: https://gitee.com/chenfengloveyuri/automation-unity-build-ios/releases/new"
