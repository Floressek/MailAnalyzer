using EmailAnalyzer.Client.Pages;

namespace EmailAnalyzer.Client;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Console.WriteLine("AppShell constructor called");
		// Routing.RegisterRoute("dateSelection", typeof(DateSelectionPage));
		Routing.RegisterRoute("///dateSelection", typeof(DateSelectionPage));
		Routing.RegisterRoute("///summary", typeof(SummaryPage));
	}
}
