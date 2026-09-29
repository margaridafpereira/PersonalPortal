using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Portal.Modules.Investimentos;

public sealed record ResultadoImportacao(IReadOnlyList<Operacao> Operacoes, IReadOnlyList<string> Avisos);

/// <summary>
/// Que coluna do ficheiro corresponde a cada campo (índices a partir de 0) e como ler o tipo de operação.
/// Serve para qualquer corretora: o portal sugere, a pessoa confirma.
/// </summary>
public sealed record Mapeamento(
    int Data,
    int? Hora,
    int? Tipo,
    int Ativo,
    int? Nome,
    int Quantidade,
    int Preco,
    int? Moeda,
    string? MoedaFixa,
    int? Comissoes,
    int? Retencao,
    int? MoedaRetencao,
    int? IdExterno,
    /// <summary>Valor da coluna de tipo → operação; null ou valor ausente = ignorar essas linhas.</summary>
    Dictionary<string, TipoOperacao?> ValoresTipo,
    /// <summary>Sem coluna de tipo (ex.: Degiro): quantidade positiva = compra, negativa = venda.</summary>
    bool VendaSeQuantidadeNegativa);

/// <summary>
/// Resultado da análise de um ficheiro, para o ecrã de mapeamento. <see cref="ValoresPorColuna"/> tem, para cada coluna,
/// os valores distintos e o tipo sugerido: se a pessoa mudar a coluna do tipo, o ecrã já sabe o que mostrar.
/// </summary>
public sealed record AnaliseFicheiro(
    string Formato, char Separador, IReadOnlyList<string> Colunas, IReadOnlyList<string[]> Amostra, Mapeamento? Sugestao, IReadOnlyList<string> CamposEmFalta,
    IReadOnlyList<Dictionary<string, TipoOperacao?>> ValoresPorColuna);

/// <summary>
/// Leitura dos ficheiros das corretoras. Formatos com leitor próprio: "trading212", "modelo" (o modelo do portal)
/// e as corretoras em Importacao.Corretoras.cs; tudo o resto é "universal" (qualquer CSV, com mapeamento de colunas).
/// </summary>
public static partial class Importacao
{
    public const string CabecalhoModelo = "data,tipo,isin,nome,quantidade,preco,moeda,comissoes_eur,retencao,moeda_retencao";

    /// <summary>Valores distintos por coluna no ecrã de mapeamento. Uma coluna com mais do que isto não é a do tipo.</summary>
    public const int MaxValoresTipo = 50;

    /// <summary>
    /// Reconhece o formato pelo cabeçalho: a pessoa só escolhe o ficheiro, sem ter de saber de que "tipo" ele é.
    /// Os formatos conhecidos têm leitor próprio; tudo o resto passa pelo mapeamento de colunas ("universal").
    /// </summary>
    public static string Detetar(string conteudo)
    {
        var primeira = conteudo.TrimStart('\uFEFF').Split('\n')[0];
        var linhas = Csv.Ler(primeira, Csv.DetetarSeparador(primeira));
        if (linhas.Count == 0)
            return "universal";
        var cabecalho = linhas[0].Select(c => c.Trim()).ToArray();
        var colunas = Csv.Colunas(cabecalho);
        if (ColunasTrading212.All(colunas.ContainsKey))
            return "trading212";
        if (CabecalhoModelo.Split(',').All(colunas.ContainsKey))
            return "modelo";
        return DetetarCorretora(cabecalho, colunas) ?? "universal";
    }

    public static ResultadoImportacao Importar(string formato, string conteudo, Mapeamento? mapeamento = null) => formato switch
    {
        "auto" => Importar(Detetar(conteudo), conteudo, mapeamento),
        "trading212" => Trading212(conteudo),
        "modelo" => Modelo(conteudo),
        "degiro" => Degiro(conteudo),
        "revolut" => Revolut(conteudo),
        "xtb" => Xtb(conteudo),
        "etoro" => EToro(conteudo),
        "ibkr" => IbkrNegocios(conteudo),
        "ibkr-dividendos" => IbkrDividendos(conteudo),
        "universal" when mapeamento is not null => Universal(conteudo, mapeamento),
        "universal" => Analisar(conteudo).Sugestao is { } sugerido
            ? Universal(conteudo, sugerido)
            : new([], ["Não foi possível reconhecer as colunas; indica-as à mão."]),
        _ => new([], [$"Formato desconhecido: {formato}."]),
    };

