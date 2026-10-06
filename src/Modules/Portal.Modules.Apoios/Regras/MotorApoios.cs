using System.Globalization;
using Portal.Core.Perfil;
using static Portal.Core.Idioma;

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

    private static string ProcuraComprarTexto => T("Procurar casa para comprar (primeira habitação própria e permanente)", "Be looking to buy (first permanent home)");

    private static bool? ProcuraComprar(PerfilUtilizador p) =>
        p.ProcuraComprarCasa == true || p.SituacaoHabitacao == SituacaoHabitacao.ProcuraComprar ? true
        : p.ProcuraComprarCasa is null && p.SituacaoHabitacao is null ? null
        : false;

    public static ResultadoApoio IrsJovem(PerfilUtilizador p, Indexantes ix, DateOnly hoje)
    {
        var max = (int)ix[ChavesIndexantes.IdadeMaximaJovem];
        var limite = ix[ChavesIndexantes.LimiteIsencaoIrsJovem];
        var apoio = new Apoio("irs-jovem", "IRS Jovem", T("Isenção parcial de IRS sobre os rendimentos do trabalho, durante 10 anos.", "Partial income-tax (IRS) exemption on earnings from work, for 10 years."),
            T("Impostos", "Taxes"), T("Na declaração de IRS; podes pedir à entidade patronal para aplicar já na retenção na fonte.", "In your IRS return; you can ask your employer to apply it to withholding right away."),
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
            .Condicao(T($"Ter entre 18 e {max} anos a 31/12/{hoje.Year}", $"Be between 18 and {max} on 31/12/{hoje.Year}"), Entre(idade, 18, max))
            .Condicao(T("Não ser dependente para efeitos de IRS", "Not be a dependant for IRS purposes"), p.Dependente is { } d ? !d : null)
            .Condicao(T("Ter rendimentos de trabalho (categoria A ou B)", "Have income from work (category A or B)"), comTrabalho)
            .Condicao(T("Ser residente fiscal em Portugal", "Be tax resident in Portugal"), p.ResidenteFiscal)
            .Estimativa(T($"Isenção até {Euros(limite)} por ano (100% no 1.º ano, a descer nos seguintes).", $"Exemption up to {Euros(limite)} a year (100% in year 1, decreasing after)."))
            .Resultado();
    }

    public static ResultadoApoio Porta65Jovem(PerfilUtilizador p, Indexantes ix, DateOnly hoje)
    {
        var max = (int)ix[ChavesIndexantes.IdadeMaximaJovem];
        var limite = ix[ChavesIndexantes.LimiteEscalao6Irs];
        var apoio = new Apoio("porta65-jovem", "Porta 65 Jovem", T("Apoio mensal à renda para jovens, durante 12 meses, renovável até 5 anos.", "Monthly rent support for young people, for 12 months, renewable for up to 5 years."),
            T("Habitação", "Housing"), T("Candidatura online no Portal da Habitação, em qualquer altura do ano (pode ser antes de teres contrato).", "Apply online at Portal da Habitação, any time of year (even before you have a lease)."),
            "https://www.portaldahabitacao.pt/", Verificado, Prazo: T("Candidaturas todo o ano", "Applications open all year"));

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
            .Condicao(T($"Ter entre 18 e {max} anos (num casal, um pode ter até {max + 2})", $"Be between 18 and {max} (in a couple, one may be up to {max + 2})"), idadeOk)
            .Condicao(T($"Rendimento do agregado até {Euros(limite)} (6.º escalão de IRS)", $"Household income up to {Euros(limite)} (6th IRS bracket)"), p.RendimentoAnualAgregado is { } r ? r <= limite : null)
            .Condicao(T("Arrendar ou procurar casa para arrendar", "Rent, or be looking for a place to rent"), arrendamento)
            .Estimativa(T("O valor depende da renda, do rendimento e da renda máxima admitida no concelho.", "The amount depends on the rent, the income and the maximum rent allowed in the municipality."))
            .Resultado();
    }

    public static ResultadoApoio ApoioExtraordinarioRenda(PerfilUtilizador p, Indexantes ix)
    {
        var limite = ix[ChavesIndexantes.LimiteEscalao6Irs];
        var maximo = ix[ChavesIndexantes.ApoioRendaMaximoMensal];
        var apoio = new Apoio("apoio-renda", T("Apoio extraordinário à renda", "Extraordinary rent support"), T("Até 200 € por mês para quem paga uma renda pesada face ao rendimento.", "Up to €200 a month for people whose rent is heavy relative to income."),
            T("Habitação", "Housing"), T("Automático: a AT e a Segurança Social atribuem-no sem pedido. Se tens direito e não recebes, verifica no Portal das Finanças.", "Automatic: the tax authority and Social Security grant it without an application. If you qualify and get nothing, check Portal das Finanças."),
            "https://www.portaldahabitacao.pt/", Verificado,
            Aviso: T("O Governo anunciou em fevereiro de 2026 a intenção de revogar este apoio. Mantém-se até a revogação ser publicada.", "In February 2026 the Government announced it intends to revoke this support. It stays until the revocation is published."));

        var arrenda = p.SituacaoHabitacao is null ? (bool?)null : p.SituacaoHabitacao == SituacaoHabitacao.Arrenda;
        decimal? taxaEsforco = p.RendaMensal is { } renda && p.RendimentoAnualAgregado is { } rend && rend > 0
            ? renda * 12 / rend : null;

        string? estimativa = null;
        if (p.RendaMensal is { } rm && p.RendimentoAnualAgregado is { } ra)
        {
            var valor = Math.Min(maximo, rm - 0.35m * ra / 12);
            if (valor > 0)
                estimativa = T($"Cerca de {Euros(Math.Round(valor))} por mês.", $"About {Euros(Math.Round(valor))} a month.");
        }

        return new Avaliacao(apoio)
            .Condicao(T("Arrendar a habitação própria e permanente", "Rent your permanent home"), arrenda)
            .Condicao(T($"Contrato celebrado até {LimiteContratoApoioRenda:dd/MM/yyyy}", $"Lease signed by {LimiteContratoApoioRenda:dd/MM/yyyy}"), p.DataContratoArrendamento is { } dc ? dc <= LimiteContratoApoioRenda : null)
            .Condicao(T($"Rendimento do agregado até {Euros(limite)}", $"Household income up to {Euros(limite)}"), p.RendimentoAnualAgregado is { } r ? r <= limite : null)
            .Condicao(T("Renda anual igual ou superior a 35% do rendimento", "Annual rent at least 35% of income"), taxaEsforco is { } t ? t >= 0.35m : null)
            .Estimativa(estimativa)
            .Resultado();
    }

    public static ResultadoApoio ImtJovem(PerfilUtilizador p, Indexantes ix, DateOnly hoje)
    {
        var max = (int)ix[ChavesIndexantes.IdadeMaximaJovem];
        var total = ix[ChavesIndexantes.ImtJovemIsencaoTotal];
        var parcial = ix[ChavesIndexantes.ImtJovemIsencaoParcial];
        var apoio = new Apoio("imt-jovem", T("IMT Jovem e Imposto do Selo", "IMT Jovem and Stamp Duty"), T("Isenção de IMT e Imposto do Selo na compra da primeira habitação própria e permanente.", "Exemption from property transfer tax (IMT) and Stamp Duty when buying your first permanent home."),
            T("Compra de casa", "Buying a home"), T("Pedido no Portal das Finanças antes da escritura (ou pelo notário ou banco).", "Request it at Portal das Finanças before the deed (or through the notary or bank)."),
            "https://info.portaldasfinancas.gov.pt/pt/apoio_contribuinte/IMT_Jovem/Pages/default.aspx", Verificado,
            Prazo: T($"Escrituras até {FimRegimesJovemCompra:dd/MM/yyyy}", $"Deeds by {FimRegimesJovemCompra:dd/MM/yyyy}"));

        var estimativa = p.OrcamentoCompra switch
        {
            null => null,
            var orc when orc <= total => T($"Isenção total para casas até {Euros(total)}.", $"Full exemption for homes up to {Euros(total)}."),
            var orc when orc <= parcial => T($"Isenção parcial: só a parte até {Euros(total)} fica isenta.", $"Partial exemption: only the part up to {Euros(total)} is exempt."),
            _ => null,
        };

        return new Avaliacao(apoio)
            .Condicao(T($"Ter até {max} anos", $"Be {max} or younger"), Entre(p.IdadeEm(hoje), 18, max))
            .Condicao(T("Não ser dependente para efeitos de IRS", "Not be a dependant for IRS purposes"), p.Dependente is { } d ? !d : null)
            .Condicao(ProcuraComprarTexto, ProcuraComprar(p))
            .Condicao(T($"Orçamento até {Euros(parcial)}", $"Budget up to {Euros(parcial)}"), p.OrcamentoCompra is { } o ? o <= parcial : null)
            .Condicao(T($"Escritura até {FimRegimesJovemCompra:dd/MM/yyyy}", $"Deed by {FimRegimesJovemCompra:dd/MM/yyyy}"), hoje <= FimRegimesJovemCompra)
            .Estimativa(estimativa)
            .Resultado();
    }

    public static ResultadoApoio GarantiaPublica(PerfilUtilizador p, Indexantes ix, DateOnly hoje)
    {
        var max = (int)ix[ChavesIndexantes.IdadeMaximaJovem];
        var valorMax = ix[ChavesIndexantes.GarantiaPublicaValorMaximo];
        var apoio = new Apoio("garantia-publica", T("Garantia pública no crédito habitação", "Public mortgage guarantee"), T("O Estado garante parte do empréstimo, permitindo financiamento até 100% do valor da casa.", "The State guarantees part of the loan, allowing financing of up to 100% of the home's value."),
            T("Compra de casa", "Buying a home"), T("Pedido ao banco, ao negociar o crédito habitação.", "Ask your bank when negotiating the mortgage."),
            "https://www.cgd.pt/Site/Saldo-Positivo/o-banco-e-eu/Pages/Como-funciona-a-garantia-do-Estado.aspx", Verificado,
            Prazo: T($"Contratos até {FimRegimesJovemCompra:dd/MM/yyyy}", $"Contracts by {FimRegimesJovemCompra:dd/MM/yyyy}"),
            Aviso: T("Há também um limite de rendimento que ainda não está verificado neste portal.", "There is also an income limit this portal does not check yet."));

        return new Avaliacao(apoio)
            .Condicao(T($"Ter entre 18 e {max} anos", $"Be between 18 and {max}"), Entre(p.IdadeEm(hoje), 18, max))
            .Condicao(ProcuraComprarTexto, ProcuraComprar(p))
            .Condicao(T($"Casa até {Euros(valorMax)}", $"Home up to {Euros(valorMax)}"), p.OrcamentoCompra is { } o ? o <= valorMax : null)
            .Condicao(T($"Contrato até {FimRegimesJovemCompra:dd/MM/yyyy}", $"Contract by {FimRegimesJovemCompra:dd/MM/yyyy}"), hoje <= FimRegimesJovemCompra)
            .Estimativa(T("Podes financiar até 100% do valor da casa, sem entrada.", "You can finance up to 100% of the home's value, with no down payment."))
            .Resultado();
    }

    public static ResultadoApoio AbonoFamilia(PerfilUtilizador p, Indexantes ix)
    {
        var ias = ix[ChavesIndexantes.IasPedidosNovosAbono];
        var apoio = new Apoio("abono-familia", T("Abono de família", "Child benefit (abono de família)"), T("Prestação mensal por cada criança ou jovem, conforme o escalão de rendimento.", "Monthly payment per child or young person, by income bracket."),
            T("Família", "Family"), T("Pedido na Segurança Social Direta.", "Apply at Segurança Social Direta."),
            "https://www.seg-social.pt/", Verificado,
            Aviso: T("Até aos 16 anos; dos 16 aos 24 só se estiver a estudar.", "Up to age 16; from 16 to 24 only while studying."));

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
            .Condicao(T("Ter filhos até aos 16 anos (ou até aos 24 se estudarem)", "Have children under 16 (or up to 24 if studying)"), comDireito > 0 ? true : p.IdadesFilhos.Count == 0 && p.NumeroAdultos is null ? null : false)
            .Condicao(T("Rendimento de referência abaixo do 5.º escalão", "Reference income below the 5th bracket"), escalao is { } e ? e <= 4 : null)
            .Estimativa(escalao is { } esc and <= 4 ? T($"{esc}.º escalão.", $"Bracket {esc}.") : null)
            .Resultado();
    }

    public static ResultadoApoio ELar() =>
        new Avaliacao(new Apoio("e-lar", T("Programa E-Lar", "E-Lar programme"), T("Apoio à troca de equipamentos a gás por elétricos eficientes.", "Support for replacing gas appliances with efficient electric ones."),
                T("Energia", "Energy"), T("Candidatura no portal do Fundo Ambiental, quando abrir.", "Apply on the Fundo Ambiental portal when it opens."),
                "https://www.fundoambiental.pt/", Verificado,
                Aviso: T("A 2.ª fase fechou a 24/03/2026. Foi anunciada uma 3.ª fase para 2026.", "Phase 2 closed on 24/03/2026. A phase 3 has been announced for 2026.")))
            .Resultado(encerrado: true);
}
