using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace EmailAnalyzer.Client;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, 
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | 
                           ConfigChanges.UiMode | ConfigChanges.ScreenLayout | 
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[]
    {
        Intent.CategoryDefault,
        Intent.CategoryBrowsable
    },
    DataScheme = "https",
    DataHost = "mailanalyzer-production.up.railway.app",
    DataPathPrefix = "/auth/callback",
    AutoVerify = true)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Platform.Init(this, savedInstanceState);
    }

    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);
        Platform.OnNewIntent(intent);
    }
}