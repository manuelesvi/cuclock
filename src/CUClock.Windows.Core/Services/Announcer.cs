using System.Diagnostics;
using System.Globalization;
using System.Media;
using System.Speech.Synthesis;
using Aphorismus.Shared.Messages;
using Aphorismus.Shared.Services;
using CommunityToolkit.Mvvm.Messaging;
using Cronos;
using CUClock.Windows.Core.Contracts.Services;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CUClock.Windows.Core;

#nullable enable

/// <summary>
/// Announces current local time at specific intervals
/// when executed as a <see cref="BackgroundService"/>,
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
    private const int Default_Duration =  41 * 100; // 41 seconds
    private const int Bells_Duration   = 150 * 100; // 150 seconds
    private const int BellsAfter_Delay = 370 * 100; // delay after 1st melody

    /// <summary>
    /// Precision for minute and second.
    /// </summary>
    private const int Precision_Second = 2;

    /// <summary>
    /// Precision for minute, second and millisecond.
    /// </summary>
    private const int Precision_Millisecond = 3;

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

    private readonly Random _random = new();
    private readonly IPhraseProvider _phraseProvider;

    /// <summary>
    /// Logging.
    /// </summary>
    private readonly ILogger<Announcer> _logger;

#pragma warning disable CA1416 // skip platform compatibility
    private readonly SoundPlayer
        _bells,
        _cucu,
        _cucaracha,
        _pistol,
        _gallo;

    private SoundPlayer? _playing;
    private readonly string _wavDir = string.Empty;

    /// <summary>
    /// TTS synth (Windows only).
    /// </summary>
    private readonly SpeechSynthesizer _synth = new();

    /// <summary>
    /// A <see cref="List{VoiceInfo}"/> of installed voices in 
    /// <see cref="Spanish">.
    /// </summary>
    private readonly List<VoiceInfo> _voices = [];

    private CancellationTokenSource? _silence;

    /// <summary>
    /// Main constructor.
    /// </summary>
    /// <param name="logger"></param>
    public Announcer(
        IPhraseProvider phraseProvider,
        ILogger<Announcer> logger)
    {
        CultureInfo.CurrentCulture = _mxCulture;
        CultureInfo.CurrentUICulture = _mxCulture;
        _phraseProvider = phraseProvider;
        _logger = logger;
        var isRegistered = WeakReferenceMessenger.Default
            .IsRegistered<PhrasePickedMessage>(this);
        if (!isRegistered)
        {
            WeakReferenceMessenger.Default.Register<PhrasePickedMessage>(this,
                (_, message) => ProcessMessage(message));
        }
        var entry = System.Reflection.Assembly.GetEntryAssembly()!
            .Location;

        var dir = Path.GetDirectoryName(entry)!;
        _wavDir = Path.Combine(dir, "WAVs");

        _bells = new SoundPlayer(_wavDir + "\\bells.wav");
        _cucu = new SoundPlayer(_wavDir + "\\CUCKOOO.WAV");
        _cucaracha = new SoundPlayer(_wavDir + "\\horn.wav");
        _pistol = new SoundPlayer(_wavDir + "\\pistol.wav");
        _gallo = new SoundPlayer();

        Trace.Assert(Directory.Exists(dir)
            && Directory.Exists(_wavDir), "dir not found");

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

    public void Silence()
    {
        _logger.LogInformation("Silencing...");
        _silence?.Cancel();
        _playing?.Stop();
        _playing?.Dispose();
        _playing = null;
        _synth.SpeakAsyncCancelAll();
        _logger.LogInformation("Silenced.");
    }

    public void SpeakPhrase(bool conGallo)
    {
        Silence();
        _silence = new CancellationTokenSource();
        var _ = Task.Run(async () =>
        {
            if (conGallo)
            {
                IList<(string file, int duration)> gallos = [
                    ("rooster1.wav", 30), // 3 segundos
                    ("rooster2.wav", 35)]; // 3.5s

                var gallo = gallos[_random.Next(0, 2)];
                _gallo.SoundLocation = _wavDir + "\\" + gallo.file;
                _gallo.Load();
                _playing = _gallo;
                _gallo.Play();

                _logger.LogInformation("@{now} - Sleeping for {time} ms.",
                    DateTime.Now.TimeOfDay, gallo.duration * 100);
                
                await Task.Delay(gallo.duration * 100);

                _logger.LogInformation("@{now} Woke up!", DateTime.Now.TimeOfDay);
            }

            _pistol.Play();
            await Task.Delay(2000);
            SpeakPhrase(_silence.Token);
        });
    }

    protected async override Task ExecuteAsync(
        CancellationToken stoppingToken)
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
        await Task.WhenAll([.. tasks]); // done
    }

    private static string PrefijoHora(int hora,
        bool conArticulo = true) => conArticulo
        ? hora > 1
            ? "Son las"
            : "Es la"
        : hora > 1
            ? "Son"
            : "Es";

    private static object SufijoHora(int hora)
        => hora > 1 ? "s" : "";

    private async Task SayCurrentTime(bool sayMilliseconds = true,
        CancellationToken? stoppingToken = null)
    {
        _silence = new CancellationTokenSource();
        var now = DateTime.Now;
        var hora = now.Hour > 12 ? now.Hour - 12: now.Hour;
        var txt = string.Format(now.Minute > HalfHour
            ? Faltan(sayMilliseconds)
            : now.Hour >= 1 && now.Hour < 12 
            ? "{0} de la mañana, {1}"
            : now.Hour == 12 
            ? "doce del medio día, "
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

        await Announce(txt, _cucaracha,
            stoppingToken ?? _silence.Token);

        SpeakPhrase(stoppingToken ?? _silence.Token);
    }

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
        await Announce(string.Format("La{1} {0} en punto",
            hora > 1 ? hora.ToString() : "una",
            hora == 1 ? "" : "s"),
            sound: _cucu, stoppingToken);

        SpeakPhrase(stoppingToken);
    }

    private async void CuartoDeHora(CancellationToken stoppingToken)
    {
        var hora = DateTime.Now.TimeOfDay.Hours < 13
            ? DateTime.Now.TimeOfDay.Hours
            : DateTime.Now.TimeOfDay.Hours - 12;
        var txt = string.Format("{0} {1} y cuarto",
            PrefijoHora(hora), hora,
            hora != 1 ? hora : "una");
        await Announce(txt, _bells, stoppingToken, Bells_Duration);
        await Task.Delay(BellsAfter_Delay, stoppingToken);
        SpeakPhrase(stoppingToken);
    }

    private async void YMedia(CancellationToken stoppingToken)
    {
        var hora = DateTime.Now.TimeOfDay.Hours < 13
            ? DateTime.Now.TimeOfDay.Hours
            : DateTime.Now.TimeOfDay.Hours - 12;
        var txt = string.Format("{0} {1} y media",
            PrefijoHora(hora, conArticulo: false), // {0}
            hora != 1 ? hora : "una"); // {1}
        await Announce(txt, _cucaracha, stoppingToken);
        SpeakPhrase(stoppingToken);
    }

    private async void CuartoPara(CancellationToken stoppingToken)
    {
        var hora = DateTime.Now.TimeOfDay.Hours + 1 < 13
            ? DateTime.Now.TimeOfDay.Hours + 1
            : DateTime.Now.TimeOfDay.Hours - 11;
        var txt = string.Format("{0} cuarto para la{2} {1}",
            PrefijoHora(hora, conArticulo: false), hora,
            hora > 1 ? "s" : "");
        await Announce(txt, _bells, stoppingToken, Bells_Duration);
        await Task.Delay(BellsAfter_Delay, stoppingToken);
        SpeakPhrase(stoppingToken);
    }

    private async Task Announce(string text,
        SoundPlayer? sound,
        CancellationToken stoppingToken,
        int pauseTimeMilliseconds = Default_Duration)
    {
        _logger.LogTrace("Announce began execution...");
        _logger.LogInformation(text);
        _playing = sound;
        _playing?.Play();
        if (sound is not null)
        {
            await Task.Delay(pauseTimeMilliseconds, stoppingToken);
        }
        SelectVoice();
        _logger.LogTrace("Calling Speak() on speech synthesizer");
        try
        {
            _synth.Speak(text);
        }
        catch (OperationCanceledException e)
        {
            _logger.LogError(e, "TTS operation was canceled.");
//#if DEBUG
//            Debugger.Break();
//#endif
        }
        _logger.LogTrace("Announce execution finished.");
    }

    private void SpeakPhrase(CancellationToken stoppingToken)
    {
        stoppingToken.Register(_synth.SpeakAsyncCancelAll);
        ((PhraseProvider)_phraseProvider).SendPhrase(_random);
    }

    private void ProcessMessage(PhrasePickedMessage message)
    {
        SelectVoice();
        _synth.SpeakAsync(message.Value.Texto);
    }

    private void SelectVoice() => _synth.SelectVoice(_voices[
            _random.Next(0, _voices.Count) // selects a random voice
    ].Name);

    public override void Dispose()
    {
        _bells?.Dispose();
        _cucaracha?.Dispose();
        _cucu?.Dispose();
        _synth?.Dispose();
#pragma warning restore CA1416
        base.Dispose();
    }
}

#nullable disable