namespace Ripple.Api.Entities
{
    public class Piscina
    {
        public int Id { get; set; }

        // Nome que aparece no subtítulo das telas
        public string Nome { get; set; } = "";

        public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
        
        public List<Evento> Eventos { get; set; } = new List<Evento>();
        public List<Responsavel> Responsaveis { get; set; } = new List<Responsavel>();
    }
}
