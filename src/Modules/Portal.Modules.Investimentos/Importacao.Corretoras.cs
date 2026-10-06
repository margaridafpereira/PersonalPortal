using static Portal.Core.Idioma;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Portal.Modules.Investimentos;

/// <summary>
/// Leitores próprios de corretoras. Os formatos vêm de ficheiros de exemplo públicos e de conversores open source
/// (docs/investimentos/02-formatos-corretoras.md), não de ficheiros da pessoa: cada importação avisa para confirmar
/// as primeiras operações com o extrato.
/// </summary>
public static partial class Importacao
{
    private static string AvisoLeitorPublico => T(
        "Leitor da {0} feito a partir de ficheiros de exemplo públicos: confirma as primeiras operações com o extrato da corretora e avisa se algo não bater certo.",
        "The {0} reader was built from public sample files: check the first transactions against your broker statement and report anything that does not match.");

    private static string FicheiroVazio => T("O ficheiro está vazio.", "The file is empty.");

    private static string? DetetarCorretora(string[] cabecalho, Dictionary<string, int> colunas)
    {
        bool Tem(params string[] nomes) => nomes.All(colunas.ContainsKey);

        // Degiro, extrato de conta: os nomes das colunas mudam com a língua, mas a estrutura não
        // (ISIN na 5.ª coluna e as colunas 9 e 11 sem nome, com os valores das moedas).
        if (cabecalho.Length >= 12 && cabecalho[4] == "ISIN" && cabecalho[8] == "" && cabecalho[10] == "")
            return "degiro";
        if (Tem("Ticker", "Type", "Quantity", "Price per share", "Total Amount", "Currency"))
            return "revolut";
        if (Tem("ID", "Type", "Time", "Symbol", "Comment", "Amount"))
            return "xtb";
        if (Tem("Date", "Type", "Details", "Amount", "Units", "Position ID"))
            return "etoro";
        if (Tem("Buy/Sell", "ISIN", "Quantity", "TradePrice", "CurrencyPrimary"))
            return "ibkr";
        if (Tem("Type", "ISIN", "Description", "Amount", "CurrencyPrimary"))
            return "ibkr-dividendos";
        return null;
    }

    // ------------------------------------------------------------------ Degiro (extrato de conta)

    /// <summary>"Koop 1 @ 33,9 USD", "Compra 10 @ 12,5 EUR", "Achat 6 QT GROUP OYJ@79,96 EUR (FI…)", "Kauf 10 zu je 50 EUR".</summary>
    private static readonly Regex NegocioDegiro = new(
        @"^\s*(?<verbo>[^\d\s]+)\s+(?<qtd>[\d.,]+)\s*(?<nome>.*?)\s*(?:@|zu je)\s*(?<preco>[\d.,]+)\s*(?<moeda>[A-Z]{3})", RegexOptions.Compiled);

    private static readonly string[] VerbosCompra = ["koop", "compra", "buy", "kauf", "achat", "acquisto"];
    private static readonly string[] VerbosVenda = ["verkoop", "venda", "venta", "sell", "verkauf", "vente", "vendita"];

    /// <summary>Palavras das linhas de custos de transação, nas línguas do extrato (lista do conversor Export-To-Ghostfolio).</summary>
    private static readonly string[] CustosDegiro =
        ["en/of", "and/or", "und/oder", "e/o", "et/ou", "y/o", "transactiekosten", "custos de transação", "comissões de transação", "transaction costs", "courtage"];

    private static readonly string[] PalavrasImposto = ["belasting", "imposto", "tax", "steuer", "impôt", "impot", "impuesto", "ritenuta", "retenção", "retencion"];

