using System.Diagnostics;
using System.Media;
using System.Speech.Synthesis;
using Cronos;
using Humanizer;
using Humanizer.Localisation;

namespace CUClock;

#pragma warning disable CA1416 // Validate platform compatibility
public class Worker : BackgroundService
{
    private delegate void Schedule(CancellationToken cancellationToken);
    private const int DefaultTimeOut = 35000;
    private const int BellsTimeOut = 65000;

    // Available on Windows only
    private readonly SpeechSynthesizer _synth = new();
    private readonly SoundPlayer
        _cucu = new(
            "C:\\Users\\manchax\\Downloads\\CUCKOOO.WAV");
    private readonly SoundPlayer
        _cucaracha = new(
            "C:\\Users\\manchax\\Downloads\\Voicy_La Cucaracha Horn.wav");
    private readonly SoundPlayer
        _bells = new(
            "C:\\Users\\manchax\\Downloads\\1-154919.wav");

    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        Schedules = new Dictionary<CronExpression, Schedule>
        {
            { CronExpression.Hourly, EnPunto },
            {
                CronExpression.Parse("15 * * * SUN-SAT"),
                CuartoDeHora
            },
            {
                CronExpression.Parse("30 * * * SUN-SAT"),
                YMedia
            },
            {
                CronExpression.Parse("45 * * * SUN-SAT"),
                CuartoPara
            }
        };

        // Configure the audio output.
        _synth.SelectVoice("Microsoft Sabina Desktop");
        // Set a value for the speaking rate.
        _synth.Rate = -1;
        _synth.SetOutputToDefaultAudioDevice();
#if DEBUG
        // list all voices as log info
        foreach (var item in _synth.GetInstalledVoices())
        {
            _logger.LogInformation("{culture} - {voice}",
                item.VoiceInfo.Culture.ToString(),
                item.VoiceInfo.Name);
        }
#endif
    }

    /// <summary>
    /// Holds <see cref="Schedule"/> instances
    /// scheduled to run at 0, 15, 30 & 45 minutes every hour.
    /// </summary>
    private Dictionary<CronExpression, Schedule> Schedules
    {
        get;
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Trace.Assert(Schedules.Count == 4);
        var tasks = new List<Task>(Schedules.Count + 1)
        {
            SayCurrentTime(stoppingToken)
        };
        foreach (var key in Schedules.Keys)
        {
            tasks.Add(
                WaitUntilNext(key, stoppingToken));
        }
        Trace.Assert(tasks.Count == 5);
        await Task.WhenAll(tasks.ToArray());
    }

    private static string PrefijoHora(int hora, bool includeArt = true)
    {
        if (includeArt)
        {
            return hora > 1 ? "Son las" : "Es la";
        }
        else
        {
            return hora > 1 ? "Son" : "Es";
        }
    }

    private async Task SayCurrentTime(CancellationToken stoppingToken)
    {
        var txt = string.Format("La hora actual es: {0}",
            DateTime.Now.TimeOfDay.Humanize(
                precision: 3,
                minUnit: TimeUnit.Second));
        await Announce(txt, _cucaracha, stoppingToken);
    }

    private async Task WaitUntilNext(CronExpression cron, CancellationToken stoppingToken)
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
            PrefijoHora(hora, false), hora,
            hora > 1 ? "s" : "");
        await Announce(txt, _bells, stoppingToken, BellsTimeOut);
    }

    private async Task Announce(string text,
        SoundPlayer? sound,
        CancellationToken stoppingToken,
        int pauseTimeMilliseconds = DefaultTimeOut)
    {
        _logger.LogInformation(text);

        sound?.Play();
        if (sound is not null)
        {
            await Task.Delay(pauseTimeMilliseconds, stoppingToken);
        }
        // Speak
        _synth.Speak(text);
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
#pragma warning restore CA1416 // Validate platform compatibility
