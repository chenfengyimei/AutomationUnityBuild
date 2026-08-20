param(
    [string]$OutputDir = "publish/desktop",
    [switch]$WinX64,
    [switch]$OsxArm64,
    [switch]$OsxX64,
    [switch]$All
)

$ErrorActionPreference = "Stop"
$projectPath = "DesktopApp/DesktopApp.csproj"
$solutionRoot = Split-Path -Parent $PSScriptRoot
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $solutionRoot "publish"))
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $solutionRoot $OutputDir))

if (-not (Test-Path -LiteralPath (Join-Path $solutionRoot $projectPath)))
{
    throw "DesktopApp project was not found under repository root: $solutionRoot"
}

if (-not $outputRoot.StartsWith($publishRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Desktop publish output must stay under publish directory: $outputRoot"
}

if (-not $WinX64 -and -not $OsxArm64 -and -not $OsxX64 -and -not $All)
{
    $All = $true
}

function Publish-SingleFile($runtime, $outputName)
{
    $outPath = Join-Path $outputRoot $runtime
    $stagingPath = Join-Path $outputRoot (".staging-$runtime-" + [Guid]::NewGuid().ToString("N"))
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Publishing $runtime -> $outputName" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    try {
        & dotnet publish (Join-Path $solutionRoot $projectPath) `
            -c Release `
            -r $runtime `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true `
            -p:DebugType=embedded `
            -o $stagingPath

        if ($LASTEXITCODE -ne 0)
        {
            throw "dotnet publish failed for $runtime with exit code $LASTEXITCODE"
        }

        $exeName = if ($runtime.StartsWith("win")) { "$outputName.exe" } else { $outputName }
        $stagingExePath = Join-Path $stagingPath $exeName
        if (-not (Test-Path -LiteralPath $stagingExePath))
        {
            throw "Published executable was not found: $stagingExePath"
        }

        if (Test-Path -LiteralPath $outPath)
        {
            Remove-Item -LiteralPath $outPath -Recurse -Force
        }
        Move-Item -LiteralPath $stagingPath -Destination $outPath
        $stagingPath = ""

        $exePath = Join-Path $outPath $exeName
        $size = (Get-Item $exePath).Length / 1MB
        Write-Host ""
        Write-Host "SUCCESS: $exePath" -ForegroundColor Green
        Write-Host ("Size: {0:N1} MB" -f $size) -ForegroundColor Green
        Write-Host "Output dir: $outPath" -ForegroundColor Gray
    }
    finally {
        if (-not [string]::IsNullOrWhiteSpace($stagingPath) -and (Test-Path -LiteralPath $stagingPath))
        {
            Remove-Item -LiteralPath $stagingPath -Recurse -Force
        }
    }
}

if ($All -or $WinX64)
{
    Publish-SingleFile "win-x64" "DesktopApp"
}

if ($All -or $OsxArm64)
{
    Publish-SingleFile "osx-arm64" "DesktopApp"
}

if ($All -or $OsxX64)
{
    Publish-SingleFile "osx-x64" "DesktopApp"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  All done!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output root: $outputRoot" -ForegroundColor White
Write-Host ""
Write-Host "Usage:" -ForegroundColor White
Write-Host "  .\scripts\publish-desktop.ps1              # All platforms" -ForegroundColor Gray
Write-Host "  .\scripts\publish-desktop.ps1 -WinX64       # Windows only" -ForegroundColor Gray
Write-Host "  .\scripts\publish-desktop.ps1 -OsxArm64     # Mac ARM only" -ForegroundColor Gray
Write-Host ""
Write-Host "Note: Mac builds should be done on Mac for code signing." -ForegroundColor Yellow
Write-Host "      Windows build can be done on Windows or Mac." -ForegroundColor Yellow