    /// <summary>
    /// Degiro: Caixa de entrada → Extrato de conta (Account.csv). Tem tudo: compras e vendas (na descrição),
    /// custos de transação (ligados à ordem) e dividendos com a retenção em linhas separadas.
    /// </summary>
    public static ResultadoImportacao Degiro(string conteudo)
    {
        const int Data = 0, Hora = 1, Produto = 3, Isin = 4, Descricao = 5, Moeda = 7, Valor = 8, Ordem = 11;
        var linhas = Csv.Ler(conteudo, Csv.DetetarSeparador(conteudo));
        var operacoes = new List<Operacao>();
        var avisos = new List<string> { string.Format(AvisoLeitorPublico, "Degiro") };
        var dividendos = new Dividendos();
        var ignoradas = 0;

        static string C(string[] l, int i) => i < l.Length ? l[i].Trim() : "";

        // Primeiro os custos, por ordem: aparecem em linhas próprias com o mesmo id da ordem.
        var custos = new Dictionary<string, (decimal Valor, string Moeda)>();
        foreach (var l in linhas.Skip(1))
        {
            var d = C(l, Descricao).ToLowerInvariant();
            if (C(l, Ordem) is { Length: > 0 } ordem && !NegocioDegiro.IsMatch(C(l, Descricao)) && CustosDegiro.Any(d.Contains))
                custos[ordem] = (custos.GetValueOrDefault(ordem).Valor + Math.Abs(Numero(C(l, Valor))), C(l, Moeda).ToUpperInvariant());
        }

        var porOrdem = new Dictionary<string, int>();
        foreach (var (l, n) in linhas.Skip(1).Select((l, i) => (l, i + 2)))
        {
            var descricao = C(l, Descricao);
            var d = descricao.ToLowerInvariant();
            if (!TentarData($"{C(l, Data)} {C(l, Hora)}", out var momento) && !TentarData(C(l, Data), out momento))
            {
                avisos.Add(DataInvalida(n, C(l, Data)));
                continue;
            }

            if (NegocioDegiro.Match(descricao) is { Success: true } m)
            {
                var verbo = m.Groups["verbo"].Value.ToLowerInvariant();
                var valor = Numero(C(l, Valor));
                var tipo = VerbosCompra.Contains(verbo) ? TipoOperacao.Compra
                    : VerbosVenda.Contains(verbo) ? TipoOperacao.Venda
                    : valor < 0 ? TipoOperacao.Compra : TipoOperacao.Venda; // dinheiro que sai = compra

                var ordem = C(l, Ordem);
                var k = porOrdem[ordem] = porOrdem.GetValueOrDefault(ordem) + 1;
                var op = new Operacao
                {
                    Tipo = tipo,
                    Momento = momento,
                    Ativo = C(l, Isin).ToUpperInvariant(),
                    Nome = C(l, Produto),
                    TipoAtivo = Classificacao.Sugerir(C(l, Produto)),
                    Moeda = m.Groups["moeda"].Value,
                    Corretora = "Degiro",
                    IdExterno = ordem.Length > 0 ? $"degiro:{ordem}:{k}" : "degiro:" + Hash(l),
                };
                // Os custos da ordem ficam na primeira execução (uma ordem pode ser executada em várias partes).
                if (k == 1 && custos.TryGetValue(ordem, out var custo))
                    (op.Comissoes, op.MoedaComissoes) = (custo.Valor, custo.Moeda);
                if (LerValores(op, m.Groups["qtd"].Value, m.Groups["preco"].Value, n, avisos))
                    operacoes.Add(op);
            }
            else if (d.Contains("divid") && PalavrasImposto.Any(d.Contains))
                dividendos.Imposto(C(l, Isin), C(l, Produto), momento, C(l, Moeda), -Numero(C(l, Valor)));
            else if (d.Contains("divid"))
                dividendos.Bruto(C(l, Isin), C(l, Produto), momento, C(l, Moeda), Numero(C(l, Valor)));
            else if (!CustosDegiro.Any(d.Contains))
                ignoradas++;
        }

        operacoes.AddRange(dividendos.Operacoes("Degiro", avisos));
        if (ignoradas > 0)
            avisos.Add(T($"Ignoradas {ignoradas} linha(s) que não são compras, vendas, custos nem dividendos (depósitos, câmbios, juros…).", $"Skipped {ignoradas} line(s) that are not buys, sells, costs or dividends (deposits, FX, interest…)."));
        return new(operacoes, avisos);
    }

