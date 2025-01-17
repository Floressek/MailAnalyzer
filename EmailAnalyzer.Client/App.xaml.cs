using EmailAnalyzer.Client.Pages;

namespace EmailAnalyzer.Client;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        Console.WriteLine("App constructor called");
        MainPage = new AppShell();
        Console.WriteLine("MainPage set to AppShell");
    }

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        
        Console.WriteLine($"Received app link: {uri}");
        if (uri.Host == "auth" && uri.LocalPath.Contains("callback"))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var code = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("code");
                var state = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("state");
                
                // Tutaj wywołujemy metodę do obsługi autoryzacji
                var loginPage = (LoginPage)Shell.Current.CurrentPage;
                await loginPage.HandleAuthCallback(code, state);
            });
        }
    }
}