using System.Diagnostics;
using System.Globalization;
using System.Media;
using System.Speech.Synthesis;
using Cronos;
using CUClock.Windows.Core.Contracts.Services;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CUClock.Windows.Core;

/// <summary>
/// Consumes underlying OS TextToSpeech synthesis
/// (currently supported on Windows only)
/// to SAY the current time at specific intervals
/// or at will using
/// <see cref="IAnnouncer.Announce(bool)"/> method.
/// </summary>
/// <remarks>
/// 
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
///  <example>
///  0 15 6-18 * * MON-SAT (daily every hour at 15 minutes monday until saturday)
///  0 15 6-18 * * 1-6 (same as above but using numbers for day of week)
///  </example>
public class Announcer : BackgroundService,
    IAnnouncer
{
    // milliseconds
    private const int Default_Length = 41 * 100;
    private const int Bells_Length   = 15 * 1000;

    /// <summary>
    /// Precision for hour, minute and second.
    /// </summary>
    private const int Precision_Second = 3;

    /// <summary>
    /// Precision for hour, minute, second
    /// and millisecond.
    /// </summary>
    private const int Precision_Millisecond = 4;

    /// <summary>
    /// TwoLetter ISO code.
    /// </summary>
    private const string Spanish = "es";

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

    private readonly Random
        _random = new(); // some randomness

    /// <summary>
    /// Defines a speaking task
    /// programmed to execute in the future.
    /// They are defined for each quarter of an hour
    /// and repeated until next hour.
    /// </summary>
    /// <param name="cancellationToken">
    /// Stop or cancels the executing task.
    /// </param>
    private delegate void Schedule(
        CancellationToken cancellationToken);

    /// <summary>
    /// Mexican spanish <see cref="CultureInfo"/>.
    /// </summary>
    private readonly CultureInfo _mxCulture
        = CultureInfo.GetCultureInfo("es-MX");

    /// <summary>
    /// Logging.
    /// </summary>
    private readonly ILogger<Announcer> _logger;

#pragma warning disable CA1416 // skip platform compatibility
    
    // TODO: move wav files and include 'em as resources
    private readonly SoundPlayer
        _bells = new(
            "C:\\Users\\manchax\\Downloads\\1-154919.wav");

    private readonly SoundPlayer
        _cucu = new(
            "C:\\Users\\manchax\\Downloads\\CUCKOOO.WAV");

    private readonly SoundPlayer
        _cucaracha = new(
            "C:\\Users\\manchax\\Downloads\\Voicy_La Cucaracha Horn.wav");

    /// <summary>
    /// TTS synth (Windows only).
    /// </summary>
    private readonly SpeechSynthesizer _synth = new();

    /// <summary>
    /// A <see cref="List{VoiceInfo}"/> of installed voices in 
    /// <see cref="Spanish">.
    /// </summary>
    private readonly List<VoiceInfo> _voices = new();

    /// <summary>
    /// Main constructor
    /// </summary>
    /// <param name="logger"></param>
    public Announcer(ILogger<Announcer> logger)
    {
        CultureInfo.CurrentCulture = _mxCulture;
        CultureInfo.CurrentUICulture = _mxCulture;
        _logger = logger;
        Schedules = new Dictionary<CronExpression, Schedule>
        {
            { CronExpression.Hourly, EnPunto },
            { CronExpression.Parse("15 * * * SUN-SAT"), CuartoDeHora },
            { CronExpression.Parse("30 * * * SUN-SAT"), YMedia },
            { CronExpression.Parse("45 * * * SUN-SAT"), CuartoPara }
        };

        var setupTTS = Task.Run(() =>
        {
            // Sets a value for the speaking rate
            _synth.Rate = -1;
            // Configures audio output
            _synth.SetOutputToDefaultAudioDevice();
        });

        var readVoices = Task.Run(() =>
        {
            // list all voices as log info
            foreach (var item in _synth.GetInstalledVoices()
                .Where(v => v.VoiceInfo.Culture
                    .TwoLetterISOLanguageName == Spanish))
            {
                _voices.Add(item.VoiceInfo);
#if DEBUG
                _logger.LogInformation("{culture} - {voice}",
                    item.VoiceInfo.Culture.ToString(),
                    item.VoiceInfo.Name);
#endif
            } // for each
        });

        _ = Task.Run(async () =>
            await Task.WhenAll(setupTTS, readVoices));
    } // Announcer .ctor()

    /// <summary>
    /// A read-only <see cref="Dictionary2{TKey, TValue}"/>
    /// that holds
    /// <see cref="Schedule"/> tasks scheduled
    /// to execute at 0, 15, 30 & 45 minutes each hour (MON-SUN).
    /// </summary>
    private Dictionary<CronExpression, Schedule> Schedules
    {
        get;
    }

    public void Announce(bool sayMilliseconds = true)
    {
        _ = Task.Run(async () =>
        {
            CultureInfo.CurrentCulture = _mxCulture;
            CultureInfo.CurrentUICulture = _mxCulture;
            await SayCurrentTime(sayMilliseconds);
        });
    }

    private static string PrefijoHora(int hora, bool includeArt = true) => includeArt
        ? hora > 1
            ? "Son las"
            : "Es la"
        : hora > 1
            ? "Son"
            : "Es";

    private static object SufijoHora(int hora) =>
        hora > 1 ? "s" : "";

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Trace.Assert(Schedules.Count == FourTasks,
            $"Invalid count, expected {FourTasks}");

        var tasks = new List<Task>(
            Schedules.Count + 1) // each quarter + present
        {
            SayCurrentTime(
                sayMilliseconds: true,
                stoppingToken)
        };

        foreach (var key in Schedules.Keys)
        {
            tasks.Add(
                WaitUntilNext(key, stoppingToken));
        }

        Trace.Assert(tasks.Count == FiveTasks,
            $"Invalid count, expected {FiveTasks}");
        await Task.WhenAll(tasks.ToArray()); // done
    }

    private async Task SayCurrentTime(
        bool sayMilliseconds = true,
        CancellationToken stoppingToken = new())
    {
        var txt = string.Format(DateTime.Now.Minute > HalfHour
            ? Faltan() : "Es : {0}",
            sayMilliseconds ? DateTime.Now.TimeOfDay
            .Humanize(
                minUnit: TimeUnit.Millisecond,
                culture: _mxCulture,
                precision: Precision_Millisecond)

            : DateTime.Now.TimeOfDay.Humanize(
                minUnit: TimeUnit.Second,
                culture: _mxCulture,
                precision: Precision_Second));

        await Announce(txt, _cucaracha, stoppingToken);
    }

    private static string Faltan(
        bool saySecondsAndMilliseconds = false)
    {
        var currentTime = DateTime.Now;
        var secondsTxt = saySecondsAndMilliseconds
            ? string.Format(
                "{0} {1}", 60 - currentTime.Second,
                1000 - currentTime.Millisecond)
            : string.Empty;
        var txt = string.Format(
            "Faltan {3} {0} para la{2} {1}",
            60 - DateTime.Now.Minute,
            DateTime.Now.Hour + 1 > 12
                ? 1 + DateTime.Now.Hour - 12
                : 1 + DateTime.Now.Hour,
            SufijoHora(DateTime.Now.Hour),
            secondsTxt);
        return txt;
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
            _logger.LogInformation("{now} {cron} - Durmiendo por {ttn} hasta {next}",
                DateTime.Now,
                cron.ToString(),
                timeToNext.Humanize(3,
                    maxUnit: TimeUnit.Day,
                    minUnit: TimeUnit.Second),
                next.ToLocalTime().ToString());

            await Task.Delay(timeToNext, stoppingToken);

            _logger.LogInformation("{now} {cron} - Despertando a las {humanized} !!!",
                DateTime.Now, cron.ToString(),
                DateTime.Now.TimeOfDay.Humanize(2,
                    maxUnit: TimeUnit.Hour,
                    minUnit: TimeUnit.Minute));

            // call delegate
            Schedules[cron](stoppingToken);

            // sleep 1s, 100 ms
            await Task.Delay(1100, stoppingToken);
            // re-calculate next ocurrence
            utcNow = DateTime.UtcNow;
            next = cron.GetNextOccurrence(utcNow)
                ?? throw new NullReferenceException();
            timeToNext = next - utcNow;
        }
    }

    private async void EnPunto(CancellationToken stoppingToken)
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

        await Announce(txt, _cucu, stoppingToken);
        await Task.Delay(250, stoppingToken);
        await Announce(string.Format("La{1} {0} en punto", hora,
            hora == 1 ? "" : "s"),
            sound: _cucu, stoppingToken);
    }

    private async void CuartoDeHora(CancellationToken stoppingToken)
    {
        var hora = DateTime.Now.TimeOfDay.Hours < 13
            ? DateTime.Now.TimeOfDay.Hours
            : DateTime.Now.TimeOfDay.Hours - 12;
        var txt = string.Format("{0} {1} y cuarto",
            PrefijoHora(hora), hora);
        await Announce(txt, _bells, stoppingToken, Bells_Length);
    }

    private async void YMedia(CancellationToken stoppingToken)
    {
        var hora = DateTime.Now.TimeOfDay.Hours < 13
            ? DateTime.Now.TimeOfDay.Hours
            : DateTime.Now.TimeOfDay.Hours - 12;
        var txt = string.Format("{0} {1} y media",
            PrefijoHora(hora), hora);
        await Announce(txt, _cucaracha, stoppingToken);
    }

    private async void CuartoPara(CancellationToken stoppingToken)
    {
        var hora = DateTime.Now.TimeOfDay.Hours + 1 < 13
            ? DateTime.Now.TimeOfDay.Hours + 1
            : DateTime.Now.TimeOfDay.Hours - 11;
        var txt = string.Format("{0} cuarto para la{2} {1}",
            PrefijoHora(hora, includeArt: false), hora,
            hora > 1 ? "s" : "");
        await Announce(txt, _bells, stoppingToken, Bells_Length);
    }

    private async Task Announce(string text,
        SoundPlayer? sound,
        CancellationToken stoppingToken,
        int pauseTimeMilliseconds = Default_Length)
    {
        if (sound is not null)
        {
            sound.Play();
            await Task.Delay(pauseTimeMilliseconds,
                stoppingToken);
        }
        SelectVoice();
        _synth.Speak(text);
        _logger.LogInformation(text);
    }

    private void SelectVoice() => _synth.SelectVoice(_voices[
            _random.Next(0, _voices.Count) // selects a random voice
    ].Name);
#pragma warning restore CA1416

    public override void Dispose()
    {
        _bells?.Dispose();
        _cucaracha?.Dispose();
        _cucu?.Dispose();
        _synth?.Dispose();
        base.Dispose();
    }
}