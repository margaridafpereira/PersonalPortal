using Portal.Core.Perfil;

namespace Portal.Tests;

public class PerfilTests
{
    [Theory]
    [InlineData("1990-06-15", "2026-06-14", 35)] // véspera do aniversário
    [InlineData("1990-06-15", "2026-06-15", 36)] // no dia do aniversário
    // Nascido a 29/02: em anos não bissextos completa anos a 28/02 (Código Civil, art. 279.º, al. c)).
    [InlineData("2000-02-29", "2026-02-27", 25)]
    [InlineData("2000-02-29", "2026-02-28", 26)]
    public void IdadeEm_conta_anos_completos(string nascimento, string data, int esperado)
    {
        var perfil = new PerfilUtilizador { DataNascimento = DateOnly.Parse(nascimento) };

        Assert.Equal(esperado, perfil.IdadeEm(DateOnly.Parse(data)));
    }

    [Fact]
    public void IdadeEm_sem_data_de_nascimento_e_null()
    {
        Assert.Null(new PerfilUtilizador().IdadeEm(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void EmFalta_devolve_so_os_campos_pedidos_que_estao_vazios()
    {
        var perfil = new PerfilUtilizador { DataNascimento = new DateOnly(1995, 1, 1), Concelho = "  " };

        var emFalta = CamposPerfil.EmFalta(perfil, [CamposPerfil.DataNascimento, CamposPerfil.Concelho, CamposPerfil.OrcamentoCompra]);

        Assert.Equal([CamposPerfil.Concelho, CamposPerfil.OrcamentoCompra], emFalta);
    }
}
