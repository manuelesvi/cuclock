using CUClock.Windows.Core;
using CUClock.Windows.Core.Contracts.Services;
using CUClock.Windows.Helpers;
using CUClock.Windows.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CUClock.Windows.Views;

public sealed partial class TimeDisplayPage : Page
{
    public TimeDisplayPage()
    {
        ViewModel = App.GetService<TimeDisplayViewModel>();
        InitializeComponent();
        // ... testing localized resources
        System.Diagnostics.Debug.WriteLine(
            "TimeDisplayPage_AnnounceBtn/Content".GetLocalized());
    }

    public TimeDisplayViewModel ViewModel
    {
        get;
    }

    private void ToggleSwitch_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.MillisecondSwitch = msToggle.IsOn;
    }
}