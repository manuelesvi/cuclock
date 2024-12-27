using System.Collections.Specialized;
using System.Text;
using CUClock.Shared.Jobs;
using Microsoft.Extensions.Logging;
using Quartz;
using CronExpression = Cronos.CronExpression;
using IQuartzScheduler = Quartz.IScheduler;

namespace CUClock.Shared.Services;
using IScheduler = Contracts.Services.IScheduler;

public class Scheduler : IScheduler
{
    private IQuartzScheduler _scheduler;
    private readonly ILogger<IScheduler> _logger;
    private readonly Task _buildScheduler;

    public Scheduler(ILogger<IScheduler> logger)
    {
        _logger = logger;
        _buildScheduler = Task.Run(BuildScheduler);
    }

    private async Task BuildScheduler()
    {
        var properties = new NameValueCollection();
        _scheduler = await SchedulerBuilder.Create(properties)
            .UseDefaultThreadPool(x => x.MaxConcurrency = 5)
            .UseInMemoryStore()
            .WithMisfireThreshold(TimeSpan.FromSeconds(60))
            .BuildScheduler();
    }

    public async Task Start()
    {
        ArgumentNullException.ThrowIfNull(_scheduler, nameof(_scheduler));
        if (_scheduler.IsStarted)
        {
            return; // already started
        }

        _buildScheduler.Wait();
        }

        await _scheduler.Start(); // start it
    }

    public async Task Stop()
    {
        ArgumentNullException.ThrowIfNull(_scheduler, nameof(_scheduler));
        if (_scheduler.InStandbyMode)
        {
            return;
        }
        await _scheduler.Standby(); // stops firing triggers
    }

    public async Task RegisterJobs(IDictionary<CronExpression, Announcer.Schedule> jobs)
    {
        var jobNumber = 0;
        foreach (var entry in jobs)
        {
            if (!await RegisterJob(++jobNumber, entry.Key))
            {
                --jobNumber;
            }
        }
    }

    private async Task<bool> RegisterJob(int jobNumber, CronExpression cron)
    {
        var expression = ParseCRONExpression(cron.ToString());
        _logger.LogInformation("Creating job detail and trigger for {cronExpr}",
            expression);
        try
        {
            var jobDetail = JobBuilder.Create<AnnounceJob>()
                .WithIdentity($"announceJob{jobNumber}", "group1")
                .UsingJobData(AnnounceJob.CRON_KEY, expression)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"cronTrigger{jobNumber}", "group1")
                .WithCronSchedule(expression)
                .StartNow()
                .Build();

            if (!_buildScheduler.IsCompleted)
            {
                _buildScheduler.Wait();
            }
            await _scheduler.ScheduleJob(jobDetail, trigger); // associated

            _logger.LogInformation("Job #{jobNumber} scheduled with CRON expression: {expr}",
                jobNumber, expression);

            var state = await _scheduler.GetTriggerState(trigger.Key);
            _logger.LogInformation("Job Trigger is in {state} state.", state);
            return true;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "CRON expression is invalid");
            _logger.LogError("Job #{jobNumber} FAILED!", jobNumber);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't create Job #{jobNumber}", jobNumber);
            return false;
        }
    }

    private static string ParseCRONExpression(string original)
    {
        var expression = "0 " + original;
        var exprBuilder = new StringBuilder();
        var index = 0;
        var lastIndex = 0;
        var fieldNumber = 1;
        while ((index = expression.IndexOf(' ', lastIndex)) > -1)
        {
            var value = expression[lastIndex..index];
            if (fieldNumber == 4) // Day of month
            {
                exprBuilder.Append("? ");
            }
            else
            {
                exprBuilder.Append(value).Append(' ');
            }
            lastIndex = index + 1;
            ++fieldNumber;
        }
        expression = exprBuilder.ToString() + "* ";
        return expression;
    }
}