namespace AutomationUnityBuildIOS;

internal sealed class BuildRunContext : IDisposable
{
    private BuildRunContext(
        BuildConfig config,
        CliOptions options,
        BuildPaths paths,
        BuildLogger logger,
        ProcessRunner processRunner)
    {
        Config = config;
        Options = options;
        Paths = paths;
        Logger = logger;
        ProcessRunner = processRunner;
    }

    public BuildConfig Config { get; }
    public CliOptions Options { get; }
    public BuildPaths Paths { get; }
    public BuildLogger Logger { get; }
    public ProcessRunner ProcessRunner { get; }
    public bool RuntimeConfigChanged { get; private set; }

    public static BuildRunContext Create(BuildConfig config, CliOptions options)
    {
        BuildPaths paths = BuildPaths.Create(config);
        BuildLogger logger = BuildLogger.Create(paths.AutomationLogPath, options.Verbose, options.DryRun);
        ProcessRunner processRunner = new(options.DryRun, options.Verbose, logger);
        return new BuildRunContext(config, options, paths, logger, processRunner);
    }

    public void MarkRuntimeConfigChanged()
    {
        RuntimeConfigChanged = true;
    }

    public void Dispose()
    {
        Logger.Dispose();
    }
}
