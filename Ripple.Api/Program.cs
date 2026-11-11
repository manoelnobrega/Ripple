using Microsoft.EntityFrameworkCore;
using Ripple.Api.Data;
using Ripple.Api.Endpoints;
using Ripple.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o => o.UseSqlite("Data Source=ripple.db"));
builder.Services.AddSingleton<TimeProvider, RelogioBrasil>();
builder.Services.AddScoped<IEventoService, EventoService>();

var app = builder.Build();

// Aplica as migrations e cria os dados de exemplo se o banco estiver vazio.
// Exige que você já tenha rodado Add-Migration Inicial.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.Migrate();
    await DbSeeder.SeedAsync(db);
}

app.MapEventoEndpoints();

app.Run();