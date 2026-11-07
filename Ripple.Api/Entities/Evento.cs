namespace Ripple.Api.Entities;

public class Evento
{
    public int Id { get; set; }

    /// <summary>"queda", "off" (descartado) ou "test" (teste).</summary>
    public string Tipo { get; set; } = "test";

    public DateTime Data { get; set; }
}
