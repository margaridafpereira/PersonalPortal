using Portal.Core.Perfil;
using Portal.Modules.Apoios;
using Portal.Modules.Apoios.Prazos;
using Portal.Modules.Apoios.Regras;

namespace Portal.Tests;

public class ApoiosRegrasTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);
    private static readonly Indexantes Ix = new IndexantesEmbebidos().Obter(2026)!;

    private static PerfilUtilizador JovemTrabalhador() => new()
    {
        DataNascimento = new DateOnly(1998, 4, 1),
        ResidenteFiscal = true,
        Dependente = false,
        CategoriaRendimento = CategoriaRendimento.TrabalhoDependente,
        RendimentoAnualAgregado = 22000,
        SituacaoHabitacao = SituacaoHabitacao.Arrenda,
        RendaMensal = 750,
        DataContratoArrendamento = new DateOnly(2022, 9, 1),
    };

    private static ResultadoApoio Apoio(PerfilUtilizador p, string id) =>
        MotorApoios.Avaliar(p, Ix, Hoje).Single(r => r.Apoio.Id == id);

    [Fact]
    public void Perfil_vazio_da_falta_de_informacao_e_nao_nao_elegivel()
    {
        var resultados = MotorApoios.Avaliar(new PerfilUtilizador(), Ix, Hoje);

        Assert.All(resultados.Where(r => r.Apoio.Id != "e-lar"),
            r => Assert.Equal(EstadoElegibilidade.FaltaInformacao, r.Estado));
    }

    [Fact]
    public void Jovem_que_arrenda_tem_irs_jovem_porta65_e_apoio_renda()
    {
        var p = JovemTrabalhador();

        Assert.Equal(EstadoElegibilidade.Provavel, Apoio(p, "irs-jovem").Estado);
        Assert.Equal(EstadoElegibilidade.Provavel, Apoio(p, "porta65-jovem").Estado);
        var renda = Apoio(p, "apoio-renda");
        Assert.Equal(EstadoElegibilidade.Provavel, renda.Estado);
        // 750 − 35% × 22 000 / 12 = 108,33 → cerca de 108 €
        Assert.Contains("108", renda.Estimativa);
    }

    [Fact]
    public void Irs_jovem_conta_a_idade_a_31_de_dezembro()
    {
        // Faz 36 anos em novembro de 2026: a 31/12 já não cumpre.
        var p = JovemTrabalhador();
        p.DataNascimento = new DateOnly(1990, 11, 15);

        Assert.Equal(EstadoElegibilidade.NaoElegivel, Apoio(p, "irs-jovem").Estado);
    }

    [Fact]
    public void Apoio_renda_exige_contrato_ate_15_de_marco_de_2023()
    {
        var p = JovemTrabalhador();
        p.DataContratoArrendamento = new DateOnly(2023, 3, 16);

        Assert.Equal(EstadoElegibilidade.NaoElegivel, Apoio(p, "apoio-renda").Estado);
    }

    [Fact]
    public void Apoio_renda_exige_taxa_de_esforco_de_35_por_cento()
    {
        var p = JovemTrabalhador();
        p.RendaMensal = 600; // 7 200 / 22 000 = 32,7%

        var r = Apoio(p, "apoio-renda");

        Assert.Equal(EstadoElegibilidade.NaoElegivel, r.Estado);
        Assert.Contains(r.Condicoes, c => c.Descricao.Contains("35%") && c.Resultado == ResultadoCondicao.NaoCumpre);
    }

    [Theory]
    [InlineData(300000, EstadoElegibilidade.Provavel, "total")]
    [InlineData(500000, EstadoElegibilidade.Provavel, "parcial")]
    [InlineData(700000, EstadoElegibilidade.NaoElegivel, null)]
    public void Imt_jovem_depende_do_orcamento(int orcamento, EstadoElegibilidade esperado, string? tipoIsencao)
    {
        var p = JovemTrabalhador();
        p.ProcuraComprarCasa = true;
        p.OrcamentoCompra = orcamento;

        var r = Apoio(p, "imt-jovem");

        Assert.Equal(esperado, r.Estado);
        if (tipoIsencao is not null)
            Assert.Contains(tipoIsencao, r.Estimativa);
    }

    [Fact]
    public void Garantia_publica_so_ate_450_mil()
    {
        var p = JovemTrabalhador();
        p.ProcuraComprarCasa = true;
        p.OrcamentoCompra = 460000;

        Assert.Equal(EstadoElegibilidade.NaoElegivel, Apoio(p, "garantia-publica").Estado);
    }

    [Theory]
    [InlineData(15000, 1, "3.º escalão.")] // referência 7 500 €
    [InlineData(60000, 1, null)]           // referência 30 000 € > 2,5 × 7 315 € = 18 287,5 € → 5.º escalão, sem direito
    public void Abono_familia_calcula_o_escalao(int rendimento, int filhos, string? esperado)
    {
        // IAS 2025 = 522,50 €; × 14 = 7 315 €. Referência = rendimento / (filhos + 1).
        // 15 000 / 2 = 7 500 € → acima de 7 315 € e abaixo de 1,7 × 7 315 = 12 435,5 € → 3.º escalão.
        var p = new PerfilUtilizador { RendimentoAnualAgregado = rendimento, IdadesFilhos = Enumerable.Repeat(4, filhos).ToList() };

        var r = Apoio(p, "abono-familia");

        if (esperado is null)
            Assert.Equal(EstadoElegibilidade.NaoElegivel, r.Estado);
        else
            Assert.Equal(esperado, r.Estimativa);
    }

    [Fact]
    public void E_lar_aparece_como_encerrado()
    {
        Assert.Equal(EstadoElegibilidade.Encerrado, Apoio(JovemTrabalhador(), "e-lar").Estado);
    }
}

