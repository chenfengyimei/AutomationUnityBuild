namespace AutomationUnityBuildIOS;

internal sealed class WorkflowStepRunner(BuildLogger logger)
{
    public void Run(string name, Action action)
    {
        using StepTimer step = Start(name);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            step.Fail(ex);
            throw;
        }
    }

    public async Task RunAsync(string name, Func<Task> action)
    {
        using StepTimer step = Start(name);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            step.Fail(ex);
            throw;
        }
    }

    private StepTimer Start(string name)
    {
        logger.StepStarted(name);
        return new StepTimer(logger, name);
    }
}
