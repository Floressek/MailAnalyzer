namespace EmailAnalyzer.Client;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		Console.WriteLine("App constructor called");
		MainPage = new AppShell();
		Console.WriteLine("MainPage set to AppShell");
	}

	// protected override Window CreateWindow(IActivationState? activationState)
	// {
	// 	return new Window(new AppShell());
	// }
}