    // ------------------------------------------------------------------ Revolut

    /// <summary>Revolut: Investir → Mais → Documentos → Extrato de conta. Colunas Date, Ticker, Type, Quantity, Price per share, Total Amount, Currency.</summary>
    public static ResultadoImportacao Revolut(string conteudo)
    {
        var linhas = Csv.Ler(conteudo, Csv.DetetarSeparador(conteudo));
        var col = Csv.Colunas(linhas[0]);
        var operacoes = new List<Operacao>();
        var avisos = new List<string> { string.Format(AvisoLeitorPublico, "Revolut") };
        var ignoradas = new Dictionary<string, int>();
        var desdobramentos = 0;

        foreach (var (l, n) in linhas.Skip(1).Select((l, i) => (l, i + 2)))
        {
            string C(string nome) => col.TryGetValue(nome, out var i) && i < l.Length ? l[i].Trim() : "";

            var tipoTexto = C("Type").ToUpperInvariant();
            TipoOperacao? tipo = tipoTexto.StartsWith("BUY") ? TipoOperacao.Compra
                : tipoTexto.StartsWith("SELL") ? TipoOperacao.Venda
                : tipoTexto.StartsWith("DIVIDEND") ? TipoOperacao.Dividendo
                : null;
            if (tipoTexto.Contains("SPLIT"))
                desdobramentos++;
            if (tipo is null)
            {
                ignoradas[tipoTexto] = ignoradas.GetValueOrDefault(tipoTexto) + 1;
                continue;
            }
            if (!TentarData(C("Date"), out var momento))
            {
                avisos.Add(DataInvalida(n, C("Date")));
                continue;
            }

            // O ISIN só vem em alguns extratos; sem ele fica o ticker e a pessoa indica o ISIN depois.
            var ativo = Paises.IsinValido(C("ISIN")) ? C("ISIN") : C("Ticker");
            var op = new Operacao
            {
                Tipo = tipo.Value, Momento = momento, Ativo = ativo.ToUpperInvariant(), Nome = C("Ticker"),
                TipoAtivo = Classificacao.Sugerir(C("Ticker")), Moeda = C("Currency").ToUpperInvariant(),
                Corretora = "Revolut", IdExterno = "revolut:" + Hash(l),
            };
            var aceite = tipo == TipoOperacao.Dividendo
                ? Aceitar(op, 1, Numero(C("Total Amount")), n, avisos) // o extrato só tem o total do dividendo
                : LerValores(op, C("Quantity"), C("Price per share"), n, avisos);
            if (aceite)
                operacoes.Add(op);
        }

        avisos.AddRange(ignoradas.Where(kv => !kv.Key.Contains("SPLIT")).Select(kv => Ignoradas(kv.Value, kv.Key)));
        if (desdobramentos > 0)
            avisos.Add(T($"Há {desdobramentos} desdobramento(s) de ações (stock split) que o portal ainda não trata: o FIFO desses títulos pode ficar errado. Confirma as quantidades.", $"There are {desdobramentos} stock split(s) the portal does not handle yet: FIFO for those holdings may be wrong. Check the quantities."));
        if (operacoes.Any(o => o.Tipo == TipoOperacao.Dividendo))
            avisos.Add(T("Revolut: o extrato só traz o valor total de cada dividendo, que pode já vir sem a retenção na fonte. Confirma o bruto e o imposto retido no extrato de dividendos da Revolut.", "Revolut: the statement only gives each dividend's total, which may already be net of withholding. Check the gross amount and tax withheld in Revolut's dividend statement."));
        return new(operacoes, avisos);
    }

    // ------------------------------------------------------------------ XTB

