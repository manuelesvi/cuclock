using Quartz;

namespace CUClock.Shared.Jobs;
internal abstract class JobBase : IJob
{
    public abstract Task Execute(IJobExecutionContext context);
}
