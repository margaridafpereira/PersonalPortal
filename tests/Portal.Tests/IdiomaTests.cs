using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Portal.Core;

namespace Portal.Tests;

public class IdiomaTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    private static async Task<string> PainelAsync(HttpClient cliente, string? idioma)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/painel");
        if (idioma is not null)
            pedido.Headers.Add("X-Idioma", idioma);
        var resposta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Sem_cabecalho_os_textos_sao_em_portugues()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);

        var painel = await PainelAsync(cliente, null);

        Assert.Contains("Radar de apoios e prazos", painel);
        Assert.DoesNotContain("Benefits and deadlines", painel);
    }

    [Fact]
    public async Task Com_X_Idioma_en_os_textos_sao_em_ingles()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);

        var painel = await PainelAsync(cliente, "en");

        Assert.Contains("Benefits and deadlines", painel);
        Assert.Contains("Property radar", painel);
        Assert.DoesNotContain("Radar de apoios e prazos", painel);
    }

    [Fact]
    public async Task O_parametro_idioma_tambem_escolhe_a_lingua()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);

        var apoios = await cliente.GetFromJsonAsync<JsonElement>("/api/apoios?idioma=en");

        Assert.Contains(apoios.EnumerateArray(), a => a.GetProperty("apoio").GetProperty("nome").GetString() == "Public mortgage guarantee");
    }

    [Fact]
    public async Task Um_pedido_em_ingles_nao_muda_a_lingua_do_seguinte()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);

        await PainelAsync(cliente, "en");
        var depois = await PainelAsync(cliente, null);

        Assert.Contains("Radar de apoios e prazos", depois);
    }

    [Fact]
    public void Fora_de_um_pedido_a_lingua_e_portugues()
    {
        Assert.False(Idioma.EmIngles);
        Assert.Equal("olá", Idioma.T("olá", "hello"));
    }
}
