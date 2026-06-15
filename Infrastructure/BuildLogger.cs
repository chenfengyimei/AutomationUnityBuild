using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AutomationUnityBuildIOS;

internal sealed class BuildLogger : IDisposable
{
    private readonly object _lock = new();
    private readonly StreamWriter? _writer;
    private readonly bool _verbose;
    private bool _disposed;

    private BuildLogger(string? logPath, StreamWriter? writer, bool verbose)
    {
        LogPath = logPath;
        _writer = writer;
        _verbose = verbose;
    }

    public string? LogPath { get; }

    public static BuildLogger Create(string logPath, bool verbose, bool dryRun)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var writer = new StreamWriter(logPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
            {
                AutoFlush = true
            };

            var logger = new BuildLogger(logPath, writer, verbose);
            logger.Info($"日志文件: {logPath}");
            return logger;
        }
        catch (Exception ex) when (dryRun)
        {
            Console.WriteLine($"[WARN] dry-run 日志文件创建失败，只输出到控制台: {ex.Message}");
            return new BuildLogger(null, null, verbose);
        }
    }

    public void Info(string message)
    {
        Write("INFO", message, writeToConsole: true);
    }

    public void Warn(string message)
    {
        Write("WARN", message, writeToConsole: true);
    }

    public void Error(string message)
    {
        Write("ERROR", message, writeToConsole: true, Console.Error);
    }

    public void Error(string message, Exception ex)
    {
        Write("ERROR", $"{message}: {ex.Message}", writeToConsole: true, Console.Error);
        if (_verbose)
        {
            Write("ERROR", ex.ToString(), writeToConsole: true, Console.Error);
        }
    }

    public void StepStarted(string name)
    {
        Write("STEP", $"START {name}", writeToConsole: true);
    }

    public void StepCompleted(string name, TimeSpan elapsed)
    {
        Write("STEP", $"DONE {name} ({FormatDuration(elapsed)})", writeToConsole: true);
    }

    public void StepFailed(string name, TimeSpan elapsed, Exception ex)
    {
        Write("STEP", $"FAIL {name} ({FormatDuration(elapsed)}): {ex.Message}", writeToConsole: true, Console.Error);
    }

    public void DryRun(string commandText)
    {
        Write("DRYRUN", SensitiveText.Redact(commandText), writeToConsole: true);
    }

    public void CommandStarted(string commandText, string workingDirectory, string? commandLogPath)
    {
        string logHint = string.IsNullOrWhiteSpace(commandLogPath) ? "" : $" | log={commandLogPath}";
        Write("CMD", $"START {SensitiveText.Redact(commandText)} | cwd={workingDirectory}{logHint}", writeToConsole: _verbose);
    }

    public void CommandOutput(string source, string line, bool isError, bool writeToConsole)
    {
        string level = isError ? "STDERR" : "STDOUT";
        TextWriter console = isError ? Console.Error : Console.Out;
        Write(level, $"{source}: {line}", writeToConsole, console);
    }

    public void CommandCompleted(string commandText, TimeSpan elapsed)
    {
        Write("CMD", $"DONE {SensitiveText.Redact(commandText)} ({FormatDuration(elapsed)})", writeToConsole: _verbose);
    }

    public void CommandFailed(string commandText, TimeSpan elapsed, int exitCode)
    {
        Write("CMD", $"FAIL exitCode={exitCode} {SensitiveText.Redact(commandText)} ({FormatDuration(elapsed)})", writeToConsole: true, Console.Error);
    }

    public void CommandFailed(string commandText, TimeSpan elapsed, Exception ex)
    {
        Write("CMD", $"FAIL {SensitiveText.Redact(commandText)} ({FormatDuration(elapsed)}): {ex.Message}", writeToConsole: true, Console.Error);
    }

    public static string FormatDuration(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss")
            : elapsed.ToString(@"mm\:ss\.fff");
    }

    private void Write(string level, string message, bool writeToConsole, TextWriter? console = null)
    {
        string line = $"[{Timestamp()}] [{level}] {SensitiveText.Redact(message)}";

        lock (_lock)
        {
            _writer?.WriteLine(line);
            if (writeToConsole)
            {
                (console ?? Console.Out).WriteLine(line);
            }
        }
    }

    private static string Timestamp()
    {
        return DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_lock)
        {
            _writer?.Dispose();
        }
    }
}

internal sealed class StepTimer(BuildLogger logger, string name) : IDisposable
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private bool _completed;

    public void Complete()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        logger.StepCompleted(name, _stopwatch.Elapsed);
    }

    public void Fail(Exception ex)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        logger.StepFailed(name, _stopwatch.Elapsed, ex);
    }

    public void Dispose()
    {
        Complete();
    }
}

internal static class CommandLogWriter
{
    public static StreamWriter? Open(string? logPath, string commandText, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var writer = new StreamWriter(logPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            AutoFlush = true
        };

        writer.WriteLine($"[{Timestamp()}] [CMD] {SensitiveText.Redact(commandText)}");
        writer.WriteLine($"[{Timestamp()}] [CWD] {workingDirectory}");
        return writer;
    }

    public static void WriteLine(StreamWriter? writer, string line, bool isError)
    {
        writer?.WriteLine($"[{Timestamp()}] [{(isError ? "STDERR" : "STDOUT")}] {SensitiveText.Redact(line)}");
    }

    private static string Timestamp()
    {
        return DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
    }
}

internal static class SensitiveText
{
    private static readonly Regex UrlCredentialRegex = new(
        @"(?<scheme>https?://)[^\s/@]+@",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GitHubTokenRegex = new(
        @"\b(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{20,}\b|\bgithub_pat_[A-Za-z0-9_]{20,}\b",
        RegexOptions.Compiled);
    private static readonly Regex GitLabTokenRegex = new(
        @"\bglpat-[A-Za-z0-9_\-]{20,}\b",
        RegexOptions.Compiled);
    private static readonly Regex BearerTokenRegex = new(
        @"(?<prefix>\bBearer\s+)[A-Za-z0-9._\-+/=]{12,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex KeyValueSecretRegex = new(
        @"(?<key>\b(password|passwd|pwd|token|secret|api[-_]?key|access[-_]?key)\b\s*[:=]\s*)(?<value>[^\s,;]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string redacted = UrlCredentialRegex.Replace(value, "${scheme}***@");
        redacted = GitHubTokenRegex.Replace(redacted, "***");
        redacted = GitLabTokenRegex.Replace(redacted, "***");
        redacted = BearerTokenRegex.Replace(redacted, "${prefix}***");
        redacted = KeyValueSecretRegex.Replace(redacted, "${key}***");
        return redacted;
    }
}
