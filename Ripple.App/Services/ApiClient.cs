using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Ripple.App.Services;

/// <summary>
/// Conversa com a Ripple.Api por HTTP. O histórico é devolvido como JSON bruto, porque o
/// index.html já espera exatamente esse formato (labels, values, events).
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    // O emulador Android enxerga o PC em 10.0.2.2 (localhost seria o próprio emulador).
    // A porta precisa ser a mesma do launchSettings.json da Ripple.Api.
    // Na sprint 4, troque pela URL HTTPS da API publicada.
    public static string BaseUrl =>
#if ANDROID
        "http://10.0.2.2:5080";
#else
        "http://localhost:5080";
#endif

    /// <summary>GET /api/eventos?periodo=week|month. Devolve o JSON bruto.</summary>
    public Task<string> ObterHistoricoAsync(string periodo) =>
        _http.GetStringAsync($"/api/eventos?periodo={Uri.EscapeDataString(periodo)}");

    /// <summary>POST /api/eventos com { "tipo": "...", "simulado": true|false }.</summary>
    public async Task RegistrarEventoAsync(string tipo, bool simulado = true)
    {
        // A API assume simulado=true quando o campo não vem; evento vindo da boia real manda false.
        var json = $"{{\"tipo\":{JsonSerializer.Serialize(tipo)},\"simulado\":{(simulado ? "true" : "false")}}}";
        using var conteudo = new StringContent(json, Encoding.UTF8, "application/json");
        using var resposta = await _http.PostAsync("/api/eventos", conteudo);
        resposta.EnsureSuccessStatusCode();
    }

    /// <summary>POST /api/responsaveis. Cadastra este aparelho e devolve o id do responsável.</summary>
    public async Task<int> CadastrarAparelhoAsync(string nome, string token)
    {
        var json = $"{{\"nome\":{JsonSerializer.Serialize(nome)},\"tokenPush\":{JsonSerializer.Serialize(token)}}}";
        using var conteudo = new StringContent(json, Encoding.UTF8, "application/json");
        using var resposta = await _http.PostAsync("/api/responsaveis", conteudo);
        resposta.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    /// <summary>PUT /api/responsaveis/{id}/token. Devolve false se o responsável não existe mais.</summary>
    public async Task<bool> AtualizarTokenAsync(int id, string token)
    {
        var json = $"{{\"token\":{JsonSerializer.Serialize(token)}}}";
        using var conteudo = new StringContent(json, Encoding.UTF8, "application/json");
        using var resposta = await _http.PutAsync($"/api/responsaveis/{id}/token", conteudo);

        if (resposta.StatusCode == HttpStatusCode.NotFound) return false;
        resposta.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>GET /api/responsaveis. Devolve o JSON bruto (lista).</summary>
    public Task<string> ListarResponsaveisAsync() => _http.GetStringAsync("/api/responsaveis");

    /// <summary>POST /api/responsaveis só com o nome (sem aparelho). Se a API recusar, lança com a mensagem dela.</summary>
    public async Task AdicionarResponsavelAsync(string nome)
    {
        var json = $"{{\"nome\":{JsonSerializer.Serialize(nome)}}}";
        using var conteudo = new StringContent(json, Encoding.UTF8, "application/json");
        using var resposta = await _http.PostAsync("/api/responsaveis", conteudo);

        if (resposta.StatusCode == HttpStatusCode.BadRequest)
        {
            // A API devolve { "erro": "..." } (nome vazio, limite de 5, etc.).
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            var msg = doc.RootElement.TryGetProperty("erro", out var e) ? e.GetString() : null;
            throw new InvalidOperationException(msg ?? "Dados inválidos.");
        }
        resposta.EnsureSuccessStatusCode();
    }

    /// <summary>DELETE /api/responsaveis/{id}. Se já não existe, considera removido.</summary>
    public async Task RemoverResponsavelAsync(int id)
    {
        using var resposta = await _http.DeleteAsync($"/api/responsaveis/{id}");
        if (resposta.StatusCode == HttpStatusCode.NotFound) return;
        resposta.EnsureSuccessStatusCode();
    }
}
