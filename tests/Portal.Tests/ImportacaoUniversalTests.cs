using System.Net.Http.Json;
using Portal.Modules.Investimentos;

namespace Portal.Tests;

public class ClassificacaoTests
{
    [Theory]
    [InlineData("iShares Core MSCI World UCITS ETF USD (Acc)", TipoAtivo.Etf)]
    [InlineData("Vanguard FTSE All-World UCITS ETF", TipoAtivo.Etf)]
    [InlineData("Xtrackers S&P 500 Swap", TipoAtivo.Etf)]
    [InlineData("Apple Inc.", TipoAtivo.Acao)]
    [InlineData("Accenture plc", TipoAtivo.Acao)] // "ACC" só conta como palavra isolada
    public void Sugere_etf_pelo_nome(string nome, TipoAtivo esperado) => Assert.Equal(esperado, Classificacao.Sugerir(nome));

    [Fact]
    public void Etf_sai_com_codigo_g20_e_acoes_com_g01()
    {
        Operacao[] ops =
        [
            new() { Tipo = TipoOperacao.Compra, Momento = new(2024, 1, 10), Ativo = "IE00B4L5Y983", Nome = "iShares", TipoAtivo = TipoAtivo.Etf, Quantidade = 1, PrecoUnitario = 80, Moeda = "EUR" },
            new() { Tipo = TipoOperacao.Venda, Momento = new(2025, 1, 10), Ativo = "IE00B4L5Y983", Nome = "iShares", TipoAtivo = TipoAtivo.Etf, Quantidade = 1, PrecoUnitario = 100, Moeda = "EUR" },
            new() { Tipo = TipoOperacao.Compra, Momento = new(2024, 1, 10), Ativo = "US0378331005", Nome = "Apple", Quantidade = 1, PrecoUnitario = 100, Moeda = "EUR" },
            new() { Tipo = TipoOperacao.Venda, Momento = new(2025, 1, 10), Ativo = "US0378331005", Nome = "Apple", Quantidade = 1, PrecoUnitario = 90, Moeda = "EUR" },
        ];

        var r = CalculoIrs.Calcular(ops, 2025, (_, _) => 1m);

        Assert.Equal("G20", r.MaisValias.Single(l => l.Ativo.StartsWith("IE")).Codigo);
        Assert.Equal("G01", r.MaisValias.Single(l => l.Ativo.StartsWith("US")).Codigo);
    }

    [Fact]
    public void Avisa_quando_o_pais_esta_na_lista_de_regimes_fiscais_favoraveis()
    {
        Operacao[] ops =
        [
            new() { Tipo = TipoOperacao.Dividendo, Momento = new(2025, 5, 1), Ativo = "KYG017191142", Nome = "Alibaba", Quantidade = 10, PrecoUnitario = 1, Moeda = "EUR" },
        ];

        var r = CalculoIrs.Calcular(ops, 2025, (_, _) => 1m);

        Assert.Contains(r.Avisos, a => a.Contains("KY") && a.Contains("35%"));
    }
}

public class ImportacaoUniversalTests
{
    // Formato ao estilo da Degiro: ";" como separador, vírgula decimal, moeda numa coluna sem nome,
    // vendas com quantidade negativa e sem coluna de tipo.
    private const string Degiro = """
        Data;Hora;Produto;ISIN;Bolsa de referência;Quantidade;Preço;;Valor local;;Custos de transação;;ID da ordem
        10-01-2024;15:30;APPLE INC;US0378331005;NDQ;10;185,20;USD;-1852,00;USD;-2,00;EUR;aaa-111
        01-03-2025;16:00;APPLE INC;US0378331005;NDQ;-4;240,00;USD;960,00;USD;-1,00;EUR;bbb-222
        """;

    [Fact]
    public void Analisa_e_sugere_o_mapeamento_de_um_ficheiro_desconhecido()
    {
        var a = Importacao.Analisar(Degiro);

        Assert.Equal(';', a.Separador);
        Assert.Empty(a.CamposEmFalta);
        var m = a.Sugestao!;
        Assert.Equal(0, m.Data);
        Assert.Equal(1, m.Hora);
        Assert.Equal(3, m.Ativo);
        Assert.Equal(5, m.Quantidade);
        Assert.Equal(6, m.Preco);
        Assert.Equal(7, m.Moeda);       // coluna sem nome a seguir ao preço
        Assert.Equal(10, m.Comissoes);
        Assert.Equal(12, m.IdExterno);
        Assert.Null(m.Tipo);
        Assert.True(m.VendaSeQuantidadeNegativa);
    }

