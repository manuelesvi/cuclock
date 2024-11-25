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
        services.AddSingleton<IScheduler, Shared.Services.Scheduler>();
        services.AddTransient<IPhraseProvider, PhraseProvider>(services =>
        {
            var logger = services.GetService<ILogger<PhraseProvider>>()!;
            var path = Path.GetDirectoryName(Environment.ProcessPath!)!;
            DirectoryInfo di;
            while ((di = Directory.GetParent(path)!).Name != "src")
            {
                path = di.FullName;
            }
            di = Directory.GetParent(path)!;
            path = Path.Combine(di.FullName,
                "aphorismus\\src\\Aphorismus\\Resources\\Raw");

            return new PhraseProvider(logger)
            {
                FileExists = filePath => Task.FromResult(
                    File.Exists(AppendRoot(filePath))),
                ReadFile = filePath => Task.FromResult(
                    (Stream)File.OpenRead(AppendRoot(filePath)))
            };

            string AppendRoot(string filePath)
            {
                filePath = filePath.Replace("/", "\\");
                var fullPath = Path.Combine(path, filePath);
                logger.LogInformation("Fetching content from: {path}", fullPath);
                return fullPath;
            }
        });
        services.AddTransient<IAnnouncer, Announcer>();
        services.AddTransient<TimeDisplayViewModel>();
        //services.AddHostedService(services =>
        //    (Announcer)services.GetService<IAnnouncer>()!);

        Shared.Helpers.Dependencies.ServiceProvider = services.BuildServiceProvider();

        return builder.Build();
    }
}
