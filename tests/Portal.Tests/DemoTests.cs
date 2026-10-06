using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Portal.Api;
using Portal.Core.Dados;

namespace Portal.Tests;

public class DemoTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    private async Task<HttpClient> EntrarEmDemoAsync()
    {
        var cliente = factory.CreateClient();
        var resposta = await cliente.PostAsync("/api/demo/entrar", null);
        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        return cliente;
    }

    [Fact]
    public async Task Sem_sessao_o_estado_diz_que_a_demonstracao_existe()
    {
        var estado = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/demo");

        Assert.True(estado.GetProperty("ativa").GetBoolean());
        Assert.False(estado.GetProperty("emDemo").GetBoolean());
    }

    [Fact]
    public async Task Entrar_em_demo_abre_uma_sessao_com_dados_de_exemplo_em_todas_as_seccoes()
    {
        var cliente = await EntrarEmDemoAsync();

        var estado = await cliente.GetFromJsonAsync<JsonElement>("/api/demo");
        Assert.True(estado.GetProperty("emDemo").GetBoolean());

        var cartoes = await cliente.GetFromJsonAsync<JsonElement>("/api/painel");
        Assert.Equal(4, cartoes.GetArrayLength());

        var operacoes = await cliente.GetFromJsonAsync<JsonElement>("/api/investimentos/operacoes");
        Assert.Equal(9, operacoes.GetArrayLength());

        // O ano que o cartão do painel mostra tem de ter vendas e dividendos, para o relatório não aparecer vazio.
        var hoje = DateTime.Today;
        var ano = hoje.Month <= 6 ? hoje.Year - 1 : hoje.Year;
        var relatorio = await cliente.GetFromJsonAsync<JsonElement>($"/api/investimentos/relatorio/{ano}");
        Assert.Equal(2, relatorio.GetProperty("maisValias").GetArrayLength());
        Assert.True(relatorio.GetProperty("dividendosBrutos").GetDecimal() > 0);
        Assert.Empty(relatorio.GetProperty("avisos").EnumerateArray());
    }

    [Fact]
    public async Task Entrar_outra_vez_no_mesmo_dia_nao_duplica_os_dados()
    {
        await EntrarEmDemoAsync();
        var cliente = await EntrarEmDemoAsync();

        var operacoes = await cliente.GetFromJsonAsync<JsonElement>("/api/investimentos/operacoes");
        Assert.Equal(9, operacoes.GetArrayLength());
    }

    [Fact]
    public async Task Os_dados_sao_repostos_no_dia_seguinte()
    {
        await EntrarEmDemoAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var contas = scope.ServiceProvider.GetRequiredService<UserManager<Utilizador>>();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();

        var amanha = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        var conta = await Demo.PrepararAsync(contas, db, amanha, DateTimeOffset.Now.AddDays(1), CancellationToken.None);

        var perfil = await db.Perfis.FindAsync(conta.Id);
        Assert.Equal(amanha, DateOnly.FromDateTime(perfil!.AtualizadoEm.LocalDateTime));
        Assert.Equal(9, db.Set<Portal.Modules.Investimentos.Operacao>().Count(o => o.UtilizadorId == conta.Id));
    }

    [Fact]
    public async Task A_conta_de_demonstracao_nao_altera_dados()
    {
        var cliente = await EntrarEmDemoAsync();

        var perfil = await cliente.PutAsJsonAsync("/api/perfil", new { nome = "Outra pessoa" });
        Assert.Equal(HttpStatusCode.Forbidden, perfil.StatusCode);

        var palavraPasse = await cliente.PostAsJsonAsync("/api/auth/manage/info", new { newPassword = "Nova-Palavra-1", oldPassword = "" });
        Assert.Equal(HttpStatusCode.Forbidden, palavraPasse.StatusCode);

        var sair = await cliente.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, sair.StatusCode);
    }

    [Fact]
    public async Task Ninguem_entra_na_conta_de_demonstracao_pelo_login_normal()
    {
        await EntrarEmDemoAsync();

        var resposta = await factory.CreateClient().PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email = Demo.Email, password = "Qualquer-Coisa-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