    // ------------------------------------------------------------------ Trading 212

    /// <summary>Taxas que a Trading 212 cobra por operação; cada uma tem a moeda numa coluna "Currency (…)".</summary>
    private static readonly string[] ColunasTrading212 = ["Action", "Time", "No. of shares", "Price / share", "Currency (Price / share)"];

    private static readonly string[] TaxasTrading212 = ["Currency conversion fee", "Transaction fee", "Finra fee", "Stamp duty reserve tax"];

    /// <summary>
    /// Trading 212: Menu → Histórico → Exportar. Colunas relevantes: Action, Time, ISIN, Name, No. of shares,
    /// Price / share, Currency (Price / share), Withholding tax, Currency (Withholding tax), taxas (ver <see cref="TaxasTrading212"/>), ID.
    /// </summary>
    public static ResultadoImportacao Trading212(string conteudo)
    {
        var linhas = Csv.Ler(conteudo, ',');
        if (linhas.Count == 0)
            return new([], ["O ficheiro está vazio."]);

        var col = Csv.Colunas(linhas[0]);
        var faltam = ColunasTrading212.Where(c => !col.ContainsKey(c)).ToList();
        if (faltam.Count > 0)
            return new([], [$"Não parece um ficheiro da Trading 212: faltam as colunas {string.Join(", ", faltam)}. Experimenta \"Outra corretora\"."]);

        var operacoes = new List<Operacao>();
        var avisos = new List<string>();
        var ignoradas = new Dictionary<string, int>();
        var taxasNaoEur = new Dictionary<string, decimal>();
        foreach (var (l, n) in linhas.Skip(1).Select((l, i) => (l, i + 2)))
        {
            string C(string nome) => col.TryGetValue(nome, out var i) && i < l.Length ? l[i].Trim() : "";

            var acao = C("Action");
            TipoOperacao? tipo = acao.EndsWith(" buy", StringComparison.OrdinalIgnoreCase) ? TipoOperacao.Compra
                : acao.EndsWith(" sell", StringComparison.OrdinalIgnoreCase) ? TipoOperacao.Venda
                : acao.StartsWith("Dividend", StringComparison.OrdinalIgnoreCase) ? TipoOperacao.Dividendo
                : null;
            if (tipo is null)
            {
                ignoradas[acao] = ignoradas.GetValueOrDefault(acao) + 1;
                continue;
            }

            if (!TentarData(C("Time"), out var momento))
            {
                avisos.Add($"Linha {n}: data inválida \"{C("Time")}\"; linha ignorada.");
                continue;
            }

            // As taxas vêm na moeda da conta (normalmente EUR) e são convertidas no cálculo, como o preço.
            // Uma operação guarda as comissões numa só moeda: se tiver taxas em duas, a de euros fica e a outra é avisada.
            var taxasPorMoeda = new Dictionary<string, decimal>();
            foreach (var taxa in TaxasTrading212)
            {
                var valor = Math.Abs(Numero(C(taxa)));
                if (valor == 0)
                    continue;
                var moedaTaxa = C($"Currency ({taxa})").ToUpperInvariant() is { Length: 3 } mt ? mt : "EUR";
                taxasPorMoeda[moedaTaxa] = taxasPorMoeda.GetValueOrDefault(moedaTaxa) + valor;
            }
            var moedaComissoes = taxasPorMoeda.Count == 1 ? taxasPorMoeda.Keys.First() : "EUR";
            foreach (var (moedaTaxa, valor) in taxasPorMoeda.Where(kv => kv.Key != moedaComissoes))
                taxasNaoEur[moedaTaxa] = taxasNaoEur.GetValueOrDefault(moedaTaxa) + valor;

            var nome = C("Name");
            var op = new Operacao
            {
                Tipo = tipo.Value,
                Momento = momento,
                Ativo = (C("ISIN") is { Length: > 0 } isin ? isin : C("Ticker")).ToUpperInvariant(),
                Nome = nome,
                TipoAtivo = Classificacao.Sugerir(nome),
                Moeda = C("Currency (Price / share)").ToUpperInvariant(),
                Comissoes = taxasPorMoeda.GetValueOrDefault(moedaComissoes),
                MoedaComissoes = moedaComissoes,
                RetencaoFonte = Math.Abs(Numero(C("Withholding tax"))),
                MoedaRetencao = C("Currency (Withholding tax)") is { Length: > 0 } m ? m.ToUpperInvariant() : null,
                Corretora = "Trading 212",
                IdExterno = C("ID") is { Length: > 0 } id ? id : null,
            };
            if (LerValores(op, C("No. of shares"), C("Price / share"), n, avisos))
                operacoes.Add(op);
        }

        avisos.AddRange(ignoradas.Select(kv => $"Ignoradas {kv.Value} linha(s) \"{kv.Key}\" (não são compras, vendas nem dividendos)."));
        avisos.AddRange(taxasNaoEur.Select(kv =>
            $"Há {kv.Value.ToString("0.00", CultureInfo.InvariantCulture)} {kv.Key} de taxas em operações que também tinham taxas em euros; ficaram fora das despesas. Confirma as despesas."));
        if (!col.ContainsKey("Currency conversion fee"))
            avisos.Add("O ficheiro não tem a coluna de comissões de câmbio; confirma as despesas.");
        return new(operacoes, avisos);
    }

