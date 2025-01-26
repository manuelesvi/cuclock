using CUClock.Shared.ViewModels;

namespace CUClock.Maui.Views;

public partial class ChaptersPage : ContentPage
{
    public ChaptersPage()
    {
        BindingContext = App.Current!.Handler.GetServiceProvider()
            .GetService<Chapters>(); // Chapters' ViewModel
        InitializeComponent();
    }
}