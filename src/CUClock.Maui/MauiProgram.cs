using Aphorismus.Shared.Services;
using CommunityToolkit.Maui;
using CUClock.Shared.Contracts.Services;
using CUClock.Shared.Services;
using CUClock.Shared.ViewModels;
using Microsoft.Extensions.Logging;

namespace CUClock.Maui;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
		builder.Logging.AddDebug();
#endif
        var services = builder.Services;
        services.AddTransient<IPhraseProvider, PhraseProvider>(services =>
        {
            var logger = services.GetService<ILogger<PhraseProvider>>()!;
            return new PhraseProvider(logger)
            {
                FileExists = filePath => Task.FromResult(
                    File.Exists(AppendRoot(filePath))),
                ReadFile = filePath => Task.FromResult(
                    (Stream)File.OpenRead(AppendRoot(filePath)))
            };
        });
        services.AddSingleton<IScheduler, Shared.Services.Scheduler>();
        services.AddSingleton<IAnnouncer, Announcer>();
        services.AddTransient<TimeDisplayViewModel>();
        //services.AddHostedService(services =>
        //    (Announcer)services.GetService<IAnnouncer>()!);

        var app = builder.Build();
        Shared.Helpers.Dependencies.ServiceProvider = app.Services;
        return app;
    }
}
