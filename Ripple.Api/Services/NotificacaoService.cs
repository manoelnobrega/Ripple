using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Ripple.Api.Data;
using Ripple.Api.Entities;

namespace Ripple.Api.Services
{
    public class NotificacaoService : INotificacaoService
    {
        private readonly AppDb _db;
        private readonly ILogger<NotificacaoService> _log;

        public NotificacaoService(AppDb db, ILogger<NotificacaoService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task NotificarQuedaAsync(Evento evento)
        {
            try
            {
                if (FirebaseApp.DefaultInstance is null)
                {
                    _log.LogWarning("Firebase não configurado: notificação de queda não enviada.");
                    return;
                }

                var tokens = await _db.Responsaveis
                    .Where(r => r.PiscinaId == evento.PiscinaId
                                && r.NotificacaoAtiva
                                && r.TokenPush != null
                                && r.TokenPush != "")
                    .Select(r => r.TokenPush!)
                    .ToListAsync();

                if (tokens.Count == 0)
                {
                    _log.LogInformation("Nenhum responsável com token: nada a enviar.");
                    return;
                }

                var mensagem = new MulticastMessage
                {
                    Tokens = tokens,
                    Notification = new Notification
                    {
                        Title = "Queda detectada!",
                        Body = "Possível queda na piscina. Verifique agora."
                    },
                    // "priority: high" no data faz o app mostrar o aviso também com o app aberto.
                    Data = new Dictionary<string, string>
                    {
                        ["tipo"] = "queda",
                        ["eventoId"] = evento.Id.ToString(),
                        ["priority"] = "high"
                    },
                    Android = new AndroidConfig { Priority = Priority.High }
                };

                var resultado = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(mensagem);
                _log.LogInformation("Push de queda: {Ok} enviados, {Falhas} falhas.",
                    resultado.SuccessCount, resultado.FailureCount);

                // Tokens que o Firebase diz que não existem mais (app desinstalado etc.) são limpos.
                var invalidos = new List<string>();
                for (var i = 0; i < resultado.Responses.Count; i++)
                {
                    var envio = resultado.Responses[i];
                    if (!envio.IsSuccess
                        && envio.Exception?.MessagingErrorCode is MessagingErrorCode.Unregistered
                            or MessagingErrorCode.InvalidArgument)
                    {
                        invalidos.Add(tokens[i]);
                    }
                }

                if (invalidos.Count > 0)
                {
                    var afetados = await _db.Responsaveis
                        .Where(r => r.TokenPush != null && invalidos.Contains(r.TokenPush))
                        .ToListAsync();
                    foreach (var afetado in afetados) afetado.TokenPush = null;
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha ao enviar a notificação de queda.");
            }
        }
    }
}
