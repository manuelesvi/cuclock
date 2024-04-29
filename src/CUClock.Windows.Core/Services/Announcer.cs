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

public class Announcer : BackgroundService,
    IAnnouncer
{
    // milliseconds
    private const int DefaultTimeOut = 4100;
    private const int BellsTimeOut = 15000;

    private readonly Random
        _random = new(); // some randomness

    /// <summary>
    /// A delegate that encapsulates
    /// a task programmed to be run in the future.
    /// </summary>
    /// <param name="cancellationToken">
    /// Stop or cancels the executing task.
    /// </param>
    private delegate void Schedule(
        CancellationToken cancellationToken);

    /// <summary>
    /// Precision for hour, minute and second.
    /// </summary>
    private const int SecondPrecision = 3;

    /// <summary>
    /// Precision for hour, minute, second
    /// and millisecond.
    /// </summary>
    private const int MillisecondPrecision = 4;

    /// <summary>
    /// TwoLetter ISO code.
    /// </summary>
    private const string Spanish = "es";

    /// <summary>
    /// Half an hour.
    /// </summary>
    private const int HalfHour = 30;
    
    private const int FourTasks = 4;
    private const int FiveTasks = 5;

    /// <summary>
    /// (Windows only)
    /// </summary>
#pragma warning disable CA1416 // skip platform compatibility
    private readonly SpeechSynthesizer _synth = new();

    /// <summary>
    /// A <see cref="List{VoiceInfo}"/> of installed voices in 
    /// <see cref="Spanish">.
    /// </summary>
    private readonly List<VoiceInfo> _voices = new();

    private readonly SoundPlayer
        _bells = new(
            "C:\\Users\\manchax\\Downloads\\1-154919.wav");

    // TODO: move wav files and include 'em as resources
    private readonly SoundPlayer
        _cucu = new(
            "C:\\Users\\manchax\\Downloads\\CUCKOOO.WAV");
    
    private readonly SoundPlayer
        _cucaracha = new(
            "C:\\Users\\manchax\\Downloads\\Voicy_La Cucaracha Horn.wav");

    /// <summary>
    /// Mexican spanish <see cref="CultureInfo"/>.
    /// </summary>
    private readonly CultureInfo _mxCulture
        = CultureInfo.GetCultureInfo("es-MX");

    private readonly ILogger<Announcer> _logger;

    public Announcer(ILogger<Announcer> logger)
    {
        CultureInfo.CurrentCulture = _mxCulture;
        CultureInfo.CurrentUICulture = _mxCulture;

        Schedules = new Dictionary<CronExpression, Schedule>
        {
            { CronExpression.Hourly, EnPunto },
            { CronExpression.Parse("15 * * * SUN-SAT"), CuartoDeHora },
            { CronExpression.Parse("30 * * * SUN-SAT"), YMedia },
            { CronExpression.Parse("45 * * * SUN-SAT"), CuartoPara }
        };

        var t1 = Task.Run(() =>
        {
            // Sets a value for the speaking rate
            _synth.Rate = -1;
            // Configures audio output
            _synth.SetOutputToDefaultAudioDevice();
        });
        var t2 = Task.Run(() =>
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

        _logger = logger;

        Task.Run(async () =>
            await Task.WhenAll(t1, t2));
    } // Announcer .ctor

    public void Announce(bool sayMilliseconds = true)
    {
        _ = Task.Run(async () =>
        {
            CultureInfo.CurrentCulture = _mxCulture;
            CultureInfo.CurrentUICulture = _mxCulture;
            await SayCurrentTime(sayMilliseconds);
        });
    }

    private void SelectVoice() => _synth.SelectVoice(_voices[
            _random.Next(0, _voices.Count) // selects a random voice
    ].Name);

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

    protected async override Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        Trace.Assert(Schedules.Count == FourTasks);
        var tasks = new List<Task>(Schedules.Count + 1)
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
            string.Format("The task count wasn't {0}",
            FiveTasks));

        await Task.WhenAll(tasks.ToArray());
    }

    private static string PrefijoHora(bool includeArt = true)
        => PrefijoHora(DateTime.Now.Hour, includeArt);

    private static string PrefijoHora(int hora,
        bool includeArt = true) => includeArt ? hora > 1 
        ? "Son las" : "Es la"
        : hora > 1 ? "Son" : "Es";

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
                precision: MillisecondPrecision)

            : DateTime.Now.TimeOfDay.Humanize(
                minUnit: TimeUnit.Second,
                culture: _mxCulture,
                precision: SecondPrecision));

        await Announce(txt, _cucaracha, stoppingToken);
    }

    private string Faltan(
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

    private static object SufijoHora(int hora) =>
        hora > 1 ? "s" : "";

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
        await Announce(txt, _bells, stoppingToken, BellsTimeOut);
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
        await Announce(txt, _bells, stoppingToken, BellsTimeOut);
    }

    private async Task Announce(string text,
        SoundPlayer? sound,
        CancellationToken stoppingToken,
        int pauseTimeMilliseconds = DefaultTimeOut)
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

    public override void Dispose()
    {
        _bells?.Dispose();
        _cucaracha?.Dispose();
        _cucu?.Dispose();
        _synth?.Dispose();
        base.Dispose();
    }
}
#pragma warning restore CA1416
