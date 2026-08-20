using System.Diagnostics;
using System.Formats.Tar;
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

        // 同时查询 Gitee 和 GitHub，取版本号更新的那个
        Task<ReleaseInfo> giteeTask = TryFetchAsync(FetchGiteeLatestReleaseAsync, "gitee", cancellationToken);
        Task<ReleaseInfo> githubTask = TryFetchAsync(FetchGitHubLatestReleaseAsync, "github", cancellationToken);
        await Task.WhenAll(giteeTask, githubTask);

        ReleaseInfo giteeRelease = await giteeTask;
        ReleaseInfo githubRelease = await githubTask;

        // 选择 tag 更新的那个作为主结果
        (ReleaseInfo primary, string primarySource, ReleaseInfo? secondary, string? secondarySource) = PickLatestRelease(giteeRelease, githubRelease);

        string latestTag = primary.TagName ?? "";
        bool updateAvailable = !string.IsNullOrWhiteSpace(latestTag) &&
                               !string.Equals(currentVersion, latestTag, StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(currentVersion, "unknown", StringComparison.OrdinalIgnoreCase);

        return new UpdateCheckResult(
            currentVersion,
            latestTag,
            primary.Name ?? "",
            primary.Body ?? "",
            primary.AssetDownloadUrl ?? "",
            primary.AssetName ?? "",
            primary.AssetSize,
            updateAvailable,
            primarySource,
            giteeRelease.TagName,
            giteeRelease.AssetDownloadUrl,
            githubRelease.TagName,
            githubRelease.AssetDownloadUrl,
            secondary?.TagName,
            secondary?.AssetDownloadUrl,
            secondarySource);
    }

    public UpdateCheckResult SelectSource(UpdateCheckResult result, string preferredSource)
    {
        // 前端指定从 gitee 或 github 下载，切换主下载源
        bool isGitee = preferredSource == "gitee";
        string? version = isGitee ? result.GiteeVersion : result.GithubVersion;
        string? downloadUrl = isGitee ? result.GiteeDownloadUrl : result.GithubDownloadUrl;

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException($"源 {preferredSource} 没有可用的更新包。");
        }

        bool updateAvailable = !string.IsNullOrWhiteSpace(version) &&
                               !string.Equals(result.CurrentVersion, version, StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(result.CurrentVersion, "unknown", StringComparison.OrdinalIgnoreCase);

        return result with
        {
            LatestVersion = version ?? result.LatestVersion,
            AssetDownloadUrl = downloadUrl,
            UpdateAvailable = updateAvailable,
            Source = preferredSource
        };
    }

    public async Task<UpdateApplyResult> ApplyUpdateAsync(UpdateCheckResult checkResult, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkResult.AssetDownloadUrl))
        {
            throw new InvalidOperationException("没有可下载的更新包。请确认 Release 包含 linux-gateway-*.tar.gz 资产。");
        }

        EnsureSafeDirectory(UpdatesDir, options.DataRoot);

        string downloadFileName = string.IsNullOrWhiteSpace(checkResult.AssetName)
            ? $"linux-gateway-{checkResult.LatestVersion}.tar.gz"
            : checkResult.AssetName;
        downloadFileName = SafeDownloadFileName(downloadFileName);
        string downloadPath = Path.Combine(UpdatesDir, downloadFileName);
        EnsureNoReparsePointsBelowRoot(downloadPath, UpdatesDir);

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
        EnsureNoReparsePointsBelowRoot(stagingDir, UpdatesDir);
        if (Directory.Exists(stagingDir))
        {
            Directory.Delete(stagingDir, recursive: true);
        }
        EnsureSafeDirectory(stagingDir, UpdatesDir);

        await Task.Run(() =>
        {
            if (downloadFileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                downloadFileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                ExtractTarGz(downloadPath, stagingDir);
            }
            else if (downloadFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ExtractZip(downloadPath, stagingDir);
            }
            else
            {
                throw new InvalidOperationException($"不支持的更新包格式: {downloadFileName}。支持 .tar.gz 和 .zip。");
            }
        }, cancellationToken);

        string scriptPath = Path.Combine(UpdatesDir, "apply-update.sh");
        string backupDir = Path.Combine(UpdatesDir, $"backup-{DateTimeOffset.Now:yyyyMMdd-HHmmss}");
        EnsureNoReparsePointsBelowRoot(scriptPath, UpdatesDir);
        EnsureNoReparsePointsBelowRoot(backupDir, UpdatesDir);
        await WriteApplyScriptAsync(scriptPath, stagingDir, backupDir, checkResult.LatestVersion);

        logger.LogInformation("更新脚本已生成: {ScriptPath}", scriptPath);
        logger.LogInformation("将在 3 秒后执行更新脚本，Gateway 随即关闭。");

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };
        startInfo.ArgumentList.Add(scriptPath);

        Process.Start(startInfo);

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);
            logger.LogInformation("正在停止 LinuxGateway 以应用更新...");
            lifetime.StopApplication();
        }, cancellationToken);

        return new UpdateApplyResult(true, scriptPath, backupDir, checkResult.LatestVersion);
    }

    private async Task<ReleaseInfo> TryFetchAsync(Func<CancellationToken, Task<ReleaseInfo>> fetch, string sourceName, CancellationToken cancellationToken)
    {
        try
        {
            return await fetch(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "从 {Source} 检查更新失败", sourceName);
            return new ReleaseInfo(null, null, null, null, null, 0);
        }
    }

    private static (ReleaseInfo primary, string primarySource, ReleaseInfo? secondary, string? secondarySource) PickLatestRelease(ReleaseInfo gitee, ReleaseInfo github)
    {
        bool giteeHas = !string.IsNullOrWhiteSpace(gitee.TagName);
        bool githubHas = !string.IsNullOrWhiteSpace(github.TagName);

        if (giteeHas && githubHas)
        {
            // 比较 tag：如果一致优先 gitee（国内更快），否则取更大的
            if (string.Equals(gitee.TagName, github.TagName, StringComparison.OrdinalIgnoreCase))
            {
                return (gitee, "gitee", github, "github");
            }
            return CompareVersions(gitee.TagName!, github.TagName!) >= 0
                ? (gitee, "gitee", github, "github")
                : (github, "github", gitee, "gitee");
        }

        if (giteeHas) return (gitee, "gitee", null, null);
        if (githubHas) return (github, "github", null, null);
        return (gitee, "gitee", null, null);
    }

    private static int CompareVersions(string left, string right)
    {
        int[] leftNumbers = ExtractVersionNumbers(left);
        int[] rightNumbers = ExtractVersionNumbers(right);
        int length = Math.Max(leftNumbers.Length, rightNumbers.Length);
        for (int i = 0; i < length; i++)
        {
            int l = i < leftNumbers.Length ? leftNumbers[i] : 0;
            int r = i < rightNumbers.Length ? rightNumbers[i] : 0;
            if (l != r) return l.CompareTo(r);
        }
        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static int[] ExtractVersionNumbers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        List<int> numbers = [];
        int current = 0;
        bool reading = false;
        foreach (char c in value)
        {
            if (char.IsDigit(c))
            {
                current = current * 10 + (c - '0');
                reading = true;
            }
            else if (reading)
            {
                numbers.Add(current);
                current = 0;
                reading = false;
            }
        }
        if (reading) numbers.Add(current);
        return numbers.ToArray();
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
        string owner = string.IsNullOrWhiteSpace(options.UpdateGithubRepoOwner) ? options.UpdateRepoOwner : options.UpdateGithubRepoOwner;
        string repo = string.IsNullOrWhiteSpace(options.UpdateGithubRepoName) ? options.UpdateRepoName : options.UpdateGithubRepoName;
        string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
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
        string versionComment = newVersion.Replace('\r', ' ').Replace('\n', ' ');

        string script = $""""
#!/bin/sh
# LinuxGateway 自动更新脚本
# 生成时间: {DateTimeOffset.Now:O}
# 目标版本: {versionComment}
set -e

APP_DIR={ShellLiteral(appDir)}
STAGING_DIR={ShellLiteral(stagingDir)}
BACKUP_DIR={ShellLiteral(backupDir)}
DATA_DIR={ShellLiteral(dataDir)}

echo "[update] waiting 3 seconds for gateway to exit..."
sleep 3

echo "[update] creating backup: $BACKUP_DIR"
mkdir -p "$BACKUP_DIR"

# 备份当前应用文件（排除数据目录）
for item in "$APP_DIR"/*; do
    base=$(basename "$item")
    case "$item" in
        "$DATA_DIR"|"$DATA_DIR"/*)
            # 数据目录可能使用自定义名称并位于应用目录内，必须整体跳过。
            ;;
        *) case "$base" in
        linuxgateway-data|updates)
            # 跳过数据目录和更新目录
            ;;
        *)
            cp -r "$item" "$BACKUP_DIR/"
            ;;
        esac ;;
    esac
done

echo "[update] applying new files from staging..."
cd "$STAGING_DIR"

# 复制新文件到应用目录
find . -type f | while read -r f; do
    target="$APP_DIR/$f"
    case "$target" in
        "$DATA_DIR"|"$DATA_DIR"/*)
            echo "[update] skipping data file: $target"
            continue
            ;;
    esac
    mkdir -p "$(dirname "$target")"
    cp "$f" "$target"
done

# Windows cross-published tar archives do not preserve Unix executable bits.
chmod 755 "$APP_DIR/LinuxGateway"

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

    internal static void ExtractTarGz(string archivePath, string destinationDir)
    {
        ValidateTarGzEntries(archivePath, destinationDir);
        using FileStream archive = File.OpenRead(archivePath);
        using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzip, destinationDir, overwriteFiles: true);
    }

    private static void ValidateTarGzEntries(string archivePath, string destinationDir)
    {
        using FileStream archive = File.OpenRead(archivePath);
        using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            EnsureArchiveEntryStaysUnderDestination(entry.Name, destinationDir);
            if (entry.EntryType is not (TarEntryType.Directory or
                                        TarEntryType.DirectoryList or
                                        TarEntryType.RegularFile or
                                        TarEntryType.V7RegularFile or
                                        TarEntryType.ContiguousFile))
            {
                throw new InvalidOperationException($"更新包包含不支持的 TAR 条目类型 {entry.EntryType}: {entry.Name}");
            }
        }
    }

    private static void ExtractZip(string archivePath, string destinationDir)
    {
        using (ZipArchive archive = ZipFile.OpenRead(archivePath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                EnsureArchiveEntryStaysUnderDestination(entry.FullName, destinationDir);
                int unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
                if (unixFileType == 0xA000)
                {
                    throw new InvalidOperationException($"更新包不能包含符号链接: {entry.FullName}");
                }
            }
        }

        ZipFile.ExtractToDirectory(archivePath, destinationDir, overwriteFiles: true);
    }

    internal static void EnsureArchiveEntryStaysUnderDestination(string entryName, string destinationDir)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.Any(char.IsControl))
        {
            throw new InvalidOperationException("更新包包含空路径或控制字符路径。");
        }

        string normalizedEntryName = entryName
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        string destinationBase = Path.GetFullPath(destinationDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string destinationRoot = destinationBase + Path.DirectorySeparatorChar;
        string destinationPath = Path.GetFullPath(Path.Combine(destinationBase, normalizedEntryName));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!destinationPath.Equals(destinationBase, comparison) &&
            !destinationPath.StartsWith(destinationRoot, comparison))
        {
            throw new InvalidOperationException($"更新包包含越界路径: {entryName}");
        }
    }

    internal static string SafeDownloadFileName(string value)
    {
        string normalized = value.Trim().Replace('\\', '/');
        string fileName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName is "." or ".." ||
            !string.Equals(fileName, normalized, StringComparison.Ordinal) ||
            fileName.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0 ||
            fileName.Any(char.IsControl))
        {
            throw new InvalidOperationException($"更新包文件名不安全: {value}");
        }

        return fileName;
    }

    internal static void EnsureNoReparsePointsBelowRoot(string path, string root)
    {
        string fullPath = Path.GetFullPath(path);
        string resolvedRoot = Path.GetFullPath(root);
        string trimmedRoot = resolvedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullRoot = string.IsNullOrEmpty(trimmedRoot) ? resolvedRoot : trimmedRoot;
        string rootPrefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.Equals(fullRoot, comparison) && !fullPath.StartsWith(rootPrefix, comparison))
        {
            throw new InvalidOperationException($"更新路径越过数据目录边界: {fullPath}");
        }

        string relativePath = Path.GetRelativePath(fullRoot, fullPath);
        string current = fullRoot;
        foreach (string component in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException($"更新路径包含符号链接或挂载跳转: {current}");
                }
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"无法安全检查更新路径: {current}", ex);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"无法安全检查更新路径: {current}", ex);
            }
        }
    }

    private static void EnsureSafeDirectory(string path, string root)
    {
        EnsureNoReparsePointsBelowRoot(path, root);
        Directory.CreateDirectory(path);
        EnsureNoReparsePointsBelowRoot(path, root);
    }

    private static string ShellLiteral(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'")}'";
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
    string Source,
    string? GiteeVersion,
    string? GiteeDownloadUrl,
    string? GithubVersion,
    string? GithubDownloadUrl,
    string? SecondaryVersion,
    string? SecondaryDownloadUrl,
    string? SecondarySource);

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
