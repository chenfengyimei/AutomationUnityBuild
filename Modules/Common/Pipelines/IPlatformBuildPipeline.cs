namespace AutomationUnityBuildIOS;

internal interface IPlatformBuildPipeline
{
    string ResultPathLabel { get; }
    string ResultPath { get; }
    void PrintSummary();
    Task RunAsync();
}
