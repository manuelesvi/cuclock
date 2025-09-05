using Cronos;
using CUClock.Shared.Services;

namespace CUClock.Shared.Contracts.Services;

using CronDictionary = IDictionary<
    CronExpression, Announcer.Schedule>;

public interface IScheduler
{
    Task RegisterJobs(CronDictionary jobs);

    /// <summary>
    /// Starts the threads that will run the jobs.
    /// </summary>
    /// <returns></returns>
    Task Start();

    Task Stop();
}