using System.Text;
using Aphorismus.Shared.Messages;
using CommunityToolkit.Mvvm.Messaging;
using CUClock.Shared.ViewModels;
using Microsoft.Extensions.Logging;

namespace CUClock.Maui.Views;

public partial class TimeDisplayPage : ContentPage
{
    public TimeDisplayPage()
    {
        InitializeComponent();
        SetupBindingContext();
        RegisterMessageRecipient();
    }

    private new TimeDisplayViewModel BindingContext
    {
        get => (TimeDisplayViewModel)base.BindingContext;
        set => base.BindingContext = value;
    }

    private void SetupBindingContext()
    {
        var services = App.Current!.Handler.GetServiceProvider();
        var vm = services.GetService<TimeDisplayViewModel>();
        BindingContext = vm!;
#if DEBUG
        var logger = services.GetService<ILogger<TimeDisplayPage>>();
        vm!.Next.CanExecuteChanged += (_, _) =>
            logger!.LogInformation("Next changed CanExecute to: {canExecute}",
                vm.Next.CanExecute(null));
        vm.Previous.CanExecuteChanged += (_, _) =>
            logger!.LogInformation("Previous changed CanExecute to: {canExecute}",
                vm.Previous.CanExecute(null));
#endif
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
        var text = sb.ToString();
        /* run on the UI */
        _ = Dispatcher.Dispatch(() =>
        {
            PhraseBox.Text = text;
            BindingContext.UpdateTexts();
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