public class PrazosTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);

    [Fact]
    public void Independente_tem_declaracao_trimestral_de_outubro()
    {
        var p = new PerfilUtilizador { CategoriaRendimento = CategoriaRendimento.TrabalhoIndependente };

        var prazos = CalendarioPrazos.Proximos(p, Hoje);

        var primeiro = prazos.First();
        Assert.Equal("Declaração trimestral à Segurança Social", primeiro.Titulo);
        Assert.Equal(new DateOnly(2026, 11, 2), primeiro.Data); // 31/10/2026 é sábado → segunda-feira
    }

    [Fact]
    public void Trabalhador_por_conta_de_outrem_nao_tem_declaracao_trimestral()
    {
        var p = new PerfilUtilizador { CategoriaRendimento = CategoriaRendimento.TrabalhoDependente };

        Assert.DoesNotContain(CalendarioPrazos.Proximos(p, Hoje), pr => pr.Categoria == CategoriaPrazo.SegurancaSocial);
    }

    [Fact]
    public void Imi_acima_de_500_euros_tem_tres_prestacoes()
    {
        var p = new PerfilUtilizador { ProprietarioImovel = true, ValorImi = 650 };

        var imi = CalendarioPrazos.Proximos(p, new DateOnly(2027, 1, 1)).Where(pr => pr.Titulo.StartsWith("Pagar o IMI")).ToList();

        Assert.Equal([5, 8, 11], imi.Select(pr => pr.Data.Month));
    }

    [Fact]
    public void Iuc_no_mes_da_matricula()
    {
        var p = new PerfilUtilizador { TemVeiculo = true, MesMatricula = 3, CategoriaRendimento = CategoriaRendimento.Nenhum };

        var iuc = Assert.Single(CalendarioPrazos.Proximos(p, Hoje));

        Assert.Equal(new DateOnly(2027, 3, 31), iuc.Data);
    }

    [Fact]
    public void Ics_tem_um_evento_por_prazo_com_alarmes()
    {
        var p = new PerfilUtilizador { CategoriaRendimento = CategoriaRendimento.TrabalhoIndependente };
        var prazos = CalendarioPrazos.Proximos(p, Hoje);

        var ics = CalendarioPrazos.ParaIcs(prazos, [14, 3], DateTimeOffset.UnixEpoch);

        Assert.StartsWith("BEGIN:VCALENDAR", ics);
        Assert.Equal(prazos.Count, CountOf(ics, "BEGIN:VEVENT"));
        Assert.Equal(prazos.Count * 2, CountOf(ics, "BEGIN:VALARM"));
        Assert.Contains("TRIGGER:-P14D", ics);
    }

    private static int CountOf(string texto, string procura) =>
        (texto.Length - texto.Replace(procura, "").Length) / procura.Length;
}
