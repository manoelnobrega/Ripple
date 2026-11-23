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
    private readonly RippleBleService _ble;

    public RippleBridge(ApiClient api, RippleBleService ble)
    {
        _api = api;
        _ble = ble;
    }

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

    /// <summary>Botão "Simular queda" da tela: registra um evento simulado, sem passar pela boia.</summary>
    public async Task<string> RegistrarQueda()
    {
        try
        {
            await _api.RegistrarEventoAsync("queda", simulado: true);
            return "{\"ok\":true}";
        }
        catch (Exception ex)
        {
            return Erro(ex);
        }
    }

    // ---------- boia (BLE) ----------

    /// <summary>Botão "Conectar com a boia": pede permissão e começa a procurar.</summary>
    public async Task<string> ConectarBoia()
    {
        try
        {
            var erro = await _ble.IniciarAsync();
            return erro is null ? "{\"ok\":true}" : $"{{\"erro\":{JsonSerializer.Serialize(erro)}}}";
        }
        catch (Exception ex)
        {
            return Erro(ex);
        }
    }

    public async Task<string> DesconectarBoia()
    {
        try
        {
            await _ble.PararAsync();
            return "{\"ok\":true}";
        }
        catch (Exception ex)
        {
            return Erro(ex);
        }
    }

    public Task<string> EstadoBoia() =>
        Task.FromResult($"{{\"estado\":{JsonSerializer.Serialize(_ble.Estado)}}}");

    /// <summary>Tira a boia do modo alerta (para os pop-ups) e volta a monitorar.</summary>
    public Task<string> RearmarBoia() => Comando("rearm");

    /// <summary>Dispara um alerta de teste na boia (gera evento do tipo "test", sem push).</summary>
    public Task<string> TestarAlertaBoia() => Comando("test_alert");

    private async Task<string> Comando(string cmd)
    {
        try
        {
            var ok = await _ble.EnviarComandoAsync(cmd);
            return ok ? "{\"ok\":true}" : "{\"erro\":\"Boia não conectada.\"}";
        }
        catch (Exception ex)
        {
            return Erro(ex);
        }
    }

    public async Task<string> ListarResponsaveis()
    {
        try { return await _api.ListarResponsaveisAsync(); }
        catch (Exception ex) { return Erro(ex); }
    }

    public async Task<string> AdicionarResponsavel(string nome)
    {
        try
        {
            await _api.AdicionarResponsavelAsync(nome);
            return "{\"ok\":true}";
        }
        catch (Exception ex) { return Erro(ex); }
    }

    public async Task<string> RemoverResponsavel(int id)
    {
        try
        {
            await _api.RemoverResponsavelAsync(id);
            return "{\"ok\":true}";
        }
        catch (Exception ex) { return Erro(ex); }
    }

    private static string Erro(Exception ex) =>
        $"{{\"erro\":{JsonSerializer.Serialize(ex.Message)}}}";
}