    /// <summary>"OPEN BUY 34/42.5658 @ 11.7480" (34 de uma ordem de 42.5658) ou "CLOSE BUY 1 @ 26.280".</summary>
    private static readonly Regex NegocioXtb = new(@"(?:OPEN|CLOSE)\s+BUY\s+(?<qtd>[\d.]+)(?:/[\d.]+)?\s*@\s*(?<preco>[\d.]+)", RegexOptions.Compiled);

    /// <summary>
    /// XTB: Histórico da conta → Operações de caixa → Exportar (CSV ou Excel). Colunas ID, Type, Time, Symbol, Comment, Amount.
    /// "Amount" está na moeda da conta; o preço usado é Amount / quantidade, já nessa moeda (inclui o câmbio que a XTB aplicou).
    /// </summary>
    public static ResultadoImportacao Xtb(string conteudo, string moedaConta = "EUR")
    {
        var linhas = Csv.Ler(conteudo, Csv.DetetarSeparador(conteudo));
        var col = Csv.Colunas(linhas[0]);
        var operacoes = new List<Operacao>();
        var avisos = new List<string>
        {
            string.Format(AvisoLeitorPublico, "XTB"),
            T($"XTB: o ficheiro não diz a moeda da conta; os valores foram lidos em {moedaConta}. Se a tua conta XTB for noutra moeda, avisa.", $"XTB: the file does not state the account currency; amounts were read as {moedaConta}. If your XTB account uses another currency, say so."),
        };
        var dividendos = new Dividendos();
        var ignoradas = new Dictionary<string, int>();

        foreach (var (l, n) in linhas.Skip(1).Select((l, i) => (l, i + 2)))
        {
            string C(string nome) => col.TryGetValue(nome, out var i) && i < l.Length ? l[i].Trim() : "";

            var tipoTexto = C("Type");
            var t = tipoTexto.ToLowerInvariant();
            if (!TentarData(C("Time"), out var momento))
            {
                if (tipoTexto.Length > 0)
                    avisos.Add(DataInvalida(n, C("Time")));
                continue;
            }
            var simbolo = C("Symbol").ToUpperInvariant();
            var valor = Numero(C("Amount"));

            if (t.Contains("withholding tax"))
                dividendos.Imposto(simbolo, simbolo, momento, moedaConta, -valor);
            else if (t.Contains("divid")) // "Dividend" ou "DIVIDENT"
                dividendos.Bruto(simbolo, simbolo, momento, moedaConta, valor);
            else if (t.Contains("purchase") || t.Contains("sale"))
            {
                if (NegocioXtb.Match(C("Comment")) is not { Success: true } m || !TentarNumero(m.Groups["qtd"].Value, out var qtd) || qtd == 0)
                {
                    avisos.Add(T($"Linha {n}: não consegui ler a quantidade em \"{C("Comment")}\"; linha ignorada.", $"Line {n}: could not read the quantity in \"{C("Comment")}\"; line skipped."));
                    continue;
                }
                var op = new Operacao
                {
                    Tipo = t.Contains("purchase") ? TipoOperacao.Compra : TipoOperacao.Venda,
                    Momento = momento, Ativo = simbolo, Nome = simbolo, TipoAtivo = Classificacao.Sugerir(simbolo),
                    Moeda = moedaConta, Corretora = "XTB",
                    IdExterno = C("ID") is { Length: > 0 } id ? "xtb:" + id : "xtb:" + Hash(l),
                };
                if (Aceitar(op, qtd, Math.Abs(valor) / qtd, n, avisos))
                    operacoes.Add(op);
            }
            else
                ignoradas[tipoTexto] = ignoradas.GetValueOrDefault(tipoTexto) + 1;
        }

        operacoes.AddRange(dividendos.Operacoes("XTB", avisos));
        avisos.AddRange(ignoradas.Select(kv => Ignoradas(kv.Value, kv.Key, explicar: true)));
        return new(operacoes, avisos);
    }

    // ------------------------------------------------------------------ eToro

