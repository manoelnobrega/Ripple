using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ripple.Api.Data;
using Ripple.Api.Entities;
using Ripple.Api.Services;

namespace Ripple.Api.Tests;

// Relógio fixo em UTC.
internal class RelogioFixo : TimeProvider
{
    private readonly DateTimeOffset _agora;
    public RelogioFixo(DateTime utc) => _agora = new DateTimeOffset(utc, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _agora;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

// Guarda as chamadas em vez de falar com o Firebase.
internal class NotificacaoFalsa : INotificacaoService
{
    public List<Evento> Chamadas { get; } = new();

    public Task NotificarQuedaAsync(Evento evento)
    {
        Chamadas.Add(evento);
        return Task.CompletedTask;
    }
}

// SQLite em memória com uma piscina; vive enquanto a conexão estiver aberta.
internal class BancoDeTeste : IDisposable
{
    private readonly SqliteConnection _conexao;
    public AppDb Db { get; }

    public BancoDeTeste()
    {
        _conexao = new SqliteConnection("Data Source=:memory:");
        _conexao.Open();

        var opcoes = new DbContextOptionsBuilder<AppDb>().UseSqlite(_conexao).Options;
        Db = new AppDb(opcoes);
        Db.Database.EnsureCreated();
        Db.Piscinas.Add(new Piscina { Nome = "Piscina de teste" });
        Db.SaveChanges();
    }

    public void Dispose()
    {
        Db.Dispose();
        _conexao.Dispose();
    }
}
