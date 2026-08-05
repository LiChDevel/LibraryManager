using Microsoft.Extensions.Logging;

namespace LibraryManager;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
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

		builder.Services.AddSingleton<Services.BookRepository>();
		builder.Services.AddSingleton<MainPage>();

		builder.Logging.AddDebug();

		return builder.Build();
	}
}
