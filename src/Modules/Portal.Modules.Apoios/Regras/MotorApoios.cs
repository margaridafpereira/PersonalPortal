using System.Globalization;
using Portal.Core.Perfil;

namespace Portal.Modules.Apoios.Regras;

/// <summary>
/// Avalia o perfil contra o catálogo de apoios (docs/apoios/02-catalogo-inicial.md).
/// Cada regra devolve as condições uma a uma, para o utilizador ver porque tem ou não direito.
/// O resultado é sempre indicativo: a confirmação é feita pela entidade oficial.
/// </summary>
public static class MotorApoios
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");
    private static readonly DateOnly Verificado = new(2026, 9, 28);

    /// <summary>Data-limite dos regimes de compra para jovens (IMT Jovem e garantia pública).</summary>
    public static readonly DateOnly FimRegimesJovemCompra = new(2026, 12, 31);

    /// <summary>Contratos de arrendamento celebrados até esta data contam para o apoio extraordinário.</summary>
    public static readonly DateOnly LimiteContratoApoioRenda = new(2023, 3, 15);

    public static IReadOnlyList<ResultadoApoio> Avaliar(PerfilUtilizador p, Indexantes ix, DateOnly hoje) =>
    [
        IrsJovem(p, ix, hoje),
        Porta65Jovem(p, ix, hoje),
        ApoioExtraordinarioRenda(p, ix),
        ImtJovem(p, ix, hoje),
        GarantiaPublica(p, ix, hoje),
        AbonoFamilia(p, ix),
        ELar(),
    ];

    private static string Euros(decimal v) => v.ToString("C0", Pt);

    private static bool? Entre(int? valor, int min, int max) => valor is { } v ? v >= min && v <= max : null;

    private static bool? ProcuraComprar(PerfilUtilizador p) =>
        p.ProcuraComprarCasa == true || p.SituacaoHabitacao == SituacaoHabitacao.ProcuraComprar ? true
        : p.ProcuraComprarCasa is null && p.SituacaoHabitacao is null ? null
        : false;

    public static ResultadoApoio IrsJovem(PerfilUtilizador p, Indexantes ix, DateOnly hoje)
    {
        var max = (int)ix[ChavesIndexantes.IdadeMaximaJovem];
        var limite = ix[ChavesIndexantes.LimiteIsencaoIrsJovem];
        var apoio = new Apoio("irs-jovem", "IRS Jovem", "Isenção parcial de IRS sobre os rendimentos do trabalho, durante 10 anos.",
            "Impostos", "Na declaração de IRS; podes pedir à entidade patronal para aplicar já na retenção na fonte.",
            "https://info.portaldasfinancas.gov.pt/", Verificado);

        // A idade conta a 31 de dezembro do ano dos rendimentos.
        var idade = p.IdadeEm(new DateOnly(hoje.Year, 12, 31));
        var comTrabalho = p.CategoriaRendimento switch
        {
            null => (bool?)null,
            CategoriaRendimento.Nenhum => false,
            _ => true,
        };

        return new Avaliacao(apoio)
            .Condicao($"Ter entre 18 e {max} anos a 31/12/{hoje.Year}", Entre(idade, 18, max))
            .Condicao("Não ser dependente para efeitos de IRS", p.Dependente is { } d ? !d : null)
            .Condicao("Ter rendimentos de trabalho (categoria A ou B)", comTrabalho)
            .Condicao("Ser residente fiscal em Portugal", p.ResidenteFiscal)
            .Estimativa($"Isenção até {Euros(limite)} por ano (100% no 1.º ano, a descer nos seguintes).")
            .Resultado();
    }

    public static ResultadoApoio Porta65Jovem(PerfilUtilizador p, Indexantes ix, DateOnly hoje)
    {
        var max = (int)ix[ChavesIndexantes.IdadeMaximaJovem];
        var limite = ix[ChavesIndexantes.LimiteEscalao6Irs];
        var apoio = new Apoio("porta65-jovem", "Porta 65 Jovem", "Apoio mensal à renda para jovens, durante 12 meses, renovável até 5 anos.",
            "Habitação", "Candidatura online no Portal da Habitação, em qualquer altura do ano (pode ser antes de teres contrato).",
            "https://www.portaldahabitacao.pt/", Verificado, Prazo: "Candidaturas todo o ano");

        var idade = p.IdadeEm(hoje);
        bool? idadeOk = idade switch
        {
            null => null,
            >= 18 and var i when i <= max => true,
            // Num casal, um dos elementos pode ter até 37 anos se o outro tiver até 35.
            var i when i <= max + 2 && p.NumeroAdultos >= 2 => null,
            _ => false,
        };
        bool? arrendamento = p.SituacaoHabitacao switch
        {
            null => null,
            SituacaoHabitacao.Arrenda or SituacaoHabitacao.ProcuraArrendar => true,
            _ => false,
        };

        return new Avaliacao(apoio)
            .Condicao($"Ter entre 18 e {max} anos (num casal, um pode ter até {max + 2})", idadeOk)
            .Condicao($"Rendimento do agregado até {Euros(limite)} (6.º escalão de IRS)", p.RendimentoAnualAgregado is { } r ? r <= limite : null)
            .Condicao("Arrendar ou procurar casa para arrendar", arrendamento)
            .Estimativa("O valor depende da renda, do rendimento e da renda máxima admitida no concelho.")
            .Resultado();
    }

    public static ResultadoApoio ApoioExtraordinarioRenda(PerfilUtilizador p, Indexantes ix)
    {
        var limite = ix[ChavesIndexantes.LimiteEscalao6Irs];
        var maximo = ix[ChavesIndexantes.ApoioRendaMaximoMensal];
        var apoio = new Apoio("apoio-renda", "Apoio extraordinário à renda", "Até 200 € por mês para quem paga uma renda pesada face ao rendimento.",
            "Habitação", "Automático: a AT e a Segurança Social atribuem-no sem pedido. Se tens direito e não recebes, verifica no Portal das Finanças.",
            "https://www.portaldahabitacao.pt/", Verificado,
            Aviso: "O Governo anunciou em fevereiro de 2026 a intenção de revogar este apoio. Mantém-se até a revogação ser publicada.");

        var arrenda = p.SituacaoHabitacao is null ? (bool?)null : p.SituacaoHabitacao == SituacaoHabitacao.Arrenda;
        decimal? taxaEsforco = p.RendaMensal is { } renda && p.RendimentoAnualAgregado is { } rend && rend > 0
            ? renda * 12 / rend : null;

        string? estimativa = null;
        if (p.RendaMensal is { } rm && p.RendimentoAnualAgregado is { } ra)
        {
            var valor = Math.Min(maximo, rm - 0.35m * ra / 12);
            if (valor > 0)
                estimativa = $"Cerca de {Euros(Math.Round(valor))} por mês.";
        }

        return new Avaliacao(apoio)
            .Condicao("Arrendar a habitação própria e permanente", arrenda)
            .Condicao($"Contrato celebrado até {LimiteContratoApoioRenda:dd/MM/yyyy}", p.DataContratoArrendamento is { } dc ? dc <= LimiteContratoApoioRenda : null)
            .Condicao($"Rendimento do agregado até {Euros(limite)}", p.RendimentoAnualAgregado is { } r ? r <= limite : null)
            .Condicao("Renda anual igual ou superior a 35% do rendimento", taxaEsforco is { } t ? t >= 0.35m : null)
            .Estimativa(estimativa)
            .Resultado();
    }

    public static ResultadoApoio ImtJovem(PerfilUtilizador p, Indexantes ix, DateOnly hoje)
    {
        var max = (int)ix[ChavesIndexantes.IdadeMaximaJovem];
        var total = ix[ChavesIndexantes.ImtJovemIsencaoTotal];
        var parcial = ix[ChavesIndexantes.ImtJovemIsencaoParcial];
        var apoio = new Apoio("imt-jovem", "IMT Jovem e Imposto do Selo", "Isenção de IMT e Imposto do Selo na compra da primeira habitação própria e permanente.",
            "Compra de casa", "Pedido no Portal das Finanças antes da escritura (ou pelo notário ou banco).",
            "https://info.portaldasfinancas.gov.pt/pt/apoio_contribuinte/IMT_Jovem/Pages/default.aspx", Verificado,
            Prazo: $"Escrituras até {FimRegimesJovemCompra:dd/MM/yyyy}");

        var estimativa = p.OrcamentoCompra switch
        {
            null => null,
            var orc when orc <= total => $"Isenção total para casas até {Euros(total)}.",
            var orc when orc <= parcial => $"Isenção parcial: só a parte até {Euros(total)} fica isenta.",
            _ => null,
        };

        return new Avaliacao(apoio)
            .Condicao($"Ter até {max} anos", Entre(p.IdadeEm(hoje), 18, max))
            .Condicao("Não ser dependente para efeitos de IRS", p.Dependente is { } d ? !d : null)
            .Condicao("Procurar casa para comprar (primeira habitação própria e permanente)", ProcuraComprar(p))
            .Condicao($"Orçamento até {Euros(parcial)}", p.OrcamentoCompra is { } o ? o <= parcial : null)
            .Condicao($"Escritura até {FimRegimesJovemCompra:dd/MM/yyyy}", hoje <= FimRegimesJovemCompra)
            .Estimativa(estimativa)
            .Resultado();
    }

    public static ResultadoApoio GarantiaPublica(PerfilUtilizador p, Indexantes ix, DateOnly hoje)
    {
        var max = (int)ix[ChavesIndexantes.IdadeMaximaJovem];
        var valorMax = ix[ChavesIndexantes.GarantiaPublicaValorMaximo];
        var apoio = new Apoio("garantia-publica", "Garantia pública no crédito habitação", "O Estado garante parte do empréstimo, permitindo financiamento até 100% do valor da casa.",
            "Compra de casa", "Pedido ao banco, ao negociar o crédito habitação.",
            "https://www.cgd.pt/Site/Saldo-Positivo/o-banco-e-eu/Pages/Como-funciona-a-garantia-do-Estado.aspx", Verificado,
            Prazo: $"Contratos até {FimRegimesJovemCompra:dd/MM/yyyy}",
            Aviso: "Há também um limite de rendimento que ainda não está verificado neste portal.");

        return new Avaliacao(apoio)
            .Condicao($"Ter entre 18 e {max} anos", Entre(p.IdadeEm(hoje), 18, max))
            .Condicao("Procurar casa para comprar (primeira habitação própria e permanente)", ProcuraComprar(p))
            .Condicao($"Casa até {Euros(valorMax)}", p.OrcamentoCompra is { } o ? o <= valorMax : null)
            .Condicao($"Contrato até {FimRegimesJovemCompra:dd/MM/yyyy}", hoje <= FimRegimesJovemCompra)
            .Estimativa("Podes financiar até 100% do valor da casa, sem entrada.")
            .Resultado();
    }

    public static ResultadoApoio AbonoFamilia(PerfilUtilizador p, Indexantes ix)
    {
        var ias = ix[ChavesIndexantes.IasPedidosNovosAbono];
        var apoio = new Apoio("abono-familia", "Abono de família", "Prestação mensal por cada criança ou jovem, conforme o escalão de rendimento.",
            "Família", "Pedido na Segurança Social Direta.",
            "https://www.seg-social.pt/", Verificado,
            Aviso: "Até aos 16 anos; dos 16 aos 24 só se estiver a estudar.");

        var criancas = p.IdadesFilhos.Count(i => i < 16);
        var estudantesPossiveis = p.IdadesFilhos.Count(i => i is >= 16 and <= 24);
        var comDireito = criancas + estudantesPossiveis;

        int? escalao = null;
        if (p.RendimentoAnualAgregado is { } rend && comDireito > 0)
        {
            var referencia = rend / (comDireito + 1);
            var anual = ias * 14;
            escalao = referencia <= 0.5m * anual ? 1
                : referencia <= 1m * anual ? 2
                : referencia <= 1.7m * anual ? 3
                : referencia <= 2.5m * anual ? 4
                : 5;
        }

        return new Avaliacao(apoio)
            .Condicao("Ter filhos até aos 16 anos (ou até aos 24 se estudarem)", comDireito > 0 ? true : p.IdadesFilhos.Count == 0 && p.NumeroAdultos is null ? null : false)
            .Condicao("Rendimento de referência abaixo do 5.º escalão", escalao is { } e ? e <= 4 : null)
            .Estimativa(escalao is { } esc and <= 4 ? $"{esc}.º escalão." : null)
            .Resultado();
    }

    public static ResultadoApoio ELar() =>
        new Avaliacao(new Apoio("e-lar", "Programa E-Lar", "Apoio à troca de equipamentos a gás por elétricos eficientes.",
                "Energia", "Candidatura no portal do Fundo Ambiental, quando abrir.",
                "https://www.fundoambiental.pt/", Verificado,
                Aviso: "A 2.ª fase fechou a 24/03/2026. Foi anunciada uma 3.ª fase para 2026."))
            .Resultado(encerrado: true);
}
