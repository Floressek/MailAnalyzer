using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using EmailAnalyzer.Shared.Models.Database;
using EmailAnalyzer.Shared.Models.Email;
using Microsoft.Maui.Controls;

namespace EmailAnalyzer.Client.Pages;

public partial class SummaryPage : ContentPage, IQueryAttributable
{
    // -----------------------
    // Fields
    // -----------------------
    private readonly HttpClient _httpClient;
    private string _provider = string.Empty;
    private DateTime _startDate;
    private DateTime _endDate;

    // -----------------------
    // Constructor
    // -----------------------
    public SummaryPage()
    {
        InitializeComponent();

        // Setup HttpClient
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://mailanalyzer-production.up.railway.app/")
        };

        // Initialize commands
        ExportCommand = new Command(async () => await OnExportPdfAsync());
        NewAnalysisCommand = new Command(async () => await OnNewAnalysisAsync());
        SearchCommand = new Command(async () => await OnSearchAsync());

        // Set BindingContext to THIS code-behind
        BindingContext = this;
    }

    // -----------------------
    // Public Bindable Properties
    // -----------------------

    // Title for the analysis, e.g., “Connected to Gmail”
    private string _analysisTitle = string.Empty;

    public string AnalysisTitle
    {
        get => _analysisTitle;
        set
        {
            if (_analysisTitle != value)
            {
                _analysisTitle = value;
                OnPropertyChanged();
            }
        }
    }

    // Date range display string (e.g., “Analysis Period: 1/1/2023 - 1/15/2023”)
    private string _dateRange = string.Empty;

    public string DateRange
    {
        get => _dateRange;
        set
        {
            if (_dateRange != value)
            {
                _dateRange = value;
                OnPropertyChanged();
            }
        }
    }

    // “Analyzing” indicator for the progress frame
    private bool _isAnalyzing;

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set
        {
            if (_isAnalyzing != value)
            {
                _isAnalyzing = value;
                OnPropertyChanged();
            }
        }
    }

    // Progress bar status text
    private string _progressStatus = string.Empty;

    public string ProgressStatus
    {
        get => _progressStatus;
        set
        {
            if (_progressStatus != value)
            {
                _progressStatus = value;
                OnPropertyChanged();
            }
        }
    }

    // Progress from 0.0 to 1.0
    private double _analysisProgress;

    public double AnalysisProgress
    {
        get => _analysisProgress;
        set
        {
            if (Math.Abs(_analysisProgress - value) > 0.001)
            {
                _analysisProgress = value;
                OnPropertyChanged();
            }
        }
    }

    // If we have “results” to display
    public bool HasResults => !string.IsNullOrWhiteSpace(Summary);

    // The final summary of the emails
    private string _summary = string.Empty;

    public string Summary
    {
        get => _summary;
        set
        {
            if (_summary != value)
            {
                _summary = value;
                OnPropertyChanged();
                // Fire property changed for HasResults too
                OnPropertyChanged(nameof(HasResults));
            }
        }
    }

    // Search query for filtering the KeyInsights
    private string _searchQuery = string.Empty;

    public ObservableCollection<EmailDocument> SearchResults { get; } = new();

    // Dodaj nową komendę w konstruktorze
    public ICommand SearchCommand { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
            }
        }
    }

    // We can auto-map TopicClusters → KeyInsights for the UI
    public List<TopicCluster> TopicClusters { get; set; } = new();

    public List<string> KeyInsights
    {
        get
        {
            if (TopicClusters == null || !TopicClusters.Any())
                return new List<string>();

            // For demonstration, we convert each cluster to a single line string
            return TopicClusters.Select(tc => $"{tc.Topic} — Emails in topic: {tc.Count}").ToList();
        }
    }

    // If you still want to keep an error property:
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    private string _errorMessage = string.Empty;

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    // -----------------------
    // Commands
    // -----------------------
    // We’re only showcasing the two new commands in the XAML:
    public ICommand ExportCommand { get; }
    public ICommand NewAnalysisCommand { get; }

    // If you still want a "BackCommand" or "RefreshCommand" from the old code, keep them:
    // public ICommand BackCommand { get; }
    // public ICommand RefreshCommand { get; }

    // -----------------------
    // Query Params
    // -----------------------
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("provider", out var providerObj) &&
            query.TryGetValue("startDate", out var startDateObj) &&
            query.TryGetValue("endDate", out var endDateObj))
        {
            _provider = providerObj.ToString() ?? string.Empty;
            _startDate = DateTime.Parse(startDateObj.ToString()!);
            _endDate = DateTime.Parse(endDateObj.ToString()!);

            // Build the display
            AnalysisTitle = $"Connected to: {char.ToUpper(_provider[0]) + _provider[1..]}";
            DateRange = $"Analysis Period: {_startDate:d} - {_endDate:d}";

            // Kick off the summary load
            MainThread.BeginInvokeOnMainThread(async () => await LoadSummary());
        }
    }

    // -----------------------
    // Methods
    // -----------------------
    private async Task LoadSummary()
    {
        try
        {
            IsAnalyzing = true;
            ProgressStatus = "Preparing analysis...";
            AnalysisProgress = 0.0;
            ErrorMessage = string.Empty;

            // Simulate partial progress update
            await Task.Delay(500);
            ProgressStatus = "Analyzing email content...";
            AnalysisProgress = 0.5;

            // The actual request
            var response = await _httpClient.PostAsync(
                $"api/email/{_provider}/analyze?startDate={_startDate:yyyy-MM-ddTHH:mm:ss}&endDate={_endDate:yyyy-MM-ddTHH:mm:ss}",
                null);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ErrorMessage = $"Failed to load summary: {error}";
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<EmailSummaryResult>();
            if (result != null)
            {
                Summary = result.FinalSummary;

                TopicClusters = result.BatchSummaries
                    .Select(b => new TopicCluster
                    {
                        Topic = b.Summary,
                        Count = b.EmailCount
                    })
                    .ToList();

                // Fire property change for KeyInsights (derived from TopicClusters)
                OnPropertyChanged(nameof(KeyInsights));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            // Mark progress as complete
            AnalysisProgress = 1.0;
            ProgressStatus = "Analysis Complete";
            IsAnalyzing = false;
        }
    }

    private async Task OnExportPdfAsync()
    {
        if (!HasResults)
        {
            await DisplayAlert("Export", "No analysis results to export.", "OK");
            return;
        }

        // Insert real export logic here. For now, just show an alert
        await DisplayAlert("Export", "PDF export would happen here.", "OK");
    }

    private async Task OnSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;

        try
        {
            IsAnalyzing = true;
            SearchResults.Clear();

            var response = await _httpClient.GetAsync(
                $"api/email/{_provider}/search?query={Uri.EscapeDataString(SearchQuery)}&startDate={_startDate:yyyy-MM-dd}&endDate={_endDate:yyyy-MM-dd}");

            if (response.IsSuccessStatusCode)
            {
                var searchResult = await response.Content.ReadFromJsonAsync<SearchResult>();
                if (searchResult?.Results != null)
                {
                    foreach (var result in searchResult.Results)
                    {
                        SearchResults.Add(new EmailDocument
                        {
                            Subject = result.Subject,
                            From = result.From,
                            Content = result.Content,
                            ReceivedDate = result.ReceivedDate,
                            Similarity = result.Similarity
                        });
                    }

                    // Pokaż analizę w osobnym alercie
                    if (!string.IsNullOrEmpty(searchResult.Analysis))
                    {
                        await DisplayAlert("AI Analysis", searchResult.Analysis, "OK");
                    }
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Search Error", error, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Search failed: {ex.Message}", "OK");
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

// Model dla odpowiedzi z API
    public class SearchResult
    {
        public string Query { get; set; } = string.Empty;
        public int TotalResults { get; set; }
        public List<SearchResultItem> Results { get; set; } = new();
        public string Analysis { get; set; } = string.Empty;
    }

    public class SearchResultItem
    {
        public string Subject { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public DateTime ReceivedDate { get; set; }
        public double Similarity { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    private async Task OnNewAnalysisAsync()
    {
        // You might reset all the fields or navigate back to the start page
        // Here we just do a quick reset:
        Summary = string.Empty;
        TopicClusters.Clear();
        OnPropertyChanged(nameof(KeyInsights));

        AnalysisTitle = string.Empty;
        DateRange = string.Empty;

        ErrorMessage = string.Empty;
        IsAnalyzing = false;
        ProgressStatus = string.Empty;
        AnalysisProgress = 0.0;

        // Example: Navigate back to the previous page
        // or to a main page for new analysis input
        await Shell.Current.GoToAsync("..");
    }
}