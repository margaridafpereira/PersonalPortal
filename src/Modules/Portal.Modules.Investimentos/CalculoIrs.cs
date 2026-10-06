using static Portal.Core.Idioma;
namespace Portal.Modules.Investimentos;

/// <summary>Uma linha do Anexo J, quadro 9.2A: uma venda (ou parte dela) casada com um lote de compra.</summary>
public sealed record LinhaMaisValia(
    string Ativo, string Nome, string Pais, string CodigoPais, string Codigo,
    DateOnly DataRealizacao, decimal ValorRealizacao,
    DateOnly DataAquisicao, decimal ValorAquisicao,
    decimal Despesas, decimal Resultado, decimal Quantidade, bool DetidoMenosDe365Dias);

/// <summary>
/// Anexo J, quadro 8A: dividendos agrupados por país da fonte. <see cref="Retencao"/> é o que abate ao IRS;
/// <see cref="RetencaoNaoCreditada"/> é o que foi retido acima da taxa da convenção e não abate.
/// </summary>
public sealed record DividendosPais(string Pais, string CodigoPais, string Codigo, decimal Bruto, decimal Retencao, int Pagamentos, decimal RetencaoNaoCreditada = 0);

public sealed record RelatorioIrs(
    int Ano,
    IReadOnlyList<LinhaMaisValia> MaisValias,
    IReadOnlyList<DividendosPais> Dividendos,
    decimal SaldoMaisValias,
    decimal ImpostoMaisValias,
    decimal DividendosBrutos,
    decimal RetencaoEstrangeiro,
    decimal ImpostoDividendos,
    IReadOnlyList<string> Avisos);

/// <summary>Câmbio de referência: 1 EUR = X unidades da moeda. null se não houver.</summary>
public delegate decimal? Cambio(string moeda, DateOnly data);

/// <summary>
/// Cálculo indicativo das mais-valias e dos dividendos de um ano, para o Anexo J.
/// Regras (docs/investimentos/01-fontes-e-regras.md):
/// - FIFO obrigatório por ativo (CIRS, art. 43.º, n.º 8, al. d));
/// - conversão para euros ao câmbio de referência do dia de cada operação;
/// - despesas (comissões) declaradas à parte, na proporção da quantidade vendida;
/// - taxa autónoma de 28% sobre o saldo positivo, sem englobamento.
/// </summary>
public static class CalculoIrs
{
    public const decimal TaxaAutonoma = 0.28m;

    /// <summary>
    /// Taxa máxima de retenção prevista na convenção com Portugal, por país da fonte. Só esta parte do imposto
    /// retido abate ao imposto português; o excesso pede-se de volta ao país da fonte.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, decimal> TaxaConvencao = new Dictionary<string, decimal>
    {
        ["US"] = 0.15m,
    };

    /// <summary>Lote de compra. Custo null: a compra não tinha câmbio, mas os títulos contam na mesma para o FIFO.</summary>
    private sealed class Lote(DateOnly data, decimal quantidade, decimal? custoUnitario, decimal despesaUnitaria)
    {
        public DateOnly Data { get; } = data;
        public decimal Quantidade { get; set; } = quantidade;
        public decimal? CustoUnitario { get; } = custoUnitario;
        public decimal DespesaUnitaria { get; } = despesaUnitaria;
    }

