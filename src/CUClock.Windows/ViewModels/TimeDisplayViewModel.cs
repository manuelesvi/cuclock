using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CUClock.Windows.Core.Contracts.Services;
using CUClock.Windows.Core.ViewModels;
using Microsoft.Extensions.Logging;

namespace CUClock.Windows.ViewModels;

/// <summary>
/// Default constructor.
/// </summary>
/// <param name="announcer"></param>
public partial class TimeDisplayViewModel : BaseViewModel
{
    private readonly IAnnouncer _announcer;
    private readonly ILogger<TimeDisplayViewModel> _logger;
    
    [ObservableProperty]
    private bool _millisecondSwitch;
    
    [ObservableProperty]
    private bool _galloSwitch;

    public TimeDisplayViewModel(IAnnouncer announcer,
        ILogger<TimeDisplayViewModel> logger)
    {
        _announcer = announcer;
        _logger = logger;

        MillisecondSwitch = true;
        GalloSwitch = true;
        
        Silence = new RelayCommand(() => _announcer.Silence());
        SpeakPhrase = new RelayCommand(() => announcer.SpeakPhrase(GalloSwitch));

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

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        _logger.LogInformation("OnPropertyChanged: {prop}", e.PropertyName);
        if (e.PropertyName == nameof(MillisecondSwitch))
        {
            _logger.LogInformation("Milliseconds: {ms}", MillisecondSwitch);
        }
        else if (e.PropertyName == nameof(GalloSwitch))
        {
            _logger.LogInformation("Gallo: {gallo}", GalloSwitch);
        }
    }
}