using Microsoft.EntityFrameworkCore;
using Ripple.Api.Data;
using Ripple.Api.Dtos;
using Ripple.Api.Entities;

namespace Ripple.Api.Services
{
    public class ResponsavelService : IResponsavelService
    {
        public const int MaximoPorPiscina = 5;

        private readonly AppDb _db;

        public ResponsavelService(AppDb db) => _db = db;

        public async Task<List<ResponsavelResponse>> ListarAsync()
        {
            var lista = await _db.Responsaveis.OrderBy(r => r.Id).ToListAsync();
            return lista.Select(ParaResponse).ToList();
        }

        public async Task<ResponsavelResponse> CadastrarAsync(NovoResponsavelRequest req)
        {
            var nome = req.Nome?.Trim();
            if (string.IsNullOrEmpty(nome))
                throw new ArgumentException("nome obrigatório");
            if (nome.Length > 80)
                throw new ArgumentException("nome deve ter até 80 caracteres");

            var token = string.IsNullOrWhiteSpace(req.TokenPush) ? null : req.TokenPush.Trim();

            // O mesmo aparelho (mesmo token) não vira dois responsáveis.
            if (token is not null)
            {
                var existente = await _db.Responsaveis.FirstOrDefaultAsync(r => r.TokenPush == token);
                if (existente is not null)
                {
                    existente.Nome = nome;
                    await _db.SaveChangesAsync();
                    return ParaResponse(existente);
                }
            }

            var piscinaId = await _db.Piscinas.OrderBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefaultAsync()
                            ?? throw new InvalidOperationException("Nenhuma piscina cadastrada.");

            var total = await _db.Responsaveis.CountAsync(r => r.PiscinaId == piscinaId);
            if (total >= MaximoPorPiscina)
                throw new ArgumentException($"limite de {MaximoPorPiscina} responsáveis por piscina");

            var novo = new Responsavel { PiscinaId = piscinaId, Nome = nome };
            if (token is not null) novo.AtualizarToken(token);

            _db.Responsaveis.Add(novo);
            await _db.SaveChangesAsync();
            return ParaResponse(novo);
        }

        public async Task<bool> AtualizarTokenAsync(int id, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("token obrigatório");
            token = token.Trim();

            var responsavel = await _db.Responsaveis.FirstOrDefaultAsync(r => r.Id == id);
            if (responsavel is null) return false;

            // O token é único. Se estava com outro responsável (aparelho trocou de dono), libera-o primeiro,
            // em um SaveChanges separado, para não violar o índice único.
            var outros = await _db.Responsaveis.Where(r => r.TokenPush == token && r.Id != id).ToListAsync();
            if (outros.Count > 0)
            {
                foreach (var outro in outros) outro.TokenPush = null;
                await _db.SaveChangesAsync();
            }

            responsavel.AtualizarToken(token);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoverAsync(int id)
        {
            var responsavel = await _db.Responsaveis.FirstOrDefaultAsync(r => r.Id == id);
            if (responsavel is null) return false;

            _db.Responsaveis.Remove(responsavel);
            await _db.SaveChangesAsync();
            return true;
        }

        private static ResponsavelResponse ParaResponse(Responsavel r) =>
            new(r.Id, r.Nome, r.NotificacaoAtiva, !string.IsNullOrWhiteSpace(r.TokenPush));
    }
}
