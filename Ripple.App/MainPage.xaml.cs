using Ripple.App.Services;

namespace Ripple.App;

public partial class MainPage : ContentPage
{
    public MainPage(RippleBridge bridge)
    {
        InitializeComponent();

        // Deixa o JavaScript chamar os métodos públicos do RippleBridge.
        hybridWebView.SetInvokeJavaScriptTarget(bridge);
    }
}
