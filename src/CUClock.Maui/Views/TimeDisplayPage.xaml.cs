using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Aphorismus.Shared.Messages;
using CommunityToolkit.Mvvm.Messaging;
using CUClock.Shared.ViewModels;

namespace CUClock.Maui.Views;

public partial class TimeDisplayPage : ContentPage
{
    public TimeDisplayPage()
    {
        InitializeComponent();
        SetupBindingContext();
        RegisterMessageRecipient();
    }

    private void SetupBindingContext()
    {
        var services = App.Current!.Handler.GetServiceProvider();
        var vm = services.GetService<TimeDisplayViewModel>();
        vm!.PropertyChanged += Vm_PropertyChanged;
        BindingContext = vm;
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimeDisplayViewModel.Caption))
        {
            var vm = BindingContext as TimeDisplayViewModel;
            var caption = vm?.Caption ?? string.Empty;
            Dispatcher.Dispatch(() =>
                CaptionText.Text = caption);
        }
    }

    private void RegisterMessageRecipient()
    {
        if (!WeakReferenceMessenger.Default
                    .IsRegistered<PhrasePickedMessage>(this))
        {
            WeakReferenceMessenger.Default.Register(this,
                (MessageHandler<object, PhrasePickedMessage>)(
                (_, message) => Process(message)));
        }
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
        _ = Dispatcher.Dispatch(() =>
        {
            PhraseBox.Text = sb.ToString();
        });

        //        var payload = string.Format(
        //            "AppNotificationSamplePayload".GetLocalized(),
        //            AppContext.BaseDirectory, message.Value.Texto);
        //        try
        //        {
        //            App.GetService<IAppNotificationService>().Show(payload);
        //        }
        //        catch (Exception ex)
        //        {
        //#if DEBUG
        //            Debug.WriteLine(ex);
        //            Debugger.Break();
        //#endif
        //        }
    }

    private void MsToggle_Toggled(object sender, ToggledEventArgs e)
    {
        msLabel.Text = e.Value switch
        {
            true => "Con milisegundos",
            false => "Sin milisegundos"
        };
    }

    private void AforismoToggle_Toggled(object sender, ToggledEventArgs e)
    {
        aforismoLabel.Text = e.Value switch
        {
            true => "Con aforismo",
            false => "Sin aforismo"
        };
    }

    private void GalloToggle_Toggled(object sender, ToggledEventArgs e)
    {
        galloLabel.Text = e.Value switch
        {
            true => "Con gallo",
            false => "Sin gallo"
        };
    }
}