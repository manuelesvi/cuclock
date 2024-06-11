using System.Diagnostics;
using Aphorismus.Shared.Messages;
using CommunityToolkit.Mvvm.Messaging;
using CUClock.Windows.Contracts.Services;
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
        if (!WeakReferenceMessenger.Default
            .IsRegistered<PhrasePickedMessage>(this))
        {
            WeakReferenceMessenger.Default.Register<PhrasePickedMessage>(this,
                (_, message) =>
                {
                    var payload = string.Format("AppNotificationSamplePayload".GetLocalized(),
                        AppContext.BaseDirectory, message.Value.Texto);
                    try
                    {
                        App.GetService<IAppNotificationService>().Show(payload);
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        Debug.WriteLine(ex);
                        Debugger.Break();
#endif
                    }
                });
        }
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