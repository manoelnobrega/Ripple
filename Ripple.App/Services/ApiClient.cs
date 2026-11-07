using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Ripple.App.Services;

/// <summary>
/// Conversa com a Ripple.Api por HTTP. Devolve o JSON da API sem converter em classes,
/// porque o index.html já espera exatamente esse formato (labels, values, events).
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

    /// <summary>POST /api/eventos com { "tipo": "..." }.</summary>
    public async Task RegistrarEventoAsync(string tipo)
    {
        // Serializa só a string, o que evita problemas com trimming no Android em Release.
        var json = $"{{\"tipo\":{JsonSerializer.Serialize(tipo)}}}";
        using var conteudo = new StringContent(json, Encoding.UTF8, "application/json");
        using var resposta = await _http.PostAsync("/api/eventos", conteudo);
        resposta.EnsureSuccessStatusCode();
    }
}