    // ------------------------------------------------------------------ Modelo do portal

    public static ResultadoImportacao Modelo(string conteudo)
    {
        var linhas = Csv.Ler(conteudo, ',');
        if (linhas.Count == 0)
            return new([], ["O ficheiro está vazio."]);

        var col = Csv.Colunas(linhas[0]);
        var faltam = CabecalhoModelo.Split(',').Where(c => !col.ContainsKey(c)).ToList();
        if (faltam.Count > 0)
            return new([], [$"Faltam as colunas {string.Join(", ", faltam)}. Usa o modelo do portal."]);

        var operacoes = new List<Operacao>();
        var avisos = new List<string>();
        foreach (var (l, n) in linhas.Skip(1).Select((l, i) => (l, i + 2)))
        {
            string C(string nome) => col.TryGetValue(nome, out var i) && i < l.Length ? l[i].Trim() : "";

            TipoOperacao? tipo = C("tipo").ToLowerInvariant() switch
            {
                "compra" => TipoOperacao.Compra,
                "venda" => TipoOperacao.Venda,
                "dividendo" => TipoOperacao.Dividendo,
                _ => null,
            };
            if (tipo is null)
            {
                avisos.Add($"Linha {n}: tipo \"{C("tipo")}\" desconhecido (usa compra, venda ou dividendo).");
                continue;
            }

            if (!TentarData(C("data"), out var momento))
            {
                avisos.Add($"Linha {n}: data inválida \"{C("data")}\" (usa AAAA-MM-DD).");
                continue;
            }

            var op = new Operacao
            {
                Tipo = tipo.Value,
                Momento = momento,
                Ativo = C("isin").ToUpperInvariant(),
                Nome = C("nome"),
                TipoAtivo = Classificacao.Sugerir(C("nome")),
                Moeda = C("moeda") is { Length: > 0 } m ? m.ToUpperInvariant() : "EUR",
                Comissoes = NumeroOpcional(C("comissoes_eur"), "comissões", n, avisos),
                RetencaoFonte = NumeroOpcional(C("retencao"), "imposto retido", n, avisos),
                MoedaRetencao = C("moeda_retencao") is { Length: > 0 } mr ? mr.ToUpperInvariant() : null,
                Corretora = "Modelo",
            };
            if (LerValores(op, C("quantidade"), C("preco"), n, avisos))
                operacoes.Add(op);
        }
        return new(operacoes, avisos);
    }

    // ------------------------------------------------------------------ Universal

