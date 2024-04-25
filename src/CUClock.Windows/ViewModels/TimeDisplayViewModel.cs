using CommunityToolkit.Mvvm.Input;
using CUClock.Windows.Core;
using CUClock.Windows.Core.Contracts.Services;
using CUClock.Windows.Core.ViewModels;

namespace CUClock.Windows.ViewModels;

public partial class TimeDisplayViewModel : BaseViewModel
{
    private readonly IAnnouncer _announcer;

    public TimeDisplayViewModel(IAnnouncer announcer)
    {
        _announcer = announcer;
        Announce = new AsyncRelayCommand(async () =>
        {
            _announcer.Announce();
            await Task.CompletedTask;
        });
    }

    public IAsyncRelayCommand Announce
    {
        get;
    }
}
