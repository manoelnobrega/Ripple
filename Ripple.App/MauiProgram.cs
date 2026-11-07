using Microsoft.Extensions.Logging;
using Ripple.App.Services;

namespace Ripple.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Serviços usados pela página
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<RippleBridge>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Services.AddHybridWebViewDeveloperTools(); // inspecionar o HTML como num navegador
#endif

        return builder.Build();
    }
}
