namespace AutomationUnityBuildIOS;

internal sealed class BuildDirectoryPreparer(BuildRunContext context)
{
    private BuildConfig _config => context.Config;
    private BuildPaths _paths => context.Paths;
    private BuildLogger _logger => context.Logger;

    public void Prepare()
    {
        _logger.Info("准备目录：不存在的目录会自动创建。");
        EnsureDirectoryExists(_paths.WorkspaceRoot, "工作区目录");
        EnsureDirectoryExists(_paths.ArtifactsRunRoot, "本次产物目录");
        EnsureDirectoryExists(_paths.LogsDirectory, "日志目录");
        EnsureParentDirectoryExists(_paths.ArchivePath, "Xcode archive 父目录");
        EnsureParentDirectoryExists(_paths.ExportOptionsPlistPath, "ExportOptions.plist 父目录");

        if (_config.CleanXcodeOutputBeforeBuild && Directory.Exists(_paths.XcodeOutputDirectory))
        {
            EnsureSafeCleanTarget(_paths.XcodeOutputDirectory, _paths.ArtifactsRunRoot);
            _logger.Warn($"清理旧 Xcode 输出目录: {_paths.XcodeOutputDirectory}");
            Directory.Delete(_paths.XcodeOutputDirectory, recursive: true);
        }

        EnsureDirectoryExists(_paths.XcodeOutputDirectory, "Xcode 输出目录");
        EnsureDirectoryExists(_paths.ExportPath, "导出目录");
    }

    private void EnsureDirectoryExists(string path, string description)
    {
        Directory.CreateDirectory(path);
        _logger.Info($"{description}: {path}");
    }

    private void EnsureParentDirectoryExists(string filePath, string description)
    {
        string? parent = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        Directory.CreateDirectory(parent);
        _logger.Info($"{description}: {parent}");
    }

    private static void EnsureSafeCleanTarget(string targetDirectory, string allowedRoot)
    {
        if (PathSafety.IsStrictChildPath(targetDirectory, allowedRoot))
        {
            return;
        }

        throw new InvalidOperationException(
            $"拒绝清理 Xcode 输出目录: {targetDirectory}{Environment.NewLine}" +
            $"为了防止误删，cleanXcodeOutputBeforeBuild=true 时 xcodeOutputDirectory 必须位于本次产物目录内: {allowedRoot}{Environment.NewLine}" +
            "如果你确实要使用外部固定目录，请把 cleanXcodeOutputBeforeBuild 改为 false，或把 xcodeOutputDirectory 改到 artifactsRoot 下面。");
    }
}
