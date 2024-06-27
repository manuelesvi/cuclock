using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Aphorismus.Shared.Messages;
using CommunityToolkit.Mvvm.Messaging;
using CUClock.Windows.Contracts.Services;
using CUClock.Windows.Helpers;
using CUClock.Shared.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CUClock.Windows.Views;

public sealed partial class TimeDisplayPage : Page
{
    public TimeDisplayPage()
    {
        ViewModel = App.GetService<TimeDisplayViewModel>();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        InitializeComponent();
        if (!WeakReferenceMessenger.Default
            .IsRegistered<PhrasePickedMessage>(this))
        {
            WeakReferenceMessenger.Default.Register(this,
                (MessageHandler<object, PhrasePickedMessage>)(
                (_, message) => Process(message)));
        }

        msToggle.Toggled += (_, _) => ViewModel.MillisecondSwitch = msToggle.IsOn;
        galloToggle.Toggled += (_, _) => ViewModel.GalloSwitch = galloToggle.IsOn;
        aforismoToggle.Toggled += (_, _) => ViewModel.AphorismSwitch = aforismoToggle.IsOn;
    }

    public TimeDisplayViewModel ViewModel
    {
        get;
    }

    private void Process(PhrasePickedMessage message)
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
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.Caption))
        {
            DispatcherQueue.TryEnqueue(() =>
                CaptionText.Text = ViewModel.Caption);
        }
    }
}