    public static RelatorioIrs Calcular(IEnumerable<Operacao> operacoes, int ano, Cambio cambio)
    {
        var avisos = new List<string>();
        var linhas = new List<LinhaMaisValia>();
        var todas = operacoes.ToList();

        // Mais-valias: todo o histórico entra no FIFO; só as vendas do ano entram no relatório.
        foreach (var ativo in todas.Where(o => o.Tipo != TipoOperacao.Dividendo).GroupBy(o => o.Ativo))
        {
            var lotes = new Queue<Lote>();
            var ordenadas = ativo.OrderBy(o => o.Momento).ThenBy(o => o.Tipo == TipoOperacao.Compra ? 0 : 1);
            foreach (var op in ordenadas)
            {
                var data = DateOnly.FromDateTime(op.Momento);
                // Sem câmbio, a operação continua a mexer nas quantidades: saltá-la desalinharia o FIFO de todas as vendas seguintes.
                var precoEur = Euros(op.PrecoUnitario, op.Moeda, data, cambio, avisos, op);
                var despesaUnitaria = op.Quantidade > 0 ? ComissoesEmEuros(op, data, cambio, avisos) / op.Quantidade : 0;

                if (op.Tipo == TipoOperacao.Compra)
                {
                    lotes.Enqueue(new Lote(data, op.Quantidade, precoEur, despesaUnitaria));
                    continue;
                }

                var porVender = op.Quantidade;
                var semCusto = 0m;
                while (porVender > 0 && lotes.Count > 0)
                {
                    var lote = lotes.Peek();
                    var qtd = Math.Min(porVender, lote.Quantidade);
                    if (data.Year == ano && lote.CustoUnitario is null)
                        semCusto += qtd;
                    else if (data.Year == ano && precoEur is not null)
                    {
                        var realizacao = Arredondar(qtd * precoEur.Value);
                        var aquisicao = Arredondar(qtd * lote.CustoUnitario!.Value);
                        var despesas = Arredondar(qtd * (lote.DespesaUnitaria + despesaUnitaria));
                        linhas.Add(new LinhaMaisValia(op.Ativo, op.Nome, Paises.Iso(op.Ativo), Paises.Codigo(op.Ativo), Classificacao.CodigoAnexoJ(op.TipoAtivo),
                            data, realizacao, lote.Data, aquisicao, despesas, realizacao - aquisicao - despesas, qtd,
                            data.DayNumber - lote.Data.DayNumber < 365));
                    }
                    lote.Quantidade -= qtd;
                    porVender -= qtd;
                    if (lote.Quantidade == 0)
                        lotes.Dequeue();
                }

                if (semCusto > 0 && precoEur is not null)
                    avisos.Add(T($"Venda de {op.Nome} ({op.Ativo}) a {data:dd/MM/yyyy}: {semCusto} título(s) vieram de uma compra sem câmbio e ficaram fora das contas.", $"Sale of {op.Nome} ({op.Ativo}) on {data:dd/MM/yyyy}: {semCusto} share(s) came from a purchase with no exchange rate and were left out."));
                if (porVender > 0 && data.Year == ano)
                    avisos.Add(T($"Venda de {op.Nome} ({op.Ativo}) a {data:dd/MM/yyyy}: faltam compras registadas para {porVender} título(s). Importa o histórico completo.", $"Sale of {op.Nome} ({op.Ativo}) on {data:dd/MM/yyyy}: no recorded purchases for {porVender} share(s). Import the full history."));
            }
        }

        // Dividendos do ano, por país da fonte.
        var dividendos = new List<(string Ativo, decimal Bruto, decimal Retencao, decimal Excesso)>();
        foreach (var op in todas.Where(o => o.Tipo == TipoOperacao.Dividendo && o.Momento.Year == ano))
        {
            var data = DateOnly.FromDateTime(op.Momento);
            var bruto = Euros(op.Quantidade * op.PrecoUnitario, op.Moeda, data, cambio, avisos, op);
            var retencao = op.RetencaoFonte == 0 ? 0 : Euros(op.RetencaoFonte, op.MoedaRetencao ?? op.Moeda, data, cambio, avisos, op);
            if (bruto is not { } b || retencao is not { } r)
                continue;
            var retidoLaFora = r;

            // Acima da taxa da convenção, a retenção não abate ao imposto português (nem se declara como tal no quadro 8A).
            if (TaxaConvencao.TryGetValue(Paises.Iso(op.Ativo), out var taxa) && r > Arredondar(b * taxa))
            {
                avisos.Add(T($"{op.Nome}: retiveram-te mais de {taxa * 100:0}% no país da fonte. Só {taxa * 100:0}% conta como crédito; o excesso pode ser pedido de volta (ou evitado com o formulário W-8BEN na corretora).", $"{op.Nome}: more than {taxa * 100:0}% was withheld in the source country. Only {taxa * 100:0}% counts as a credit; the excess can be reclaimed (or avoided with a W-8BEN form at your broker)."));
                r = b * taxa;
            }
            dividendos.Add((op.Ativo, Arredondar(b), Arredondar(r), Arredondar(retidoLaFora) - Arredondar(r)));
        }

        var porPais = dividendos
            .GroupBy(d => Paises.Iso(d.Ativo))
            .Select(g => new DividendosPais(g.Key, Paises.CodigoDeIso(g.Key), "E11", g.Sum(d => d.Bruto), g.Sum(d => d.Retencao), g.Count(), g.Sum(d => d.Excesso)))
            .OrderByDescending(d => d.Bruto)
            .ToList();

        var saldo = linhas.Sum(l => l.Resultado);
        var brutos = porPais.Sum(d => d.Bruto);
        var retido = porPais.Sum(d => d.Retencao);

        // Crédito por dupla tributação: o imposto pago lá fora abate até ao imposto português sobre esse rendimento.
        var impostoDividendos = porPais.Sum(d => Math.Max(0, Arredondar(d.Bruto * TaxaAutonoma) - d.Retencao));

        if (linhas.Any(l => l.DetidoMenosDe365Dias))
            avisos.Add(T("Há vendas de títulos detidos menos de 365 dias. Se o teu rendimento coletável total chegar ao último escalão de IRS, o englobamento destes ganhos é obrigatório.", "Some sales are of shares held under 365 days. If your total taxable income reaches the top IRS bracket, these gains must be aggregated with your other income."));
        var paraisos = linhas.Select(l => l.Pais).Concat(porPais.Select(d => d.Pais)).Where(Classificacao.RegimesFiscaisFavoraveis.Contains).Distinct().ToList();
        if (paraisos.Count > 0)
            avisos.Add(T($"Há rendimentos com origem em {string.Join(", ", paraisos)}, que está na lista de regimes fiscais mais favoráveis. Pode aplicar-se uma taxa de 35% em vez de 28%: confirma com um contabilista.", $"Some income comes from {string.Join(", ", paraisos)}, which is on the list of preferential tax regimes. A 35% rate may apply instead of 28%: check with an accountant."));
        if (linhas.Any(l => l.Pais == "??") || porPais.Any(d => d.Pais == "??"))
            avisos.Add(T("Há títulos sem ISIN (algumas corretoras só dão o ticker): o país da fonte ficou por identificar. Indica o ISIN de cada um no separador Operações.", "Some holdings have no ISIN (some brokers only give the ticker), so the source country is unknown. Enter each ISIN in the Transactions tab."));

        return new RelatorioIrs(ano, linhas.OrderBy(l => l.DataRealizacao).ToList(), porPais,
            saldo, Arredondar(Math.Max(0, saldo) * TaxaAutonoma), brutos, retido, impostoDividendos, avisos.Distinct().ToList());
    }

