using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ripple.Api;
using Ripple.Api.Data;
using Ripple.Api.Dtos;
using Ripple.Api.Entities;
using Ripple.Api.Services;
using Xunit;

namespace Ripple.Api.Tests;

// Relógio fixo em UTC: quinta-feira, 12/11/2026, 15:00.
file class RelogioFixo : TimeProvider
{
    private readonly DateTimeOffset _agora;
    public RelogioFixo(DateTime utc) => _agora = new DateTimeOffset(utc, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _agora;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

public class EventoServiceTests : IDisposable
{
    private static readonly DateTime Agora = new(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _conexao;
    private readonly AppDb _db;
    private readonly EventoService _servico;

    public EventoServiceTests()
    {
        // Banco SQLite em memória: vive enquanto a conexão estiver aberta.
        _conexao = new SqliteConnection("Data Source=:memory:");
        _conexao.Open();

        var opcoes = new DbContextOptionsBuilder<AppDb>().UseSqlite(_conexao).Options;
        _db = new AppDb(opcoes);
        _db.Database.EnsureCreated();
        _db.Piscinas.Add(new Piscina { Nome = "Piscina de teste" });
        _db.SaveChanges();

        _servico = new EventoService(_db, new RelogioFixo(Agora));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexao.Dispose();
    }

    private void Adicionar(TipoEvento tipo, DateTime utc)
    {
        var piscinaId = _db.Piscinas.Select(p => p.Id).First();
        _db.Eventos.Add(new Evento { PiscinaId = piscinaId, Tipo = tipo, DataHora = utc });
        _db.SaveChanges();
    }

    // ---------- RegistrarAsync ----------

    [Fact]
    public async Task Registrar_Queda_GravaComDataDoRelogioEMarcaSimulado()
    {
        var evento = await _servico.RegistrarAsync(new NovoEventoRequest("queda", null));

        Assert.Equal(TipoEvento.Queda, evento.Tipo);
        Assert.Equal(Agora, evento.DataHora);
        Assert.True(evento.Simulado);
        Assert.Equal(1, await _db.Eventos.CountAsync());
    }

    [Fact]
    public async Task Registrar_SimuladoFalso_GravaComoReal()
    {
        var evento = await _servico.RegistrarAsync(new NovoEventoRequest("queda", false));

        Assert.False(evento.Simulado);
    }

    [Theory]
    [InlineData("queda", TipoEvento.Queda)]
    [InlineData("off", TipoEvento.Descartado)]
    [InlineData("descartado", TipoEvento.Descartado)]
    [InlineData("test", TipoEvento.Teste)]
    [InlineData("TESTE", TipoEvento.Teste)]
    [InlineData("  Queda ", TipoEvento.Queda)]
    public async Task Registrar_AceitaOsNomesEsperados(string texto, TipoEvento esperado)
    {
        var evento = await _servico.RegistrarAsync(new NovoEventoRequest(texto, null));

        Assert.Equal(esperado, evento.Tipo);
    }

    [Theory]
    [InlineData("xyz")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Registrar_TipoInvalido_LancaArgumentExceptionENaoGrava(string? texto)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _servico.RegistrarAsync(new NovoEventoRequest(texto!, null)));

        Assert.Equal(0, await _db.Eventos.CountAsync());
    }

    // ---------- ObterHistoricoAsync ----------

    [Fact]
    public async Task Historico_Semana_TemSeteDiasTerminandoHoje()
    {
        var h = await _servico.ObterHistoricoAsync("week");

        // 12/11/2026 é quinta; a janela vai de sexta (06/11) a quinta (12/11).
        Assert.Equal(new[] { "Sex", "Sáb", "Dom", "Seg", "Ter", "Qua", "Qui" }, h.Labels);
        Assert.Equal(new int[7], h.Values);
        Assert.Empty(h.Events);
    }

    [Fact]
    public async Task Historico_PeriodoNulo_TrataComoSemana()
    {
        var h = await _servico.ObterHistoricoAsync(null);

        Assert.Equal(7, h.Labels.Length);
    }

    [Fact]
    public async Task Historico_Semana_ContaPorDiaEIgnoraEventosAntigos()
    {
        Adicionar(TipoEvento.Teste, new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc)); // qui
        Adicionar(TipoEvento.Queda, new DateTime(2026, 11, 12, 14, 0, 0, DateTimeKind.Utc)); // qui
        Adicionar(TipoEvento.Descartado, new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc)); // ter
        Adicionar(TipoEvento.Teste, new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc)); // antes da janela

        var h = await _servico.ObterHistoricoAsync("week");

        Assert.Equal(new[] { 0, 0, 0, 0, 1, 0, 2 }, h.Values);
        Assert.Equal(3, h.Events.Count);
    }

    [Fact]
    public async Task Historico_Eventos_VemDoMaisRecenteComTituloETipoParaOFrontend()
    {
        Adicionar(TipoEvento.Descartado, new DateTime(2026, 11, 10, 9, 5, 0, DateTimeKind.Utc));
        Adicionar(TipoEvento.Queda, new DateTime(2026, 11, 12, 14, 30, 0, DateTimeKind.Utc));

        var h = await _servico.ObterHistoricoAsync("week");

        Assert.Equal(new EventoResponse("Queda detectada", "Qui, 14:30", "queda"), h.Events[0]);
        Assert.Equal(new EventoResponse("Ondulação leve descartada", "Ter, 09:05", "off"), h.Events[1]);
    }

    [Fact]
    public async Task Historico_Mes_AgrupaPorBlocosDeSeteDias()
    {
        // A janela do mês começa em 16/10 (27 dias antes de 12/11).
        Adicionar(TipoEvento.Teste, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc)); // bloco 1
        Adicionar(TipoEvento.Teste, new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc)); // bloco 1
        Adicionar(TipoEvento.Teste, new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc)); // bloco 2
        Adicionar(TipoEvento.Queda, new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc)); // bloco 4
        Adicionar(TipoEvento.Teste, new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc)); // fora da janela

        var h = await _servico.ObterHistoricoAsync("month");

        Assert.Equal(new[] { "Sem 1", "Sem 2", "Sem 3", "Sem 4" }, h.Labels);
        Assert.Equal(new[] { 2, 1, 0, 1 }, h.Values);
        Assert.Equal(4, h.Events.Count);
    }
}
