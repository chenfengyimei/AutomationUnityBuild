using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinuxGateway.Services;

public sealed class SelfUpdateService(
    HttpClient httpClient,
    LinuxGatewayOptions options,
    IHostApplicationLifetime lifetime,
    ILogger<SelfUpdateService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private string ContentRoot => AppContext.BaseDirectory;
    private string UpdatesDir => Path.Combine(options.DataRoot, "updates");
    private string VersionFilePath => Path.Combine(ContentRoot, "VERSION");

    public string GetCurrentVersion()
    {
        try
        {
            return File.Exists(VersionFilePath) ? File.ReadAllText(VersionFilePath).Trim() : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        string currentVersion = GetCurrentVersion();
        string source = string.IsNullOrWhiteSpace(options.UpdateSource) ? "gitee" : options.UpdateSource.Trim().ToLowerInvariant();

        ReleaseInfo release = source switch
        {
            "github" => await FetchGitHubLatestReleaseAsync(cancellationToken),
            _ => await FetchGiteeLatestReleaseAsync(cancellationToken)
        };

        string latestTag = release.TagName ?? "";
        bool updateAvailable = !string.IsNullOrWhiteSpace(latestTag) &&
                               !string.Equals(currentVersion, latestTag, StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(currentVersion, "unknown", StringComparison.OrdinalIgnoreCase);

        return new UpdateCheckResult(
            currentVersion,
            latestTag,
            release.Name ?? "",
            release.Body ?? "",
            release.AssetDownloadUrl ?? "",
            release.AssetName ?? "",
            release.AssetSize,
            updateAvailable,
            source);
    }

    public async Task<UpdateApplyResult> ApplyUpdateAsync(UpdateCheckResult checkResult, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkResult.AssetDownloadUrl))
        {
            throw new InvalidOperationException("没有可下载的更新包。请确认 Release 包含 linux-gateway-*.tar.gz 资产。");
        }

        Directory.CreateDirectory(UpdatesDir);

        string downloadFileName = string.IsNullOrWhiteSpace(checkResult.AssetName)
            ? $"linux-gateway-{checkResult.LatestVersion}.tar.gz"
            : checkResult.AssetName;
        string downloadPath = Path.Combine(UpdatesDir, downloadFileName);

        logger.LogInformation("开始下载更新包: {Url} -> {Path}", checkResult.AssetDownloadUrl, downloadPath);

        using (HttpResponseMessage response = await httpClient.GetAsync(checkResult.AssetDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using (FileStream fs = File.Create(downloadPath))
            {
                await response.Content.CopyToAsync(fs, cancellationToken);
            }
        }

        logger.LogInformation("下载完成，开始解压到 staging 目录");

        string stagingDir = Path.Combine(UpdatesDir, "staging");
        if (Directory.Exists(stagingDir))
        {
            Directory.Delete(stagingDir, recursive: true);
        }
        Directory.CreateDirectory(stagingDir);

        await Task.Run(() =>
        {
            if (downloadFileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                downloadFileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                ExtractTarGz(downloadPath, stagingDir);
            }
            else if (downloadFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(downloadPath, stagingDir, overwriteFiles: true);
            }
            else
            {
                throw new InvalidOperationException($"不支持的更新包格式: {downloadFileName}。支持 .tar.gz 和 .zip。");
            }
        }, cancellationToken);

        string scriptPath = Path.Combine(UpdatesDir, "apply-update.sh");
        string backupDir = Path.Combine(UpdatesDir, $"backup-{DateTimeOffset.Now:yyyyMMdd-HHmmss}");
        await WriteApplyScriptAsync(scriptPath, stagingDir, backupDir, checkResult.LatestVersion);

        logger.LogInformation("更新脚本已生成: {ScriptPath}", scriptPath);
        logger.LogInformation("将在 3 秒后执行更新脚本，Gateway 随即关闭。");

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = scriptPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        Process.Start(startInfo);

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);
            logger.LogInformation("正在停止 LinuxGateway 以应用更新...");
            lifetime.StopApplication();
        }, cancellationToken);

        return new UpdateApplyResult(true, scriptPath, backupDir, checkResult.LatestVersion);
    }

    private async Task<ReleaseInfo> FetchGiteeLatestReleaseAsync(CancellationToken cancellationToken)
    {
        string url = $"https://gitee.com/api/v5/repos/{options.UpdateRepoOwner}/{options.UpdateRepoName}/releases/latest";
        logger.LogInformation("正在从 Gitee 检查最新 Release: {Url}", url);

        using HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gitee API 返回 {response.StatusCode}。请确认仓库 {options.UpdateRepoOwner}/{options.UpdateRepoName} 存在且已发布 Release。");
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        GiteeRelease? release = JsonSerializer.Deserialize<GiteeRelease>(json, JsonOptions);
        if (release is null)
        {
            throw new InvalidOperationException("Gitee API 返回数据解析失败。");
        }

        GiteeAsset? asset = release.Assets?.FirstOrDefault(a =>
            !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl) &&
            (a.Name?.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) == true ||
             a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true));

        return new ReleaseInfo(
            release.TagName,
            release.Name,
            release.Body,
            asset?.BrowserDownloadUrl,
            asset?.Name,
            asset?.Size ?? 0);
    }

    private async Task<ReleaseInfo> FetchGitHubLatestReleaseAsync(CancellationToken cancellationToken)
    {
        string url = $"https://api.github.com/repos/{options.UpdateRepoOwner}/{options.UpdateRepoName}/releases/latest";
        logger.LogInformation("正在从 GitHub 检查最新 Release: {Url}", url);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("LinuxGateway-SelfUpdate");

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GitHub API 返回 {response.StatusCode}。请确认仓库 {options.UpdateRepoOwner}/{options.UpdateRepoName} 存在且已发布 Release。");
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        GitHubRelease? release = JsonSerializer.Deserialize<GitHubRelease>(json, JsonOptions);
        if (release is null)
        {
            throw new InvalidOperationException("GitHub API 返回数据解析失败。");
        }

        GitHubAsset? asset = release.Assets?.FirstOrDefault(a =>
            !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl) &&
            (a.Name?.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) == true ||
             a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true));

        return new ReleaseInfo(
            release.TagName,
            release.Name,
            release.Body,
            asset?.BrowserDownloadUrl,
            asset?.Name,
            asset?.Size ?? 0);
    }

    private async Task WriteApplyScriptAsync(string scriptPath, string stagingDir, string backupDir, string newVersion)
    {
        string appDir = ContentRoot;
        string dataDir = options.DataRoot;

        string script = $""""
#!/bin/sh
# LinuxGateway 自动更新脚本
# 生成时间: {DateTimeOffset.Now:O}
# 目标版本: {newVersion}
set -e

APP_DIR="{appDir}"
STAGING_DIR="{stagingDir}"
BACKUP_DIR="{backupDir}"
DATA_DIR="{dataDir}"

echo "[update] waiting 3 seconds for gateway to exit..."
sleep 3

echo "[update] creating backup: $BACKUP_DIR"
mkdir -p "$BACKUP_DIR"

# 备份当前应用文件（排除数据目录）
for item in "$APP_DIR"/*; do
    base=$(basename "$item")
    case "$base" in
        linuxgateway-data|updates)
            # 跳过数据目录和更新目录
            ;;
        *)
            cp -r "$item" "$BACKUP_DIR/"
            ;;
    esac
done

echo "[update] applying new files from staging..."
cd "$STAGING_DIR"

# 复制新文件到应用目录
find . -type f | while read -r f; do
    target="$APP_DIR/$f"
    mkdir -p "$(dirname "$target")"
    cp "$f" "$target"
done

echo "[update] files updated."

# 尝试 systemctl 重启
if systemctl list-unit-files 2>/dev/null | grep -q 'linuxgateway'; then
    echo "[update] restarting via systemctl..."
    systemctl restart linuxgateway
    echo "[update] service restarted."
else
    echo "[update] systemctl service not found."
    echo "[update] please restart LinuxGateway manually:"
    echo "  cd $APP_DIR && ./LinuxGateway"
fi

echo "[update] done."

"""";
        await File.WriteAllTextAsync(scriptPath, script);
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch { }
        }
    }

    private static void ExtractTarGz(string archivePath, string destinationDir)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"xzf \"{archivePath}\" -C \"{destinationDir}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(processInfo)
            ?? throw new InvalidOperationException("无法启动 tar 命令。");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            string error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"tar 解压失败 (exit {process.ExitCode}): {error}");
        }
    }
}

public sealed record UpdateCheckResult(
    string CurrentVersion,
    string LatestVersion,
    string ReleaseName,
    string ReleaseNotes,
    string AssetDownloadUrl,
    string AssetName,
    long AssetSize,
    bool UpdateAvailable,
    string Source);

public sealed record UpdateApplyResult(
    bool Success,
    string ScriptPath,
    string BackupDir,
    string NewVersion);

internal sealed record ReleaseInfo(
    string? TagName,
    string? Name,
    string? Body,
    string? AssetDownloadUrl,
    string? AssetName,
    long AssetSize);

internal sealed class GiteeRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("body")]
    public string? Body { get; set; }
    [JsonPropertyName("assets")]
    public List<GiteeAsset>? Assets { get; set; }
}

internal sealed class GiteeAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
    [JsonPropertyName("size")]
    public long Size { get; set; }
}

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("body")]
    public string? Body { get; set; }
    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
    [JsonPropertyName("size")]
    public long Size { get; set; }
}
