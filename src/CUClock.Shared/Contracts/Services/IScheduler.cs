﻿using Cronos;
using CUClock.Shared.Services;

namespace CUClock.Shared.Contracts.Services;

public interface IScheduler
{
    Task Start();

    Task Stop();

    Task RegisterJobs(IDictionary<CronExpression, Announcer.Schedule> jobs);
}
