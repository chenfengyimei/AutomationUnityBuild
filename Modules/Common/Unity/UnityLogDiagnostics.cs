namespace AutomationUnityBuildIOS;

internal sealed class UnityLogDiagnostics(BuildLogger logger)
{
    private const int ReadRetryCount = 8;
    private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(200);

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
            logger.Error($"{title} does not exist: {directory}");
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
        if (!TryReadAllLines(logPath, title, out string[] lines))
        {
            return;
        }

        string[] matches = lines
            .Where(line => keywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .TakeLast(60)
            .ToArray();

        if (matches.Length == 0)
        {
            logger.Warn($"{title} log did not contain common error keywords.");
            return;
        }

        logger.Error($"----- {title} matching error lines -----");
        foreach (string line in matches)
        {
            logger.Error(line);
        }
    }

    public void LogTail(string logPath, string title, int lineCount)
    {
        if (!TryReadAllLines(logPath, title, out string[] lines))
        {
            return;
        }

        logger.Error($"----- {title} last {lineCount} lines -----");
        foreach (string line in lines.TakeLast(lineCount))
        {
            logger.Error(line);
        }
    }

    private bool TryReadAllLines(string logPath, string title, out string[] lines)
    {
        lines = [];
        if (string.IsNullOrWhiteSpace(logPath))
        {
            logger.Warn($"{title} log path is empty.");
            return false;
        }

        Exception? lastException = null;
        for (int attempt = 1; attempt <= ReadRetryCount; attempt++)
        {
            try
            {
                if (!File.Exists(logPath))
                {
                    logger.Warn($"{title} log does not exist: {logPath}");
                    return false;
                }

                lines = ReadAllLinesShared(logPath);
                return true;
            }
            catch (IOException ex)
            {
                lastException = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastException = ex;
            }

            if (attempt < ReadRetryCount)
            {
                Thread.Sleep(ReadRetryDelay);
            }
        }

        logger.Warn($"{title} log is temporarily locked and could not be read: {logPath}. {lastException?.Message}");
        return false;
    }

    private static string[] ReadAllLinesShared(string logPath)
    {
        using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        List<string> lines = [];
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines.ToArray();
    }
}
