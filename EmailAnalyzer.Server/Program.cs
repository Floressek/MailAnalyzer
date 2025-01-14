using EmailAnalyzer.Server.Services;
using EmailAnalyzer.Server.Services.Email;
using EmailAnalyzer.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5045); // Nasłuchuj na wszystkich interfejsach
});

// Konfiguracja logowania
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Konfiguracja email services
builder.Services.Configure<OutlookConfiguration>(
    builder.Configuration.GetSection("Outlook"));
builder.Services.Configure<GmailConfiguration>(
    builder.Configuration.GetSection("Gmail"));

// Rejestracja serwisów email
builder.Services.AddScoped<OutlookEmailService>();
builder.Services.AddScoped<GmailEmailService>();

// Rejestracja factory dla serwisów email
builder.Services.AddScoped<IEmailServiceFactory>(sp =>
{
    return new EmailServiceFactory(
        outlook: sp.GetRequiredService<OutlookEmailService>(),
        gmail: sp.GetRequiredService<GmailEmailService>()
    );
});

// Rejestracja TokenStorageService dla serwera
builder.Services.AddSingleton<ITokenStorageService, ServerTokenStorageService>();

// Dodaj CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.Use(async (context, next) =>
{
    Console.WriteLine($"[REQUEST] {context.Request.Method} {context.Request.Path} from {context.Connection.RemoteIpAddress}");
    await next();
    Console.WriteLine($"[RESPONSE] {context.Response.StatusCode}");
});


// Dodaj middleware w odpowiedniej kolejności
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// Konfiguracja routingu
app.MapControllerRoute(
    name: "auth-callback",
    pattern: "auth/callback",
    defaults: new { controller = "Auth", action = "Callback" }
);

app.MapControllers();

app.Use(async (context, next) =>
{
    Console.WriteLine($"[MIDDLEWARE] Request: {context.Request.Method} {context.Request.Path}");
    await next();
    Console.WriteLine($"[MIDDLEWARE] Response: {context.Response.StatusCode}");
});
app.Run();