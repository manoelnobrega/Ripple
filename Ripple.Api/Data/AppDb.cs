using Microsoft.EntityFrameworkCore;
using Ripple.Api.Entities;

namespace Ripple.Api.Data
{
    public class AppDb : DbContext
    {
        public AppDb(DbContextOptions<AppDb> options) : base(options) { }

        public DbSet<Piscina> Piscinas => Set<Piscina>();
        public DbSet<Evento> Eventos => Set<Evento>();
        public DbSet<Responsavel> Responsaveis => Set<Responsavel>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<Piscina>(e =>
            {
                e.Property(p => p.Nome).IsRequired().HasMaxLength(80);
            });

            mb.Entity<Evento>(e =>
            {
                // Guarda "Queda", "Descartado" ou "Teste" como texto, o que facilita ler a tabela.
                e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);

                // O histórico sempre filtra por piscina e período.
                e.HasIndex(x => new { x.PiscinaId, x.DataHora });

                e.HasOne(x => x.Piscina)
                 .WithMany(p => p.Eventos)
                 .HasForeignKey(x => x.PiscinaId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            mb.Entity<Responsavel>(e =>
            {
                e.Property(x => x.Nome).IsRequired().HasMaxLength(80);

                // Um mesmo token não pode aparecer duas vezes. Vários nulos são permitidos.
                e.HasIndex(x => x.TokenPush).IsUnique();

                e.HasOne(x => x.Piscina)
                 .WithMany(p => p.Responsaveis)
                 .HasForeignKey(x => x.PiscinaId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}