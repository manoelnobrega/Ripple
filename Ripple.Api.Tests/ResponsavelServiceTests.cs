using Microsoft.EntityFrameworkCore;
using Ripple.Api.Dtos;
using Ripple.Api.Services;
using Xunit;

namespace Ripple.Api.Tests;

public class ResponsavelServiceTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();
    private readonly ResponsavelService _servico;

    public ResponsavelServiceTests() => _servico = new ResponsavelService(_banco.Db);

    public void Dispose() => _banco.Dispose();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Cadastrar_NomeVazio_Lanca(string? nome)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _servico.CadastrarAsync(new NovoResponsavelRequest(nome!, null)));
    }

    [Fact]
    public async Task Cadastrar_SemToken_NaoPodeReceberPush()
    {
        var r = await _servico.CadastrarAsync(new NovoResponsavelRequest("Ana", null));

        Assert.Equal("Ana", r.Nome);
        Assert.False(r.TemToken);
        Assert.True(r.NotificacaoAtiva);
    }

    [Fact]
    public async Task Cadastrar_ComToken_GuardaToken()
    {
        var r = await _servico.CadastrarAsync(new NovoResponsavelRequest("Ana", "tok-1"));

        Assert.True(r.TemToken);
        Assert.Equal("tok-1", (await _banco.Db.Responsaveis.SingleAsync()).TokenPush);
    }

    [Fact]
    public async Task Cadastrar_MesmoToken_NaoDuplicaEAtualizaNome()
    {
        var a = await _servico.CadastrarAsync(new NovoResponsavelRequest("Ana", "tok-1"));
        var b = await _servico.CadastrarAsync(new NovoResponsavelRequest("Pixel 7", "tok-1"));

        Assert.Equal(a.Id, b.Id);
        Assert.Equal("Pixel 7", b.Nome);
        Assert.Equal(1, await _banco.Db.Responsaveis.CountAsync());
    }

    [Fact]
    public async Task Cadastrar_AlemDoLimite_Lanca()
    {
        for (var i = 0; i < ResponsavelService.MaximoPorPiscina; i++)
            await _servico.CadastrarAsync(new NovoResponsavelRequest($"R{i}", $"tok-{i}"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _servico.CadastrarAsync(new NovoResponsavelRequest("Extra", "tok-x")));
    }

    [Fact]
    public async Task AtualizarToken_IdInexistente_DevolveFalse()
    {
        Assert.False(await _servico.AtualizarTokenAsync(999, "tok"));
    }

    [Fact]
    public async Task AtualizarToken_TokenVazio_Lanca()
    {
        var r = await _servico.CadastrarAsync(new NovoResponsavelRequest("Ana", null));

        await Assert.ThrowsAsync<ArgumentException>(() => _servico.AtualizarTokenAsync(r.Id, " "));
    }

    [Fact]
    public async Task AtualizarToken_GravaNovoToken()
    {
        var r = await _servico.CadastrarAsync(new NovoResponsavelRequest("Ana", "velho"));

        Assert.True(await _servico.AtualizarTokenAsync(r.Id, "novo"));
        Assert.Equal("novo", (await _banco.Db.Responsaveis.SingleAsync()).TokenPush);
    }

    [Fact]
    public async Task AtualizarToken_TokenQueEstavaComOutro_EhMovido()
    {
        var a = await _servico.CadastrarAsync(new NovoResponsavelRequest("Ana", "tok-a"));
        var b = await _servico.CadastrarAsync(new NovoResponsavelRequest("Bia", "tok-b"));

        Assert.True(await _servico.AtualizarTokenAsync(b.Id, "tok-a"));

        var lista = await _banco.Db.Responsaveis.OrderBy(r => r.Id).ToListAsync();
        Assert.Null(lista.Single(r => r.Id == a.Id).TokenPush);
        Assert.Equal("tok-a", lista.Single(r => r.Id == b.Id).TokenPush);
    }

    [Fact]
    public async Task Remover_Existente_True_Inexistente_False()
    {
        var r = await _servico.CadastrarAsync(new NovoResponsavelRequest("Ana", null));

        Assert.True(await _servico.RemoverAsync(r.Id));
        Assert.False(await _servico.RemoverAsync(r.Id));
        Assert.Equal(0, await _banco.Db.Responsaveis.CountAsync());
    }

    [Fact]
    public async Task Listar_VemOrdenadoPorId()
    {
        await _servico.CadastrarAsync(new NovoResponsavelRequest("Ana", null));
        await _servico.CadastrarAsync(new NovoResponsavelRequest("Bia", null));

        var lista = await _servico.ListarAsync();

        Assert.Equal(new[] { "Ana", "Bia" }, lista.Select(r => r.Nome).ToArray());
    }
}
