using CUClock.Maui.Model;
using CUClock.Shared.Contracts.Services;
using CUClock.Shared.ViewModels;
using System.Diagnostics;

namespace CUClock.Maui.Views;

public partial class SettingsPage : ContentPage
{
    private readonly IFileService _fileService;
    public SettingsPage()
    {
        InitializeComponent();
        var services = App.Current!.Handler.GetServiceProvider();
        _fileService = services.GetRequiredService<IFileService>();
        var vm = services.GetService<Announcer>();
        this.BindingContext = vm!;
        LoadSettings();
    }

    private new Announcer BindingContext
    {
        get => (Announcer)base.BindingContext;
        set => base.BindingContext = value;
    }

    private void LoadSettings()
    {
        var settings = _fileService.Read<Settings>("settings.json");
        if (settings is null)
        {
            return;
        }
        BindingContext.MillisecondSwitch = settings.MillisecondSwitch;
        BindingContext.AphorismSwitch = settings.AphorismSwitch;
        BindingContext.GalloSwitch = settings.GalloSwitch;
    }

    private void SaveSettings()
    {
        var settings = new Settings(
            BindingContext.MillisecondSwitch,
            BindingContext.AphorismSwitch,
            BindingContext.GalloSwitch);
        _fileService.Save("settings.json", settings);
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

    private void Save_Clicked(object sender, EventArgs e)
    {
        SaveSettings();
        Pop();
    }

    private void Cancel_Clicked(object sender, EventArgs e)
    {
        Pop();
    }

    private static void Pop()
    {
        try
        {
            Shell.Current.Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}