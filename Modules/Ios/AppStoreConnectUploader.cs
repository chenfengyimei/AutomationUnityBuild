namespace AutomationUnityBuildIOS;

internal sealed class AppStoreConnectUploader(BuildRunContext context)
{
    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildPaths _paths => context.Paths;
    private ProcessRunner _processRunner => context.ProcessRunner;
    private BuildLogger _logger => context.Logger;

    public async Task UploadAsync()
    {
        if (!_config.AppStoreConnectUploadEnabled)
        {
            _logger.Info("未启用 App Store Connect 自动上传。");
            return;
        }

        string apiKeyPath = _config.ResolveConfiguredPath(_config.AppStoreConnectApiKeyPath);
        string uploadOptionsPlistPath = Path.Combine(_paths.ArtifactsRunRoot, "AppStoreConnectUploadOptions.plist");
        string uploadWorkingDirectory = Path.Combine(_paths.ArtifactsRunRoot, "AppStoreConnectUpload");
        string uploadLogPath = Path.Combine(_paths.LogsDirectory, "xcode-upload.log");

        _logger.Info($"App Store Connect 上传日志: {uploadLogPath}");
        _logger.Info("上传成功后，构建会进入 App Store Connect/TestFlight 处理队列；后续提交审核或发布策略仍由 App Store Connect 控制。");

        if (_options.DryRun)
        {
            _logger.Info($"[dry-run] 生成 App Store Connect UploadOptions.plist: {uploadOptionsPlistPath}");
        }
        else
        {
            if (!Directory.Exists(_paths.ArchivePath))
            {
                throw new DirectoryNotFoundException($"找不到可上传的 Xcode archive: {_paths.ArchivePath}");
            }

            if (!File.Exists(apiKeyPath))
            {
                throw new FileNotFoundException($"找不到 App Store Connect API Key .p8 文件: {apiKeyPath}");
            }

            Directory.CreateDirectory(uploadWorkingDirectory);
            ExportOptionsPlist.Write(_config, uploadOptionsPlistPath, destination: "upload");
            _logger.Info($"生成 App Store Connect UploadOptions.plist: {uploadOptionsPlistPath}");
        }

        var args = new List<string>
        {
            "-exportArchive",
            "-archivePath", _paths.ArchivePath,
            "-exportPath", uploadWorkingDirectory,
            "-exportOptionsPlist", uploadOptionsPlistPath,
            "-authenticationKeyPath", apiKeyPath,
            "-authenticationKeyID", _config.AppStoreConnectApiKeyId.Trim(),
            "-authenticationKeyIssuerID", _config.AppStoreConnectApiIssuerId.Trim()
        };

        if (_config.AllowProvisioningUpdates)
        {
            args.Add("-allowProvisioningUpdates");
        }

        await _processRunner.RunAsync(
            "xcodebuild",
            args,
            _paths.XcodeOutputDirectory,
            uploadLogPath,
            _config.Environment);
    }
}
