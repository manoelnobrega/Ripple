using Ripple.Api.Dtos;
using Ripple.Api.Entities;

namespace Ripple.Api.Services;

public interface IEventoService
{
    /// <summary>Grava um evento. Lança ArgumentException se o tipo for inválido.</summary>
    Task<Evento> RegistrarAsync(NovoEventoRequest req);

    /// <summary>Monta o histórico semanal ("week", padrão) ou mensal ("month").</summary>
    Task<HistoricoResponse> ObterHistoricoAsync(string? periodo);
}
