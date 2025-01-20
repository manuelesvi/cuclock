using Aphorismus.Shared.Services;
using CommunityToolkit.Maui;
using CUClock.Shared.Contracts.Services;
using CUClock.Shared.Helpers;
using CUClock.Shared.Services;
using CUClock.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;

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
        builder.AddAudio(); // NuGet: Plugin.Maui.Audio
        
        var services = builder.Services;
        // PhraseProvider
        services.AddTransient<IPhraseProvider, PhraseProvider>(services =>
        {
            var logger = services.GetService<ILogger<PhraseProvider>>()!;
            return new PhraseProvider(logger)
            {
                FileExists = filePath => FileSystem.AppPackageFileExistsAsync(filePath),
                ReadFile = filePath => FileSystem.OpenAppPackageFileAsync(filePath)
            };
        });
        
        services.AddSingleton<IScheduler, Shared.Services.Scheduler>();
        services.AddSingleton<IAnnouncer, Announcer>();
        services.AddTransient<TimeDisplayViewModel>();
        services.AddTransient<Chapters>();

        // background processing is done by IScheduler,
        // ExecuteAsync has become obsolete

        //services.AddHostedService(services =>
        //    (Announcer)services.GetService<IAnnouncer>()!);

        var app = builder.Build();

        Dependencies.ServiceProvider = app.Services;
        
        return app;
    }
}
