param(
    [string]$Runtime = "linux-x64",
    [string]$Configuration = "Release",
    [string]$Output = "publish/linux-gateway"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# 获取版本号：优先 git tag，否则用日期
$gitTag = git describe --tags --always --dirty 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitTag)) {
    $gitTag = "v$(Get-Date -Format 'yyyy-MM-dd')"
}

dotnet publish .\LinuxGateway\LinuxGateway.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $Output

# 写入 VERSION 文件（SelfUpdateService 读取此文件判断当前版本）
$versionPath = Join-Path $Output "VERSION"
$gitTag | Out-File -FilePath $versionPath -Encoding utf8NoBOM -NoNewline
Write-Host "VERSION file written: $gitTag"

# 打包为 tar.gz（用于 Gitee/GitHub Release 上传）
$tarGzName = "linux-gateway-$gitTag.tar.gz"
$tarGzPath = Join-Path $repoRoot $tarGzName
if (Test-Path $tarGzPath) { Remove-Item $tarGzPath -Force }

# 排除数据目录
$tempArchive = Join-Path $env:TEMP "lgw-archive-staging"
if (Test-Path $tempArchive) { Remove-Item $tempArchive -Recurse -Force }
New-Item -ItemType Directory -Path $tempArchive -Force | Out-Null

# 复制发布内容到临时目录（排除数据目录）
$publishDir = Join-Path $repoRoot $Output
Get-ChildItem -Path $publishDir -Recurse |
    Where-Object { $_.FullName -notmatch 'linuxgateway-data' -and $_.FullName -notmatch '\\data\\' } |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($publishDir.Length).TrimStart('\', '/')
        $targetPath = Join-Path $tempArchive $relativePath
        if ($_.PSIsContainer) {
            New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
        } else {
            $targetDir = Split-Path $targetPath -Parent
            if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
            Copy-Item $_.FullName $targetPath
        }
    }

tar -czf $tarGzPath -C $tempArchive .
Remove-Item $tempArchive -Recurse -Force

Write-Host ""
Write-Host "LinuxGateway published to $Output"
Write-Host "Version: $gitTag"
Write-Host "Release package: $tarGzPath"
Write-Host ""
Write-Host "Upload $tarGzName to Gitee Release as an asset."
Write-Host "  Gitee: https://gitee.com/chenfengloveyuri/automation-unity-build-ios/releases/new"
