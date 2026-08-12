param(
    [string]$OutputDir = "publish/desktop",
    [switch]$WinX64,
    [switch]$OsxArm64,
    [switch]$OsxX64,
    [switch]$All
)

$ErrorActionPreference = "Stop"
$projectPath = "DesktopApp/DesktopApp.csproj"
$solutionRoot = $PSScriptRoot

if (-not (Test-Path (Join-Path $solutionRoot $projectPath)))
{
    $solutionRoot = Get-Location
}

if (-not $WinX64 -and -not $OsxArm64 -and -not $OsxX64 -and -not $All)
{
    $All = $true
}

function Publish-SingleFile($runtime, $outputName)
{
    $outPath = Join-Path $solutionRoot "$OutputDir/$runtime"
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Publishing $runtime -> $outputName" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    if (Test-Path $outPath)
    {
        Remove-Item -Recurse -Force $outPath
    }

    & dotnet publish (Join-Path $solutionRoot $projectPath) `
        -c Release `
        -r $runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=embedded `
        -o $outPath

    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "FAILED: $runtime" -ForegroundColor Red
        return
    }

    $exeName = if ($runtime.StartsWith("win")) { "$outputName.exe" } else { $outputName }
    $exePath = Join-Path $outPath $exeName

    if (Test-Path $exePath)
    {
        $size = (Get-Item $exePath).Length / 1MB
        Write-Host ""
        Write-Host "SUCCESS: $exePath" -ForegroundColor Green
        Write-Host ("Size: {0:N1} MB" -f $size) -ForegroundColor Green
        Write-Host "Output dir: $outPath" -ForegroundColor Gray
    }
    else
    {
        Write-Host "WARNING: Expected exe not found at $exePath" -ForegroundColor Yellow
        Write-Host "Files in output:" -ForegroundColor Gray
        Get-ChildItem $outPath -Name | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
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
Write-Host "Output:" -ForegroundColor White
Write-Host "  Windows:  $OutputDir/win-x64/DesktopApp.exe" -ForegroundColor Gray
Write-Host "  Mac ARM:  $OutputDir/osx-arm64/DesktopApp" -ForegroundColor Gray
Write-Host "  Mac Intel: $OutputDir/osx-x64/DesktopApp" -ForegroundColor Gray
Write-Host ""
Write-Host "Usage:" -ForegroundColor White
Write-Host "  .\scripts\publish-desktop.ps1              # All platforms" -ForegroundColor Gray
Write-Host "  .\scripts\publish-desktop.ps1 -WinX64       # Windows only" -ForegroundColor Gray
Write-Host "  .\scripts\publish-desktop.ps1 -OsxArm64     # Mac ARM only" -ForegroundColor Gray
Write-Host ""
Write-Host "Note: Mac builds should be done on Mac for code signing." -ForegroundColor Yellow
Write-Host "      Windows build can be done on Windows or Mac." -ForegroundColor Yellow
