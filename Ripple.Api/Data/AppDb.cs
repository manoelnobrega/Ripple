using Microsoft.EntityFrameworkCore;
using Ripple.Api.Entities;

namespace Ripple.Api.Data;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }

    public DbSet<Evento> Eventos => Set<Evento>();
}
