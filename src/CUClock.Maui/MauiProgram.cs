using Aphorismus.Shared.Services;
using CommunityToolkit.Maui;
using CUClock.Shared.Contracts.Services;
using CUClock.Shared.Helpers;
using CUClock.Shared.Services;
using CUClock.Shared.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using Plugin.Maui.Audio;
using AnnouncerVM = CUClock.Shared.ViewModels.Announcer;

namespace CUClock.Maui;

public static class MauiProgram
{
    public static IHost CreateBackgroundHost()
    {
        var hostBuilder = new HostBuilder();
        hostBuilder.ConfigureServices(services =>
        {
            services
                .AddLogging(configure => configure.AddDebug())
                .AddTransient<IPhraseProvider, PhraseProvider>(services =>
                {
                    var logger = services.GetService<ILogger<PhraseProvider>>()!;
                    // lambdas call local file system methods to read txt files
                    return new PhraseProvider(logger)
                    {
                        FileExists = filePath => FileSystem.AppPackageFileExistsAsync(filePath),
                        ReadFile = filePath => FileSystem.OpenAppPackageFileAsync(filePath)
                    };
                })
                .AddEmbeddingGenerator(services =>
                {
                    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
                            new OllamaApiClient(
                                new Uri("http://127.0.0.1:11434"),
                                defaultModel: "all-minilm");
                    return embeddingGenerator;
                }, ServiceLifetime.Transient);
            services.AddHostedService<SemanticSearch>();
        });
        var host = hostBuilder.Build();
        return host;
    }

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
                // https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/design/style/segoe-fluent-icons-font.md#icon-list
                fonts.AddFont("Segoe-Fluent-Icons.ttf", "SegoeFluentIcons");
            });
#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.AddAudio(); // NuGet: Plugin.Maui.Audio
        builder.Services
            .AddServices()
            .AddViewModels();

        var app = builder.Build();
        Dependencies.ServiceProvider = app.Services;
        return app;
    }

    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<AnnouncerVM>();
        services.AddTransient<Chapters>();
        return services;
    }

    // PhraseProvider factory with file access methods (exists and read)
    private static IServiceCollection AddServices(this IServiceCollection services) => services
        .AddTransient<IPhraseProvider, PhraseProvider>(services =>
        {
            var logger = services.GetService<ILogger<PhraseProvider>>()!;
            // lambdas call local file system methods to read txt files
            return new PhraseProvider(logger)
            {
                FileExists = filePath => FileSystem.AppPackageFileExistsAsync(filePath),
                ReadFile = filePath => FileSystem.OpenAppPackageFileAsync(filePath)
            };
        })
        .AddSingleton<IScheduler, Shared.Services.Scheduler>()
        .AddSingleton<IAnnouncer, Shared.Services.Announcer>()
        .AddTransient<IFileService, FileService>();
}