    /// <summary>Sem câmbio, as comissões ficam de fora (com aviso): despesas a menos só aumentam o imposto estimado.</summary>
    private static decimal ComissoesEmEuros(Operacao op, DateOnly data, Cambio cambio, List<string> avisos)
    {
        if (op.Comissoes == 0)
            return 0;
        var moeda = op.MoedaComissoes ?? "EUR";
        if (cambio(moeda, data) is { } taxa and > 0)
            return op.Comissoes / taxa;
        avisos.Add(T($"Sem câmbio de {moeda} para {data:dd/MM/yyyy} ({op.Nome}): as comissões desta operação ficaram fora das despesas.", $"No {moeda} exchange rate for {data:dd/MM/yyyy} ({op.Nome}): this transaction's fees were left out of expenses."));
        return 0;
    }

    private static decimal? Euros(decimal valor, string moeda, DateOnly data, Cambio cambio, List<string> avisos, Operacao op)
    {
        if (cambio(moeda, data) is { } taxa and > 0)
            return valor / taxa;
        avisos.Add(T($"Sem câmbio de {moeda} para {data:dd/MM/yyyy} ({op.Nome}): o valor desta operação ficou fora das contas.", $"No {moeda} exchange rate for {data:dd/MM/yyyy} ({op.Nome}): this transaction was left out."));
        return null;
    }

    private static decimal Arredondar(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// País da fonte a partir das duas primeiras letras do ISIN, e o código numérico usado no Anexo J
/// (ISO 3166-1 numérico; confirmar na tabela de países da declaração).
/// </summary>
public static class Paises
{
    private static readonly Dictionary<string, string> Codigos = new()
    {
        ["US"] = "840", ["IE"] = "372", ["DE"] = "276", ["FR"] = "250", ["NL"] = "528", ["GB"] = "826", ["LU"] = "442",
        ["ES"] = "724", ["IT"] = "380", ["BE"] = "056", ["CH"] = "756", ["DK"] = "208", ["SE"] = "752", ["FI"] = "246",
        ["NO"] = "578", ["AT"] = "040", ["CA"] = "124", ["JP"] = "392", ["AU"] = "036", ["KY"] = "136", ["BM"] = "060",
        ["JE"] = "832", ["GG"] = "831", ["HK"] = "344", ["CN"] = "156", ["TW"] = "158", ["KR"] = "410", ["BR"] = "076",
        ["PT"] = "620",
    };

    /// <summary>País do ISIN, ou "??" se não for um ISIN (ex.: um ticker como "AAPL", que daria "AA").</summary>
    public static string Iso(string isin) => IsinValido(isin) ? isin[..2].ToUpperInvariant() : "??";

    /// <summary>Formato ISIN (ISO 6166): 2 letras do país, 9 caracteres alfanuméricos e 1 dígito de controlo.</summary>
    public static bool IsinValido(string? texto) =>
        texto is { Length: 12 } t && char.IsAsciiLetter(t[0]) && char.IsAsciiLetter(t[1])
        && t[2..11].All(char.IsAsciiLetterOrDigit) && char.IsAsciiDigit(t[11]);

    public static string Codigo(string isin) => CodigoDeIso(Iso(isin));

    public static string CodigoDeIso(string iso) => Codigos.GetValueOrDefault(iso, "?");
}
