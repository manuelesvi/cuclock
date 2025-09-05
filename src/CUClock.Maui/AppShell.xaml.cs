using CUClock.Maui.Views;

namespace CUClock.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("//SettingsPage", typeof(SettingsPage));
    }

    private void Settings_Clicked(object sender, EventArgs e)
    {
        Shell.Current.GoToAsync("//SettingsPage");
    }
}