    [Fact]
    public void Importa_com_o_mapeamento_sugerido()
    {
        var r = Importacao.Importar("universal", Degiro);

        Assert.Equal(2, r.Operacoes.Count);
        var compra = r.Operacoes[0];
        Assert.Equal(TipoOperacao.Compra, compra.Tipo);
        Assert.Equal(new DateTime(2024, 1, 10, 15, 30, 0), compra.Momento);
        Assert.Equal(185.20m, compra.PrecoUnitario);
        Assert.Equal("USD", compra.Moeda);
        Assert.Equal(2m, compra.Comissoes);
        Assert.Equal("aaa-111", compra.IdExterno);
        var venda = r.Operacoes[1];
        Assert.Equal(TipoOperacao.Venda, venda.Tipo);
        Assert.Equal(4m, venda.Quantidade);
    }

    [Fact]
    public void Coluna_de_tipo_em_ingles_e_valores_a_ignorar()
    {
        const string csv = """
            Trade Date,Type,ISIN,Description,Quantity,Price,Currency,Commission
            2025-02-03,BUY,US5949181045,MICROSOFT CORP,2,410.5,USD,1
            2025-02-04,DEPOSIT,,,0,0,EUR,0
            2025-06-10,SELL,US5949181045,MICROSOFT CORP,1,470,USD,1
            """;

        var a = Importacao.Analisar(csv);
        var r = Importacao.Universal(csv, a.Sugestao!);

        Assert.Equal(TipoOperacao.Compra, a.Sugestao!.ValoresTipo["BUY"]);
        Assert.Null(a.Sugestao.ValoresTipo["DEPOSIT"]);
        Assert.Equal([TipoOperacao.Compra, TipoOperacao.Venda], r.Operacoes.Select(o => o.Tipo));
        Assert.All(r.Operacoes, o => Assert.StartsWith("linha:", o.IdExterno)); // sem coluna de id
    }

    [Fact]
    public void Ficheiro_sem_isin_indica_o_que_falta()
    {
        var a = Importacao.Analisar("Data,Titulo,Quantidade,Preco\n2025-01-01,X,1,1");

        Assert.Null(a.Sugestao);
        Assert.Equal(["ISIN"], a.CamposEmFalta);
    }

    [Theory]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1234,56", 1234.56)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("1,234,567", 1234567)]
    [InlineData("1.234.567,89", 1234567.89)]
    [InlineData("-3", -3)]
    [InlineData("12,50 €", 12.50)]
    [InlineData("", 0)]
    public void Le_numeros_em_varios_formatos(string texto, decimal esperado) => Assert.Equal(esperado, Importacao.Numero(texto));

    [Theory]
    [InlineData("2024-01-10")]
    [InlineData("N/A")]
    [InlineData("1-2")]
    public void Texto_que_nao_e_numero_falha(string texto) => Assert.False(Importacao.TentarNumero(texto, out _));

    [Fact]
    public void Valor_de_tipo_fora_da_lista_confirmada_e_ignorado_e_nao_adivinhado()
    {
        const string csv = """
            Date,Type,ISIN,Name,Quantity,Price,Currency
            2025-02-03,Buy,US5949181045,Microsoft,2,410.5,USD
            2025-02-04,Sell,US5949181045,Microsoft,1,420,USD
            """;
        var m = Importacao.Analisar(csv).Sugestao! with { ValoresTipo = new() { ["Buy"] = TipoOperacao.Compra } };

        var r = Importacao.Universal(csv, m);

        Assert.Equal(TipoOperacao.Compra, Assert.Single(r.Operacoes).Tipo);
        Assert.Contains(r.Avisos, a => a.Contains("\"Sell\""));
    }

