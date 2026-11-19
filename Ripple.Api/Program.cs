using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Ripple.Api.Data;
using Ripple.Api.Endpoints;
using Ripple.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o => o.UseSqlite("Data Source=ripple.db"));
builder.Services.AddSingleton<TimeProvider, RelogioBrasil>();
builder.Services.AddScoped<IEventoService, EventoService>();
builder.Services.AddScoped<IResponsavelService, ResponsavelService>();
builder.Services.AddScoped<INotificacaoService, NotificacaoService>();

// Firebase: a chave da conta de serviço fica fora do Git (veja o .gitignore).
// Sem a chave a API funciona normalmente, só não envia notificações.
var caminhoChave = Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["Firebase:CredentialPath"] ?? "firebase-service-account.json");

if (File.Exists(caminhoChave))
{
    FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(caminhoChave) });
}
else
{
    Console.WriteLine($"AVISO: chave do Firebase não encontrada em {caminhoChave}. Notificações desativadas.");
}

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
app.MapResponsavelEndpoints();

app.Run();