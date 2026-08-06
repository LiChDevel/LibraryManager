namespace LibraryManager;

public partial class App : Application
{
	public App(MainPage mainPage)
	{
		InitializeComponent();

		// La inyección de dependencias crea MainPage con su repositorio y registrador.
		MainPage = new AppShell(mainPage);
	}
}
