using System.Net;
using System.Net.Http.Json;
using Portal.Core.Perfil;
using Portal.Modules.Anuncios;
using Portal.Modules.Anuncios.Mercado;

namespace Portal.Tests;

public class LinksPortaisTests
{
    [Theory]
    [InlineData("Vila Nova de Gaia", "vila-nova-de-gaia")]
    [InlineData("Setúbal", "setubal")]
    [InlineData("  Póvoa de Varzim ", "povoa-de-varzim")]
    [InlineData("Santa Maria da Feira", "santa-maria-da-feira")]
    public void Slug(string texto, string esperado) => Assert.Equal(esperado, LinksPortais.Slug(texto));

    [Fact]
    public void Imovirtual_usa_o_formato_verificado()
    {
        var p = new PesquisaGuardada { Negocio = Negocio.Comprar, Tipo = TipoImovel.Terreno, Distrito = "Lisboa", Concelho = "Sintra", PrecoMaximo = 90000 };

        var link = LinksPortais.Para(p).Single(l => l.Portal == "Imovirtual");

        Assert.True(link.Verificado);
        Assert.Equal("https://www.imovirtual.com/pt/resultados/comprar/terreno/lisboa/sintra?priceMax=90000", link.Url);
    }

    [Fact]
    public void Gera_um_link_por_portal()
    {
        var p = new PesquisaGuardada { Negocio = Negocio.Arrendar, Tipo = TipoImovel.Apartamento, Distrito = "Porto", Concelho = "Valongo" };

        var links = LinksPortais.Para(p);

        Assert.Equal(["Imovirtual", "Idealista", "Casa Yes", "Casa Sapo", "Supercasa"], links.Select(l => l.Portal));
        Assert.All(links, l => Assert.Contains("valongo", l.Url));
    }

    [Theory]
    [InlineData("https://www.idealista.pt/imovel/12345/", "Idealista")]
    [InlineData("https://www.imovirtual.com/pt/anuncio/t2-ID1abc", "Imovirtual")]
    [InlineData("https://www.remax.pt/imoveis/123", "remax.pt")]
    public void Portal_de(string url, string esperado) => Assert.Equal(esperado, LinksPortais.PortalDe(url));
}

public class MercadoIneTests
{
    // Extrato real da resposta do INE (indicador 0012234, 1.º trimestre de 2026).
    private const string Resposta = """
        [{"IndicadorCod":"0012234","Dados":{"S5A20261":[
          {"geocod":"PT","geodsg":"Portugal","dim_3":"H1","valor":"2168"},
          {"geocod":"1A01106","geodsg":"Lisboa","dim_3":"H1","valor":"5082"},
          {"geocod":"1A01106","geodsg":"Lisboa","dim_3":"H11","valor":"6226"},
          {"geocod":"1A01106","geodsg":"Lisboa","dim_3":"H12","valor":"4896"},
          {"geocod":"1A0110656","geodsg":"Arroios","dim_3":"H1","valor":"4776"},
          {"geocod":"1120303","geodsg":"Braga","dim_3":"H1","valor":"2100"},
          {"geocod":"1120303","geodsg":"Braga","dim_3":"H11","valor":"x"}
        ]}}]
        """;

    [Fact]
    public void Interpreta_so_concelhos_e_ignora_valores_confidenciais()
    {
        var tabela = MercadoIne.Interpretar(Resposta)!;

        Assert.Equal(["lisboa", "braga"], tabela.Keys);
        Assert.Equal(new MedianaConcelho("Lisboa", "S5A20261", 5082, 6226, 4896), tabela["lisboa"]);
        Assert.Null(tabela["braga"].Novos);
    }

    [Fact]
    public void Periodo_inexistente_devolve_null()
    {
        const string erro = """[{"Sucesso":{"Falso":[{"Msg":"Código não válido"}]}}]""";

        Assert.Null(MercadoIne.Interpretar(erro));
    }
}

