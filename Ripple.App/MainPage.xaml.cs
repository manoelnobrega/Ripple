using Ripple.App.Services;

namespace Ripple.App;

public partial class MainPage : ContentPage
{
    private readonly PushService _push;
    private readonly RippleBleService _ble;

    public MainPage(RippleBridge bridge, PushService push, RippleBleService ble)
    {
        InitializeComponent();
        _push = push;
        _ble = ble;

        // Deixa o JavaScript chamar os métodos públicos do RippleBridge.
        hybridWebView.SetInvokeJavaScriptTarget(bridge);

        // Eventos da boia viram mensagens para o JavaScript (ver listener no index.html).
        _ble.MensagemParaTela += msg =>
            MainThread.BeginInvokeOnMainThread(() => hybridWebView.SendRawMessage(msg));
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
