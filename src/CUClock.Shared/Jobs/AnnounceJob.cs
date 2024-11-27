using CUClock.Shared.Contracts.Services;
using CUClock.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CUClock.Shared.Jobs;

internal class AnnounceJob : JobBase
{
    public const string CRON_KEY = "cronExpr";

    public override Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(Dependencies.ServiceProvider, nameof(Dependencies.ServiceProvider));

        var cronExpr = (string)context.MergedJobDataMap[CRON_KEY];
        var logger = Dependencies.ServiceProvider.GetService<ILogger<IJob>>();
        var announcer = Dependencies.ServiceProvider.GetService<IAnnouncer>();
        
        logger.LogInformation("CRON expression: {cronExpr}", cronExpr);
        logger.LogInformation("Scheduled FireTime was: {scheduledFireTime}",
            context.ScheduledFireTimeUtc?.ToLocalTime().TimeOfDay);
        logger.LogInformation("Actual FireTime is: {fireTime}",
            context.FireTimeUtc.ToLocalTime().TimeOfDay);

        logger.LogInformation("Calling Scheduler delegate...");
        announcer.GetScheduleFor(cronExpr)();
        logger.LogInformation("Delegate call finished...");
        return Task.CompletedTask;
    }
}
