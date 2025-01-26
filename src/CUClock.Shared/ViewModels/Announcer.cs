using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CUClock.Shared.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace CUClock.Shared.ViewModels;

/// <summary>
/// Announces time and aphorism from Tolstoi's book.
/// </summary>
public partial class Announcer : BaseViewModel
{
    private readonly IAnnouncer _announcer;
    private readonly ILogger<Announcer> _logger;

    [ObservableProperty]
    private bool _millisecondSwitch;

    [ObservableProperty]
    private bool _galloSwitch;

    [ObservableProperty]
    private bool _aphorismSwitch;

    [ObservableProperty]
    private Frase _frase;

    [ObservableProperty]
    private string _anteriorText;

    [ObservableProperty]
    private string _siguienteText;

    public Announcer(IAnnouncer announcer,
        ILogger<Announcer> logger)
    {
        _announcer = announcer;
        _logger = logger;

        MillisecondSwitch = false;
        GalloSwitch = true;
        AphorismSwitch = true;

        Silence = new RelayCommand(() =>
            _announcer.Silence());

        SpeakPhrase = new RelayCommand(() =>
        {
            _announcer.SpeakPhrase(GalloSwitch);
        });

        Repeat = new RelayCommand(() =>
            _announcer.SpeakPhrase(Frase));

        Previous = new RelayCommand(() =>
        {
            _announcer.Previous();
        }, () => _announcer.PreviousCount > 0);

        Next = new RelayCommand(() =>
        {
            _announcer.Next();
        }, () => _announcer.NextCount > 0);

        UpdateTexts();

        if (!WeakReferenceMessenger.Default
            .IsRegistered<PhrasePickedMessage>(this))
        {
            WeakReferenceMessenger.Default
                .Register<PhrasePickedMessage>(this, (_, message) =>
                    Frase = message.Value);
        }
    }

    /// <summary>
    /// Announce command.
    /// </summary>
    public IRelayCommand Announce => new RelayCommand(() =>
        _announcer.Announce(MillisecondSwitch));

    /// <summary>
    /// Silence command.
    /// </summary>
    public IRelayCommand Silence
    {
        get;
    }

    /// <summary>
    /// SpeakPhrase command.
    /// </summary>
    public IRelayCommand SpeakPhrase
    {
        get;
    }

    /// <summary>
    /// Repeats current phrase.
    /// </summary>
    public IRelayCommand Repeat
    {
        get;
    }

    /// <summary>
    /// Goes back to the previous phrase.
    /// </summary>
    public IRelayCommand Previous
    {
        get;
    }

    /// <summary>
    /// Moves to the next phrase.
    /// </summary>
    public IRelayCommand Next
    {
        get;
    }

    /// <summary>
    /// Text that is being spoken by TTS.
    /// </summary>
    public string Caption
    {
        get; set;
    } = string.Empty;

    public void UpdateTexts([CallerMemberName] string callerName = "")
    {
        _logger.LogInformation("UpdateTexts called from {callerName}", callerName);

        AnteriorText = string.Format("Anterior ({0})", _announcer.PreviousCount);
        SiguienteText = string.Format("Siguiente ({0})", _announcer.NextCount);

        Next.NotifyCanExecuteChanged();
        Previous.NotifyCanExecuteChanged();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MillisecondSwitch))
        {
            _logger.LogInformation("OnPropertyChanged: {prop}", e.PropertyName);
            _logger.LogInformation("Milliseconds: {ms}", MillisecondSwitch);
        }
        else if (e.PropertyName == nameof(GalloSwitch))
        {
            _logger.LogInformation("OnPropertyChanged: {prop}", e.PropertyName);
            _logger.LogInformation("Gallo: {gallo}", GalloSwitch);
        }
        else if (e.PropertyName == nameof(AphorismSwitch))
        {
            _announcer.EnableAphorisms = AphorismSwitch;
        }
        base.OnPropertyChanged(e);
    }
}