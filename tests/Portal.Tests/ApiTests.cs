using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Portal.Core.Modulos;
using Portal.Core.Perfil;

namespace Portal.Tests;

/// <summary>Arranca a API real com uma base SQLite temporária.</summary>
public sealed class PortalFactory : WebApplicationFactory<Program>
{
    private readonly string _ficheiro = Path.Combine(Path.GetTempPath(), $"portal-teste-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("BaseDeDados:Fornecedor", "Sqlite");
        builder.UseSetting("ConnectionStrings:Portal", $"Data Source={_ficheiro};Pooling=False");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(_ficheiro);
    }
}

public class ApiTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    private async Task<HttpClient> ClienteAutenticadoAsync()
    {
        var cliente = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@exemplo.pt";
        const string password = "Palavra-Passe-1";

        (await cliente.PostAsJsonAsync("/api/auth/register", new { email, password })).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password })).EnsureSuccessStatusCode();
        return cliente;
    }

    [Fact]
    public async Task Painel_sem_sessao_devolve_401()
    {
        var resposta = await factory.CreateClient().GetAsync("/api/painel");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Utilizador_novo_ve_todas_as_seccoes()
    {
        var cliente = await ClienteAutenticadoAsync();

        var painel = await cliente.GetFromJsonAsync<List<CartaoPainel>>("/api/painel");

        Assert.Equal(["apoios", "anuncios"], painel!.Select(c => c.ModuloId));
    }

    [Fact]
    public async Task Preferencias_definem_quais_seccoes_aparecem_e_a_ordem()
    {
        var cliente = await ClienteAutenticadoAsync();

        var put = await cliente.PutAsJsonAsync("/api/preferencias", new PreferenciasUtilizador { SeccoesAtivas = ["anuncios", "apoios"] });
        put.EnsureSuccessStatusCode();
        var painel = await cliente.GetFromJsonAsync<List<CartaoPainel>>("/api/painel");
        Assert.Equal(["anuncios", "apoios"], painel!.Select(c => c.ModuloId));

        await cliente.PutAsJsonAsync("/api/preferencias", new PreferenciasUtilizador { SeccoesAtivas = ["apoios"] });
        painel = await cliente.GetFromJsonAsync<List<CartaoPainel>>("/api/painel");
        Assert.Equal(["apoios"], painel!.Select(c => c.ModuloId));
    }

    [Fact]
    public async Task Preferencias_rejeitam_seccao_desconhecida()
    {
        var cliente = await ClienteAutenticadoAsync();

        var resposta = await cliente.PutAsJsonAsync("/api/preferencias", new PreferenciasUtilizador { SeccoesAtivas = ["nao-existe"] });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Perfil_e_guardado_e_usado_no_painel()
    {
        var cliente = await ClienteAutenticadoAsync();
        var perfil = new PerfilUtilizador
        {
            DataNascimento = new DateOnly(1996, 5, 20),
            Concelho = "Lisboa",
            ProcuraComprarCasa = true,
            OrcamentoCompra = 280000,
            IdadesFilhos = [3],
        };

        (await cliente.PutAsJsonAsync("/api/perfil", perfil)).EnsureSuccessStatusCode();
        var guardado = await cliente.GetFromJsonAsync<PerfilUtilizador>("/api/perfil");
        var painel = await cliente.GetFromJsonAsync<List<CartaoPainel>>("/api/painel");

        Assert.Equal("Lisboa", guardado!.Concelho);
        Assert.Equal([3], guardado.IdadesFilhos);
        var anuncios = painel!.Single(c => c.ModuloId == "anuncios");
        Assert.Empty(anuncios.CamposPerfilEmFalta);
    }

    [Fact]
    public async Task Cada_utilizador_so_ve_o_proprio_perfil()
    {
        var ana = await ClienteAutenticadoAsync();
        var rui = await ClienteAutenticadoAsync();

        await ana.PutAsJsonAsync("/api/perfil", new PerfilUtilizador { Concelho = "Porto" });
        var perfilRui = await rui.GetFromJsonAsync<PerfilUtilizador>("/api/perfil");

        Assert.Null(perfilRui!.Concelho);
    }
}
