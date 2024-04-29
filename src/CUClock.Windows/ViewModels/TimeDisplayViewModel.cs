using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CUClock.Windows.Core.Contracts.Services;
using CUClock.Windows.Core.ViewModels;

namespace CUClock.Windows.ViewModels;

public partial class TimeDisplayViewModel : BaseViewModel
{
    private readonly IAnnouncer _announcer;

    [ObservableProperty]
    private bool _millisecondSwitch;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="announcer"></param>
    public TimeDisplayViewModel(IAnnouncer announcer)
    {
        _announcer = announcer;
        Announce = new AsyncRelayCommand(async () =>
        {
            _announcer.Announce(
                sayMilliseconds: MillisecondSwitch);
            await Task.CompletedTask;
        });
    }

    /// <summary>
    /// Announce command.
    /// </summary>
    public IAsyncRelayCommand Announce
    {
        get;
    }
}
