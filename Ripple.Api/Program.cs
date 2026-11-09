using Microsoft.EntityFrameworkCore;
using Ripple.Api.Data;
using Ripple.Api.Dtos;
using Ripple.Api.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o => o.UseSqlite("Data Source=ripple.db"));

var app = builder.Build();

// Aplica as migrations e cria os dados de exemplo se o banco estiver vazio.
// Exige que você já tenha rodado Add-Migration Inicial.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.Migrate();
    await DbSeeder.SeedAsync(db);
}

var fuso = ObterFuso();
string[] diasSemana = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

// Histórico: GET /api/eventos?periodo=week|month
app.MapGet("/api/eventos", async (string? periodo, AppDb db) =>
{
    var semana = periodo != "month";

    // Os períodos são contados em horário local (Brasil); o banco guarda UTC.
    var hojeLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso).Date;
    var inicioLocal = DateTime.SpecifyKind(
        semana ? hojeLocal.AddDays(-6) : hojeLocal.AddDays(-27), DateTimeKind.Unspecified);
    var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(inicioLocal, fuso);

    var piscinaId = await db.Piscinas.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();

    var lista = await db.Eventos
        .Where(e => e.PiscinaId == piscinaId && e.DataHora >= inicioUtc)
        .OrderByDescending(e => e.DataHora)
        .ToListAsync();

    // semana: um balde por dia (7); mês: um balde por bloco de 7 dias (4)
    var baldes = semana ? 7 : 4;
    var values = new int[baldes];
    var events = new List<EventoResponse>();

    foreach (var e in lista)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(e.DataHora, DateTimeKind.Utc), fuso);

        var dias = (local.Date - inicioLocal).Days;
        var i = semana ? dias : dias / 7;
        if (i >= 0 && i < baldes) values[i]++;

        events.Add(new EventoResponse(Titulo(e.Tipo), $"{diasSemana[(int)local.DayOfWeek]}, {local:HH:mm}", ParaJs(e.Tipo)));
    }

    var labels = semana
        ? Enumerable.Range(0, 7).Select(d => diasSemana[(int)inicioLocal.AddDays(d).DayOfWeek]).ToArray()
        : new[] { "Sem 1", "Sem 2", "Sem 3", "Sem 4" };

    return Results.Ok(new HistoricoResponse(labels, values, events));
});

// Registrar evento: POST /api/eventos  com { "tipo": "queda" }
app.MapPost("/api/eventos", async (NovoEventoRequest dto, AppDb db) =>
{
    TipoEvento? tipo = dto.Tipo?.ToLowerInvariant() switch
    {
        "queda" => TipoEvento.Queda,
        "off" or "descartado" => TipoEvento.Descartado,
        "test" or "teste" => TipoEvento.Teste,
        _ => null
    };

    if (tipo is null)
        return Results.BadRequest(new { erro = "tipo inválido" });

    var piscinaId = await db.Piscinas.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();

    db.Eventos.Add(new Evento
    {
        PiscinaId = piscinaId,
        Tipo = tipo.Value,
        DataHora = DateTime.UtcNow,
        // Até existir o sensor real, tudo que chega por aqui é simulação.
        Simulado = dto.Simulado ?? true
    });
    await db.SaveChangesAsync();

    return Results.Created("/api/eventos", new { ok = true });
});

app.Run();

// ---------- funções auxiliares ----------

static TimeZoneInfo ObterFuso()
{
    foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { }
    }
    return TimeZoneInfo.Local;
}

// Tradução entre o enum e o texto que o index.html espera ("queda", "off", "test").
static string ParaJs(TipoEvento t) => t switch
{
    TipoEvento.Queda => "queda",
    TipoEvento.Descartado => "off",
    _ => "test"
};

static string Titulo(TipoEvento t) => t switch
{
    TipoEvento.Queda => "Queda detectada",
    TipoEvento.Descartado => "Ondulação leve descartada",
    _ => "Teste de alerta"
};