    /// <summary>
    /// eToro: Portefólio → Histórico → Extrato de conta → Excel, folha "Account Activity".
    /// Colunas Date, Type, Details ("AMD/USD"), Amount, Units, Position ID, Asset type. Valores na moeda da conta (USD).
    /// </summary>
    public static ResultadoImportacao EToro(string conteudo)
    {
        var linhas = Csv.Ler(conteudo, Csv.DetetarSeparador(conteudo));
        var col = Csv.Colunas(linhas[0]);
        var operacoes = new List<Operacao>();
        var avisos = new List<string>
        {
            string.Format(AvisoLeitorPublico, "eToro"),
            T("eToro: os valores estão na moeda da conta (USD), convertidos para euros ao câmbio do dia de cada operação.", "eToro: amounts are in the account currency (USD), converted to euros at each day's rate."),
        };
        var ignoradas = new Dictionary<string, int>();
        var outrosAtivos = 0;

        foreach (var (l, n) in linhas.Skip(1).Select((l, i) => (l, i + 2)))
        {
            string C(string nome) => col.TryGetValue(nome, out var i) && i < l.Length ? l[i].Trim() : "";

            var t = C("Type").ToLowerInvariant();
            TipoOperacao? tipo = t.Contains("open position") ? TipoOperacao.Compra
                : t.Contains("position closed") ? TipoOperacao.Venda
                : t.Contains("dividend") ? TipoOperacao.Dividendo
                : null;
            if (tipo is null)
            {
                ignoradas[C("Type")] = ignoradas.GetValueOrDefault(C("Type")) + 1;
                continue;
            }
            // CFD e cripto têm regras fiscais diferentes das ações: ficam de fora do Anexo J aqui.
            if (C("Asset type") is { Length: > 0 } classe && classe is not ("Stocks" or "ETF" or "ETFs"))
            {
                outrosAtivos++;
                continue;
            }
            if (!TentarData(C("Date"), out var momento))
            {
                avisos.Add(DataInvalida(n, C("Date")));
                continue;
            }

            var simbolo = C("Details").Split('/')[0].Trim().ToUpperInvariant();
            var valor = Math.Abs(Numero(C("Amount")));
            var op = new Operacao
            {
                Tipo = tipo.Value, Momento = momento, Ativo = simbolo, Nome = simbolo, TipoAtivo = Classificacao.Sugerir(simbolo),
                Moeda = "USD", Corretora = "eToro",
                IdExterno = $"etoro:{C("Position ID")}:{tipo}:{momento:yyyyMMddHHmmss}",
            };
            bool aceite;
            if (tipo == TipoOperacao.Dividendo)
                aceite = Aceitar(op, 1, valor, n, avisos);
            else if (TentarNumero(C("Units"), out var unidades) && unidades != 0)
                aceite = Aceitar(op, unidades, valor / Math.Abs(unidades), n, avisos);
            else
            {
                avisos.Add(T($"Linha {n}: unidades \"{C("Units")}\" inválidas; linha ignorada.", $"Line {n}: invalid units \"{C("Units")}\"; line skipped."));
                aceite = false;
            }
            if (aceite)
                operacoes.Add(op);
        }

        avisos.AddRange(ignoradas.Select(kv => Ignoradas(kv.Value, kv.Key)));
        if (outrosAtivos > 0)
            avisos.Add(T($"Ignoradas {outrosAtivos} linha(s) de CFD ou cripto: têm regras fiscais próprias e não entram neste cálculo.", $"Skipped {outrosAtivos} CFD or crypto line(s): they have their own tax rules and are not part of this calculation."));
        if (operacoes.Any(o => o.Tipo == TipoOperacao.Dividendo))
            avisos.Add(T("eToro: os dividendos no extrato podem já vir sem a retenção na fonte. Confirma o bruto e o imposto retido no separador de dividendos do extrato.", "eToro: dividends in the statement may already be net of withholding. Check the gross amount and tax withheld in the statement's dividends tab."));
        return new(operacoes, avisos);
    }

    // ------------------------------------------------------------------ Interactive Brokers (Flex Query)

