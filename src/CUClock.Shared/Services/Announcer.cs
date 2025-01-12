using System.Diagnostics;
using System.Globalization;
using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Messages;
using Aphorismus.Shared.Services;
using CommunityToolkit.Mvvm.Messaging;
using Cronos;
using CUClock.Shared.Contracts.Services;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using Plugin.Maui.Audio;

namespace CUClock.Shared.Services;

#nullable enable

/// <summary>
/// Announces current local time at specific intervals
/// when executed as a <see cref="BackgroundService"/>
/// or at will calling
/// <see cref="IAnnouncer.Announce(bool)"/> method.
/// </summary>
/// <remarks>
/// Intervals are defined using CRON expressions:
///
///                                         Allowed values    Allowed special characters Comment
///  ┌───────────── second(optional)        0-59              * , - /
///  │ ┌───────────── minute                0-59              * , - /
///  │ │ ┌───────────── hour                0-23              * , - /
///  │ │ │ ┌───────────── day of month      1-31              * , - / L W ?
///  │ │ │ │ ┌───────────── month           1-12 or JAN-DEC   * , - /
///  │ │ │ │ │ ┌───────────── day of week   0-6  or SUN-SAT   * , - / # L ?              Both 0 and 7 means SUN
///  │ │ │ │ │ │
///  * * * * * *
///  </remarks>
///
///  <example>
///  0 15 6-18 * * MON-SAT (daily every hour at 15 minutes monday until saturday)
///  0 15 6-18 * * 1-6 (same as above but using numbers for day of week)
///  </example>
public class Announcer : BackgroundService, IAnnouncer
{
    private const int Default_Duration = 41 * 100; // 4.1 seconds
    private const int Bells_Duration = 150 * 100; // 15 seconds
    private const int Bells_AfterDelay = 370 * 100; // delay after 1st melody

    /// <summary>
    /// Precision for minute and second.
    /// </summary>
    private const int Precision_Second = 2;

    /// <summary>
    /// Precision for minute, second and millisecond.
    /// </summary>
    private const int Precision_Millisecond = 3;

    /// <summary>
    /// Half an hour.
    /// </summary>
    private const int HalfHour = 30;

    /// <summary>
    /// Expected amount of <see cref="Task"/>s
    /// at the beginning of
    /// <see cref="ExecuteAsync(CancellationToken)"/>.
    /// </summary>
    private const int FourTasks = 4;

    /// <summary>
    /// Expected amount of <see cref="Task"/>s
    /// at the end of
    /// <see cref="ExecuteAsync(CancellationToken)"/>.
    /// </summary>
    private const int FiveTasks = 5;

    /// <summary>
    /// Defines a speaking task
    /// programmed to execute in the future.
    /// They are defined for each quarter of an hour
    /// and repeated every hour.
    /// </summary>
    public delegate Task Schedule();

    /// <summary>
    /// Mexican spanish <see cref="CultureInfo"/>.
    /// </summary>
    private readonly CultureInfo _mxCulture
        = CultureInfo.GetCultureInfo("es-MX");

    private readonly IPhraseProvider _phraseProvider;
    private readonly IScheduler _scheduler;
    private readonly ILogger<Announcer> _logger;
    private readonly IAudioManager _audioManager;
    private readonly Random _random = new();

    private const string BellsWAV = "bells.wav";
    private const string CuCuWAV = "CUCKOOO.WAV";
    private const string CucarachaWAV = "horn.wav";
    private const string PistolWAV = "pistol.wav";
    private const string Gallo1WAV = "rooster1.wav";
    private const string Gallo2WAV = "rooster2.wav";
    private const string PajaroLocoWAV = "pajaro_loco.wav";

    private readonly SpeechOptions _ttsOptions = new();
    private readonly Task _loadLocales;
    private Locale[]? _esLocales;
    private CancellationTokenSource? _silence = new();

    private readonly Stack<Frase> _previous = new();
    private readonly Stack<Frase> _next = new();

    /// <summary>
    /// Initializes an <see cref="Announcer"/>.
    /// </summary>
    /// <param name="phraseProvider">Aphorisms provider.</param>
    /// <param name="logger"></param>
    public Announcer(
        IPhraseProvider phraseProvider,
        IScheduler scheduler,
        IAudioManager audioManager,
        ILogger<Announcer> logger)
    {
        CultureInfo.CurrentCulture = _mxCulture;
        CultureInfo.CurrentUICulture = _mxCulture;

        _phraseProvider = phraseProvider;
        _scheduler = scheduler;
        _audioManager = audioManager;
        _logger = logger;

        Schedules = new Dictionary<CronExpression, Schedule>
        {
            { CronExpression.Hourly, EnPunto },
            { CronExpression.Parse("15 * * * SUN-SAT"), CuartoDeHora },
            { CronExpression.Parse("30 * * * SUN-SAT"), YMedia },
            { CronExpression.Parse("45 * * * SUN-SAT"), CuartoPara }
        };

        _loadLocales = Task.Run(async () =>
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            _esLocales = locales
                .Where(l => l.Language.StartsWith(
                    _mxCulture.TwoLetterISOLanguageName))
                .ToArray();
        });

