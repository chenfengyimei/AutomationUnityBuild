namespace AutomationUnityBuildIOS;

internal static class UnityCommandBuilder
{
    public static List<string> CreateBatchModeArgs(BuildConfig config, BuildPaths paths, string buildTarget)
    {
        return
        [
            "-batchmode",
            "-quit",
            "-nographics",
            "-accept-apiupdate",
            "-projectPath",
            paths.UnityProjectRoot,
            "-buildTarget",
            buildTarget,
            "-executeMethod",
            config.UnityBuildMethod,
            "-logFile",
            paths.UnityLogPath
        ];
    }

    public static void AddBundleVersionArgs(List<string> args, BuildConfig config, BuildLogger logger)
    {
        AddPair(args, "-customBuildNumber", config.BuildNumber);
        if (config.SyncBundleVersionFromUnity)
        {
            logger.Info("Bundle Version 同步 Unity 项目设置，本次不会用配置文件强制覆盖。");
            return;
        }

        AddPair(args, "-customBundleVersion", config.BundleVersion);
        logger.Info($"Bundle Version 使用配置文件固定值: {config.BundleVersion}");
    }

    public static void AddCommonPlayerArgs(List<string> args, BuildConfig config)
    {
        AddPair(args, "-customBundleIdentifier", config.BundleIdentifier);
        AddPair(args, "-customProductName", config.ProductName);
    }

    public static void AddMetadataPath(List<string> args, BuildPaths paths)
    {
        AddPair(args, "-customBuildMetadataPath", paths.UnityBuildMetadataPath);
    }

    public static void AddPair(List<string> args, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        args.Add(key);
        args.Add(value);
    }
}
