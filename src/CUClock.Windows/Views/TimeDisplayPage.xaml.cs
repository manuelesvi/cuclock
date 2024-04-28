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

        //System.Diagnostics.Debug.WriteLine(
        //    Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride);
        System.Diagnostics.Debug.WriteLine("TimeDisplayPage_AnnounceBtn/Content".GetLocalized());
    }

    public TimeDisplayViewModel ViewModel
    {
        get;
    }
}
