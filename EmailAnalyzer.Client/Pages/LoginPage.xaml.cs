﻿using System.Net.Http.Json;
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
        _httpClient = new HttpClient { BaseAddress = new Uri("http://192.168.1.78:5045/") };
        BindingContext = this;
    }
    
    private void LogDebugInfo(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DebugLabel.Text = $"[{DateTime.Now:HH:mm:ss}] {message}";
        });
    }

    private async void OnOutlookClicked(object sender, EventArgs e)
    {
        try
        {
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
        await StartAuth("gmail");
    }

    private async Task StartAuth(string provider)
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            _currentProvider = provider;
            LogDebugInfo($"Starting auth for {provider}");

            var response = await _httpClient.GetAsync($"api/auth/url/{provider}");
            var content = await response.Content.ReadAsStringAsync();
            LogDebugInfo($"Got response: {content}");

            if (!response.IsSuccessStatusCode)
            {
                LogDebugInfo($"Error: {response.StatusCode}");
                await DisplayAlert("Error", 
                    $"Authentication failed: {response.StatusCode}", 
                    "OK");
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthUrlResponse>();
            if (result?.Url == null)
            {
                LogDebugInfo("No URL in response");
                await DisplayAlert("Error", "Invalid authentication URL", "OK");
                return;
            }

            LogDebugInfo("Opening WebView...");
            AuthWebView.IsVisible = true;
            WebViewContainer.IsVisible = true;
            await Task.Delay(100); // Daj czas na update UI
            
            LogDebugInfo($"Loading URL: {result.Url}");
            AuthWebView.Source = result.Url;
            LogDebugInfo($"WebView loaded with URL");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed");
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
            LogDebugInfo($"WebView navigated to: {e.Url}");
            if (e.Url.StartsWith($"{_httpClient.BaseAddress}auth/callback"))
            {
                var uri = new Uri(e.Url);
                var code = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("code");
                var state = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("state");

                if (string.IsNullOrEmpty(code))
                {
                    await DisplayAlert("Error", "Authentication failed - no code received", "OK");
                    AuthWebView.IsVisible = false;
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

                        AuthWebView.IsVisible = false;
                        await DisplayAlert("Success", "Successfully connected!", "OK");
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Error", $"Authentication failed: {error}", "OK");
                }
                
                AuthWebView.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Callback processing failed");
            await DisplayAlert("Error", $"Authentication callback failed: {ex.Message}", "OK");
            AuthWebView.IsVisible = false;
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