    /// <summary>
    /// IBKR: Relatórios → Flex Queries, secção "Trades" em CSV, com os campos Buy/Sell, TradeDate, ISIN, Description,
    /// Quantity, TradePrice, CurrencyPrimary, IBCommission, IBCommissionCurrency e TradeID.
    /// </summary>
    public static ResultadoImportacao IbkrNegocios(string conteudo)
    {
        var linhas = Csv.Ler(conteudo, Csv.DetetarSeparador(conteudo));
        var col = Csv.Colunas(linhas[0]);
        var operacoes = new List<Operacao>();
        var avisos = new List<string> { string.Format(AvisoLeitorPublico, "Interactive Brokers") };
        var semIsin = 0;

        foreach (var (l, n) in linhas.Skip(1).Select((l, i) => (l, i + 2)))
        {
            string C(params string[] nomes) =>
                nomes.Select(nome => col.TryGetValue(nome, out var i) && i < l.Length ? l[i].Trim() : "").FirstOrDefault(v => v.Length > 0) ?? "";

            var lado = C("Buy/Sell").ToUpperInvariant();
            if (lado is not ("BUY" or "SELL"))
                continue;
            // Sem ISIN são conversões de moeda (EUR.USD), não títulos.
            if (C("ISIN") is not { Length: > 0 } isin)
            {
                semIsin++;
                continue;
            }
            if (!TentarData(C("DateTime", "TradeDate", "Date/Time"), out var momento))
            {
                avisos.Add(DataInvalida(n, C("DateTime", "TradeDate", "Date/Time")));
                continue;
            }

            var nome = C("Description", "Symbol") is { Length: > 0 } d ? d : isin;
            var op = new Operacao
            {
                Tipo = lado == "BUY" ? TipoOperacao.Compra : TipoOperacao.Venda,
                Momento = momento, Ativo = isin.ToUpperInvariant(), Nome = nome, TipoAtivo = Classificacao.Sugerir(nome),
                Moeda = C("CurrencyPrimary").ToUpperInvariant(),
                Comissoes = Math.Abs(Numero(C("IBCommission"))),
                MoedaComissoes = C("IBCommissionCurrency", "CurrencyPrimary").ToUpperInvariant(),
                Corretora = "Interactive Brokers",
                IdExterno = "ibkr:" + (C("TradeID", "IBExecID", "TransactionID") is { Length: > 0 } id ? id : Hash(l)),
            };
            if (LerValores(op, C("Quantity"), C("TradePrice"), n, avisos))
                operacoes.Add(op);
        }

        if (semIsin > 0)
            avisos.Add($"Ignoradas {semIsin} linha(s) sem ISIN (conversões de moeda).");
        return new(operacoes, avisos);
    }

    /// <summary>
    /// IBKR: Flex Query, secção "Cash Transactions" em CSV, com os campos Type, SettleDate, ISIN, Description, Amount e CurrencyPrimary.
    /// Os dividendos e a retenção vêm em linhas separadas ("Dividends" e "Withholding Tax").
    /// </summary>
    public static ResultadoImportacao IbkrDividendos(string conteudo)
    {
        var linhas = Csv.Ler(conteudo, Csv.DetetarSeparador(conteudo));
        var col = Csv.Colunas(linhas[0]);
        var avisos = new List<string> { string.Format(AvisoLeitorPublico, "Interactive Brokers") };
        var dividendos = new Dividendos();
        var reembolsos = 0;

        foreach (var (l, n) in linhas.Skip(1).Select((l, i) => (l, i + 2)))
        {
            string C(params string[] nomes) =>
                nomes.Select(nome => col.TryGetValue(nome, out var i) && i < l.Length ? l[i].Trim() : "").FirstOrDefault(v => v.Length > 0) ?? "";

            var tipo = C("Type").ToLowerInvariant();
            if (!tipo.Contains("dividend") && !tipo.Contains("withholding"))
                continue;
            if (C("Description").Contains("Return of Capital", StringComparison.OrdinalIgnoreCase))
            {
                // Reembolso de capital não é dividendo: reduz o custo de aquisição.
                reembolsos++;
                continue;
            }
            if (!TentarData(C("SettleDate", "PayDate", "ReportDate", "DateTime", "Date/Time"), out var momento))
            {
                avisos.Add($"Linha {n}: data inválida; linha ignorada.");
                continue;
            }

            var isin = C("ISIN").ToUpperInvariant();
            var nome = C("Description").Split('(')[0].Trim() is { Length: > 0 } d ? d : isin;
            var moeda = C("CurrencyPrimary").ToUpperInvariant();
            var valor = Numero(C("Amount"));
            if (tipo.Contains("withholding"))
                dividendos.Imposto(isin, nome, momento, moeda, -valor);
            else
                dividendos.Bruto(isin, nome, momento, moeda, valor);
        }

        if (reembolsos > 0)
            avisos.Add($"Ignoradas {reembolsos} linha(s) de reembolso de capital (\"Return of Capital\"): não são dividendos. Confirma com um contabilista como tratar.");
        var operacoes = dividendos.Operacoes("Interactive Brokers", avisos).ToList();
        return new(operacoes, avisos);
    }

