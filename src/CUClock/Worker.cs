using System.Media;
using System.Speech.Synthesis;
using Cronos;
using Humanizer;
using Humanizer.Localisation;

namespace CUClock;

#pragma warning disable CA1416 // Validate platform compatibility
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly Dictionary<CronExpression, Action<CancellationToken>> _schedules;

    // Available on Windows only
    // Initialize a new instance of the SpeechSynthesizer.

    private readonly SpeechSynthesizer _synth = new();
    private readonly SoundPlayer _cucu = new(
        "C:\\Users\\manchax\\Downloads\\CUCKOOO.WAV");

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _schedules = new Dictionary<CronExpression, Action<CancellationToken>>
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
        _synth.SetOutputToDefaultAudioDevice();
        // Set a value for the speaking rate.
        _synth.Rate = -2;
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Now(stoppingToken);
        foreach (var key in _schedules.Keys)
        {
            var utcNow = DateTime.UtcNow;
            var next = key.GetNextOccurrence(utcNow)
                ?? throw new ApplicationException(
                    "Next Ocurrence was NULL, verify ScheduleFormat.");
            var timeToNext = next - utcNow;
            _ = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Sleeping for {ttn}",
                        timeToNext.Humanize(3,
                            maxUnit: TimeUnit.Day,
                            minUnit: TimeUnit.Second));

                    await Task.Delay(timeToNext, stoppingToken);

                    _logger.LogInformation("Waking up at {time}",
                        DateTimeOffset.Now);

                    // call lambda (Action<>)
                    _schedules[key](stoppingToken);

                    // sleep 1min 100 ms
                    await Task.Delay(60 * 1000 + 100, stoppingToken);
                    // re-calculate next ocurrence
                    utcNow = DateTime.UtcNow;
                    next = key.GetNextOccurrence(utcNow)
                        ?? throw new NullReferenceException();
                    timeToNext = next - utcNow;
                }
            }, stoppingToken);
        }
        await Task.CompletedTask;
    }

    private async Task Speak(string text, CancellationToken stoppingToken)
    {
        _cucu.Play();
        await Task.Delay(3500, stoppingToken);
        // Speak a string.
        _synth.Speak(text);
    }

    private async Task Now(CancellationToken stoppingToken)
    {
        var txt = string.Format("La hora actual es: {0}",
            DateTime.Now.TimeOfDay.Humanize(
                precision: 3,
                minUnit: TimeUnit.Second));
        await Speak(txt, stoppingToken);
        _logger.LogInformation(txt);
    }

    private async void EnPunto(CancellationToken stoppingToken)
    {
        var txt = string.Format(
            "{0} {1} en punto",
            DateTime.Now.TimeOfDay.Hours < 13
                ? DateTime.Now.TimeOfDay.Hours
                : DateTime.Now.TimeOfDay.Hours - 12,
            DateTime.Now.TimeOfDay.Hours < 13
                ? "del día"
                : "de la noche");
        await Speak(txt, stoppingToken);
        _logger.LogInformation(txt);
    }

    private async void CuartoDeHora(CancellationToken stoppingToken)
    {
        var txt = string.Format("Son las {0} y cuarto",
            DateTime.Now.TimeOfDay.Hours < 13
                ? DateTime.Now.TimeOfDay.Hours
                : DateTime.Now.TimeOfDay.Hours - 12);
        await Speak(txt, stoppingToken);
        _logger.LogInformation(txt);
    }

    private async void YMedia(CancellationToken stoppingToken)
    {
        var txt = string.Format("Son las {0} y media",
            DateTime.Now.TimeOfDay.Hours < 13
                ? DateTime.Now.TimeOfDay.Hours
                : DateTime.Now.TimeOfDay.Hours - 12);
        await Speak(txt, stoppingToken);
        _logger.LogInformation(txt);
    }

    private async void CuartoPara(CancellationToken stoppingToken)
    {
        var txt = string.Format("Son cuarto para las {0}",
            DateTime.Now.TimeOfDay.Hours + 1 < 13
                ? DateTime.Now.TimeOfDay.Hours + 1
                : DateTime.Now.TimeOfDay.Hours - 11);
        await Speak(txt, stoppingToken);
        _logger.LogInformation(txt);
    }

}
#pragma warning restore CA1416 // Validate platform compatibility
