using System.Net.Http.Json;
using EmailAnalyzer.Client.Pages;
using EmailAnalyzer.Shared.Models.Auth;
using EmailAnalyzer.Shared.Services;

namespace EmailAnalyzer.Client;

public partial class App : Application
{
    public static IServiceProvider? Services =>
        Current?.Handler?.MauiContext
            ?.Services; // This is a helper property to access services from anywhere in the app

    public App()
    {
        InitializeComponent();
        Console.WriteLine("App constructor called");
        MainPage = new AppShell();
        Console.WriteLine("MainPage set to AppShell");
    }

    protected override async void OnStart()
    {
        base.OnStart();

        var tokenStorage = Services?.GetService<ITokenStorageService>();
        if (tokenStorage != null)
        {
            var gmailToken = await tokenStorage.GetTokenAsync("gmail");
            Console.WriteLine(gmailToken.accessToken != null
                ? $"Gmail token found: {gmailToken.accessToken}"
                : "No Gmail token found.");
            var outlookToken = await tokenStorage.GetTokenAsync("outlook");
            Console.WriteLine(outlookToken.accessToken != null
                ? $"Outlook token found: {outlookToken.accessToken}"
                : "No Outlook token found.");
        }
        else
        {
            Console.WriteLine("Token storage service is not available.");
        }
    }


    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);

        Console.WriteLine($"Received app link: {uri}");

        if (uri.Host == "mailanalyzer-production.up.railway.app" && uri.LocalPath.Contains("callback"))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var code = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("code");
                    var state = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("state");

                    Console.WriteLine($"Got code: {code?.Length} chars, state: {state}");

                    using var httpClient = new HttpClient
                    {
                        BaseAddress = new Uri("https://mailanalyzer-production.up.railway.app/")
                    };

                    var authRequest = new AuthRequest
                    {
                        Provider = state?.ToLower() ?? "",
                        AuthCode = code ?? ""
                    };

                    var response = await httpClient.PostAsJsonAsync("api/auth/authenticate", authRequest);
                    var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

                    if (authResponse?.Success == true)
                    {
                        Console.WriteLine("Authentication successful. Saving token...");
                        var tokenStorage = Application.Current.Handler.MauiContext.Services
                            .GetRequiredService<ITokenStorageService>();

                        await tokenStorage.StoreTokenAsync(
                            state!,
                            authResponse.AccessToken!,
                            authResponse.RefreshToken ?? "",
                            authResponse.ExpiresAt
                        );

                        await Shell.Current.GoToAsync($"///dateSelection?provider={state}");
                    }
                    else
                    {
                        Console.WriteLine("Authentication failed: " + response.ReasonPhrase);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling callback: {ex}");
                }
            });
        }
    }
}