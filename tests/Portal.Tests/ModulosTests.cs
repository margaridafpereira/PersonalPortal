using Microsoft.Extensions.DependencyInjection;
using Portal.Core.Modulos;
using Portal.Core.Perfil;
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
        Assert.Empty(cartao.Itens);
    }

    [Fact]
    public async Task Cartao_apoios_conta_os_apoios_provaveis()
    {
        var perfil = new PerfilUtilizador
        {
            DataNascimento = new DateOnly(1998, 4, 1),
            ResidenteFiscal = true,
            Dependente = false,
            CategoriaRendimento = CategoriaRendimento.TrabalhoDependente,
        };

        var cartao = await new ModuloApoios().ObterCartaoAsync(Contexto(perfil), default);

        Assert.Equal("1", cartao.Indicadores[0].Valor); // IRS Jovem
        Assert.Equal("IRS Jovem", Assert.Single(cartao.Itens).Texto);
    }
}
