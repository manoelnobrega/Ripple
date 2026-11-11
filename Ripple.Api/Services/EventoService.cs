using Microsoft.EntityFrameworkCore;
using Ripple.Api.Data;
using Ripple.Api.Dtos;
using Ripple.Api.Entities;

namespace Ripple.Api.Services;

public class EventoService : IEventoService
{
    private static readonly string[] DiasSemana = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

    private readonly AppDb _db;
    private readonly TimeProvider _relogio;

    public EventoService(AppDb db, TimeProvider relogio)
    {
        _db = db;
        _relogio = relogio;
    }

    public async Task<Evento> RegistrarAsync(NovoEventoRequest req)
    {
        var tipo = ConverterTipo(req.Tipo)
                   ?? throw new ArgumentException("tipo inválido (use queda, off ou test)");

        var evento = new Evento
        {
            PiscinaId = await ObterPiscinaIdAsync(),
            Tipo = tipo,
            DataHora = _relogio.GetUtcNow().UtcDateTime,
            // Até existir o sensor real, tudo que chega por aqui é simulação.
            Simulado = req.Simulado ?? true
        };

        _db.Eventos.Add(evento);
        await _db.SaveChangesAsync();
        return evento;
    }

    public async Task<HistoricoResponse> ObterHistoricoAsync(string? periodo)
    {
        var semana = periodo != "month";
        var fuso = _relogio.LocalTimeZone;

        // Os períodos são contados em horário local; o banco guarda UTC.
        var agoraUtc = _relogio.GetUtcNow().UtcDateTime;
        var hojeLocal = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, fuso).Date;
        var inicioLocal = DateTime.SpecifyKind(
            semana ? hojeLocal.AddDays(-6) : hojeLocal.AddDays(-27), DateTimeKind.Unspecified);
        var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(inicioLocal, fuso);

        var piscinaId = await ObterPiscinaIdAsync();

        var lista = await _db.Eventos
            .Where(e => e.PiscinaId == piscinaId && e.DataHora >= inicioUtc)
            .OrderByDescending(e => e.DataHora)
            .ToListAsync();

        // semana: um balde por dia (7); mês: um balde por bloco de 7 dias (4)
        var baldes = semana ? 7 : 4;
        var values = new int[baldes];
        var events = new List<EventoResponse>();

        foreach (var e in lista)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(e.DataHora, DateTimeKind.Utc), fuso);

            var dias = (local.Date - inicioLocal).Days;
            var i = semana ? dias : dias / 7;
            if (i >= 0 && i < baldes) values[i]++;

            events.Add(new EventoResponse(
                Titulo(e.Tipo),
                $"{DiasSemana[(int)local.DayOfWeek]}, {local:HH:mm}",
                ParaJs(e.Tipo)));
        }

        var labels = semana
            ? Enumerable.Range(0, 7).Select(d => DiasSemana[(int)inicioLocal.AddDays(d).DayOfWeek]).ToArray()
            : new[] { "Sem 1", "Sem 2", "Sem 3", "Sem 4" };

        return new HistoricoResponse(labels, values, events);
    }

    // ---------- auxiliares ----------

    private async Task<int> ObterPiscinaIdAsync()
    {
        // No protótipo há uma só piscina.
        var id = await _db.Piscinas.OrderBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefaultAsync();
        return id ?? throw new InvalidOperationException("Nenhuma piscina cadastrada.");
    }

    private static TipoEvento? ConverterTipo(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "queda" => TipoEvento.Queda,
        "off" or "descartado" => TipoEvento.Descartado,
        "test" or "teste" => TipoEvento.Teste,
        _ => null
    };

    // Tradução do enum para o texto que o index.html espera ("queda", "off", "test").
    private static string ParaJs(TipoEvento t) => t switch
    {
        TipoEvento.Queda => "queda",
        TipoEvento.Descartado => "off",
        _ => "test"
    };

    private static string Titulo(TipoEvento t) => t switch
    {
        TipoEvento.Queda => "Queda detectada",
        TipoEvento.Descartado => "Ondulação leve descartada",
        _ => "Teste de alerta"
    };
}
