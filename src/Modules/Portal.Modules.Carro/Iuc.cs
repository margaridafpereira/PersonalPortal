using static Portal.Core.Idioma;
namespace Portal.Modules.Carro;

/// <param name="Valor">Estimativa anual em euros, ou null se faltarem dados.</param>
/// <param name="Detalhe">As parcelas do cálculo, para a pessoa perceber de onde vem o valor.</param>
/// <param name="EmFalta">Campos do veículo que faltam para calcular.</param>
public sealed record EstimativaIuc(decimal? Valor, string Categoria, IReadOnlyList<string> Detalhe, IReadOnlyList<string> EmFalta, string? Aviso);

/// <summary>
/// Estimativa do IUC dos ligeiros de passageiros. Tabelas do Código do IUC (art. 9.º e 10.º) em vigor em 2024, 2025 e 2026:
/// o OE 2026 (Lei n.º 73-A/2025) não as alterou. Fontes: DECO PROteste e impostosobreveiculos.info (29/09/2026);
/// ver docs/carro/01-fontes-e-regras.md. É uma estimativa: o valor oficial está no Portal das Finanças.
/// </summary>
public static class Iuc
{
    /// <summary>Página do IUC no Portal das Finanças (simulação, consulta e pagamento).</summary>
    public const string Simulador = "https://www.portaldasfinancas.gov.pt/pt/menu.action?pai=5225";

    private static readonly DateOnly InicioCategoriaB = new(2007, 7, 1);

    // Categoria B: (limite superior, taxa). O último escalão não tem limite.
    private static readonly (int Ate, decimal Taxa)[] TaxaCilindradaB = [(1250, 31.77m), (1750, 63.74m), (2500, 127.35m), (int.MaxValue, 435.84m)];
    private static readonly (int Ate, decimal Taxa)[] Co2Nedc = [(120, 65.15m), (180, 97.63m), (250, 212.04m), (int.MaxValue, 363.25m)];
    private static readonly (int Ate, decimal Taxa)[] Co2Wltp = [(140, 65.15m), (205, 97.63m), (260, 212.04m), (int.MaxValue, 363.25m)];

    /// <summary>Taxa adicional dos escalões mais altos de CO2, para carros matriculados a partir de 2017.</summary>
    private static readonly decimal[] AdicionalCo2 = [0m, 0m, 31.77m, 63.74m];

    private static readonly (int Ate, decimal Taxa)[] AdicionalGasoleoB = [(1250, 5.02m), (1750, 10.07m), (2500, 20.12m), (int.MaxValue, 68.85m)];

    // Categoria A (matrícula de 1981 a junho de 2007): taxa por cilindrada e escalão de idade (1996–2007, 1990–1995, 1981–1989).
    private static readonly (int Ate, decimal[] Taxas)[] GasolinaA =
    [
        (1000, [19.90m, 12.20m, 8.80m]), (1300, [39.95m, 22.45m, 12.55m]), (1750, [62.40m, 34.87m, 17.49m]),
        (2600, [158.31m, 83.49m, 36.09m]), (3500, [287.49m, 156.54m, 79.72m]), (int.MaxValue, [512.23m, 263.11m, 120.90m]),
    ];
    private static readonly (int Ate, decimal[] Taxas)[] GasoleoA =
    [
        (1500, [22.48m, 14.18m, 10.19m]), (2000, [45.13m, 25.37m, 14.18m]), (3000, [70.50m, 39.40m, 19.76m]), (int.MaxValue, [178.86m, 94.33m, 40.77m]),
    ];

