using CUClock.Shared.Contracts.Services;
using CUClock.Shared.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CUClock.Maui;

public partial class App : Application
{
    private CancellationTokenSource _cts = new();

    public App()
    {
        InitializeComponent();
    }

    public static IHost? BackgroundHost { get; set; }

    internal static void StartScheduler()
    {
        var scheduler = Dependencies.ServiceProvider.GetService<IScheduler>();
        var logger = Dependencies.ServiceProvider.GetService<ILogger<App>>();
        logger!.LogInformation("Window Resumed: starting scheduler...");
        scheduler?.Start();
        logger!.LogInformation("Window Resumed: scheduler started.");
    }

    internal static void StopScheduler()
    {
        var scheduler = Dependencies.ServiceProvider.GetService<IScheduler>();
        var logger = Dependencies.ServiceProvider.GetService<ILogger<App>>();
        logger!.LogInformation("Window Stopped: stoping scheduler...");
        scheduler?.Stop();
        logger!.LogInformation("Window Stopped: scheduler stopped.");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = new(new AppShell())
        {
            Title = "CUClock, cu cu.",
            Height = 500,
            Width = 700
        };
        BackgroundHost?.RunAsync(_cts.Token);
        return window;
    }
}