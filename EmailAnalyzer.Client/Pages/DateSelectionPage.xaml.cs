using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using EmailAnalyzer.Client.Services;
using EmailAnalyzer.Shared.Models;
using EmailAnalyzer.Shared.Services;

namespace EmailAnalyzer.Client.Pages;

public partial class DateSelectionPage : ContentPage, IQueryAttributable
{
    private readonly ITokenStorageService _tokenStorage;
    private readonly HttpClient _httpClient;
    private string _provider = string.Empty;


    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime MinimumDate { get; set; }
    public DateTime MaximumDate { get; set; }
    public bool IsLoading { get; set; }
    public string ProviderInfo { get; set; } = string.Empty;
    public bool CanAnalyze => EndDate >= StartDate; // This property is used to enable/disable the analyze button

    public Command AnalyzeCommand { get; }
    
    public DateSelectionPage(ITokenStorageService tokenStorage)
    {
        InitializeComponent();
        _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
        
        StartDate = DateTime.Today.AddMonths(-1);
        EndDate = DateTime.Today;
        MinimumDate = DateTime.Today.AddMonths(-6);
        MaximumDate = DateTime.Today;

        AnalyzeCommand = new Command(async () => await OnAnalyze(), () => !IsLoading);
        BindingContext = this;
    }


    private async Task VerifyTokenAndRefreshIfNeeded()
    {
        try
        {
            Console.WriteLine($"DEBUG: Verifying token for provider {_provider}");

            var (accessToken, refreshToken, expiresAt) = await _tokenStorage.GetTokenAsync(_provider);
            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("DEBUG: No token found, redirecting to login");
                await Shell.Current.GoToAsync("///login");
                await DisplayAlert($"Error", $"No token found for {_provider}. Please authenticate first", "OK");
                return;
            }

            Console.WriteLine($"DEBUG: Token found for {_provider}, expires at {expiresAt}");

            if (expiresAt <= DateTime.UtcNow.AddMinutes(5))
            {
                Console.WriteLine("DEBUG: Token is expired, attempting refresh");

                var response = await _httpClient.PostAsync($"api/auth/refresh?provider={_provider}", null);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("DEBUG: Token refresh failed, redirecting to login");
                    await Shell.Current.GoToAsync("///login");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Token verification failed: {ex.Message}");
            await Shell.Current.GoToAsync("///login");
        }
    }


    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("provider", out var providerObj))
        {
            _provider = providerObj.ToString()!.Trim();
            Console.WriteLine($"DEBUG: Provider set to: {_provider}"); // do debugowania
            ProviderInfo = $"Connected to: {char.ToUpper(_provider[0]) + _provider[1..]}";
            OnPropertyChanged(nameof(ProviderInfo));

            // Asynchroniczne operacje powinny być wywołane po ustawieniu providera
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await VerifyTokenAndRefreshIfNeeded();
                await InitializeDateRange();
            });
        }
        else
        {
            Console.WriteLine("DEBUG: Provider not found in query parameters");
        }
    }

    /// <summary>
    /// This method initializes the date range for the selected provider.
    /// </summary>
    private async Task InitializeDateRange()
    {
        try
        {
            IsLoading = true;

            var response = await _httpClient.GetFromJsonAsync<DateRangeInfo>(
                $"api/email/available-range/{_provider}");

            if (response != null)
            {
                StartDate = response.DefaultStartDate;
                EndDate = response.DefaultEndDate;
                MinimumDate = response.EarliestDate;
                MaximumDate = response.LatestDate;
                ProviderInfo = $"Analyzing emails from {response.Provider}"; // Set the provider info

                // Notify the UI that the properties have changed
                OnPropertyChanged(nameof(MinimumDate));
                OnPropertyChanged(nameof(MaximumDate));
                OnPropertyChanged(nameof(StartDate));
                OnPropertyChanged(nameof(EndDate));
                OnPropertyChanged(nameof(ProviderInfo));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error",
                $"Could not initialize date range: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }


    /// <summary>
    /// This method is used to analyze the emails for the selected date range and to navigate to the summary page.
    /// </summary>
    private async Task OnAnalyze()
    {
        try
        {
            Console.WriteLine($"DEBUG: Starting analysis for provider {_provider}");

            await VerifyTokenAndRefreshIfNeeded();
            IsLoading = true;

            var url = $"api/Email/{_provider}?startDate={StartDate:yyyy-MM-ddTHH:mm:ss}&endDate={EndDate:yyyy-MM-ddTHH:mm:ss}";
            Console.WriteLine($"DEBUG: Sending request to {url}");

            var response = await _httpClient.PostAsync(url, new StringContent(""));
            if (response.IsSuccessStatusCode)
            {
                var emails = await response.Content.ReadFromJsonAsync<List<EmailMessage>>();
                Console.WriteLine($"DEBUG: Found {emails?.Count ?? 0} emails");
                await DisplayAlert("Success", $"Found {emails?.Count ?? 0} emails", "OK");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"DEBUG: Error response: {error}");
                await DisplayAlert("Error", $"Failed to analyze emails: {error}", "OK");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Analysis failed: {ex.Message}");
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }


    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var tokenStorage = App.Services?.GetService<ITokenStorageService>();
        if (tokenStorage != null)
        {
            var gmailToken = await tokenStorage.GetTokenAsync("gmail");
            if (gmailToken.accessToken == null)
            {
                Console.WriteLine("Redirecting to login because Gmail token is missing.");
                await Shell.Current.GoToAsync(nameof(LoginPage));
            }
            else
            {
                Console.WriteLine($"Gmail token found: {gmailToken.accessToken}");
            }
        }
        else
        {
            Console.WriteLine("Token storage service is not available.");
        }
    }

}