    public static EstimativaIuc Estimar(Veiculo v)
    {
        var matricula = v.DataPrimeiraMatricula;
        var gasoleo = v.Combustivel is Combustivel.GasoleoSimples or Combustivel.GasoleoEspecial;

        if (v.Categoria == CategoriaVeiculo.LigeiroMercadorias)
            return new(null, "C", [], [], T("Os ligeiros de mercadorias pagam pelo peso bruto e pelo uso: usa o simulador do Portal das Finanças.", "Light goods vehicles pay by gross weight and use: use the Portal das Finanças simulator."));
        if (matricula.Year < 1981)
            return new(0, "A", [T("Carros matriculados antes de 1981 estão isentos.", "Cars registered before 1981 are exempt.")], [], null);

        if (matricula < InicioCategoriaB)
        {
            if (v.Combustivel == Combustivel.Eletrico)
                return new(null, "A", [], [], T("Os elétricos anteriores a julho de 2007 pagam pela voltagem: usa o simulador do Portal das Finanças.", "Electric cars from before July 2007 pay by voltage: use the Portal das Finanças simulator."));
            if (v.Cilindrada is not { } cc)
                return new(null, "A", [], ["cilindrada"], null);

            var escalaoIdade = matricula.Year >= 1996 ? 0 : matricula.Year >= 1990 ? 1 : 2;
            var tabela = gasoleo ? GasoleoA : GasolinaA;
            var taxa = tabela.First(t => cc <= t.Ate).Taxas[escalaoIdade];
            return new(taxa, "A",
                [T($"Categoria A ({(gasoleo ? "gasóleo" : "gasolina")}, {cc} cm³, matrícula de {matricula.Year}): {taxa:0.00} €", $"Category A ({(gasoleo ? "diesel" : "petrol")}, {cc} cm³, registered {matricula.Year}): {taxa:0.00} €")],
                [],
                gasoleo ? T("Não inclui o adicional de IUC dos carros a gasóleo da categoria A: confirma no simulador.", "Does not include the IUC surcharge on category A diesel cars: check the simulator.") : null);
        }

        // Categoria B: os 100% elétricos estão isentos (CIUC, art. 5.º).
        if (v.Combustivel == Combustivel.Eletrico)
            return new(0, "B", [T("Veículos 100% elétricos estão isentos de IUC.", "Fully electric vehicles are exempt from IUC.")], [], null);

        var emFalta = new List<string>();
        if (v.Cilindrada is null) emFalta.Add(T("cilindrada", "engine size"));
        if (v.EmissoesCo2 is null) emFalta.Add(T("emissões de CO2", "CO2 emissions"));
        if (emFalta.Count > 0)
            return new(null, "B", [], emFalta, null);

        var cilindrada = v.Cilindrada!.Value;
        var co2 = v.EmissoesCo2!.Value;
        // Sem norma indicada: WLTP é obrigatória nas matrículas novas desde setembro de 2018.
        var norma = v.NormaCo2 ?? (matricula >= new DateOnly(2018, 9, 1) ? NormaEmissoes.Wltp : NormaEmissoes.Nedc);
        var escalaoCo2 = Array.FindIndex(norma == NormaEmissoes.Wltp ? Co2Wltp : Co2Nedc, t => co2 <= t.Ate);

        var taxaCilindrada = TaxaCilindradaB.First(t => cilindrada <= t.Ate).Taxa;
        var taxaCo2 = (norma == NormaEmissoes.Wltp ? Co2Wltp : Co2Nedc)[escalaoCo2].Taxa;
        var coeficiente = matricula.Year switch { 2007 => 1.00m, 2008 => 1.05m, 2009 => 1.10m, _ => 1.15m };

        var detalhe = new List<string>
        {
            T($"Cilindrada ({cilindrada} cm³): {taxaCilindrada:0.00} €", $"Engine size ({cilindrada} cm³): {taxaCilindrada:0.00} €"),
            $"CO2 ({co2} g/km, {norma.ToString().ToUpperInvariant()}): {taxaCo2:0.00} €",
            T($"Coeficiente do ano da matrícula ({matricula.Year}): × {coeficiente:0.00}", $"Registration-year coefficient ({matricula.Year}): × {coeficiente:0.00}"),
        };
        var total = (taxaCilindrada + taxaCo2) * coeficiente;

        if (matricula.Year >= 2017 && AdicionalCo2[escalaoCo2] > 0)
        {
            total += AdicionalCo2[escalaoCo2];
            detalhe.Add(T($"Adicional de CO2 (matrícula desde 2017): + {AdicionalCo2[escalaoCo2]:0.00} €", $"CO2 surcharge (registered since 2017): + {AdicionalCo2[escalaoCo2]:0.00} €"));
        }
        if (gasoleo)
        {
            var adicional = AdicionalGasoleoB.First(t => cilindrada <= t.Ate).Taxa;
            total += adicional;
            detalhe.Add(T($"Adicional de gasóleo: + {adicional:0.00} €", $"Diesel surcharge: + {adicional:0.00} €"));
        }

        return new(Math.Round(total, 2, MidpointRounding.AwayFromZero), "B", detalhe, [],
            v.NormaCo2 is null ? T($"Assumi a norma {norma.ToString().ToUpperInvariant()} para o CO2; confirma no certificado de matrícula.", $"Assumed the {norma.ToString().ToUpperInvariant()} standard for CO2; check the registration certificate.") : null);
    }
}
