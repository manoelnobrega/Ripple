using System.Reflection.Metadata.Ecma335;

namespace Ripple.Api.Entities
{
    public class Responsavel
    {
        public int Id { get; set; }

        public int PiscinaId { get; set; }
        public Piscina Piscina { get; set; } = null;

        public string Nome { get; set; } = "";

        // "Notificação ativa" na tela. Só quem ta ativo e tem token pode receber o push.
        public bool NotificacaoAtiva { get; set; } = true;

        // Token do firebase do aparelho. Nulo até o app registrar.
        public string? TokenPush { get; set; }
        public DateTime? TokenAtualizadoEm { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        // Troca o token e a data:
        public void AtualizarToken(string token)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token);
            TokenPush = token;
            TokenAtualizadoEm = DateTime.UtcNow;
        }

        public void Desativar() => NotificacaoAtiva = false;

        public bool PodeReceberPush => NotificacaoAtiva && !string.IsNullOrWhiteSpace(TokenPush);

    }
}
