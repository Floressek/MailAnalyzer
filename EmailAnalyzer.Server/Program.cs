using EmailAnalyzer.Server.Services.Email;

var builder = WebApplication.CreateBuilder(args);

// Konfiguracja logowania
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Konfiguracja email services
builder.Services.Configure<OutlookConfiguration>(
    builder.Configuration.GetSection("Outlook"));
builder.Services.Configure<GmailConfiguration>(
    builder.Configuration.GetSection("Gmail"));

// Rejestracja obu serwisów email
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

app.MapControllerRoute( // Added auth-callback route
    name: "auth-callback",
    pattern: "auth/callback",
    defaults: new { controller = "Auth", action = "Callback" }
);

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();