    /// <summary>Nomes de coluna habituais nas corretoras (PT, EN, DE, FR, ES), já normalizados.</summary>
    private static readonly Dictionary<string, string[]> Sinonimos = new()
    {
        ["data"] = ["data", "date", "datum", "fecha", "trade date", "data da operacao", "data de execucao", "execution date", "data/hora", "date/time", "time", "open time", "close time"],
        ["hora"] = ["hora", "time", "uhrzeit", "heure"],
        ["tipo"] = ["tipo", "type", "action", "side", "buy/sell", "transaction type", "operacao", "tipo de operacao", "tipo de transacao", "transaktionstyp", "sens"],
        ["ativo"] = ["isin", "codigo isin"],
        ["nome"] = ["nome", "name", "product", "produto", "instrument", "instrumento", "description", "descricao", "titulo", "security", "wertpapier", "symbol"],
        ["quantidade"] = ["quantidade", "quantity", "qty", "shares", "no. of shares", "units", "anzahl", "stuck", "quantite", "cantidad", "volume"],
        ["preco"] = ["preco", "price", "price / share", "unit price", "preco unitario", "kurs", "prix", "precio", "t. price", "open price"],
        ["moeda"] = ["moeda", "currency", "currency (price / share)", "divisa", "wahrung", "devise"],
        ["moedaRetencao"] = ["moeda_retencao", "moeda retencao", "moeda da retencao", "currency (withholding tax)", "withholding tax currency"],
        ["comissoes"] = ["comissoes", "comissao", "fee", "fees", "commission", "commissions", "comm/fee", "transaction costs", "transaction and/or third party fees", "custos de transacao", "gebuhren", "frais", "custos", "taxas"],
        ["retencao"] = ["retencao", "imposto retido", "withholding tax", "tax withheld", "quellensteuer", "retencion"],
        ["id"] = ["id", "order id", "transaction id", "id da ordem", "reference", "referencia", "id da transacao"],
    };

    /// <summary>Lê o cabeçalho e as primeiras linhas e sugere o mapeamento.</summary>
    public static AnaliseFicheiro Analisar(string conteudo)
    {
        var separador = Csv.DetetarSeparador(conteudo);
        var linhas = Csv.Ler(conteudo, separador);
        if (linhas.Count == 0)
            return new("universal", separador, [], [], null, ["o ficheiro está vazio"], []);

        var cabecalho = linhas[0];
        var normalizados = cabecalho.Select(Normalizar).ToArray();
        var dados = linhas.Skip(1).ToList();

        int? Procurar(string campo, params int?[] excluir)
        {
            // Primeiro correspondência exata, depois "contém".
            // "id" só por correspondência exata: "contém id" apanharia colunas como "Side".
            foreach (var exato in campo == "id" ? new[] { true } : new[] { true, false })
                foreach (var sinonimo in Sinonimos[campo])
                    for (var i = 0; i < normalizados.Length; i++)
                        if (!excluir.Contains(i) && (exato ? normalizados[i] == sinonimo : normalizados[i].Contains(sinonimo)))
                            return i;
            return null;
        }

        var data = Procurar("data");
        var ativo = Procurar("ativo");
        var quantidade = Procurar("quantidade");
        var preco = Procurar("preco", quantidade);
        var hora = data is null ? null : Procurar("hora", data);
        if (hora is not null && dados.Take(5).All(l => hora < l.Length && l[hora.Value].Contains('-')))
            hora = null; // a coluna "time" era afinal a data completa
        var tipo = Procurar("tipo");
        // A moeda da retenção primeiro, para não ser confundida com a moeda do preço nem com o valor retido.
        var moedaRetencao = Procurar("moedaRetencao");
        var moeda = Procurar("moeda", moedaRetencao);
        var retencao = Procurar("retencao", moedaRetencao);

        // Degiro: a moeda do preço vem numa coluna sem nome logo a seguir ao preço.
        if (moeda is null && preco is { } p && p + 1 < cabecalho.Length && string.IsNullOrWhiteSpace(cabecalho[p + 1])
            && dados.Take(5).All(l => p + 1 < l.Length && l[p + 1].Trim().Length == 3))
            moeda = p + 1;

        var faltam = new List<string>();
        if (data is null) faltam.Add("data");
        if (ativo is null) faltam.Add("ISIN");
        if (quantidade is null) faltam.Add("quantidade");
        if (preco is null) faltam.Add("preço");

        var valoresPorColuna = Enumerable.Range(0, cabecalho.Length)
            .Select(c => dados.Where(l => c < l.Length).Select(l => l[c].Trim()).Where(v => v.Length > 0).Distinct().Take(MaxValoresTipo)
                .ToDictionary(v => v, SugerirTipo))
            .ToList();

        var sugestao = faltam.Count > 0 ? null : new Mapeamento(
            data!.Value, hora, tipo, ativo!.Value, Procurar("nome"), quantidade!.Value, preco!.Value,
            moeda, moeda is null ? "EUR" : null, Procurar("comissoes"), retencao, moedaRetencao, Procurar("id"),
            tipo is { } t ? new(valoresPorColuna[t]) : [], VendaSeQuantidadeNegativa: tipo is null);

        return new(Detetar(conteudo), separador, cabecalho, dados.Take(5).ToList(), sugestao, faltam, valoresPorColuna);
    }

