using System.Net.Http.Json;
using EmailAnalyzer.Shared.Models.Auth;
using EmailAnalyzer.Shared.Services;
using Microsoft.Extensions.Logging;

namespace EmailAnalyzer.Client.Pages;

public partial class LoginPage : ContentPage
{
    private class AuthUrlResponse
    {
        public string? Url { get; set; }
    }

    private readonly ITokenStorageService _tokenStorage;
    private readonly ILogger<LoginPage> _logger;
    private readonly HttpClient _httpClient;
    private string _currentProvider = string.Empty;

    public bool IsLoginVisible { get; set; } = true;

    public LoginPage(
        ITokenStorageService tokenStorage,
        ILogger<LoginPage> logger)
    {
        InitializeComponent();
        _tokenStorage = tokenStorage;
        _logger = logger;

        // Adjust BaseAddress if needed
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://mailanalyzer-production.up.railway.app/")
        };
        BindingContext = this;
    }

    private void LogDebugInfo(string message)
    {
        MainThread.BeginInvokeOnMainThread(() => { DebugLabel.Text = $"[{DateTime.Now:HH:mm:ss}] {message}"; });
    }

    private async void OnOutlookClicked(object sender, EventArgs e)
    {
        try
        {
            // Test server connectivity first
            _logger.LogInformation("Testing server connection...");
            var response = await _httpClient.GetAsync("api/health");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Server connection successful");
                await StartAuth("outlook");
            }
            else
            {
                _logger.LogError($"Server returned: {response.StatusCode}");
                await DisplayAlert("Error",
                    $"Server connection failed with status: {response.StatusCode}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            await DisplayAlert("Connection Error",
                $"Could not connect to server: {ex.Message}",
                "OK");
        }
    }

    private async void OnGmailClicked(object sender, EventArgs e)
    {
        try
        {
            // Test server connectivity first
            _logger.LogInformation("Testing server connection...");
            var response = await _httpClient.GetAsync("api/health");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Server connection successful");
                await StartAuth("gmail");
            }
            else
            {
                _logger.LogError($"Server returned: {response.StatusCode}");
                await DisplayAlert("Error",
                    $"Server connection failed with status: {response.StatusCode}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            await DisplayAlert("Connection Error",
                $"Could not connect to server: {ex.Message}",
                "OK");
        }
    }

    private async Task StartAuth(string provider)
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            _currentProvider = provider;

            // 1. Pobierz URL do autoryzacji
            var response = await _httpClient.GetAsync($"api/auth/url/{provider}");
            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("Error", "Could not get auth URL", "OK");
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthUrlResponse>();
            if (result?.Url == null)
            {
                await DisplayAlert("Error", "Invalid auth URL", "OK");
                return;
            }

            // 2. Otwórz przeglądarkę
            await Browser.Default.OpenAsync(result.Url, BrowserLaunchMode.SystemPreferred);
        
            // 3. Poczekaj chwilę aż użytkownik się zaloguje
            await Task.Delay(3000);
            var testResponse = await _httpClient.GetAsync($"api/email/test/{provider}");
            if (testResponse.IsSuccessStatusCode)
            {
                await Shell.Current.GoToAsync($"///dateSelection?provider={provider}");
            }
            else 
            {
                await DisplayAlert("Error", "Authentication failed", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void AuthWebView_Navigated(object sender, WebNavigatedEventArgs e)
    {
        try
        {
            LogDebugInfo($"WebView navigated to: {e.Url}");
            if (e.Url.StartsWith($"{_httpClient.BaseAddress}auth/callback"))
            {
                var uri = new Uri(e.Url);
                var code = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("code");
    
                if (string.IsNullOrEmpty(code))
                {
                    await DisplayAlert("Error", "Authentication failed - no code received", "OK");
                    WebViewOverlay.IsVisible = false;
                    return;
                }
    
                var authRequest = new AuthRequest
                {
                    Provider = _currentProvider,
                    AuthCode = code
                };
    
                var response = await _httpClient.PostAsJsonAsync("api/auth/authenticate", authRequest);
                if (response.IsSuccessStatusCode)
                {
                    var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                    if (authResponse?.Success == true)
                    {
                        await _tokenStorage.StoreTokenAsync(
                            _currentProvider,
                            authResponse.AccessToken!,
                            authResponse.RefreshToken ?? "",
                            authResponse.ExpiresAt
                        );

                        WebViewOverlay.IsVisible = false;
                        await DisplayAlert("Success", "Successfully connected!", "OK");
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Error", $"Authentication failed: {error}", "OK");
                }
    
                WebViewOverlay.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Callback processing failed");
            await DisplayAlert("Error", $"Authentication callback failed: {ex.Message}", "OK");
            WebViewOverlay.IsVisible = false;
        }
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        var (outlookToken, _, outlookExpiry) = await _tokenStorage.GetTokenAsync("outlook");
        var (gmailToken, _, gmailExpiry) = await _tokenStorage.GetTokenAsync("gmail");

        OutlookButton.Text = outlookToken != null && outlookExpiry > DateTime.UtcNow
            ? "Outlook Connected"
            : "Connect Outlook";

        GmailButton.Text = gmailToken != null && gmailExpiry > DateTime.UtcNow
            ? "Gmail Connected"
            : "Connect Gmail";
    }
    
}

