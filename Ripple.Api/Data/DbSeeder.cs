using Microsoft.EntityFrameworkCore;
using Ripple.Api.Entities;

namespace Ripple.Api.Data
{
    // Dados de exemplo para o protótipo
    public class DbSeeder
    {
        public static async Task SeedAsync(AppDb db)
        {
            if (await db.Piscinas.AnyAsync()) return;

            var piscina = new Piscina { Nome = "Piscina do quintal" };

            piscina.Responsaveis.Add(new Responsavel { Nome = "Responsável 1" });
            piscina.Responsaveis.Add(new Responsavel { Nome = "Responsável 2" });

            var agora = DateTime.UtcNow;
            piscina.Eventos.Add(new Evento { Tipo = TipoEvento.Descartado, DataHora = agora.AddDays(-2), Simulado = true });
            piscina.Eventos.Add(new Evento { Tipo = TipoEvento.Teste, DataHora = agora.AddDays(-4), Simulado = true });
            piscina.Eventos.Add(new Evento { Tipo = TipoEvento.Teste, DataHora = agora.AddDays(-6), Simulado = true });

            db.Piscinas.Add(piscina);
            await db.SaveChangesAsync();
        }
    }
}
