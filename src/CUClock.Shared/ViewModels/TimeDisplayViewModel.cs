using System.ComponentModel;
using Aphorismus.Shared.Entities;
using Aphorismus.Shared.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CUClock.Shared.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace CUClock.Shared.ViewModels;

/// <summary>
/// ViewModel for TimeDisplay view.
/// </summary>
public partial class TimeDisplayViewModel : BaseViewModel
{
    private readonly IAnnouncer _announcer;
    private readonly ILogger<TimeDisplayViewModel> _logger;
    
    [ObservableProperty]
    private bool _millisecondSwitch;
    
    [ObservableProperty]
    private bool _galloSwitch;

    [ObservableProperty]
    private bool _aphorismSwitch;

    [ObservableProperty]
    private Frase _frase;

    public TimeDisplayViewModel(IAnnouncer announcer,
        ILogger<TimeDisplayViewModel> logger)
    {
        _announcer = announcer;
        _logger = logger;

        MillisecondSwitch = true;
        GalloSwitch = true;
        AphorismSwitch = true;
        
        Silence = new RelayCommand(() => _announcer.Silence());
        SpeakPhrase = new RelayCommand(() => announcer.SpeakPhrase(GalloSwitch));
        Repeat = new RelayCommand(() => announcer.SpeakPhrase(Frase));
        Caption = string.Empty;
        _announcer.CaptionChanged += (_, e) =>
        {
            Caption = e.Text;
            OnPropertyChanged(nameof(Caption));
        };

        if (!WeakReferenceMessenger.Default
            .IsRegistered<PhrasePickedMessage>(this))
        {
            WeakReferenceMessenger.Default.Register(this,
                (MessageHandler<object, PhrasePickedMessage>)(
                (_, message) => Frase = message.Value));
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
    /// Repeats the last phrase.
    /// </summary>
    public IRelayCommand Repeat
    {
        get;
    }

    public string Caption
    {
        get; set;
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