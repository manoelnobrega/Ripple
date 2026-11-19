using Ripple.Api.Entities;

namespace Ripple.Api.Services
{
    public interface INotificacaoService
    {
        // Envia a notificação de queda aos responsaveis ativos da piscina.
        Task NotificarQuedaAsync(Evento evento);
    }
}