public class AnunciosApiTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    private async Task<HttpClient> ClienteAsync() => await ApiTests.ClienteAutenticadoAsync(factory);

    [Fact]
    public async Task Pesquisa_guardada_devolve_links_e_pode_ser_apagada()
    {
        var cliente = await ClienteAsync();

        var criada = await cliente.PostAsJsonAsync("/api/anuncios/pesquisas",
            new NovaPesquisa(null, Negocio.Comprar, TipoImovel.Apartamento, "Porto", "Valongo", 300000, 2));
        criada.EnsureSuccessStatusCode();
        var corpo = await criada.Content.ReadFromJsonAsync<PesquisaComLinks>();

        Assert.Equal("Apartamentos à venda em Valongo até 300 000 €", corpo!.Pesquisa.Nome.Replace(' ', ' ').Replace(' ', ' '));
        Assert.Equal(5, corpo.Links.Count);

        var apagar = await cliente.DeleteAsync($"/api/anuncios/pesquisas/{corpo.Pesquisa.Id}");
        Assert.Equal(HttpStatusCode.NoContent, apagar.StatusCode);
        Assert.Empty((await cliente.GetFromJsonAsync<List<PesquisaComLinks>>("/api/anuncios/pesquisas"))!);
    }

    [Fact]
    public async Task Favorito_regista_historico_de_precos_e_compara_com_o_ine()
    {
        var cliente = await ClienteAsync();
        await cliente.PutAsJsonAsync("/api/perfil", new PerfilUtilizador { DataNascimento = new DateOnly(1996, 1, 1) });

        var resposta = await cliente.PostAsJsonAsync("/api/anuncios/favoritos",
            new NovoFavorito("https://www.imovirtual.com/pt/anuncio/t2-lisboa", "T2 Arroios", TipoImovel.Apartamento, "T2", 60, "Lisboa", 320000, null));
        resposta.EnsureSuccessStatusCode();
        var favorito = (await resposta.Content.ReadFromJsonAsync<FavoritoVista>())!;

        Assert.Equal("Imovirtual", favorito.Portal);
        Assert.Equal(5333, favorito.EurM2);                  // 320 000 / 60
        Assert.Equal(4.9m, favorito.DiferencaMedianaPercent); // vs 5 082 €/m² (INE falso)
        Assert.Contains("Garantia pública", favorito.Etiquetas);
        Assert.Contains("IMT Jovem: isenção total", favorito.Etiquetas);

        // O mesmo dia substitui o registo em vez de acumular.
        await cliente.PostAsJsonAsync($"/api/anuncios/favoritos/{favorito.Id}/precos", new NovoPreco(305000));
        var lista = (await cliente.GetFromJsonAsync<List<FavoritoVista>>("/api/anuncios/favoritos"))!;
        var atualizado = Assert.Single(lista);
        Assert.Equal(305000, atualizado.PrecoAtual);
        Assert.Single(atualizado.Precos);
    }

    [Fact]
    public async Task Favorito_rejeita_link_invalido()
    {
        var cliente = await ClienteAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/anuncios/favoritos",
            new NovoFavorito("idealista T2", null, TipoImovel.Apartamento, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Nao_se_ve_nem_altera_favoritos_de_outro_utilizador()
    {
        var ana = await ClienteAsync();
        var rui = await ClienteAsync();
        var r = await ana.PostAsJsonAsync("/api/anuncios/favoritos",
            new NovoFavorito("https://www.idealista.pt/imovel/1/", "Casa da Ana", TipoImovel.Moradia, null, null, null, 250000, null));
        var favorito = (await r.Content.ReadFromJsonAsync<FavoritoVista>())!;

        Assert.Empty((await rui.GetFromJsonAsync<List<FavoritoVista>>("/api/anuncios/favoritos"))!);
        var alterar = await rui.PostAsJsonAsync($"/api/anuncios/favoritos/{favorito.Id}/precos", new NovoPreco(1));
        Assert.Equal(HttpStatusCode.NotFound, alterar.StatusCode);
    }

    [Fact]
    public async Task Apoios_e_prazos_respondem_para_perfil_vazio()
    {
        var cliente = await ClienteAsync();

        var apoios = await cliente.GetFromJsonAsync<List<ApoioResposta>>("/api/apoios");
        var ics = await cliente.GetAsync("/api/apoios/prazos.ics");

        Assert.Equal(7, apoios!.Count);
        Assert.Equal("text/calendar", ics.Content.Headers.ContentType?.MediaType);
    }

    private sealed record PesquisaComLinks(PesquisaGuardada Pesquisa, List<LinkPortal> Links);

    private sealed record ApoioResposta(string Estado);
}

/// <summary>Substitui a API do INE nos testes: dados fixos, sem rede.</summary>
public sealed class MercadoFalso : IMercadoImobiliario
{
    public Task<MedianaConcelho?> MedianaVendasAsync(string concelho, CancellationToken ct) =>
        Task.FromResult(concelho.Equals("Lisboa", StringComparison.OrdinalIgnoreCase)
            ? new MedianaConcelho("Lisboa", "S5A20261", 5082, 6226, 4896)
            : null);
}