        var startScheduler = Task.Run(async () =>
        {
            await _scheduler.RegisterJobs(Schedules).ContinueWith(async (t) =>
            {
                await _scheduler.Start();
                _logger.LogInformation(
                    "Job Scheduler started on {time}...",
                    DateTime.Now.ToLongTimeString());
            });
        });

        _ = Task.Run(async () =>
            await Task.WhenAll(_loadLocales, startScheduler));
    } // Announcer .ctor()

    public bool EnableAphorisms
    {
        get; set;
    } = true;

    public int PreviousCount => _previous.Count;

    public int NextCount => _next.Count;

    /// <summary>
    /// A read-only token that silences the TTS system.
    /// </summary>
    public CancellationToken SilenceToken => _silence!.Token;

    /// <summary>
    /// A read-only <see cref="Dictionary2{TKey, TValue}"/>
    /// that holds
    /// <see cref="Schedule"/> tasks scheduled
    /// to run at 0, 15, 30 & 45 minutes each hour (MON-SUN).
    /// </summary>
    private Dictionary<CronExpression, Schedule> Schedules
    {
        get;
    }

    public void Announce(bool sayMilliseconds = true)
    {
        _logger.LogInformation("Announce began execution. {time}",
            DateTime.Now.ToLongTimeString());
        _ = Task.Run(async () =>
        {
            CultureInfo.CurrentCulture = _mxCulture;
            CultureInfo.CurrentUICulture = _mxCulture;
            await SayCurrentTime(sayMilliseconds);
            _logger.LogInformation("Announce executed. {time}",
                DateTime.Now.ToLongTimeString());
        });
    }

    public void Silence()
    {
        _logger.LogInformation("Silencing...");
        _silence?.Cancel();
        _silence = new();
        _logger.LogInformation("Silenced.");
    }

    public void SpeakPhrase(bool conGallo)
    {
        Silence();
        
        ArgumentNullException.ThrowIfNull(_silence,
            nameof(_silence));
        ArgumentNullException.ThrowIfNull(_silence?.Token,
            nameof(_silence.Token));

        _ = Task.Run(async () =>
        {
            if (conGallo)
            {
                IList<(string file, int duration)> gallos = [
                    (Gallo1WAV, 30), // 3 segundos
                    (Gallo2WAV, 35)]; // 3.5s

                var (file, duration) = gallos[_random.Next(0, 2)];
                PlaySound(file);
                await Task.Delay(duration * 100);
            }

            PlaySound(PistolWAV);
            await Task.Delay(2000);
            SpeakPhrase();
        });
    }

    public void SpeakPhrase(Frase frase) => Speak(frase.Texto);

    public void Previous()
    {
        if (_previous.Count == 0)
        {
            return;
        }
        var f = _previous.Pop();
        _next.Push(f);
        SendMessage(f); // previous one
    }

    public void Next()
    {
        if (_next.Count == 0)
        {
            return;
        }
        var f = _next.Pop();
        _previous.Push(f);
        SendMessage(f); // next one
    }

    public Schedule GetScheduleFor(string cronExpression)
    {
        var minutes = int.Parse(cronExpression.Split(' ')[1]);
        return GetSchedule(minutes);
    }

    protected async override Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        Trace.Assert(Schedules.Count == FourTasks,
            $"Invalid count, expected {FourTasks}");

        var tasks = new List<Task>(
            Schedules.Count + 1) // each quarter + present
        {
            SayCurrentTime(sayMilliseconds: true)
        };
        foreach (var key in Schedules.Keys)
        {
            tasks.Add(
                WaitUntilNext(key, stoppingToken));
        }

        Trace.Assert(tasks.Count == FiveTasks,
            $"Invalid count, expected {FiveTasks}");
        await Task.WhenAll(tasks); // done
    }

    private async Task WaitUntilNext(CronExpression cron,
        CancellationToken stoppingToken)
    {
        var utcNow = DateTime.UtcNow;
        var next = cron.GetNextOccurrence(utcNow)
            ?? throw new NullReferenceException();
        var timeToNext = next - utcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "{now} {cron} - Durmiendo por {ttn} hasta {next}",
                DateTime.Now, cron.ToString(),
                timeToNext.Humanize(3,
                    maxUnit: TimeUnit.Day,
                    minUnit: TimeUnit.Second),
                next.ToLocalTime().ToString());

            await Task.Delay(timeToNext, stoppingToken);

            _logger.LogInformation(
                "{now} {cron} - Despertando a las {humanized} !!!",
                DateTime.Now, cron.ToString(),
                DateTime.Now.TimeOfDay.Humanize(2,
                    maxUnit: TimeUnit.Hour,
                    minUnit: TimeUnit.Minute));

            // call delegate
            _logger.LogInformation("Task.Delay is being replaced with Quartz Jobs, see AnnounceJob & RegisterJobs method from this project's Scheduler ...NOT Quartz!!!");
            // Schedules[cron](stoppingToken);

            // sleep 1s, 100 ms
            await Task.Delay(1100, stoppingToken);
            // re-calculate next ocurrence
            utcNow = DateTime.UtcNow;
            next = cron.GetNextOccurrence(utcNow)
                ?? throw new NullReferenceException();
            timeToNext = next - utcNow;
        }
    }

    private static string PrefijoHora(int hora, bool conArticulo = true) => conArticulo
        ? hora > 1
            ? "Son las"
            : "Es la"
        : hora > 1
            ? "Son"
            : "Es";

    private static string SufijoHora(int hora)
        => hora > 1 ? "s" : "";

    private static string Faltan(
        bool saySecondsAndMilliseconds = false)
    {
        var currentTime = DateTime.Now;
        var secondsTxt = saySecondsAndMilliseconds
            ? string.Format("{1} milisegundos, {0} segundos y, ",
                60 - currentTime.Second,
                1000 - currentTime.Millisecond)
            : string.Empty;

        var horaSig = DateTime.Now.Hour + 1 > 12
            ? 1 + DateTime.Now.Hour - 12
            : 1 + DateTime.Now.Hour;

        var txt = string.Format(
            "Faltan {3} {0} {4} para la{2} {1}",
            60 - DateTime.Now.Minute,
            horaSig, SufijoHora(horaSig),
            secondsTxt,
            saySecondsAndMilliseconds ? "minutos" : string.Empty);

        return txt;
    }
    
    private Schedule GetSchedule(int minutes) => minutes switch
    {
        0 => EnPunto,
        15 => CuartoDeHora,
        30 => YMedia,
        45 => CuartoPara,
        _ => throw new NotSupportedException(),
    };

    private async Task SayCurrentTime(bool sayMilliseconds = true)
    {
        _silence = new CancellationTokenSource();
        var now = DateTime.Now;
        var hora = now.Hour > 12 ? now.Hour - 12 : now.Hour;
        var txt = string.Format(now.Minute > HalfHour
            ? Faltan(sayMilliseconds)
            : now.Hour >= 1 && now.Hour < 12
            ? "{0} de la mañana, {1}"
            : now.Hour == 12
            ? "doce del medio día, {1}"
            : now.Hour >= 13
            ? "{0} de la {2}, {1}"
            : "doce de la noche, ", // now.Hour == 0
            string.Format("{0} {1}", PrefijoHora(hora), hora),
            sayMilliseconds ? now.TimeOfDay.Add(TimeSpan.FromHours(-1 * now.Hour))
                .Humanize(
                    maxUnit: TimeUnit.Minute,
                    minUnit: TimeUnit.Millisecond,
                    culture: _mxCulture,
                    precision: Precision_Millisecond)
            : now.TimeOfDay.Add(TimeSpan.FromHours(-1 * now.Hour)).Humanize(
                maxUnit: TimeUnit.Minute,
                minUnit: TimeUnit.Second,
                culture: _mxCulture,
                precision: Precision_Second),
            now.Hour >= 13 && now.Hour < 20 ? "tarde" : "noche");

        await Announce(txt, CucarachaWAV);
        SpeakPhrase();
    }

    private async Task EnPunto()
    {
        var hora = DateTime.Now.TimeOfDay.Hours < 13
            ? DateTime.Now.TimeOfDay.Hours
            : DateTime.Now.TimeOfDay.Hours - 12;

        var txt = string.Format(
            "{0} {1} {2}",
            PrefijoHora(hora), hora,
            DateTime.Now.TimeOfDay.Hours > 0 &&
            DateTime.Now.TimeOfDay.Hours < 4
                ? "de la madrugada"
                : DateTime.Now.TimeOfDay.Hours < 11
                ? "de la mañana"
                : DateTime.Now.TimeOfDay.Hours < 12
                ? "del día"
                : DateTime.Now.TimeOfDay.Hours == 12
                ? "del medio día"
                : DateTime.Now.TimeOfDay.Hours < 19
                ? "de la tarde"
                : "de la noche");

        await Announce(txt, wavFile: CuCuWAV);
        await Task.Delay(250, SilenceToken);
        await Announce(string.Format("La{1} {0} en punto",
            hora > 1 ? hora.ToString() : "una",
            hora == 1 ? "" : "s"));
        SpeakPhrase();
    }

    private async Task CuartoDeHora()
    {
        var hora = DateTime.Now.TimeOfDay.Hours < 13
            ? DateTime.Now.TimeOfDay.Hours
            : DateTime.Now.TimeOfDay.Hours - 12;
        var txt = string.Format("{0} {1} y cuarto",
            PrefijoHora(hora),
            hora != 1 ? hora : "una");
        await Announce(txt, PajaroLocoWAV);
        SpeakPhrase();
    }

    private async Task YMedia()
    {
        var hora = DateTime.Now.TimeOfDay.Hours < 13
            ? DateTime.Now.TimeOfDay.Hours
            : DateTime.Now.TimeOfDay.Hours - 12;
        var txt = string.Format("{0} {1} y media",
            PrefijoHora(hora, conArticulo: false), // {0}
            hora != 1 ? hora : "una"); // {1}
        await Announce(txt, CucarachaWAV);
        SpeakPhrase();
    }

    private async Task CuartoPara()
    {
        var hora = DateTime.Now.TimeOfDay.Hours + 1 < 13
            ? DateTime.Now.TimeOfDay.Hours + 1
            : DateTime.Now.TimeOfDay.Hours - 11;
        var txt = string.Format("{0} cuarto para la{2} {1}",
            PrefijoHora(hora, conArticulo: false),
            hora != 1 ? hora : "una",
            hora > 1 ? "s" : "");
        await Announce(txt, BellsWAV, Bells_Duration);
        await Task.Delay(Bells_AfterDelay, SilenceToken);
        SpeakPhrase();
    }

    private async Task Announce(string text,
        string wavFile = "",
        int pauseTimeMilliseconds = Default_Duration)
    {
        _logger.LogTrace("Announce began execution...");
        _logger.LogInformation(message: text);
        await Task.Run(async () =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(wavFile))
                {
                    PlaySound(wavFile);
                    await Task.Delay(pauseTimeMilliseconds, SilenceToken);
                }
                Speak(text);
            }
            catch (OperationCanceledException e)
            {
                _logger.LogError(e, "TTS operation was canceled.");
#if DEBUG
                Debugger.Break();
#endif
            }
        });
        _logger.LogTrace("Announce execution finished.");
    }

    private async void PlaySound(string wavFile)
    {
        if (string.IsNullOrWhiteSpace(wavFile))
        {
            return;
        }
        using var player = _audioManager.CreateAsyncPlayer(
            await FileSystem.OpenAppPackageFileAsync(
                filename: Path.Combine("WAVs", wavFile)));
        await player.PlayAsync(_silence!.Token);
    }

    private void Speak(string text)
    {
        if (!_loadLocales.IsCompleted)
        {
            _loadLocales.Wait();
        }
        SelectVoice();
        _silence ??= new();
        TextToSpeech.Default.SpeakAsync(text, _ttsOptions,
            cancelToken: _silence.Token);
    }

    private void SelectVoice()
    {
        if (!_loadLocales.IsCompleted)
        {
            _loadLocales.Wait();
        }
        _ttsOptions.Locale = _esLocales![
            _random.Next(0, _esLocales.Length)
        ];
        _logger.LogInformation("Selected voice: {voice}, language = {language}, country = {country}",
            _ttsOptions.Locale.Name,
            _ttsOptions.Locale.Language,
            _ttsOptions.Locale.Country);
    }

    private void SpeakPhrase()
    {
        if (!EnableAphorisms)
        {
            return;
        }

        // move all items in _next stack to _previous
        var next = _next.ToArray();
        _next.Clear();
        foreach (var item in next)
        {
            _previous.Push(item);
        }

        var phrase = _phraseProvider.GetRandomPhrase(_random);
        _previous.Push(phrase);
        SendMessage(phrase); // new one
    }

    private void SendMessage(Frase f)
    {
        // sends chosen phrase
        WeakReferenceMessenger.Default
            .Send(new PhrasePickedMessage(f));
        _logger.LogInformation("PhrasePickedMessage sent.");

        SpeakPhrase(f);
    }

    public override void Dispose()
    {
        _previous.Clear();
        _next.Clear();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
#nullable disable