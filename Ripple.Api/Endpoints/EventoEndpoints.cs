using Ripple.Api.Dtos;
using Ripple.Api.Services;

namespace Ripple.Api.Endpoints;

public static class EventoEndpoints
{
    public static void MapEventoEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/eventos");

        // GET /api/eventos?periodo=week|month
        grupo.MapGet("/", async (string? periodo, IEventoService servico) =>
            Results.Ok(await servico.ObterHistoricoAsync(periodo)));

        // POST /api/eventos  com { "tipo": "queda" }
        grupo.MapPost("/", async (NovoEventoRequest req, IEventoService servico) =>
        {
            try
            {
                await servico.RegistrarAsync(req);
                return Results.Created("/api/eventos", new { ok = true });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { erro = ex.Message });
            }
        });
    }
}
