using EmailAnalyzer.Server.Services;
using EmailAnalyzer.Server.Services.Database;
using EmailAnalyzer.Server.Services.Email;
using EmailAnalyzer.Server.Services.OpenAI;
using EmailAnalyzer.Shared.Services;

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

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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