using System.Diagnostics;
using CUClock.Shared.Contracts.Services;
using CUClock.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace CUClock.Shared.Jobs;

internal class AnnounceJob : JobBase
{
    public const string CRON_KEY = "cronExpr";

    public override Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(Dependencies.ServiceProvider, nameof(Dependencies.ServiceProvider));

        var cronExpr = (string)context.MergedJobDataMap[CRON_KEY];
        Debug.WriteLine("CRON expression: " + cronExpr);
        Debug.WriteLine("Scheduled FireTime was: " + context.ScheduledFireTimeUtc?.ToLocalTime().TimeOfDay);
        Debug.WriteLine("Actual FireTime is: " + context.FireTimeUtc.ToLocalTime().TimeOfDay);

        var announcer = Dependencies.ServiceProvider.GetService<IAnnouncer>();
        announcer.GetScheduleFor(cronExpr)(announcer.SilenceToken);

        return Task.CompletedTask;
    }
}
