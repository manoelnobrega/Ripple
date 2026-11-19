using Ripple.Api.Dtos;

namespace Ripple.Api.Services
{
    public interface IResponsavelService
    {
        // Inicia a lista de responsaveis
        Task<List<ResponsavelResponse>> ListarAsync();

        // Cadastra um novo responsável, se o token já existe ele atualiza o nome
        Task<ResponsavelResponse> CadastrarAsync(NovoResponsavelRequest req);

        // Atualiza o token, false se o id não existir
        Task<bool> AtualizarTokenAsync(int id, string token);

        // Remove o responsavel, false de o id não existir
        Task<bool> RemoverAsync(int id);
    }
}
