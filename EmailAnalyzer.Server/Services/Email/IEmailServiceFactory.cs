namespace EmailAnalyzer.Server.Services.Email;

public interface IEmailServiceFactory
{
    IEmailService GetService(string provider);
}

public class EmailServiceFactory : IEmailServiceFactory
{
    private readonly Dictionary<string, IEmailService> _services;

    public EmailServiceFactory(
        OutlookEmailService outlook,
        GmailEmailService gmail)
    {
        _services = new Dictionary<string, IEmailService>
        {
            { "outlook", outlook },
            { "gmail", gmail }
        };
    }

    public IEmailService GetService(string provider)
    {
        if (!_services.TryGetValue(provider.ToLower(), out var service))
        {
            throw new ArgumentException($"Unknown email provider: {provider}");
        }
        return service;
    }
}