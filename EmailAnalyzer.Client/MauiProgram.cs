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

        builder.Services.AddSingleton<ITokenStorageService, SecureTokenStorageService>();
        builder.Services.AddSingleton<AppShell>(); // Dodaj to
        builder.Services.AddTransient<LoginPage>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.ClearProviders();
#endif

        return builder.Build();
    }
}