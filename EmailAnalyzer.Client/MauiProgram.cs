﻿using EmailAnalyzer.Client.Pages;
using EmailAnalyzer.Client.Services;
using EmailAnalyzer.Shared.Services;
using Microsoft.Extensions.Logging;

namespace EmailAnalyzer.Client;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});
		
		builder.Services.AddSingleton<ITokenStorageService, SecureTokenStorageService>();
		builder.Services.AddTransient<LoginPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
