using EmailAnalyzer.Client.Pages;
using EmailAnalyzer.Client.Services;
using EmailAnalyzer.Shared.Services;
using Microsoft.Extensions.Logging;

namespace EmailAnalyzer.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Console.WriteLine("Creating MAUI app...");

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        Console.WriteLine("Registering services...");
        
        builder.Services.AddHttpClient<ITokenStorageService, ClientTokenStorageService>(client =>
        {
            client.BaseAddress = new Uri("https://mailanalyzer-production.up.railway.app/");
        });
        
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DateSelectionPage>();
        builder.Services.AddTransient<SummaryPage>();
#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.ClearProviders();
#endif

        return builder.Build();
    }
}