using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Portal.Tests;

/// <summary>A API como fica publicada: ambiente de produção, registo só para convidados, avisos disparados por chave.</summary>
public sealed class PortalProducaoFactory : WebApplicationFactory<Program>
{
    public const string ChaveAvisos = "chave-de-teste-com-mais-de-20-caracteres";
    private readonly string _ficheiro = Path.Combine(Path.GetTempPath(), $"portal-producao-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("BaseDeDados:Fornecedor", "Sqlite");
        builder.UseSetting("ConnectionStrings:Portal", $"Data Source={_ficheiro};Pooling=False");
        builder.UseSetting("Registo:EmailsPermitidos:0", "convidada@exemplo.pt");
        builder.UseSetting("Email:ChaveExecucao", ChaveAvisos);
        builder.UseSetting("Email:AvisosAutomaticos", "false");
        builder.UseSetting("Email:Modo", "Pasta");
        builder.UseSetting("Email:Pasta", Path.Combine(Path.GetTempPath(), $"portal-emails-{Guid.NewGuid():N}"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(_ficheiro);
    }
}

public class ProducaoTests(PortalProducaoFactory factory) : IClassFixture<PortalProducaoFactory>
{
    [Fact]
    public async Task So_os_emails_convidados_se_podem_registar()
    {
        var cliente = factory.CreateClient();

        var intruso = await cliente.PostAsJsonAsync("/api/auth/register", new { email = "intruso@exemplo.pt", password = "Palavra-passe-1" });
        var convidada = await cliente.PostAsJsonAsync("/api/auth/register", new { email = "Convidada@Exemplo.pt", password = "Palavra-passe-1" });

        Assert.Equal(HttpStatusCode.Forbidden, intruso.StatusCode);
        Assert.Contains("privado", await intruso.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, convidada.StatusCode);
    }

    [Fact]
    public async Task Avisos_so_correm_com_a_chave_certa()
    {
        var cliente = factory.CreateClient();

        var semChave = await cliente.PostAsync("/api/avisos/executar", null);
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/avisos/executar");
        pedido.Headers.Add("X-Chave-Avisos", PortalProducaoFactory.ChaveAvisos);
        var comChave = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.NotFound, semChave.StatusCode);
        Assert.Equal(HttpStatusCode.OK, comChave.StatusCode);
    }

    [Fact]
    public async Task Saude_responde_sem_sessao()
    {
        var r = await factory.CreateClient().GetAsync("/api/saude");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }
}
