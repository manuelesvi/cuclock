using CUClock.Shared.Contracts.Services;
using CUClock.Shared.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CUClock.Maui;

public partial class App : Application
{
    private IHost? _backHost;
    private CancellationTokenSource _cancellationTokenSource = new();

    public App()
    {
        InitializeComponent();
        _backHost = MauiProgram.CreateBackgroundHost();
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

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = new(new AppShell())
        {
            Title = "CUClock, cu cu.",
            Height = 500,
            Width = 700
        };
        window.Stopped += async (s, e) =>
        {
            _cancellationTokenSource.Cancel();
            if (_backHost is null) return;
            await _backHost.StopAsync();
            _backHost.Dispose();
        };
        _backHost?.RunAsync(_cancellationTokenSource.Token);
        return window;
    }
}