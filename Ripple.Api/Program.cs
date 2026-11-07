using Microsoft.EntityFrameworkCore;
using Ripple.Api.Data;
using Ripple.Api.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o => o.UseSqlite("Data Source=ripple.db"));

var app = builder.Build();

// Aplica as migrations ao iniciar (cria o ripple.db e as tabelas se ainda não existirem).
// Exige que você já tenha rodado Add-Migration Inicial.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.Migrate();
}

string[] diasSemana = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

// Histórico: GET /api/eventos?periodo=week|month
app.MapGet("/api/eventos", async (string? periodo, AppDb db) =>
{
    var semana = periodo != "month";
    var hoje = DateTime.Now.Date;
    var inicio = semana ? hoje.AddDays(-6) : hoje.AddDays(-27);

    var lista = await db.Eventos
        .Where(e => e.Data >= inicio)
        .OrderByDescending(e => e.Data)
        .ToListAsync();

    // semana: um balde por dia (7); mês: um balde por bloco de 7 dias (4)
    var baldes = semana ? 7 : 4;
    var values = new int[baldes];
    foreach (var e in lista)
    {
        var dias = (e.Data.Date - inicio).Days;
        var i = semana ? dias : dias / 7;
        if (i >= 0 && i < baldes) values[i]++;
    }

    var labels = semana
        ? Enumerable.Range(0, 7).Select(d => diasSemana[(int)inicio.AddDays(d).DayOfWeek]).ToArray()
        : new[] { "Sem 1", "Sem 2", "Sem 3", "Sem 4" };

    var events = lista.Select(e => new
    {
        titulo = e.Tipo switch
        {
            "queda" => "Queda detectada",
            "off" => "Ondulação leve descartada",
            _ => "Teste de alerta"
        },
        quando = $"{diasSemana[(int)e.Data.DayOfWeek]}, {e.Data:HH:mm}",
        tipo = e.Tipo
    }).ToList();

    return Results.Ok(new { labels, values, events });
});

// Registrar evento: POST /api/eventos  com { "tipo": "queda" }
app.MapPost("/api/eventos", async (NovoEvento dto, AppDb db) =>
{
    var tipo = dto.Tipo?.ToLowerInvariant();
    if (tipo is not ("queda" or "off" or "test"))
        return Results.BadRequest(new { erro = "tipo inválido" });

    db.Eventos.Add(new Evento { Tipo = tipo, Data = DateTime.Now });
    await db.SaveChangesAsync();
    return Results.Created("/api/eventos", new { ok = true });
});

app.Run();

record NovoEvento(string Tipo);
