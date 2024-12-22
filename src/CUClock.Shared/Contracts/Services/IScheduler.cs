using Cronos;
using CUClock.Shared.Services;

namespace CUClock.Shared.Contracts.Services;

using CronDictionary = IDictionary<
    CronExpression, Announcer.Schedule>;

public interface IScheduler
{
    Task RegisterJobs(CronDictionary jobs);

    Task Start();

    Task Stop();
}