using System.Net.Http.Json;
using EmailAnalyzer.Shared.Models.Auth;
using EmailAnalyzer.Shared.Services;
using Microsoft.Extensions.Logging;

namespace EmailAnalyzer.Client.Pages;

public partial class LoginPage : ContentPage
{
    private class AuthUrlResponse // Used to deserialize the response from the API
    {
        public string? Url { get; set; }
    }
    
    private readonly ITokenStorageService _tokenStorage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LoginPage> _logger;
    private string _currentProvider = string.Empty;
    
    public bool IsLoginVisible { get; set; } = true;  // Add this property

    public LoginPage(
        ITokenStorageService tokenStorage,
        ILogger<LoginPage> logger)
    {
        InitializeComponent();
        _tokenStorage = tokenStorage;
        _logger = logger;
        // _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5045/") };
        _httpClient = new HttpClient { BaseAddress = new Uri("http://10.0.2.2:5045/") }; // dla emulatora
        BindingContext = this; // Add this for the IsLoginVisible property
    }

    private async void OnOutlookClicked(object sender, EventArgs e)
    {
        await StartAuth("outlook");
    }

    private async void OnGmailClicked(object sender, EventArgs e)
    {
        await StartAuth("gmail");
    }

    private async Task StartAuth(string provider)
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            _currentProvider = provider;

            _logger.LogInformation($"Starting auth for {provider} with base address: {_httpClient.BaseAddress}");
            var response = await _httpClient.GetAsync($"api/auth/url/{provider}");
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"Response status: {response.StatusCode}");
            _logger.LogInformation($"Response content: {content}");

            if (!response.IsSuccessStatusCode)
            {
                var error = $"Failed to start authentication: {response.StatusCode} - {content}";
                _logger.LogError(error);
                await DisplayAlert("Error", error, "OK");
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthUrlResponse>();
            _logger.LogInformation($"Received auth URL: {result?.Url}");
        
            if (result?.Url == null)
            {
                await DisplayAlert("Error", "Invalid authentication URL", "OK");
                return;
            }

            AuthWebView.IsVisible = true;
            AuthWebView.Source = result.Url;
            _logger.LogInformation($"Loading URL in WebView: {result.Url}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            await DisplayAlert("Error", $"Authentication failed: {ex.Message}", "OK");
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
            // Sprawdź czy to callback URL
            if (e.Url.StartsWith("http://10.0.2.2:5045/auth/callback"))
            {
                var uri = new Uri(e.Url);
                var code = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("code");
                var state = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("state");

                if (!string.IsNullOrEmpty(code))
                {
                    // Wyślij kod do API
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
                            // Zapisz token
                            await _tokenStorage.StoreTokenAsync(
                                _currentProvider,
                                authResponse.AccessToken!,
                                authResponse.RefreshToken ?? "",
                                authResponse.ExpiresAt
                            );

                            // Ukryj WebView i pokaż sukces
                            AuthWebView.IsVisible = false;
                            await DisplayAlert("Success", "Successfully connected!", "OK");
                            
                            // TODO: Navigate to next page
                        }
                    }
                }
                else
                {
                    await DisplayAlert("Error", "Authentication failed", "OK");
                }
                
                // Wróć do widoku logowania
                AuthWebView.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing authentication callback");
            await DisplayAlert("Error", "Failed to complete authentication", "OK");
            AuthWebView.IsVisible = false;
        }
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // Sprawdź czy mamy już tokeny
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