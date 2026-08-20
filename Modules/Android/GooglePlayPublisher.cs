namespace AutomationUnityBuildIOS;

internal sealed class GooglePlayPublisher(BuildRunContext context)
{
    private const string AndroidPublisherScope = "https://www.googleapis.com/auth/androidpublisher";

    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildPaths _paths => context.Paths;
    private BuildLogger _logger => context.Logger;

    public async Task PublishAsync()
    {
        if (!_config.GooglePlayUploadEnabled)
        {
            _logger.Info("Google Play 上传: 关闭");
            return;
        }

        string packageName = _config.EffectiveGooglePlayPackageName();
        IReadOnlyList<GooglePlayUploadArtifact> artifacts = ResolveUploadArtifacts(requireFiles: !_options.DryRun);
        if (_options.DryRun)
        {
            LogDryRunPlan(packageName, artifacts);
            return;
        }

        GoogleServiceAccount serviceAccount = GoogleServiceAccount.Load(ResolveSecretPath(_config.GooglePlayServiceAccountJsonPath));
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        string accessToken = await GoogleOAuthTokenProvider.GetAccessTokenAsync(httpClient, serviceAccount, AndroidPublisherScope);
        var apiClient = new GooglePlayApiClient(httpClient, accessToken, _logger);

        string? editId = null;
        try
        {
            editId = await apiClient.CreateEditAsync(packageName);
            List<string> versionCodes = [];
            foreach (GooglePlayUploadArtifact artifact in artifacts)
            {
                string versionCode = artifact.Kind == AndroidBuildFormats.Aab
                    ? await apiClient.UploadBundleAsync(packageName, editId, artifact.Path)
                    : await apiClient.UploadApkAsync(packageName, editId, artifact.Path);
                versionCodes.Add(versionCode);
            }

            await apiClient.UpdateTrackAsync(
                packageName,
                editId,
                _config.GooglePlayTrack,
                versionCodes.Distinct(StringComparer.Ordinal).ToArray(),
                _config.GooglePlayReleaseStatus,
                _config.GooglePlayReleaseName,
                _config.GooglePlayUserFraction);

            await apiClient.CommitEditAsync(packageName, editId, _config.GooglePlayChangesNotSentForReview);
            _logger.Info($"Google Play 上传完成: package={packageName}, track={_config.GooglePlayTrack}, versionCodes={string.Join(",", versionCodes)}");
        }
        catch (Exception)
        {
            if (!string.IsNullOrWhiteSpace(editId))
            {
                try
                {
                    await apiClient.TryDeleteEditAsync(packageName, editId);
                }
                catch (Exception cleanupError)
                {
                    _logger.Warn($"Google Play edit 回滚失败，可能需要到 Play Console 检查 edit 状态: {cleanupError.Message}");
                }
            }

            throw;
        }
    }

    private void LogDryRunPlan(string packageName, IReadOnlyList<GooglePlayUploadArtifact> artifacts)
    {
        _logger.Info($"[dry-run] Google Play: edits.insert package={packageName}");
        foreach (GooglePlayUploadArtifact artifact in artifacts)
        {
            _logger.Info($"[dry-run] Google Play: 上传 {artifact.Kind.ToUpperInvariant()} {artifact.Path}");
        }

        _logger.Info($"[dry-run] Google Play: tracks.update track={_config.GooglePlayTrack}, status={GooglePlayReleaseStatus.Normalize(_config.GooglePlayReleaseStatus)}");
        _logger.Info("[dry-run] Google Play: edits.commit");
    }

    private IReadOnlyList<GooglePlayUploadArtifact> ResolveUploadArtifacts(bool requireFiles)
    {
        var artifacts = new List<GooglePlayUploadArtifact>();
        if (AndroidBuildFormats.IncludesAab(_config.GooglePlayUploadArtifact))
        {
            if (requireFiles && !_config.ShouldBuildAab && !File.Exists(_paths.AabOutputPath))
            {
                throw new FileNotFoundException($"googlePlayUploadArtifact 包含 aab，但当前没有可上传的 AAB: {_paths.AabOutputPath}");
            }

            artifacts.Add(new GooglePlayUploadArtifact(AndroidBuildFormats.Aab, _paths.AabOutputPath));
        }

        if (AndroidBuildFormats.IncludesApk(_config.GooglePlayUploadArtifact))
        {
            if (requireFiles && !_config.ShouldBuildApk && !File.Exists(_paths.ApkOutputPath))
            {
                throw new FileNotFoundException($"googlePlayUploadArtifact 包含 apk，但当前没有可上传的 APK: {_paths.ApkOutputPath}");
            }

            artifacts.Add(new GooglePlayUploadArtifact(AndroidBuildFormats.Apk, _paths.ApkOutputPath));
        }

        if (!requireFiles)
        {
            return artifacts;
        }

        foreach (GooglePlayUploadArtifact artifact in artifacts)
        {
            if (!File.Exists(artifact.Path))
            {
                throw new FileNotFoundException($"Google Play 上传文件不存在: {artifact.Path}");
            }
        }

        return artifacts;
    }

    private string ResolveSecretPath(string path)
    {
        string fullPath = _config.ResolveConfiguredPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Google Play Service Account JSON 不存在: {fullPath}");
        }

        return fullPath;
    }
}
