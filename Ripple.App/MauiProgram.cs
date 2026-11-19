using Microsoft.Extensions.Logging;
using Plugin.FirebasePushNotifications;
using Ripple.App.Services;

namespace Ripple.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseFirebasePushNotifications(o =>
            {
#if ANDROID
                // Avisos de queda aparecem como pop-up (heads-up), não só como ícone na barra.
                // A importância do canal só vale na criação: se mudar depois, desinstale o app.
                o.Android.DefaultNotificationImportance = Android.App.NotificationImportance.High;
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Serviços usados pela página
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<RippleBridge>();
        builder.Services.AddSingleton<PushService>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Services.AddHybridWebViewDeveloperTools(); // inspecionar o HTML como num navegador
#endif

        return builder.Build();
    }
}
