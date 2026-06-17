namespace AutomationUnityBuildIOS;

internal static class PlatformBuildPipelineFactory
{
    public static IPlatformBuildPipeline Create(BuildRunContext context, WorkflowStepRunner stepRunner)
    {
        return context.Config.IsAndroid
            ? new AndroidBuildPipeline(context, stepRunner)
            : new IosBuildPipeline(context, stepRunner);
    }
}
