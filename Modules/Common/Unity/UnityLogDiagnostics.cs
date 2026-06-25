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

    public string? TryGetKnownFailureMessage(BuildPaths paths)
    {
        string[] lines = ReadExistingLines(paths.UnityLogPath)
            .Concat(ReadExistingLines(paths.UnityProcessLogPath))
            .ToArray();

        return TryGetKnownFailureMessage(lines);
    }

    internal static string? TryGetKnownFailureMessage(IEnumerable<string> lines)
    {
        string[] normalizedLines = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        string? androidSigningEvidence = normalizedLines.FirstOrDefault(IsAndroidSigningFailureLine);
        if (androidSigningEvidence is not null)
        {
            return
                "Android 签名配置不完整或密码不可用。请检查 androidKeystoreName、androidKeystorePass、androidKeyaliasName、androidKeyaliasPass；如果 Key Alias 密码和 keystore 密码一致，可以填同一个值。" +
                $" 日志线索: {androidSigningEvidence.Trim()}";
        }

        bool licenseWasAccepted = normalizedLines.Any(IsUnityLicenseSuccessLine);
        string? licenseEvidence = normalizedLines.LastOrDefault(IsUnityLicenseFailureLine);
        if (licenseEvidence is not null && !licenseWasAccepted)
        {
            return
                "Unity Editor License 未激活或不可用。请在这台打包机上使用运行 BuildServer 的同一个 Windows/macOS 用户打开 Unity Hub，登录并激活 Unity Editor 许可证，或安装有效的 .ulf 离线许可证；确认 Unity Hub 和 Unity Editor 的 Licensing Client 正常后重新打包。" +
                $" 日志线索: {licenseEvidence.Trim()}";
        }

        return null;
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

    private static string[] ReadExistingLines(string logPath)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(logPath) && File.Exists(logPath)
                ? ReadAllLinesShared(logPath)
                : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsUnityLicenseFailureLine(string line)
    {
        return ContainsAny(
            line,
            "No valid Unity Editor license found",
            "Please activate your license",
            "No ULF license found",
            "Token not found in cache",
            "Access token is unavailable",
            "Unable to update licenses",
            "com.unity.editor.headless",
            "Failed to handshake to channel",
            "Unsupported protocol version",
            "LicensingClient has failed validation");
    }

    private static bool IsUnityLicenseSuccessLine(string line)
    {
        return ContainsAny(
            line,
            "Successfully updated license",
            "Serial number assigned",
            "Successfully resolved entitlement details");
    }

    private static bool IsAndroidSigningFailureLine(string line)
    {
        return ContainsAny(
            line,
            "Can not sign the application",
            "Unable to sign the application; please provide passwords",
            "Android signing",
            "签名缺少",
            "缺少 Key Alias",
            "缺少 keystore");
    }

    private static bool ContainsAny(string line, params string[] needles)
    {
        return needles.Any(needle => line.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
