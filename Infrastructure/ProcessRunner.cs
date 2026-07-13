using System.Diagnostics;
using System.Text;

namespace AutomationUnityBuildIOS;

internal sealed class ProcessRunner(bool dryRun, bool verbose, BuildLogger logger)
{
    public async Task RunAsync(
        string fileName,
        IReadOnlyList<string> args,
        string? workingDirectory = null,
        string? logPath = null,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        string commandText = CommandLineFormatter.Format(fileName, args);
        string resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Environment.CurrentDirectory
            : workingDirectory;

        if (dryRun)
        {
            logger.DryRun(commandText);
            WriteDryRunCommandLog(logPath, commandText, resolvedWorkingDirectory);
            return;
        }

        if (verbose || string.IsNullOrWhiteSpace(logPath))
        {
            logger.Info($"执行命令: {commandText}");
        }
        else
        {
            logger.Info($"{fileName} {args.FirstOrDefault()} ...");
        }

        logger.CommandStarted(commandText, resolvedWorkingDirectory, logPath);
        using var logWriter = CommandLogWriter.Open(logPath, commandText, resolvedWorkingDirectory);
        var stopwatch = Stopwatch.StartNew();

        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.WorkingDirectory = resolvedWorkingDirectory;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;

        foreach (string arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (environment is not null)
        {
            foreach ((string key, string value) in environment)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    process.StartInfo.Environment[key] = value;
                }
            }
        }

        var outputClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        object writeLock = new();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                outputClosed.TrySetResult();
                return;
            }

            WriteLine(eventArgs.Data, isError: false);
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                errorClosed.TrySetResult();
                return;
            }

            WriteLine(eventArgs.Data, isError: true);
            lock (writeLock)
            {
                stderrBuilder.AppendLine(eventArgs.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            await Task.WhenAll(outputClosed.Task, errorClosed.Task);
        }
        catch (Exception ex)
        {
            ProcessSafety.KillProcessIfRunning(process);
            logger.CommandFailed(commandText, stopwatch.Elapsed, ex);
            throw;
        }

        if (process.ExitCode != 0)
        {
            string hint = string.IsNullOrWhiteSpace(logPath) ? "" : $"，日志: {logPath}";
            string stderr = stderrBuilder.ToString().Trim();
            string detail = string.IsNullOrWhiteSpace(stderr) ? "" : $"{Environment.NewLine}{SensitiveText.Redact(stderr)}";
            logger.CommandFailed(commandText, stopwatch.Elapsed, process.ExitCode);
            throw new InvalidOperationException($"命令执行失败({process.ExitCode}): {SensitiveText.Redact(commandText)}{detail}{hint}");
        }

        logger.CommandCompleted(commandText, stopwatch.Elapsed);

        void WriteLine(string line, bool isError)
        {
            lock (writeLock)
            {
                CommandLogWriter.WriteLine(logWriter, line, isError);
                logger.CommandOutput(fileName, line, isError, verbose || logWriter is null || isError);
            }
        }
    }

    private static void WriteDryRunCommandLog(string? logPath, string commandText, string workingDirectory)
    {
        using StreamWriter? logWriter = CommandLogWriter.Open(logPath, commandText, workingDirectory);
        CommandLogWriter.WriteLine(logWriter, "[dry-run] Command was not executed.", isError: false);
    }

    public async Task<string> RunCaptureStdoutAsync(
        string fileName,
        IReadOnlyList<string> args,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        string commandText = CommandLineFormatter.Format(fileName, args);
        string resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Environment.CurrentDirectory
            : workingDirectory;

        if (dryRun)
        {
            logger.DryRun(commandText);
            return "";
        }

        if (verbose)
        {
            logger.Info($"执行命令: {commandText}");
        }

        logger.CommandStarted(commandText, resolvedWorkingDirectory, commandLogPath: null);
        var stopwatch = Stopwatch.StartNew();

        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.WorkingDirectory = resolvedWorkingDirectory;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;

        foreach (string arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (environment is not null)
        {
            foreach ((string key, string value) in environment)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    process.StartInfo.Environment[key] = value;
                }
            }
        }

        try
        {
            process.Start();
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                logger.CommandFailed(commandText, stopwatch.Elapsed, process.ExitCode);
                string detail = string.IsNullOrWhiteSpace(stderr) ? "" : $"{Environment.NewLine}{stderr.Trim()}";
                throw new InvalidOperationException($"命令执行失败({process.ExitCode}): {SensitiveText.Redact(commandText)}{SensitiveText.Redact(detail)}");
            }

            logger.CommandCompleted(commandText, stopwatch.Elapsed);
            return stdout;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            ProcessSafety.KillProcessIfRunning(process);
            logger.CommandFailed(commandText, stopwatch.Elapsed, ex);
            throw;
        }
    }
}

internal static class CommandLineFormatter
{
    public static string Format(string fileName, IReadOnlyList<string> args)
    {
        return string.Join(" ", new[] { Quote(fileName) }.Concat(args.Select(Quote)));
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        bool needsQuote = value.Any(char.IsWhiteSpace) || value.Contains('"');
        if (!needsQuote)
        {
            return value;
        }

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}

internal static class ProcessSafety
{
    public static void KillProcessIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
