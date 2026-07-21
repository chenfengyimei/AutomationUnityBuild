namespace AutomationUnityBuildIOS;

internal static class PlatformBuildPipelineFactory
{
    public static IPlatformBuildPipeline Create(BuildRunContext context, WorkflowStepRunner stepRunner)
    {
        if (context.Config.IsTiktok)
        {
            return new TiktokBuildPipeline(context, stepRunner);
        }

        return context.Config.IsAndroid
            ? new AndroidBuildPipeline(context, stepRunner)
            : new IosBuildPipeline(context, stepRunner);
    }
}
