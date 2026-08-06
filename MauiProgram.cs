using Microsoft.Extensions.Logging;

namespace LibraryManager;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// Forzar fechas en inglés de forma consistente, sin importar el idioma de Windows.
		var englishCulture = new System.Globalization.CultureInfo("en-US", useUserOverride: false);
		System.Globalization.CultureInfo.DefaultThreadCurrentCulture = englishCulture;
		System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = englishCulture;
		System.Globalization.CultureInfo.CurrentCulture = englishCulture;
		System.Globalization.CultureInfo.CurrentUICulture = englishCulture;

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Una instancia del repositorio y la página administra la sesión local.
		builder.Services.AddSingleton<Services.BookRepository>();
		builder.Services.AddSingleton<MainPage>();

		// Mantener disponibles los fallos operativos en el depurador en toda compilación.
		builder.Logging.AddDebug();

		return builder.Build();
	}
}
