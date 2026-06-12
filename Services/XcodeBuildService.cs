namespace AutomationUnityBuildIOS;

internal sealed class XcodeBuildService(BuildRunContext context, XcodeProjectLocator xcodeProjectLocator)
{
    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildPaths _paths => context.Paths;
    private ProcessRunner _processRunner => context.ProcessRunner;
    private BuildLogger _logger => context.Logger;
    private XcodeProjectLocator _xcodeProjectLocator => xcodeProjectLocator;

    public async Task ArchiveAndExportAsync()
    {
        _logger.Info($"Xcode archive 日志: {_paths.XcodeArchiveLogPath}");
        _logger.Info($"Xcode export 日志: {_paths.XcodeExportLogPath}");

        string? selectedProjectOrWorkspace;

        if (_options.DryRun)
        {
            selectedProjectOrWorkspace = Path.Combine(_paths.XcodeOutputDirectory, "Unity-iPhone.xcodeproj");
        }
        else
        {
            selectedProjectOrWorkspace = _xcodeProjectLocator.Find();
        }

        if (selectedProjectOrWorkspace is null)
        {
            throw new FileNotFoundException($"Unity 导出的 Xcode 工程不存在: {_paths.XcodeOutputDirectory}");
        }

        var archiveArgs = new List<string>();
        if (selectedProjectOrWorkspace.EndsWith(".xcworkspace", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info($"使用 Xcode workspace: {selectedProjectOrWorkspace}");
            archiveArgs.AddRange(["-workspace", selectedProjectOrWorkspace]);
        }
        else
        {
            _logger.Info($"使用 Xcode project: {selectedProjectOrWorkspace}");
            archiveArgs.AddRange(["-project", selectedProjectOrWorkspace]);
        }

        archiveArgs.AddRange([
            "-scheme", _config.Scheme,
            "-configuration", _config.Configuration,
            "-archivePath", _paths.ArchivePath
        ]);

        if (_config.AllowProvisioningUpdates)
        {
            archiveArgs.Add("-allowProvisioningUpdates");
        }

        AddXcodeSetting(archiveArgs, "DEVELOPMENT_TEAM", _config.TeamId);
        AddXcodeSetting(archiveArgs, "PRODUCT_BUNDLE_IDENTIFIER", _config.BundleIdentifier);
        AddXcodeSetting(archiveArgs, "CODE_SIGN_STYLE", ToXcodeSigningStyle(_config.SigningStyle));

        foreach ((string key, string value) in _config.XcodeBuildSettings)
        {
            AddXcodeSetting(archiveArgs, key, value);
        }

        archiveArgs.Add("archive");

        if (_config.GenerateExportOptionsPlist)
        {
            if (_options.DryRun)
            {
                _logger.Info($"[dry-run] 生成 ExportOptions.plist: {_paths.ExportOptionsPlistPath}");
            }
            else
            {
                ExportOptionsPlist.Write(_config, _paths.ExportOptionsPlistPath);
                _logger.Info($"生成 ExportOptions.plist: {_paths.ExportOptionsPlistPath}");
            }
        }

        await _processRunner.RunAsync(
            "xcodebuild",
            archiveArgs,
            _paths.XcodeOutputDirectory,
            _paths.XcodeArchiveLogPath,
            _config.Environment);

        CopyArchiveToOrganizer();

        await _processRunner.RunAsync(
            "xcodebuild",
            [
                "-exportArchive",
                "-archivePath", _paths.ArchivePath,
                "-exportPath", _paths.ExportPath,
                "-exportOptionsPlist", _paths.ExportOptionsPlistPath
            ],
            _paths.XcodeOutputDirectory,
            _paths.XcodeExportLogPath,
            _config.Environment);
    }

    private void CopyArchiveToOrganizer()
    {
        if (!_config.CopyArchiveToOrganizer)
        {
            _logger.Info("未启用复制 archive 到 Xcode Organizer。");
            return;
        }

        string organizerDateDirectory = PathTools.ExpandHome(
            Path.Combine("~/Library/Developer/Xcode/Archives", DateTime.Now.ToString("yyyy-MM-dd")));
        string targetArchivePath = GetUniqueDirectoryPath(
            Path.Combine(organizerDateDirectory, $"{SanitizePathComponent(ArchiveDisplayName())}-{_paths.RunId}.xcarchive"));

        if (_options.DryRun)
        {
            _logger.Info($"[dry-run] 复制 archive 到 Xcode Organizer: {_paths.ArchivePath} -> {targetArchivePath}");
            return;
        }

        if (!Directory.Exists(_paths.ArchivePath))
        {
            throw new DirectoryNotFoundException($"Xcode archive 命令已完成，但没有找到归档目录: {_paths.ArchivePath}");
        }

        Directory.CreateDirectory(organizerDateDirectory);
        CopyDirectory(_paths.ArchivePath, targetArchivePath);
        _logger.Info($"已复制 archive 到 Xcode Organizer: {targetArchivePath}");
    }

    private string ArchiveDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(_config.ProductName))
        {
            return _config.ProductName;
        }

        if (!string.IsNullOrWhiteSpace(_config.ProjectDirectoryName))
        {
            return _config.ProjectDirectoryName;
        }

        return _config.Scheme;
    }

    private static string GetUniqueDirectoryPath(string path)
    {
        if (!Directory.Exists(path))
        {
            return path;
        }

        string directory = Path.GetDirectoryName(path) ?? "";
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        for (int index = 2; ; index++)
        {
            string candidate = Path.Combine(directory, $"{fileNameWithoutExtension}-{index}{extension}");
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static string SanitizePathComponent(string value)
    {
        string sanitized = string.IsNullOrWhiteSpace(value) ? "UnityArchive" : value.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidChar, '-');
        }

        return sanitized.Replace(' ', '-');
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (string filePath in Directory.EnumerateFiles(sourceDirectory))
        {
            string targetFilePath = Path.Combine(targetDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, targetFilePath, overwrite: false);
        }

        foreach (string directoryPath in Directory.EnumerateDirectories(sourceDirectory))
        {
            string targetSubdirectory = Path.Combine(targetDirectory, Path.GetFileName(directoryPath));
            CopyDirectory(directoryPath, targetSubdirectory);
        }
    }

    private static void AddXcodeSetting(List<string> args, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        args.Add($"{key}={value}");
    }

    private static string ToXcodeSigningStyle(string signingStyle)
    {
        return signingStyle.Equals("manual", StringComparison.OrdinalIgnoreCase)
            ? "Manual"
            : "Automatic";
    }
}
