namespace AutomationUnityBuildIOS;

internal static class ConfigWizard
{
    public static string Run(string defaultConfigPath, bool configWasSpecified, bool force)
    {
        return IosConfigWizard.Run(defaultConfigPath, configWasSpecified, force);
    }
}
