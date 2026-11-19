using Microsoft.Extensions.Logging.Abstractions;
using Ripple.Api.Entities;
using Ripple.Api.Services;
using Xunit;

namespace Ripple.Api.Tests;

public class NotificacaoServiceTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();

    public void Dispose() => _banco.Dispose();

    [Fact]
    public async Task SemFirebaseConfigurado_NaoLancaExcecao()
    {
        // Nos testes o FirebaseApp não é criado; o serviço deve apenas ignorar o envio.
        var servico = new NotificacaoService(_banco.Db, NullLogger<NotificacaoService>.Instance);
        var evento = new Evento { PiscinaId = 1, Tipo = TipoEvento.Queda, DataHora = DateTime.UtcNow };

        var ex = await Record.ExceptionAsync(() => servico.NotificarQuedaAsync(evento));

        Assert.Null(ex);
    }
}
