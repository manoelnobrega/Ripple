namespace Ripple.Api.Entities;

public class Evento
{
    public int Id { get; set; }

    public int PiscinaId { get; set; }
    public Piscina Piscina { get; set; } = null!;

    public TipoEvento Tipo { get; set; }
    public DateTime DataHora { get; set; } = DateTime.UtcNow;
    public bool Simulado { get; set; }
}
