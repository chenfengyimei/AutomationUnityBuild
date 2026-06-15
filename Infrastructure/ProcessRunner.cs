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
            return;
        }

        if (verbose || string.IsNullOrWhiteSpace(logPath))
        {
            Console.WriteLine(commandText);
        }
        else
        {
            Console.WriteLine($"{fileName} {args.FirstOrDefault()} ...");
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
            logger.CommandFailed(commandText, stopwatch.Elapsed, ex);
            throw;
        }

        if (process.ExitCode != 0)
        {
            string hint = string.IsNullOrWhiteSpace(logPath) ? "" : $"，日志: {logPath}";
            logger.CommandFailed(commandText, stopwatch.Elapsed, process.ExitCode);
            throw new InvalidOperationException($"命令执行失败({process.ExitCode}): {SensitiveText.Redact(commandText)}{hint}");
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
            Console.WriteLine(commandText);
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
            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

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
