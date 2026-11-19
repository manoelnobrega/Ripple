using Ripple.Api.Dtos;
using Ripple.Api.Services;

namespace Ripple.Api.Endpoints
{
    public static class ResponsavelEndpoints
    {
        public static void MapResponsavelEndpoints(this WebApplication app)
        {
            var grupo = app.MapGroup("/api/responsaveis");

            // GET /api/responsaveis
            grupo.MapGet("/", async (IResponsavelService servico) =>
                Results.Ok(await servico.ListarAsync()));

            // POST /api/responsaveis  com { "nome": "...", "tokenPush": "..." }
            grupo.MapPost("/", async (NovoResponsavelRequest req, IResponsavelService servico) =>
            {
                try
                {
                    var criado = await servico.CadastrarAsync(req);
                    return Results.Created($"/api/responsaveis/{criado.Id}", criado);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { erro = ex.Message });
                }
            });

            // PUT /api/responsaveis/{id}/token  com { "token": "..." }
            grupo.MapPut("/{id:int}/token", async (int id, AtualizarTokenRequest req, IResponsavelService servico) =>
            {
                try
                {
                    return await servico.AtualizarTokenAsync(id, req.Token)
                        ? Results.NoContent()
                        : Results.NotFound();
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { erro = ex.Message });
                }
            });

            // DELETE /api/responsaveis/{id}
            grupo.MapDelete("/{id:int}", async (int id, IResponsavelService servico) =>
                await servico.RemoverAsync(id) ? Results.NoContent() : Results.NotFound());
        }
    }
}