    // ------------------------------------------------------------------ Utilitários

    private static string Hash(string[] linha) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', linha))))[..24];

    /// <summary>
    /// Junta dividendos e retenções que vêm em linhas separadas (mesmo título, dia e moeda) numa só operação:
    /// quantidade 1 e preço = valor bruto, porque estes extratos só trazem o total.
    /// </summary>
    private sealed class Dividendos
    {
        private sealed class Grupo
        {
            public string Nome = "";
            public DateTime Momento;
            public decimal Bruto;
            public decimal Imposto;
        }

        private readonly Dictionary<(string Ativo, DateOnly Dia, string Moeda), Grupo> _grupos = [];

        private Grupo Obter(string ativo, string nome, DateTime momento, string moeda)
        {
            var chave = (ativo.ToUpperInvariant(), DateOnly.FromDateTime(momento), moeda.ToUpperInvariant());
            if (!_grupos.TryGetValue(chave, out var g))
                _grupos[chave] = g = new Grupo { Nome = nome, Momento = momento };
            return g;
        }

        public void Bruto(string ativo, string nome, DateTime momento, string moeda, decimal valor) => Obter(ativo, nome, momento, moeda).Bruto += valor;

        public void Imposto(string ativo, string nome, DateTime momento, string moeda, decimal valor) => Obter(ativo, nome, momento, moeda).Imposto += valor;

        public IEnumerable<Operacao> Operacoes(string corretora, List<string> avisos)
        {
            foreach (var ((ativo, dia, moeda), g) in _grupos)
            {
                if (g.Bruto <= 0)
                {
                    avisos.Add($"{g.Nome} ({dia:dd/MM/yyyy}): há imposto retido sem o dividendo correspondente no ficheiro; ficou de fora.");
                    continue;
                }
                var op = new Operacao
                {
                    Tipo = TipoOperacao.Dividendo, Momento = g.Momento, Ativo = ativo, Nome = g.Nome, TipoAtivo = Classificacao.Sugerir(g.Nome),
                    Quantidade = 1, PrecoUnitario = g.Bruto, Moeda = moeda,
                    RetencaoFonte = Math.Max(0, g.Imposto), MoedaRetencao = moeda,
                    Corretora = corretora,
                    IdExterno = $"{corretora.ToLowerInvariant()}:dividendo:{ativo}:{dia:yyyyMMdd}:{moeda}:{g.Bruto.ToString(CultureInfo.InvariantCulture)}",
                };
                if (op.Ativo.Length is > 0 and <= 20 && op.Moeda.Length == 3)
                    yield return op;
                else
                    avisos.Add($"{g.Nome} ({dia:dd/MM/yyyy}): dividendo sem título ou moeda válidos; ficou de fora.");
            }
        }
    }
}
