using LocationHeatMap.Services;
using LocationHeatMap.ViewModels;
using LocationHeatMap.Views;
using Microsoft.Extensions.Logging;

namespace LocationHeatMap;

/// <summary>
/// Application composition root: enables the Maps control and registers every service, view
/// model and page with the dependency injection container.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // Required by the Microsoft.Maui.Controls.Maps package: registers the platform
            // handler that backs <maps:Map> with MapKit on iOS and Google Maps on Android.
            // Without this call the control throws at page-load time.
            .UseMauiMaps()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Singletons: one database connection and one location listener for the app's lifetime.
        builder.Services.AddSingleton<ILocationDatabase, LocationDatabase>();
        builder.Services.AddSingleton<ILocationTrackingService, LocationTrackingService>();

        // MAUI Essentials' geolocation implementation, injected rather than used through the
        // static Geolocation.Default accessor so the tracking service stays testable.
        builder.Services.AddSingleton(Geolocation.Default);

        // The page and its view model are transient: a fresh pair each time the route is shown.
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainView>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
