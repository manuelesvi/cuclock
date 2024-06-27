using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        Caption = string.Empty;
        _announcer.CaptionChanged += (_, e) =>
        {
            Caption = e.Text;
            OnPropertyChanged(nameof(Caption));
        };
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

    public string Caption
    {
        get; set;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        _logger.LogInformation(e.PropertyName);
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