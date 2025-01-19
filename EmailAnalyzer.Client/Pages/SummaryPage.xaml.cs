using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace EmailAnalyzer.Client.Pages;

using System.Windows.Input;
using EmailAnalyzer.Shared.Models.Database;
using EmailAnalyzer.Shared.Models.Email;

public partial class SummaryPage : ContentPage, IQueryAttributable
{
    private readonly HttpClient _httpClient;
    private string _provider = string.Empty;
    private DateTime _startDate;
    private DateTime _endDate;
    
    public bool IsLoading { get; set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public string ErrorMessage { get; set; } = string.Empty;
    public string ProviderInfo { get; set; } = string.Empty;
    public string DateRangeInfo { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int EmailCount { get; set; }
    public List<TopicCluster> TopicClusters { get; set; } = new();
    
    public ICommand BackCommand { get; }
    public ICommand RefreshCommand { get; }

    public SummaryPage()
    {
        InitializeComponent();
        
        // Setup HttpClient
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://mailanalyzer-production.up.railway.app/")
        };

        // Initialize commands
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        RefreshCommand = new Command(async () => await LoadSummary());
        
        BindingContext = this;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("provider", out var providerObj) &&
            query.TryGetValue("startDate", out var startDateObj) &&
            query.TryGetValue("endDate", out var endDateObj))
        {
            _provider = providerObj.ToString()!;
            _startDate = DateTime.Parse(startDateObj.ToString()!);
            _endDate = DateTime.Parse(endDateObj.ToString()!);

            ProviderInfo = $"Connected to: {char.ToUpper(_provider[0]) + _provider[1..]}";
            DateRangeInfo = $"Analysis Period: {_startDate:d} - {_endDate:d}";
            
            OnPropertyChanged(nameof(ProviderInfo));
            OnPropertyChanged(nameof(DateRangeInfo));

            MainThread.BeginInvokeOnMainThread(async () => await LoadSummary());
        }
    }

    private async Task LoadSummary()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(HasError));

            // Request email analysis
            var response = await _httpClient.PostAsync(
                $"api/email/{_provider}/analyze?startDate={_startDate:yyyy-MM-ddTHH:mm:ss}&endDate={_endDate:yyyy-MM-ddTHH:mm:ss}",
                null);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<EmailSummaryResult>();
                if (result != null)
                {
                    Summary = result.FinalSummary;
                    EmailCount = result.TotalEmails;
                    TopicClusters = result.BatchSummaries
                        .Select(b => new TopicCluster 
                        { 
                            Topic = b.Summary, 
                            Count = b.EmailCount 
                        })
                        .ToList();

                    OnPropertyChanged(nameof(Summary));
                    OnPropertyChanged(nameof(EmailCount));
                    OnPropertyChanged(nameof(TopicClusters));
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                ErrorMessage = $"Failed to load summary: {error}";
                OnPropertyChanged(nameof(HasError));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLoading));
        }
    }
}