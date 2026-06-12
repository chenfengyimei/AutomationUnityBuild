namespace AutomationUnityBuildIOS;

internal sealed record CliOptions(
    string ConfigPath,
    bool ConfigWasSpecified,
    bool DryRun,
    bool Force,
    bool SkipGit,
    bool SkipUnity,
    bool SkipXcode,
    bool AllowNonMac,
    bool Verbose,
    bool Template)
{
    public static CliOptions Parse(IEnumerable<string> args)
    {
        string configPath = "build-ios.json";
        bool configWasSpecified = false;
        bool dryRun = false;
        bool force = false;
        bool skipGit = false;
        bool skipUnity = false;
        bool skipXcode = false;
        bool allowNonMac = false;
        bool verbose = false;
        bool template = false;

        using IEnumerator<string> enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            string arg = enumerator.Current;
            switch (arg)
            {
                case "--config":
                case "-c":
                    configPath = NextValue(enumerator, arg);
                    configWasSpecified = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--skip-git":
                    skipGit = true;
                    break;
                case "--skip-unity":
                    skipUnity = true;
                    break;
                case "--skip-xcode":
                    skipXcode = true;
                    break;
                case "--allow-non-mac":
                    allowNonMac = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--template":
                    template = true;
                    break;
                default:
                    throw new ArgumentException($"无法识别参数: {arg}");
            }
        }

        return new CliOptions(
            configPath,
            configWasSpecified,
            dryRun,
            force,
            skipGit,
            skipUnity,
            skipXcode,
            allowNonMac,
            verbose,
            template);
    }

    private static string NextValue(IEnumerator<string> enumerator, string optionName)
    {
        if (!enumerator.MoveNext() || string.IsNullOrWhiteSpace(enumerator.Current))
        {
            throw new ArgumentException($"{optionName} 后面需要一个值。");
        }

        return enumerator.Current;
    }
}

