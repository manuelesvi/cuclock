using CUClock.Windows.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace CUClock.Windows.Views;

public sealed partial class TimeDisplayPage : Page
{
    public TimeDisplayViewModel ViewModel
    {
        get;
    }

    public TimeDisplayPage()
    {
        ViewModel = App.GetService<TimeDisplayViewModel>();
        InitializeComponent();
    }
}
