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
        _buildScheduler = Task.Run(BuildScheduler);
        _logger = logger;
    }

    public async Task Start()
    {
        ArgumentNullException.ThrowIfNull(_scheduler, nameof(_scheduler));
        if (_scheduler.IsStarted)
        {
            return;
        }

        _buildScheduler.Wait();
        await _scheduler.Start();
    }

    public async Task Stop()
    {
        ArgumentNullException.ThrowIfNull(_scheduler, nameof(_scheduler));
        if (_scheduler.InStandbyMode)
        {
            return;
        }
        await _scheduler.Standby();
    }

    public async Task RegisterJobs(IDictionary<CronExpression, Announcer.Schedule> jobs)
    {
        var jobCount = 0;
        foreach (var entry in jobs)
        {
            var expression = "0 " + entry.Key.ToString();
            var exprBuilder = new StringBuilder();
            var index = 0;
            var lastIndex = 0;
            var fieldNumber = 1;
            while ((index = expression.IndexOf(' ', lastIndex)) > -1)
            {
                var value = expression[lastIndex..index];
                // seconds
                if (fieldNumber == 4)
                {
                    // Day of month
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
            _logger.LogInformation("Creating job detail and trigger for {cronExpr}",
                expression);
            try
            {
                var jobDetail = JobBuilder.Create<AnnounceJob>()
                    .WithIdentity($"announceJob{++jobCount}", "group1")
                    .UsingJobData(AnnounceJob.CRON_KEY, expression)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"cronTrigger{jobCount}", "group1")
                    .WithCronSchedule(expression)
                    .StartNow()
                    .Build();

                _buildScheduler.Wait();
                await _scheduler.ScheduleJob(jobDetail, trigger); // associated

                _logger.LogInformation("Job #{jobCount} scheduled with CRON expression: {expr}",
                    jobCount, expression);

                var state = await _scheduler.GetTriggerState(trigger.Key);
                _logger.LogInformation("Job Trigger is in {state} state.", state);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "CRON expression is invalid");
                _logger.LogError("Job #{jobCount} FAILED!", jobCount);
            }
        }
    }

    private async Task BuildScheduler()
    {
        // you can have base properties
        var properties = new NameValueCollection();
        // and override values via builder
        _scheduler = await SchedulerBuilder.Create(properties)
            // default max concurrency is 10
            .UseDefaultThreadPool(x => x.MaxConcurrency = 5)
            .UseInMemoryStore()
            // this is the default 
            .WithMisfireThreshold(TimeSpan.FromSeconds(60))
            //.UsePersistentStore(x =>
            //{
            //    // force job data map values to be considered as strings
            //    // prevents nasty surprises if object is accidentally serialized and then 
            //    // serialization format breaks, defaults to false
            //    x.UseProperties = true;
            //    x.UseClustering();
            //    // there are other SQL providers supported too 
            //    x.UseSqlServer("my connection string");
            //    // this requires Quartz.Serialization.Json NuGet package
            //    x.UseJsonSerializer();
            //})
            // job initialization plugin handles our xml reading, without it defaults are used
            // requires Quartz.Plugins NuGet package
            //.UseXmlSchedulingConfiguration(x =>
            //{
            //    x.Files = new[] { "~/quartz_jobs.xml" };
            //    // this is the default
            //    x.FailOnFileNotFound = true;
            //    // this is not the default
            //    x.FailOnSchedulingError = true;
            //})
            .BuildScheduler();
    }
}