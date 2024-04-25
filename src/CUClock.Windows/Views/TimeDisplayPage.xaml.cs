using CUClock.Windows.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CUClock.Windows.Views;

public sealed partial class TimeDisplayPage : Page
{
    public TimeDisplayPage()
    {
        ViewModel = App.GetService<TimeDisplayViewModel>();
        InitializeComponent();
    }

    public TimeDisplayViewModel ViewModel
    {
        get;
    }
}