    public static TipoOperacao? SugerirTipo(string valor)
    {
        var v = Normalizar(valor);
        if (v.Contains("divid")) return TipoOperacao.Dividendo;
        if (v is "b" or "buy" or "c" || v.Contains("buy") || v.Contains("compra") || v.Contains("kauf") || v.Contains("achat") || v.Contains("subscri")) return TipoOperacao.Compra;
        if (v is "s" or "sell" or "v" || v.Contains("sell") || v.Contains("venda") || v.Contains("verkauf") || v.Contains("vente") || v.Contains("resgate")) return TipoOperacao.Venda;
        return null;
    }

    public static ResultadoImportacao Universal(string conteudo, Mapeamento m)
    {
        if (m.Moeda is null && !MoedaValida(m.MoedaFixa))
            return new([], [$"Moeda \"{m.MoedaFixa}\" inválida: indica o código de 3 letras (ex.: EUR, USD)."]);

        var separador = Csv.DetetarSeparador(conteudo);
        var linhas = Csv.Ler(conteudo, separador);
        var operacoes = new List<Operacao>();
        var avisos = new List<string>();
        var ignoradas = new Dictionary<string, int>();

        foreach (var (l, n) in linhas.Skip(1).Select((l, i) => (l, i + 2)))
        {
            string C(int? i) => i is { } x && x >= 0 && x < l.Length ? l[x].Trim() : "";

            var textoQuantidade = C(m.Quantidade);
            TipoOperacao? tipo;
            if (m.Tipo is not null)
            {
                // Só o que a pessoa viu e confirmou no ecrã: um valor fora da lista é ignorado (e dito nos avisos),
                // em vez de ser adivinhado aqui sem ela saber.
                var valor = C(m.Tipo);
                tipo = m.ValoresTipo.GetValueOrDefault(valor);
                if (tipo is null)
                {
                    ignoradas[valor] = ignoradas.GetValueOrDefault(valor) + 1;
                    continue;
                }
            }
            else if (m.VendaSeQuantidadeNegativa && TentarNumero(textoQuantidade, out var q) && q != 0)
                tipo = q > 0 ? TipoOperacao.Compra : TipoOperacao.Venda;
            else
            {
                avisos.Add($"Linha {n}: não foi possível saber se é compra, venda ou dividendo.");
                continue;
            }

            var textoData = m.Hora is null ? C(m.Data) : $"{C(m.Data)} {C(m.Hora)}";
            if (!TentarData(textoData, out var momento))
            {
                avisos.Add($"Linha {n}: data inválida \"{textoData}\"; linha ignorada.");
                continue;
            }

            var ativo = C(m.Ativo).ToUpperInvariant();
            var nome = C(m.Nome) is { Length: > 0 } nm ? nm : ativo;
            var op = new Operacao
            {
                Tipo = tipo.Value,
                Momento = momento,
                Ativo = ativo,
                Nome = nome,
                TipoAtivo = Classificacao.Sugerir(nome),
                Moeda = (C(m.Moeda) is { Length: 3 } md ? md : m.MoedaFixa ?? "EUR").ToUpperInvariant(),
                Comissoes = Math.Abs(NumeroOpcional(C(m.Comissoes), "comissões", n, avisos)),
                RetencaoFonte = Math.Abs(NumeroOpcional(C(m.Retencao), "imposto retido", n, avisos)),
                MoedaRetencao = C(m.MoedaRetencao) is { Length: 3 } mr ? mr.ToUpperInvariant() : null,
                Corretora = "Importação universal",
                // Sem id da corretora, a própria linha serve de id: reimportar o ficheiro não duplica.
                IdExterno = C(m.IdExterno) is { Length: > 0 } id ? id : "linha:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', l))))[..24],
            };
            if (LerValores(op, textoQuantidade, C(m.Preco), n, avisos))
                operacoes.Add(op);
        }

        avisos.AddRange(ignoradas.Select(kv => $"Ignoradas {kv.Value} linha(s) do tipo \"{(kv.Key.Length > 0 ? kv.Key : "(vazio)")}\"."));
        if (m.Comissoes is null)
            avisos.Add("Não foi indicada a coluna das comissões: as despesas ficaram a zero.");
        return new(operacoes, avisos);
    }

