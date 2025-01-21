using System.Reflection;
using EmailAnalyzer.Server.Services;
using EmailAnalyzer.Server.Services.Database;
using EmailAnalyzer.Server.Services.Email;
using EmailAnalyzer.Server.Services.OpenAI;
using EmailAnalyzer.Shared.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Keep the Kestrel configuration for production (Railway)
if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        // Railway injects PORT environment variable
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        serverOptions.ListenAnyIP(int.Parse(port));
        Console.WriteLine($"[STARTUP] Configuring server to listen on port {port}");
    });
}

// Add Swagger with documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Email Analyzer API",
        Version = "v1",
        Description = @"API for Email Analyzer application allowing for email analysis and semantic search.

PL: API do aplikacji Email Analyzer umożliwiające analizę maili i wyszukiwanie semantyczne.

Main features / Główne funkcjonalności:
- OAuth2 authentication with Gmail and Outlook / Uwierzytelnianie OAuth2 z Gmail i Outlook
- Email fetching and analysis / Pobieranie i analiza maili
- AI-powered semantic search / Wyszukiwanie semantyczne wspierane przez AI
- Email summaries and insights / Podsumowania i wnioski z maili",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@emailanalyzer.com"
        }
    });
    // Documentation XML file
    var xmlFIle = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFIle); // xml path to the base directory
    c.IncludeXmlComments(xmlPath);
});

// Enable XML documentation
builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.WriteIndented = true; })
    .AddXmlSerializerFormatters();

// Logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Email service configuration
builder.Services.Configure<OutlookConfiguration>(
    builder.Configuration.GetSection("Outlook"));
builder.Services.Configure<GmailConfiguration>(
    builder.Configuration.GetSection("Gmail"));

// Email service registration
builder.Services.AddScoped<OutlookEmailService>();
builder.Services.AddScoped<GmailEmailService>();
builder.Services.AddScoped<IEmailServiceFactory>(sp =>
    new EmailServiceFactory(
        outlook: sp.GetRequiredService<OutlookEmailService>(),
        gmail: sp.GetRequiredService<GmailEmailService>()
    ));

// Token storage service
builder.Services.AddSingleton<ServerTokenStorageService>();
builder.Services.AddSingleton<ITokenStorageService, ServerTokenStorageService>();

// MongoDB configuration
builder.Services.Configure<MongoDBConfiguration>(builder.Configuration.GetSection("MongoDB"));
builder.Services.AddSingleton<MongoDBService>();
builder.Services.AddScoped<EmailProcessingService>();

// OpenAI configuration
builder.Services.Configure<OpenAIConfiguration>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddScoped<OpenAIService>();

// Configure HttpClient for OpenAI service
builder.Services.AddHttpClient<OpenAIService>();

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.SetIsOriginAllowed(_ => true) // FIXME: PRODUCTION DELETE
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();


var app = builder.Build();

// Development specific middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Swagger UI dostępny również w produkcji
app.UseSwagger();
app.UseSwaggerUI();

// Request logging middleware
app.Use(async (context, next) =>
{
    Console.WriteLine($"[REQUEST] {context.Request.Method} {context.Request.Path}");
    await next();
    Console.WriteLine($"[RESPONSE] {context.Response.StatusCode}");
});

app.UseCors("AllowAll");

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "auth-callback",
    pattern: "auth/callback",
    defaults: new { controller = "Auth", action = "Callback" }
);

app.MapControllers();

app.Run();