using Android.App;
using Android.Content.PM;
using Android.OS;

namespace CUClock.Maui;
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        App.StartScheduler();
        base.OnCreate(savedInstanceState);
    }
    protected override void OnPause()
    {
        App.StopScheduler();
        base.OnPause();
    }

    protected override void OnStop()
    {
        App.StopScheduler();
        base.OnStop();
    }
}
