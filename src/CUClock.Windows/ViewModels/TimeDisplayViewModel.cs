using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CUClock.Windows.Core.Contracts.Services;
using CUClock.Windows.Core.ViewModels;

namespace CUClock.Windows.ViewModels;

public partial class TimeDisplayViewModel : BaseViewModel
{
    [ObservableProperty]
    private bool _millisecondSwitch;

    private readonly IAnnouncer _announcer;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="announcer"></param>
    public TimeDisplayViewModel(IAnnouncer announcer)
    {
        _announcer = announcer;
        Announce = new RelayCommand(() => 
            _announcer.Announce(
                sayMilliseconds: MillisecondSwitch));

        Silence = new RelayCommand(() => 
            _announcer.Silence());

        SpeakPhrase = new RelayCommand(() =>
            _announcer.SpeakPhrase());
    }

    /// <summary>
    /// Announce command.
    /// </summary>
    public IRelayCommand Announce
    {
        get;
    }
    
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
    /// Announcer component.
    /// </summary>
    public IAnnouncer Announcer => _announcer;
}
