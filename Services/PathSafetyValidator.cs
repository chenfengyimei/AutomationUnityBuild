namespace AutomationUnityBuildIOS;

internal sealed class PathSafetyValidator(BuildRunContext context)
{
    private BuildConfig _config => context.Config;
    private BuildPaths _paths => context.Paths;
    private BuildLogger _logger => context.Logger;

    public void Validate()
    {
        string workspaceRoot = PathSafety.NormalizePath(_paths.WorkspaceRoot);
        string artifactsRoot = PathSafety.NormalizePath(_paths.ArtifactsRoot);
        IReadOnlyList<string> allowedWorkspaceRoots = ResolveAllowedRoots(_config.AllowedWorkspaceRoots, workspaceRoot);
        IReadOnlyList<string> allowedArtifactsRoots = ResolveAllowedRoots(_config.AllowedArtifactsRoots, artifactsRoot);

        EnsureRootsAreSafe(allowedWorkspaceRoots, "allowedWorkspaceRoots");
        EnsureRootsAreSafe(allowedArtifactsRoots, "allowedArtifactsRoots");

        RequireUnderAnyRoot(_paths.WorkspaceRoot, allowedWorkspaceRoots, "workspaceRoot");
        RequireStrictChild(_paths.RepositoryRoot, [_paths.WorkspaceRoot], "Git 仓库目录");
        RequireUnderAnyRoot(_paths.UnityProjectRoot, [_paths.RepositoryRoot], "Unity 工程目录");

        RequireUnderAnyRoot(_paths.ArtifactsRoot, allowedArtifactsRoots, "artifactsRoot");
        RequireStrictChild(_paths.ArtifactsRunRoot, [_paths.ArtifactsRoot], "本次产物目录");
        RequireUnderAnyRoot(_paths.XcodeOutputDirectory, [_paths.ArtifactsRunRoot], "Xcode 输出目录");
        RequireUnderAnyRoot(_paths.ArchivePath, [_paths.ArtifactsRunRoot], "Xcode archive 路径");
        RequireUnderAnyRoot(_paths.ExportPath, [_paths.ArtifactsRunRoot], "导出目录");
        RequireUnderAnyRoot(_paths.LogsDirectory, [_paths.ArtifactsRunRoot], "日志目录");
        RequireParentUnderAnyRoot(_paths.ConfigSnapshotPath, [_paths.ArtifactsRunRoot], "配置快照");
        RequireParentUnderAnyRoot(_paths.ExportOptionsPlistPath, [_paths.ArtifactsRunRoot], "ExportOptions.plist");

        _logger.Info("路径安全边界校验通过。");
    }

    private static IReadOnlyList<string> ResolveAllowedRoots(IReadOnlyList<string> configuredRoots, string fallbackRoot)
    {
        if (configuredRoots.Count == 0)
        {
            return [fallbackRoot];
        }

        return configuredRoots
            .Select(PathTools.ExpandHome)
            .Select(PathSafety.NormalizePath)
            .Distinct(StringComparerForPaths())
            .ToArray();
    }

    private static void EnsureRootsAreSafe(IReadOnlyList<string> roots, string fieldName)
    {
        foreach (string root in roots)
        {
            if (PathSafety.IsFilesystemRoot(root))
            {
                throw new InvalidOperationException($"{fieldName} 不能配置为磁盘根目录: {root}");
            }
        }
    }

    private static void RequireParentUnderAnyRoot(string filePath, IReadOnlyList<string> allowedRoots, string description)
    {
        string? parent = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            throw new InvalidOperationException($"{description} 路径无效: {filePath}");
        }

        RequireUnderAnyRoot(parent, allowedRoots, description);
    }

    private static void RequireStrictChild(string path, IReadOnlyList<string> allowedRoots, string description)
    {
        foreach (string root in allowedRoots)
        {
            if (PathSafety.IsStrictChildPath(path, root))
            {
                return;
            }
        }

        throw BuildPathError(path, allowedRoots, description);
    }

    private static void RequireUnderAnyRoot(string path, IReadOnlyList<string> allowedRoots, string description)
    {
        foreach (string root in allowedRoots)
        {
            if (PathSafety.IsSameOrChildPath(path, root))
            {
                return;
            }
        }

        throw BuildPathError(path, allowedRoots, description);
    }

    private static InvalidOperationException BuildPathError(string path, IReadOnlyList<string> allowedRoots, string description)
    {
        string allowed = string.Join(Environment.NewLine, allowedRoots.Select(root => $"  - {root}"));
        return new InvalidOperationException(
            $"{description} 不在允许的路径根目录内，已停止打包，避免误读写或误删文件。{Environment.NewLine}" +
            $"当前路径: {PathSafety.NormalizePath(path)}{Environment.NewLine}" +
            $"允许根目录:{Environment.NewLine}{allowed}");
    }

    private static StringComparer StringComparerForPaths()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}
