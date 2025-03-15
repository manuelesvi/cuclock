using Quartz;

namespace CUClock.Shared.Jobs;

/// <summary>
/// Base class used to implement a background job.
/// </summary>
internal abstract class BaseJob : IJob
{
    public abstract Task Execute(IJobExecutionContext context);
}
