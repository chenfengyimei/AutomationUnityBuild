using System.Diagnostics;
using System.Text;
using AutomationUnityBuildIOS;

namespace DesktopApp.Services;

public sealed class BuildRunner
{
    public async Task<(bool Success, string? LogPath, string? ArtifactsRoot, string? Error)> RunAsync(
        string configPath,
        bool dryRun,
        bool skipGit,
        bool skipUnity,
        bool skipXcode,
        bool allowNonMac,
        Action<string> logSink,
        CancellationToken ct)
    {
        string? logPath = null;
        string? artifactsRoot = null;

        try
        {
            logSink($"正在加载配置: {configPath}");
            BuildConfig config = BuildConfig.Load(configPath);
            logSink($"配置加载成功: {config.ConfigName} / {config.BuildPlatform}");

            var options = new CliOptions(
                configPath, true, dryRun, false,
                skipGit, skipUnity, skipXcode, allowNonMac,
                true, false, BuildPlatforms.Ios);

            using var workflow = new AutomationWorkflow(config, options);
            logSink($"工作流已创建，开始执行...");

            var logTailCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var logTailTask = TailLogAsync(() =>
            {
                try
                {
                    var ctx = typeof(AutomationWorkflow)
                        .GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(workflow);
                    if (ctx is BuildRunContext brc)
                    {
                        logPath = brc.Paths.AutomationLogPath;
                        artifactsRoot = brc.Paths.ArtifactsRunRoot;
                        return brc.Paths.AutomationLogPath;
                    }
                }
                catch { }
                return null;
            }, logSink, logTailCts.Token);

            await workflow.RunAsync();
            logTailCts.Cancel();
            try { await logTailTask; } catch { }

            logSink("✅ 打包流程完成。");
            return (true, logPath, artifactsRoot, null);
        }
        catch (OperationCanceledException)
        {
            logSink("⚠️ 打包已取消。");
            return (false, logPath, artifactsRoot, "已取消");
        }
        catch (Exception ex)
        {
            logSink($"❌ 打包失败: {ex.Message}");
            return (false, logPath, artifactsRoot, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> CheckPrerequisitesAsync(
        string configPath, Action<string> logSink, CancellationToken ct)
    {
        try
        {
            logSink($"正在加载配置: {configPath}");
            BuildConfig config = BuildConfig.Load(configPath);
            var options = new CliOptions(
                configPath, true, false, false,
                false, false, false, true,
                true, false, BuildPlatforms.Ios);

            using var workflow = new AutomationWorkflow(config, options);
            logSink("开始环境检查...");

            var logTailCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var logTailTask = TailLogAsync(() =>
            {
                try
                {
                    var ctx = typeof(AutomationWorkflow)
                        .GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(workflow);
                    if (ctx is BuildRunContext brc) return brc.Paths.AutomationLogPath;
                }
                catch { }
                return null;
            }, logSink, logTailCts.Token);

            await workflow.CheckPrerequisitesAsync();
            logTailCts.Cancel();
            try { await logTailTask; } catch { }

            logSink("✅ 环境检查完成。");
            return (true, null);
        }
        catch (Exception ex)
        {
            logSink($"❌ 环境检查失败: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private static async Task TailLogAsync(Func<string?> getLogPath, Action<string> logSink, CancellationToken ct)
    {
        string? logPath = null;
        long lastOffset = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (logPath is null)
                {
                    logPath = getLogPath();
                    if (logPath is null) { await Task.Delay(200, ct); continue; }
                }

                if (File.Exists(logPath))
                {
                    using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    fs.Seek(lastOffset, SeekOrigin.Begin);
                    using var reader = new StreamReader(fs, Encoding.UTF8);
                    string? line;
                    while ((line = await reader.ReadLineAsync(ct)) is not null)
                    {
                        logSink(line);
                    }
                    lastOffset = fs.Position;
                }
            }
            catch (OperationCanceledException) { break; }
            catch { }

            await Task.Delay(300, ct);
        }
    }
}
