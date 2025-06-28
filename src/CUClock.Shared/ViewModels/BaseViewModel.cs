using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace CUClock.Shared.ViewModels;

/// <summary>
/// An intermediary between the view and the model.
/// </summary>
public abstract class BaseViewModel(ILogger logger) : ObservableRecipient
{
    private readonly ILogger _logger = logger;

    protected override void OnActivated()
    {
        _logger.LogInformation("BaseViewModel activated.");
        base.OnActivated();
    }

    protected override void OnDeactivated()
    {
        _logger.LogInformation("BaseViewModel deactivated.");
        base.OnDeactivated();
    }
}