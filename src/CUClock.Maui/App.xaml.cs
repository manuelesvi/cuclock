using CUClock.Shared.Contracts.Services;
using CUClock.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace CUClock.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = new(new AppShell())
        {
            Title = "CUClock, cu cu.",
        };

        window.Stopped += Window_Stopped;
        window.Resumed += Window_Resumed;

        return window;
    }

    private void Window_Resumed(object? sender, EventArgs e)
    {
        StartScheduler();
    }

    private void Window_Stopped(object? sender, EventArgs e)
    {
        StopScheduler();
    }

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
}