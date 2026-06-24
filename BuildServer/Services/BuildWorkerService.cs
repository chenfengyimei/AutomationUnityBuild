using System.Diagnostics;
using System.Text;
using BuildServer.Persistence;
using BuildServer.Reverse;
using BuildServer.Security;

namespace BuildServer.Services;

public sealed class BuildWorkerService(
    JsonDatabase database,
    BuildServerOptions options,
    ArtifactScanner artifactScanner,
    IWebHostEnvironment environment,
    IGatewayPushChannel gatewayPushChannel,
    ILogger<BuildWorkerService> logger) : BackgroundService
{
    private readonly object _processLock = new();
    private Process? _currentProcess;
    private string _currentJobId = "";
    private readonly string _workerId = $"worker-{Environment.MachineName}";

    public async Task<bool> CancelRunningAsync(string jobId, CurrentUser user)
    {
        Process? processToKill = null;
        lock (_processLock)
        {
            if (!string.Equals(_currentJobId, jobId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            processToKill = _currentProcess;
        }

        try
        {
            if (processToKill is not null && !processToKill.HasExited)
            {
                processToKill.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "取消任务 {JobId} 时终止进程失败", jobId);
        }

        await database.UpdateAsync(db =>
        {
            BuildJobRecord? job = db.Jobs.FirstOrDefault(job => job.Id == jobId);
            if (job is not null)
            {
                job.Status = BuildStatuses.Canceled;
                job.FinishedAt = DateTimeOffset.Now;
                job.Error = "用户取消正在运行的任务。";
                AuthService.AddAudit(db, user.Id, user.UserName, "build.cancel-running", "job", job.Id, "取消正在运行的打包任务。");
            }
        });

        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RegisterWorkerAsync(WorkerStatuses.Idle, "");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                BuildJobRecord? job = await DequeueAsync();
                if (job is null)
                {
                    await RegisterWorkerAsync(WorkerStatuses.Idle, "");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                await RunJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Worker 循环异常");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }

        await RegisterWorkerAsync(WorkerStatuses.Offline, "");
    }

    private async Task<BuildJobRecord?> DequeueAsync()
    {
        return await database.UpdateAsync(db =>
        {
            if (db.Jobs.Any(job => job.Status == BuildStatuses.Running))
            {
                return null;
            }

            BuildJobRecord? job = db.Jobs
                .Where(job => job.Status == BuildStatuses.Queued)
                .OrderBy(job => job.CreatedAt)
                .FirstOrDefault();
            if (job is null)
            {
                return null;
            }

            job.Status = BuildStatuses.Running;
            job.StartedAt = DateTimeOffset.Now;
            job.WorkerId = _workerId;
            return Clone(job);
        });
    }

    private async Task RunJobAsync(BuildJobRecord job, CancellationToken stoppingToken)
    {
        await RegisterWorkerAsync(WorkerStatuses.Running, job.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(job.WorkerLogPath)!);

        try
        {
            AutomationCommand command = AutomationToolLocator.Locate(options, environment);
            List<string> args = [.. command.PrefixArgs, "run", "--config", job.MaterializedConfigPath];
            if (job.DryRun) args.Add("--dry-run");
            if (job.SkipGit) args.Add("--skip-git");
            if (job.SkipUnity) args.Add("--skip-unity");
            if (job.SkipXcode) args.Add("--skip-xcode");
            if (job.AllowNonMac || job.DryRun) args.Add("--allow-non-mac");

            int exitCode = await RunProcessAsync(job.Id, command.FileName, args, command.WorkingDirectory, job.WorkerLogPath, stoppingToken);
            await database.UpdateAsync(db =>
            {
                BuildJobRecord? storedJob = db.Jobs.FirstOrDefault(item => item.Id == job.Id);
                if (storedJob is null)
                {
                    return;
                }

                storedJob.ExitCode = exitCode;
                storedJob.FinishedAt = DateTimeOffset.Now;
                storedJob.Status = storedJob.Status == BuildStatuses.Canceled
                    ? BuildStatuses.Canceled
                    : exitCode == 0 ? BuildStatuses.Succeeded : BuildStatuses.Failed;
                if (exitCode != 0 && string.IsNullOrWhiteSpace(storedJob.Error))
                {
                    storedJob.Error = $"打包工具退出码: {exitCode}";
                }
            });

            BuildJobRecord? completedJob = await database.ReadAsync(db => db.Jobs.FirstOrDefault(item => item.Id == job.Id));
            if (completedJob is not null)
            {
                await artifactScanner.ScanAsync(completedJob);
            }

            if (gatewayPushChannel.IsConnected)
            {
                _ = gatewayPushChannel.PushJobUpdatedAsync(job.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "执行任务 {JobId} 失败", job.Id);
            await database.UpdateAsync(db =>
            {
                BuildJobRecord? storedJob = db.Jobs.FirstOrDefault(item => item.Id == job.Id);
                if (storedJob is null)
                {
                    return;
                }

                storedJob.Status = storedJob.Status == BuildStatuses.Canceled ? BuildStatuses.Canceled : BuildStatuses.Failed;
                storedJob.FinishedAt = DateTimeOffset.Now;
                storedJob.Error = ex.Message;
            });
        }
        finally
        {
            lock (_processLock)
            {
                _currentProcess = null;
                _currentJobId = "";
            }

            await RegisterWorkerAsync(WorkerStatuses.Idle, "");
        }
    }

    private async Task<int> RunProcessAsync(
        string jobId,
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        string logPath,
        CancellationToken cancellationToken)
    {
        using var logStream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        using var logWriter = new StreamWriter(logStream, Encoding.UTF8) { AutoFlush = true };
        object writeLock = new();
        await logWriter.WriteLineAsync($"[{DateTimeOffset.Now:O}] START {fileName} {string.Join(" ", args.Select(Quote))}");
        await logWriter.WriteLineAsync($"[{DateTimeOffset.Now:O}] CWD {workingDirectory}");

        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

        foreach (string arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                lock (writeLock)
                {
                    logWriter.WriteLine(eventArgs.Data);
                }
                if (gatewayPushChannel.IsConnected)
                {
                    _ = gatewayPushChannel.PushLogChunkAsync(jobId, eventArgs.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                lock (writeLock)
                {
                    logWriter.WriteLine(eventArgs.Data);
                }
                if (gatewayPushChannel.IsConnected)
                {
                    _ = gatewayPushChannel.PushLogChunkAsync(jobId, eventArgs.Data);
                }
            }
        };

        process.Start();
        lock (_processLock)
        {
            _currentProcess = process;
            _currentJobId = jobId;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        await logWriter.WriteLineAsync($"[{DateTimeOffset.Now:O}] EXIT {process.ExitCode}");
        return process.ExitCode;
    }

    private async Task RegisterWorkerAsync(string status, string currentJobId)
    {
        await database.UpdateAsync(db =>
        {
            WorkerNodeRecord? worker = db.Workers.FirstOrDefault(worker => worker.Id == _workerId);
            if (worker is null)
            {
                worker = new WorkerNodeRecord
                {
                    Id = _workerId,
                    Name = options.WorkerName,
                    HostName = Environment.MachineName,
                    Enabled = true
                };
                db.Workers.Add(worker);
            }

            worker.Status = status;
            worker.CurrentJobId = currentJobId;
            worker.LastSeenAt = DateTimeOffset.Now;
        });
    }

    private static BuildJobRecord Clone(BuildJobRecord job)
    {
        return new BuildJobRecord
        {
            Id = job.Id,
            ProjectId = job.ProjectId,
            ConfigId = job.ConfigId,
            RequestedByUserId = job.RequestedByUserId,
            Source = job.Source,
            Status = job.Status,
            BuildPlatform = job.BuildPlatform,
            Branch = job.Branch,
            BuildNumber = job.BuildNumber,
            DryRun = job.DryRun,
            SkipGit = job.SkipGit,
            SkipUnity = job.SkipUnity,
            SkipXcode = job.SkipXcode,
            AllowNonMac = job.AllowNonMac,
            ClientRequestId = job.ClientRequestId,
            Notes = job.Notes,
            MaterializedConfigPath = job.MaterializedConfigPath,
            WorkerLogPath = job.WorkerLogPath,
            ArtifactRoot = job.ArtifactRoot,
            ExitCode = job.ExitCode,
            Error = job.Error,
            WorkerId = job.WorkerId,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt
        };
    }

    private static string Quote(string value)
    {
        return value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    }
}