    [Fact]
    public void Analise_da_os_valores_de_todas_as_colunas_para_trocar_a_coluna_do_tipo()
    {
        const string csv = """
            Date,Side,Kind,ISIN,Quantity,Price
            2025-02-03,x,Compra,US5949181045,2,410.5
            2025-02-04,y,Venda,US5949181045,1,420
            """;

        var a = Importacao.Analisar(csv);

        Assert.Equal(1, a.Sugestao!.Tipo); // "Side" parece o tipo, mas é a coluna 2 que tem compras e vendas
        Assert.Equal(TipoOperacao.Compra, a.ValoresPorColuna[2]["Compra"]);
        Assert.Equal(TipoOperacao.Venda, a.ValoresPorColuna[2]["Venda"]);
    }

    [Fact]
    public void Linha_com_quantidade_ilegivel_e_ignorada_com_aviso()
    {
        const string csv = """
            Date,Type,ISIN,Name,Quantity,Price,Currency
            2025-02-03,Buy,US5949181045,Microsoft,dois,410.5,USD
            2025-02-04,Buy,US5949181045,Microsoft,2,410.5,USD
            """;

        var r = Importacao.Importar("universal", csv);

        Assert.Single(r.Operacoes);
        Assert.Contains(r.Avisos, a => a.Contains("Linha 2: quantidade \"dois\""));
    }

    [Fact]
    public void Moeda_fixa_invalida_e_recusada()
    {
        var m = Importacao.Analisar("Date,ISIN,Quantity,Price\n2025-01-01,US5949181045,1,1").Sugestao! with { MoedaFixa = "US" };

        var r = Importacao.Universal("Date,ISIN,Quantity,Price\n2025-01-01,US5949181045,1,1", m);

        Assert.Empty(r.Operacoes);
        Assert.Contains("Moeda \"US\" inválida", Assert.Single(r.Avisos));
    }

    [Fact]
    public void Modelo_do_portal_pela_via_universal_mantem_a_moeda_da_retencao()
    {
        var csv = Importacao.CabecalhoModelo + "\n2025-05-15,dividendo,US0378331005,Apple,10,0.26,EUR,,0.39,USD\n";

        var a = Importacao.Analisar(csv);
        var op = Assert.Single(Importacao.Universal(csv, a.Sugestao!).Operacoes);

        Assert.Equal(6, a.Sugestao!.Moeda);
        Assert.Equal(8, a.Sugestao.Retencao);
        Assert.Equal(9, a.Sugestao.MoedaRetencao);
        Assert.Equal("EUR", op.Moeda);
        Assert.Equal("USD", op.MoedaRetencao);
    }
}

public class ImportacaoUniversalApiTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    [Fact]
    public async Task Importacao_universal_nao_duplica_e_tipo_do_titulo_pode_mudar()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);
        const string csv = """
            Date,Type,ISIN,Name,Quantity,Price,Currency
            2024-02-03,Buy,IE00B4L5Y983,Global Equity Fund,2,80,EUR
            2025-06-10,Sell,IE00B4L5Y983,Global Equity Fund,2,100,EUR
            """;

        var analise = await (await cliente.PostAsJsonAsync("/api/investimentos/analisar", new PedidoAnalise(csv))).Content.ReadFromJsonAsync<AnaliseFicheiro>();
        var pedido = new PedidoImportacao("universal", csv, analise!.Sugestao);
        await cliente.PostAsJsonAsync("/api/investimentos/importar", pedido);
        var segunda = await (await cliente.PostAsJsonAsync("/api/investimentos/importar", pedido)).Content.ReadFromJsonAsync<Resultado>();

        // O nome não denuncia um ETF: fica como ação até a pessoa corrigir.
        var antes = await cliente.GetFromJsonAsync<RelatorioIrs>("/api/investimentos/relatorio/2025");
        await cliente.PutAsJsonAsync("/api/investimentos/ativos/IE00B4L5Y983/tipo", new NovoTipoAtivo(TipoAtivo.Fundo));
        var depois = await cliente.GetFromJsonAsync<RelatorioIrs>("/api/investimentos/relatorio/2025");

        Assert.Equal(0, segunda!.Importadas);
        Assert.Equal("G01", Assert.Single(antes!.MaisValias).Codigo);
        Assert.Equal("G20", Assert.Single(depois!.MaisValias).Codigo);
    }

    private sealed record Resultado(int Importadas, List<string> Avisos);
}
