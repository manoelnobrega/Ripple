using System.Text.Json;

namespace Ripple.App.Services;

/// <summary>
/// Os métodos públicos desta classe são chamados pelo JavaScript do index.html:
///   window.HybridWebView.InvokeDotNet('ObterHistorico', ['week'])
/// Todos devolvem uma string JSON. Se algo falhar, devolvem {"erro":"mensagem"}
/// em vez de lançar exceção, e o index.html mostra o aviso na tela.
/// </summary>
public class RippleBridge
{
    private readonly ApiClient _api;

    public RippleBridge(ApiClient api) => _api = api;

    public async Task<string> ObterHistorico(string periodo)
    {
        try
        {
            if (periodo != "week" && periodo != "month") periodo = "week";
            return await _api.ObterHistoricoAsync(periodo);
        }
        catch (Exception ex)
        {
            return Erro(ex);
        }
    }

    public async Task<string> RegistrarQueda()
    {
        try
        {
            await _api.RegistrarEventoAsync("queda");
            return "{\"ok\":true}";
        }
        catch (Exception ex)
        {
            return Erro(ex);
        }
    }

    private static string Erro(Exception ex) =>
        $"{{\"erro\":{JsonSerializer.Serialize(ex.Message)}}}";
}
