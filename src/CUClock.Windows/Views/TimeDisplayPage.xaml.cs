using System.Diagnostics;
using System.Text;
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
                    var value = message.Value;
                    var sb = new StringBuilder(string.Format("{0} - {1}",
                        value.Capitulo!.NumeroCapitulo,
                        value.Capitulo!.Nombre));
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine(value.Texto);
                    _ = DispatcherQueue.TryEnqueue(() =>
                    {
                        PhraseBox.Text = sb.ToString();
                    });

                    var payload = string.Format(
                        "AppNotificationSamplePayload".GetLocalized(),
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

        msToggle.Toggled += (_, _) => ViewModel.MillisecondSwitch = msToggle.IsOn;
        galloToggle.Toggled += (_, _) => ViewModel.GalloSwitch = galloToggle.IsOn;
    }

    public TimeDisplayViewModel ViewModel
    {
        get;
    }
}