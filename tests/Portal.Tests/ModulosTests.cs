using Microsoft.Extensions.DependencyInjection;
using Portal.Core.Modulos;
using Portal.Core.Perfil;
using Portal.Modules.Anuncios;
using Portal.Modules.Apoios;

namespace Portal.Tests;

public class ModulosTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);

    private static ContextoUtilizador Contexto(PerfilUtilizador perfil)
    {
        var servicos = new ServiceCollection();
        new ModuloApoios().RegistarServicos(servicos, null!);
        return new ContextoUtilizador("u1", perfil, Hoje, servicos.BuildServiceProvider());
    }

    [Fact]
    public void Indexantes_2026_estao_embebidos_e_completos()
    {
        var indexantes = new IndexantesEmbebidos().Obter(2026);

        Assert.NotNull(indexantes);
        Assert.Equal(537.13m, indexantes[ChavesIndexantes.Ias]);
        Assert.Equal(43090m, indexantes[ChavesIndexantes.LimiteEscalao6Irs]);
        Assert.Equal(450000m, indexantes[ChavesIndexantes.GarantiaPublicaValorMaximo]);
        Assert.All(indexantes.Valores.Values, v => Assert.StartsWith("https://", v.Fonte));
    }

    [Fact]
    public void Indexantes_de_ano_sem_ficheiro_devolve_null()
    {
        Assert.Null(new IndexantesEmbebidos().Obter(1999));
    }

    [Fact]
    public async Task Cartao_apoios_com_perfil_vazio_pede_os_campos_em_falta()
    {
        var modulo = new ModuloApoios();

        var cartao = await modulo.ObterCartaoAsync(Contexto(new PerfilUtilizador()), default);

        Assert.Equal("apoios", cartao.ModuloId);
        Assert.Equal(modulo.CamposPerfil.Count, cartao.CamposPerfilEmFalta.Count);
        Assert.DoesNotContain(cartao.Itens, i => i.Texto.Contains("IRS Jovem"));
    }

    [Theory]
    [InlineData("1995-03-10", true)]  // 31 anos
    [InlineData("1990-01-01", false)] // 36 anos
    public async Task Cartao_apoios_mostra_idade_jovem_ate_35(string nascimento, bool esperaItemJovem)
    {
        var perfil = new PerfilUtilizador { DataNascimento = DateOnly.Parse(nascimento) };

        var cartao = await new ModuloApoios().ObterCartaoAsync(Contexto(perfil), default);

        Assert.Equal(esperaItemJovem, cartao.Itens.Any(i => i.Texto.Contains("IRS Jovem")));
    }

    [Fact]
    public async Task Cartao_anuncios_resume_a_procura()
    {
        var perfil = new PerfilUtilizador { ProcuraComprarCasa = true, OrcamentoCompra = 300000, Concelho = "Sintra" };

        var cartao = await new ModuloAnuncios().ObterCartaoAsync(Contexto(perfil), default);

        Assert.Empty(cartao.CamposPerfilEmFalta);
        var item = Assert.Single(cartao.Itens);
        Assert.Contains("Sintra", item.Texto);
        Assert.Contains("300", item.Texto);
    }
}
