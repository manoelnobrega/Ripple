using Ripple.App.Services;

namespace Ripple.App;

public partial class MainPage : ContentPage
{
    private readonly PushService _push;

    public MainPage(RippleBridge bridge, PushService push)
    {
        InitializeComponent();
        _push = push;

        // Deixa o JavaScript chamar os métodos públicos do RippleBridge.
        hybridWebView.SetInvokeJavaScriptTarget(bridge);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _push.InicializarAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Push] Erro ao inicializar: {ex}");
        }
    }
}
