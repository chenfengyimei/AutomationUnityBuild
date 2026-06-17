namespace AutomationUnityBuildIOS;

internal sealed class UnityLogDiagnostics(BuildLogger logger)
{
    public void LogFailureDetails(BuildPaths paths, string message, IReadOnlyList<string> keywords)
    {
        logger.Error(message);
        LogMatchingLogLines(paths.UnityLogPath, "Unity Editor", keywords);
        LogTail(paths.UnityLogPath, "Unity Editor", 80);
        LogTail(paths.UnityProcessLogPath, "Unity Process", 80);
    }

    public void LogDirectorySnapshot(string directory, string title)
    {
        if (!Directory.Exists(directory))
        {
            logger.Error($"{title}不存在: {directory}");
            return;
        }

        logger.Error($"----- {title}: {directory} -----");
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory).Take(80))
        {
            logger.Error(entry);
        }
    }

    public void LogMatchingLogLines(string logPath, string title, IReadOnlyList<string> keywords)
    {
        if (!File.Exists(logPath))
        {
            logger.Warn($"{title} 日志不存在: {logPath}");
            return;
        }

        string[] matches = File.ReadLines(logPath)
            .Where(line => keywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .TakeLast(60)
            .ToArray();

        if (matches.Length == 0)
        {
            logger.Warn($"{title} 日志里没有匹配到常见错误关键字。");
            return;
        }

        logger.Error($"----- {title} 关键错误行 -----");
        foreach (string line in matches)
        {
            logger.Error(line);
        }
    }

    public void LogTail(string logPath, string title, int lineCount)
    {
        if (!File.Exists(logPath))
        {
            logger.Warn($"{title} 日志不存在: {logPath}");
            return;
        }

        logger.Error($"----- {title} 日志最后 {lineCount} 行 -----");
        foreach (string line in File.ReadLines(logPath).TakeLast(lineCount))
        {
            logger.Error(line);
        }
    }
}
