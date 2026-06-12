namespace AutomationUnityBuildIOS;

internal sealed class UnityProjectValidator(BuildRunContext context)
{
    private BuildPaths _paths => context.Paths;
    private BuildLogger _logger => context.Logger;

    public void Validate()
    {
        if (!Directory.Exists(_paths.UnityProjectRoot))
        {
            string candidates = FormatUnityProjectCandidates();
            throw new DirectoryNotFoundException(
                $"Unity 工程目录不存在: {_paths.UnityProjectRoot}{Environment.NewLine}" +
                $"请检查配置 unityProjectRelativePath。它必须指向包含 Assets 和 ProjectSettings 的 Unity 工程根目录，通常填 \".\"。{candidates}");
        }

        bool hasAssets = Directory.Exists(Path.Combine(_paths.UnityProjectRoot, "Assets"));
        bool hasProjectSettings = Directory.Exists(Path.Combine(_paths.UnityProjectRoot, "ProjectSettings"));
        if (!hasAssets || !hasProjectSettings)
        {
            string candidates = FormatUnityProjectCandidates();
            throw new InvalidOperationException(
                $"当前路径不是 Unity 工程根目录: {_paths.UnityProjectRoot}{Environment.NewLine}" +
                $"缺少目录: {(hasAssets ? "" : "Assets ")}{(hasProjectSettings ? "" : "ProjectSettings")}{Environment.NewLine}" +
                $"请把 unityProjectRelativePath 改成包含 Assets 和 ProjectSettings 的目录，通常填 \".\"。{candidates}");
        }

        _logger.Info($"Unity 工程目录校验通过: {_paths.UnityProjectRoot}");
        if (!Directory.Exists(Path.Combine(_paths.UnityProjectRoot, "Library")))
        {
            _logger.Warn("Unity 工程没有 Library 目录，说明这是 Git 新拉下来的干净工程。Unity 命令行会自动导入资源，不需要手动打开；第一次会比较慢。");
        }
    }

    private string FormatUnityProjectCandidates()
    {
        if (!Directory.Exists(_paths.RepositoryRoot))
        {
            return "";
        }

        string[] candidates = FindUnityProjectCandidates(_paths.RepositoryRoot, maxDepth: 4).Take(5).ToArray();
        if (candidates.Length == 0)
        {
            return "";
        }

        string lines = string.Join(
            Environment.NewLine,
            candidates.Select(path => $"  - {Path.GetRelativePath(_paths.RepositoryRoot, path)}"));

        return $"{Environment.NewLine}仓库里检测到可能的 Unity 工程目录:{Environment.NewLine}{lines}";
    }

    private static IEnumerable<string> FindUnityProjectCandidates(string root, int maxDepth)
    {
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            (string path, int depth) = queue.Dequeue();
            if (Directory.Exists(Path.Combine(path, "Assets")) &&
                Directory.Exists(Path.Combine(path, "ProjectSettings")))
            {
                yield return path;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (string child in EnumerateDirectoriesSafe(path))
            {
                string name = Path.GetFileName(child);
                if (name is ".git" or "Library" or "Temp" or "Obj" or "Build" or "Builds")
                {
                    continue;
                }

                queue.Enqueue((child, depth + 1));
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).ToArray();
        }
        catch
        {
            return [];
        }
    }
}
