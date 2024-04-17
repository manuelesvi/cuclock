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

    private readonly ILogger<Worker> _logger;
    private readonly Dictionary<CronExpression, Schedule> _schedules;

    // Available on Windows only    
    private readonly SpeechSynthesizer _synth = new();
    private readonly SoundPlayer _cucu = new(
        "C:\\Users\\manchax\\Downloads\\CUCKOOO.WAV");
    private readonly SoundPlayer _cucaracha = new(
        "C:\\Users\\manchax\\Downloads\\Voicy_La Cucaracha Horn.wav");

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _schedules = new Dictionary<CronExpression, Schedule>
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

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SayCurrentTime(stoppingToken);
        var tasks = new List<Task>();
        foreach (var key in _schedules.Keys)
        {
            var utcNow = DateTime.UtcNow;
            var next = key.GetNextOccurrence(utcNow)
                ?? throw new ApplicationException();
            var timeToNext = next - utcNow;
            tasks.Add(Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("{now} {cron} - Sleeping for {ttn} until {next}",
                        DateTimeOffset.Now,
                        key.ToString(),
                        timeToNext.Humanize(3,
                            maxUnit: TimeUnit.Day,
                            minUnit: TimeUnit.Second),
                        next.ToLocalTime().TimeOfDay.ToString());

                    await Task.Delay(timeToNext, stoppingToken);

                    _logger.LogInformation("{now} {cron} - Waking up!!!",
                        DateTimeOffset.Now,
                        key.ToString());

                    // call delegate
                    _schedules[key](stoppingToken);

                    // sleep 1s, 100 ms
                    await Task.Delay(1100, stoppingToken);
                    // re-calculate next ocurrence
                    utcNow = DateTime.UtcNow;
                    next = key.GetNextOccurrence(utcNow)
                        ?? throw new NullReferenceException();
                    timeToNext = next - utcNow;
                }
            }, stoppingToken));
        }
        await Task.WhenAll(tasks.ToArray());
    }

    private async Task Speak(string text,
        SoundPlayer? sound,
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(text);
        sound?.Play();
        if (sound is not null)
        { 
            await Task.Delay(3500, stoppingToken);
        }
        // Speak
        _synth.Speak(text);
    }

    private async Task SayCurrentTime(CancellationToken stoppingToken)
    {
        var txt = string.Format("La hora actual es: {0}",
            DateTime.Now.TimeOfDay.Humanize(
                precision: 3,
                minUnit: TimeUnit.Second));
        await Speak(txt, _cucaracha, stoppingToken);
    }

    private string PrefijoHora(int hora, bool includeArt = true)
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

    private async void EnPunto(CancellationToken stoppingToken)
    {
        var hora = DateTime.Now.TimeOfDay.Hours < 13
            ? DateTime.Now.TimeOfDay.Hours
            : DateTime.Now.TimeOfDay.Hours - 12;

        var txt = string.Format(
            "{0} {1} {2}",
            PrefijoHora(hora), hora,
            DateTime.Now.TimeOfDay.Hours < 13
                ? "del d�a"
                : DateTime.Now.TimeOfDay.Hours < 19 
                ? "de la tarde" : "de la noche");
        await Speak(txt, _cucu, stoppingToken);
        await Task.Delay(500, stoppingToken);
        await Speak(string.Format("La{1} {0} en punto", hora, 
            hora == 1 ? "" : "s"),
            sound: null, stoppingToken);
    }

    private async void CuartoDeHora(CancellationToken stoppingToken)
    {
        var hora = DateTime.Now.TimeOfDay.Hours < 13
            ? DateTime.Now.TimeOfDay.Hours
            : DateTime.Now.TimeOfDay.Hours - 12;
        var txt = string.Format("{0} {1} y cuarto", PrefijoHora(hora), hora);
        await Speak(txt, _cucu, stoppingToken);
    }

    private async void YMedia(CancellationToken stoppingToken)
    {
        var hora = DateTime.Now.TimeOfDay.Hours < 13
            ? DateTime.Now.TimeOfDay.Hours
            : DateTime.Now.TimeOfDay.Hours - 12;
        var txt = string.Format("{0} {1} y media", PrefijoHora(hora), hora);
        await Speak(txt, _cucaracha, stoppingToken);
    }

    private async void CuartoPara(CancellationToken stoppingToken)
    {
        var hora = DateTime.Now.TimeOfDay.Hours + 1 < 13
            ? DateTime.Now.TimeOfDay.Hours + 1
            : DateTime.Now.TimeOfDay.Hours - 11;
        var txt = string.Format("{0} cuarto para la{2} {1}",
            PrefijoHora(hora, false), hora,
            hora > 1 ? "s" : "");
        await Speak(txt, _cucu, stoppingToken);
    }
}
#pragma warning restore CA1416 // Validate platform compatibility