    // ------------------------------------------------------------------ Utilitários

    /// <summary>
    /// Lê a quantidade e o preço e valida a operação. Devolve false (com aviso) se a linha não pode entrar:
    /// um número ilegível que passasse a 0 daria uma compra de zero títulos ou uma venda a preço zero, sem ninguém notar.
    /// </summary>
    private static bool LerValores(Operacao op, string quantidade, string preco, int linha, List<string> avisos)
    {
        if (!TentarNumero(quantidade, out var q) || q == 0)
        {
            avisos.Add($"Linha {linha}: quantidade \"{quantidade}\" inválida; linha ignorada.");
            return false;
        }
        if (!TentarNumero(preco, out var p))
        {
            avisos.Add($"Linha {linha}: preço \"{preco}\" inválido; linha ignorada.");
            return false;
        }
        return Aceitar(op, q, p, linha, avisos);
    }

    /// <summary>Valida a operação e normaliza-a (quantidade e preço positivos, pence → libras). False = linha ignorada, com aviso.</summary>
    private static bool Aceitar(Operacao op, decimal quantidade, decimal preco, int linha, List<string> avisos)
    {
        if (op.Ativo.Length is 0 or > 20)
        {
            avisos.Add($"Linha {linha}: ISIN \"{op.Ativo}\" inválido; linha ignorada.");
            return false;
        }
        if (quantidade == 0)
        {
            avisos.Add($"Linha {linha}: quantidade zero; linha ignorada.");
            return false;
        }
        if (!MoedaValida(op.Moeda))
        {
            avisos.Add($"Linha {linha}: moeda \"{op.Moeda}\" inválida; linha ignorada.");
            return false;
        }

        op.Quantidade = Math.Abs(quantidade);
        op.PrecoUnitario = Math.Abs(preco);
        if (op.MoedaComissoes == "EUR")
            op.MoedaComissoes = null;

        // Bolsa de Londres: as cotações vêm em pence (GBX), que o Banco de Portugal não publica.
        if (op.Moeda == "GBX")
        {
            op.Moeda = "GBP";
            op.PrecoUnitario /= 100;
        }
        return true;
    }

    private static decimal NumeroOpcional(string texto, string campo, int linha, List<string> avisos)
    {
        if (TentarNumero(texto, out var v))
            return v;
        avisos.Add($"Linha {linha}: {campo} \"{texto}\" ilegível; ficou a zero.");
        return 0m;
    }

    private static bool MoedaValida(string? moeda) => moeda is { Length: 3 } && moeda.All(char.IsAsciiLetter);

    /// <summary>Aceita "1234.56", "1,234.56", "1.234,56", "1234,56", "1.234.567", "-3", "(3)", "12,50 €", "$52.07". Texto vazio = 0; ilegível = 0.</summary>
    public static decimal Numero(string texto) => TentarNumero(texto, out var v) ? v : 0m;

