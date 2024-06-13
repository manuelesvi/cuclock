using System.Globalization;
using Aphorismus.Shared.Services;
using CUClock.Windows.Activation;
using CUClock.Windows.Contracts.Services;
using CUClock.Windows.Core;
using CUClock.Windows.Core.Contracts.Services;
using CUClock.Windows.Core.Services;
using CUClock.Windows.Models;
using CUClock.Windows.Notifications;
using CUClock.Windows.Services;
using CUClock.Windows.ViewModels;
using CUClock.Windows.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace CUClock.Windows;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static WindowEx MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar
    {
        get; set;
    }

    public App()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-MX");
        InitializeComponent();
        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) =>
            {
                // Default Activation Handler
                services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

                // Other Activation Handlers
                services.AddTransient<IActivationHandler, AppNotificationActivationHandler>();

                // Services
                services.AddSingleton<IAppNotificationService, AppNotificationService>();
                services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
                services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
                services.AddTransient<INavigationViewService, NavigationViewService>();

                services.AddSingleton<IActivationService, ActivationService>();
                services.AddSingleton<IPageService, PageService>();
                services.AddSingleton<INavigationService, NavigationService>();

                // Core Services
                services.AddSingleton<IFileService, FileService>();
                services.AddSingleton<IAnnouncer, Announcer>();
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

                // Views and ViewModels
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SettingsPage>();
                services.AddTransient<TimeDisplayViewModel>();
                services.AddTransient<TimeDisplayPage>();
                services.AddTransient<ShellPage>();
                services.AddTransient<ShellViewModel>();

                // Configuration
                services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(
                    nameof(LocalSettingsOptions)));

                services.AddHostedService(services =>
                    (Announcer)services.GetService<IAnnouncer>()!);
            }).Build();

        App.GetService<IAppNotificationService>().Initialize();
        Task.Run(async () => await Host.RunAsync());
        UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // TODO: Log and handle exceptions as appropriate.
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        //App.GetService<IAppNotificationService>().Show(string.Format(
        //    "AppNotificationSamplePayload".GetLocalized(),
        //    AppContext.BaseDirectory));

        await App.GetService<IActivationService>().ActivateAsync(args);
    }
}
