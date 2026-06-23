namespace AutomationUnityBuildIOS;

internal sealed class RuntimeConfigUpdater(BuildRunContext context)
{
    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildLogger _logger => context.Logger;

    public void PrepareBuildNumberForRun()
    {
        if (!_config.AutoIncrementBuildNumber)
        {
            _logger.Info("Build Number 自动+1: 关闭");
            return;
        }

        if (_options.DryRun)
        {
            _logger.Info($"[dry-run] Build Number 自动+1: {BuildDisplay.BuildNumber(_config.BuildNumber)} -> {NextBuildNumber(_config.BuildNumber)}");
            return;
        }

        if (_options.SkipUnity)
        {
            _logger.Info("跳过 Unity 导出，本次不自动增加 Build Number。");
            return;
        }

        string previousBuildNumber = _config.BuildNumber;
        _config.BuildNumber = NextBuildNumber(previousBuildNumber);
        context.MarkRuntimeConfigChanged();
        _logger.Info($"Build Number 自动+1: {BuildDisplay.BuildNumber(previousBuildNumber)} -> {_config.BuildNumber}");
    }

    public void SaveChangesIfNeeded()
    {
        if (!context.RuntimeConfigChanged)
        {
            return;
        }

        string configPath = Path.GetFullPath(_options.ConfigPath);
        ConfigFileWriter.Save(configPath, _config);
        _logger.Info($"已保存运行时更新到配置文件: {configPath}");
    }

    public static string NextBuildNumber(string currentBuildNumber)
    {
        string current = currentBuildNumber.Trim();
        if (string.IsNullOrWhiteSpace(current))
        {
            return "1";
        }

        if (!CanIncrementBuildNumber(current, out ulong numericBuildNumber))
        {
            throw new InvalidOperationException(
                $"autoIncrementBuildNumber=true 时 buildNumber 必须是纯数字，当前值是 {currentBuildNumber}。可以改成数字，或在配置里关闭 autoIncrementBuildNumber。");
        }

        checked
        {
            numericBuildNumber++;
        }

        string next = numericBuildNumber.ToString();
        return current.Length > next.Length && current.StartsWith('0')
            ? next.PadLeft(current.Length, '0')
            : next;
    }

    public static bool CanIncrementBuildNumber(string buildNumber, out ulong value)
    {
        value = 0;
        string current = buildNumber.Trim();
        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        if (!current.All(char.IsDigit) || !ulong.TryParse(current, out value))
        {
            return false;
        }

        try
        {
            checked
            {
                ulong incremented = value + 1;
            }

            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