    /// <summary>
    /// Como <see cref="Numero"/>, mas diz se conseguiu ler. Texto vazio conta como 0; texto com conteúdo que não é número
    /// (ex.: uma data na coluna errada, "N/A") falha.
    /// </summary>
    public static bool TentarNumero(string texto, out decimal valor)
    {
        valor = 0m;
        if (string.IsNullOrWhiteSpace(texto))
            return true;

        var t = new string(texto.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());
        if (texto.Trim().StartsWith('(') && texto.Trim().EndsWith(')') && !t.StartsWith('-'))
            t = "-" + t;
        var pontos = t.Count(c => c == '.');
        var virgulas = t.Count(c => c == ',');
        if (pontos > 0 && virgulas > 0)
            // O último separador é o decimal; o outro é de milhares.
            t = t.LastIndexOf('.') > t.LastIndexOf(',') ? t.Replace(",", "") : t.Replace(".", "").Replace(',', '.');
        else if (pontos > 1)
            t = t.Replace(".", "");  // 1.234.567
        else if (virgulas > 1)
            t = t.Replace(",", "");  // 1,234,567
        else if (virgulas == 1)
            t = t.Replace(',', '.');

        return decimal.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor);
    }

    private static bool TentarData(string texto, out DateTime data)
    {
        string[] formatos =
        [
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd HH:mm", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ",
            "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy HH:mm", "dd-MM-yyyy", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
            "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy", "yyyyMMdd", "yyyyMMdd;HHmmss", "yyyy-MM-dd, HH:mm:ss", "yyyy/MM/dd",
        ];
        if (DateTime.TryParseExact(texto.Trim(), formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out data))
            return true;
        // ISO 8601 com fuso (ex.: Revolut "2023-09-22T13:30:10.514Z"): fica em UTC, como vem no ficheiro.
        if (texto.Contains('T') && DateTimeOffset.TryParse(texto.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var iso))
        {
            data = iso.UtcDateTime;
            return true;
        }
        return false;
    }

    private static string Normalizar(string texto) =>
        new string(texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
}

/// <summary>Leitor de CSV (RFC 4180): separador configurável, aspas e quebras de linha dentro de aspas.</summary>
internal static class Csv
{
    /// <summary>O separador mais frequente na primeira linha, fora de aspas: vírgula, ponto e vírgula ou tabulação.</summary>
    public static char DetetarSeparador(string texto)
    {
        var primeira = texto.TrimStart('﻿').Split('\n')[0];
        var contagem = new Dictionary<char, int> { [','] = 0, [';'] = 0, ['\t'] = 0 };
        var entreAspas = false;
        foreach (var c in primeira)
        {
            if (c == '"') entreAspas = !entreAspas;
            else if (!entreAspas && contagem.ContainsKey(c)) contagem[c]++;
        }
        return contagem.MaxBy(kv => kv.Value).Key;
    }

    public static List<string[]> Ler(string texto, char separador)
    {
        var linhas = new List<string[]>();
        var campos = new List<string>();
        var atual = new StringBuilder();
        var entreAspas = false;
        texto = texto.TrimStart('﻿');

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (entreAspas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"') { atual.Append('"'); i++; }
                else if (c == '"') entreAspas = false;
                else atual.Append(c);
            }
            else if (c == '"') entreAspas = true;
            else if (c == separador) { campos.Add(atual.ToString()); atual.Clear(); }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                campos.Add(atual.ToString()); atual.Clear();
                if (campos.Count > 1 || campos[0].Length > 0) linhas.Add([.. campos]);
                campos.Clear();
            }
            else atual.Append(c);
        }
        campos.Add(atual.ToString());
        if (campos.Count > 1 || campos[0].Length > 0) linhas.Add([.. campos]);
        return linhas;
    }

    public static Dictionary<string, int> Colunas(string[] cabecalho) =>
        cabecalho.Select((nome, i) => (nome.Trim(), i)).GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.First().i);
}
