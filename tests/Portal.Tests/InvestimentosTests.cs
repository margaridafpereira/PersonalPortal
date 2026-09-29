using System.Net.Http.Json;
using Portal.Modules.Investimentos;

namespace Portal.Tests;

public class CalculoIrsTests
{
    // Câmbios fixos: 1 EUR = 1,10 USD em 2024 e 1,25 USD em 2025.
    private static decimal? Cambio(string moeda, DateOnly data) => moeda switch
    {
        "EUR" => 1m,
        "USD" => data.Year == 2024 ? 1.10m : 1.25m,
        _ => null,
    };

    private static Operacao Op(TipoOperacao tipo, string data, decimal qtd, decimal preco, string moeda = "USD", decimal comissoes = 0, string isin = "US0378331005") =>
        new() { Tipo = tipo, Momento = DateTime.Parse(data), Ativo = isin, Nome = "Apple", Quantidade = qtd, PrecoUnitario = preco, Moeda = moeda, Comissoes = comissoes };

    [Fact]
    public void Fifo_vende_primeiro_os_titulos_mais_antigos()
    {
        Operacao[] ops =
        [
            Op(TipoOperacao.Compra, "2024-01-10", 10, 110),   // 100 €/título
            Op(TipoOperacao.Compra, "2024-06-10", 10, 165),   // 150 €/título
            Op(TipoOperacao.Venda, "2025-03-01", 15, 250),    // 200 €/título
        ];

        var r = CalculoIrs.Calcular(ops, 2025, Cambio);

        Assert.Equal(2, r.MaisValias.Count);
        var primeira = r.MaisValias[0];
        Assert.Equal(10, primeira.Quantidade);
        Assert.Equal(new DateOnly(2024, 1, 10), primeira.DataAquisicao);
        Assert.Equal(2000m, primeira.ValorRealizacao);
        Assert.Equal(1000m, primeira.ValorAquisicao);
        var segunda = r.MaisValias[1];
        Assert.Equal(5, segunda.Quantidade);
        Assert.Equal(750m, segunda.ValorAquisicao);
        Assert.Equal(1000m + 250m, r.SaldoMaisValias);
        Assert.Equal(350m, r.ImpostoMaisValias); // 28% de 1 250 €
        Assert.Equal("840", primeira.CodigoPais);
        Assert.Equal("G01", primeira.Codigo);
    }

    [Fact]
    public void Comissoes_entram_como_despesas_na_proporcao_vendida()
    {
        Operacao[] ops =
        [
            Op(TipoOperacao.Compra, "2024-01-10", 10, 100, "EUR", comissoes: 2),
            Op(TipoOperacao.Venda, "2025-01-10", 5, 120, "EUR", comissoes: 1),
        ];

        var linha = Assert.Single(CalculoIrs.Calcular(ops, 2025, Cambio).MaisValias);

        Assert.Equal(2m, linha.Despesas);       // 5 × 0,20 € da compra + 1 € da venda
        Assert.Equal(98m, linha.Resultado);     // 600 − 500 − 2
        Assert.False(linha.DetidoMenosDe365Dias);
    }

    [Fact]
    public void Menos_valias_compensam_mais_valias_e_imposto_nao_fica_negativo()
    {
        Operacao[] ops =
        [
            Op(TipoOperacao.Compra, "2025-01-10", 10, 100, "EUR"),
            Op(TipoOperacao.Venda, "2025-02-10", 10, 80, "EUR", isin: "US0378331005"),
        ];

        var r = CalculoIrs.Calcular(ops, 2025, Cambio);

        Assert.Equal(-200m, r.SaldoMaisValias);
        Assert.Equal(0m, r.ImpostoMaisValias);
        Assert.Contains(r.Avisos, a => a.Contains("365 dias"));
    }

    [Fact]
    public void Venda_sem_compras_suficientes_gera_aviso()
    {
        var r = CalculoIrs.Calcular([Op(TipoOperacao.Venda, "2025-02-10", 3, 200)], 2025, Cambio);

        Assert.Empty(r.MaisValias);
        Assert.Contains(r.Avisos, a => a.Contains("faltam compras"));
    }

    [Fact]
    public void Dividendos_agrupados_por_pais_com_credito_da_retencao()
    {
        Operacao[] ops =
        [
            new() { Tipo = TipoOperacao.Dividendo, Momento = new DateTime(2025, 5, 15), Ativo = "US0378331005", Nome = "Apple", Quantidade = 100, PrecoUnitario = 1.25m, Moeda = "USD", RetencaoFonte = 18.75m, MoedaRetencao = "USD" },
            new() { Tipo = TipoOperacao.Dividendo, Momento = new DateTime(2025, 8, 15), Ativo = "IE00B4L5Y983", Nome = "iShares", Quantidade = 10, PrecoUnitario = 5m, Moeda = "EUR" },
            new() { Tipo = TipoOperacao.Dividendo, Momento = new DateTime(2024, 8, 15), Ativo = "IE00B4L5Y983", Nome = "iShares", Quantidade = 10, PrecoUnitario = 5m, Moeda = "EUR" },
        ];

        var r = CalculoIrs.Calcular(ops, 2025, Cambio);

        var eua = r.Dividendos.Single(d => d.Pais == "US");
        Assert.Equal(100m, eua.Bruto);       // 125 USD / 1,25
        Assert.Equal(15m, eua.Retencao);     // 18,75 USD / 1,25
        Assert.Equal("E11", eua.Codigo);
        Assert.Equal(50m, r.Dividendos.Single(d => d.Pais == "IE").Bruto); // só 2025
        Assert.Equal(13m + 14m, r.ImpostoDividendos); // EUA: 28 − 15; Irlanda: 14
    }

    [Fact]
    public void Sem_cambio_a_operacao_fica_de_fora_com_aviso()
    {
        var r = CalculoIrs.Calcular([Op(TipoOperacao.Compra, "2025-01-10", 1, 100, "XYZ")], 2025, Cambio);

        Assert.Contains(r.Avisos, a => a.Contains("Sem câmbio de XYZ"));
    }

    [Fact]
    public void Retencao_acima_da_convencao_so_abate_15_por_cento()
    {
        // 30% retidos nos EUA (sem W-8BEN): só 15% contam como crédito.
        Operacao[] ops =
        [
            new() { Tipo = TipoOperacao.Dividendo, Momento = new DateTime(2025, 5, 15), Ativo = "US0378331005", Nome = "Apple", Quantidade = 100, PrecoUnitario = 1, Moeda = "EUR", RetencaoFonte = 30 },
        ];

        var r = CalculoIrs.Calcular(ops, 2025, Cambio);

        Assert.Equal(15m, r.Dividendos.Single().Retencao);
        Assert.Equal(13m, r.ImpostoDividendos); // 28 − 15, e não 0
        Assert.Equal(15m, r.Dividendos.Single().RetencaoNaoCreditada);
        Assert.Contains(r.Avisos, a => a.Contains("W-8BEN"));
    }

    [Fact]
    public void Venda_sem_cambio_consome_os_lotes_na_mesma()
    {
        Operacao[] ops =
        [
            Op(TipoOperacao.Compra, "2024-01-10", 5, 100, "EUR"),
            Op(TipoOperacao.Compra, "2024-02-10", 5, 200, "EUR"),
            Op(TipoOperacao.Venda, "2024-06-10", 5, 150, "XYZ"),  // sem câmbio: consome o lote de janeiro
            Op(TipoOperacao.Venda, "2025-03-01", 5, 300, "EUR"),
        ];

        var r = CalculoIrs.Calcular(ops, 2025, Cambio);

        var linha = Assert.Single(r.MaisValias);
        Assert.Equal(1000m, linha.ValorAquisicao); // lote de fevereiro (5 × 200), não o de janeiro
        Assert.DoesNotContain(r.Avisos, a => a.Contains("faltam compras"));
    }

    [Fact]
    public void Compra_sem_cambio_conta_para_o_fifo_mas_nao_para_as_contas()
    {
        Operacao[] ops =
        [
            Op(TipoOperacao.Compra, "2024-01-10", 5, 100, "XYZ"),
            Op(TipoOperacao.Compra, "2024-02-10", 5, 200, "EUR"),
            Op(TipoOperacao.Venda, "2025-03-01", 10, 300, "EUR"),
        ];

        var r = CalculoIrs.Calcular(ops, 2025, Cambio);

        var linha = Assert.Single(r.MaisValias);
        Assert.Equal(5m, linha.Quantidade);
        Assert.Equal(1000m, linha.ValorAquisicao);
        Assert.Contains(r.Avisos, a => a.Contains("5 título(s) vieram de uma compra sem câmbio"));
        Assert.DoesNotContain(r.Avisos, a => a.Contains("faltam compras"));
    }
}

public class ImportacaoTests
{
    private const string Trading212 = """
        Action,Time,ISIN,Ticker,Name,No. of shares,Price / share,Currency (Price / share),Exchange rate,Result,Currency (Result),Total,Currency (Total),Withholding tax,Currency (Withholding tax),Currency conversion fee,Currency (Currency conversion fee),ID
        Deposit,2024-01-02 10:00:00,,,,,,,,,,1000.00,EUR,,,,,D1
        Market buy,2024-01-10 15:30:01,US0378331005,AAPL,"Apple Inc.",2.5,185.20,USD,1.0950,,,423.25,EUR,,,0.63,EUR,EOF1
        Market sell,2025-03-01 16:00:00.123,US0378331005,AAPL,"Apple Inc.",1,240.00,USD,1.0400,52.10,EUR,230.20,EUR,,,0.35,EUR,EOF2
        Dividend (Ordinary),2025-05-15 09:00:00,US0378331005,AAPL,"Apple Inc.",1.5,0.25,USD,1.1200,,,0.28,EUR,0.06,USD,,,
        """;

    [Fact]
    public void Le_compras_vendas_e_dividendos_da_trading212()
    {
        var r = Importacao.Trading212(Trading212);

        Assert.Equal(3, r.Operacoes.Count);
        var compra = r.Operacoes[0];
        Assert.Equal(TipoOperacao.Compra, compra.Tipo);
        Assert.Equal("US0378331005", compra.Ativo);
        Assert.Equal("Apple Inc.", compra.Nome);
        Assert.Equal(2.5m, compra.Quantidade);
        Assert.Equal(185.20m, compra.PrecoUnitario);
        Assert.Equal("USD", compra.Moeda);
        Assert.Equal(0.63m, compra.Comissoes);
        Assert.Equal("EOF1", compra.IdExterno);
        Assert.Equal(new DateTime(2025, 3, 1, 16, 0, 0, 123), r.Operacoes[1].Momento);
        var dividendo = r.Operacoes[2];
        Assert.Equal(TipoOperacao.Dividendo, dividendo.Tipo);
        Assert.Equal(0.06m, dividendo.RetencaoFonte);
        Assert.Equal("USD", dividendo.MoedaRetencao);
        Assert.Contains(r.Avisos, a => a.Contains("Deposit"));
    }

    [Fact]
    public void Ficheiro_de_outra_corretora_e_recusado_com_mensagem()
    {
        var r = Importacao.Trading212("Data,Produto,ISIN,Quantidade\n2025-01-01,X,Y,1");

        Assert.Empty(r.Operacoes);
        Assert.Contains("Não parece um ficheiro da Trading 212", Assert.Single(r.Avisos));
    }

    [Fact]
    public void Le_o_modelo_do_portal_e_avisa_linhas_invalidas()
    {
        var csv = Importacao.CabecalhoModelo + "\n" +
            "2024-03-15,compra,US0378331005,Apple,10,172.50,USD,1.00,,\n" +
            "15/03/2024,oferta,US0378331005,Apple,1,1,USD,,,\n" +
            "ontem,venda,US0378331005,Apple,1,1,USD,,,\n";

        var r = Importacao.Modelo(csv);

        Assert.Single(r.Operacoes);
        Assert.Equal(2, r.Avisos.Count);
    }

    [Fact]
    public void Taxas_da_trading212_que_nao_estao_em_euros_ficam_de_fora_com_aviso()
    {
        const string csv = """
            Action,Time,ISIN,Name,No. of shares,Price / share,Currency (Price / share),Currency conversion fee,Currency (Currency conversion fee),Transaction fee,Currency (Transaction fee),ID
            Market buy,2024-01-10 15:30:01,US0378331005,Apple,1,185.20,USD,0.30,EUR,2.00,USD,A1
            """;

        var r = Importacao.Trading212(csv);

        Assert.Equal(0.30m, Assert.Single(r.Operacoes).Comissoes);
        Assert.Contains(r.Avisos, a => a.Contains("2.00 USD"));
    }

    [Fact]
    public void Reconhece_o_formato_pelo_cabecalho()
    {
        Assert.Equal("trading212", Importacao.Detetar(Trading212));
        Assert.Equal("modelo", Importacao.Detetar(Importacao.CabecalhoModelo + "\n"));
        Assert.Equal("universal", Importacao.Detetar("Data;ISIN;Quantidade;Preço\n"));
        Assert.Equal("trading212", Importacao.Analisar(Trading212).Formato);
        Assert.Equal(3, Importacao.Importar("auto", Trading212).Operacoes.Count);
    }

    [Fact]
    public void Precos_em_pence_passam_a_libras()
    {
        var csv = Importacao.CabecalhoModelo + "\n2024-03-15,compra,GB00B03MLX29,Shell,10,2650,GBX,,,\n";

        var op = Assert.Single(Importacao.Modelo(csv).Operacoes);

        Assert.Equal("GBP", op.Moeda);
        Assert.Equal(26.50m, op.PrecoUnitario);
    }
}

public class CambiosTests
{
    [Fact]
    public void Interpreta_a_resposta_do_banco_de_portugal()
    {
        // Extrato real do BPstat (série 12531971, USD).
        const string json = """
            {"value":[1.149,1.1463,null],"dimension":{"reference_date":{"category":{"index":["2026-09-21","2026-09-22","2026-09-23"]}}}}
            """;

        var taxas = CambiosBancoDePortugal.Interpretar(json);

        Assert.Equal(2, taxas.Count);
        Assert.Equal(1.1463m, taxas[new DateOnly(2026, 9, 22)]);
    }
}

public class InvestimentosApiTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    [Fact]
    public async Task Importa_a_trading212_sem_duplicar_e_calcula_o_relatorio()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);
        var csv = """
            Action,Time,ISIN,Ticker,Name,No. of shares,Price / share,Currency (Price / share),Currency conversion fee,ID
            Market buy,2024-01-10 15:30:01,US0378331005,AAPL,Apple,10,110.00,USD,0.00,A1
            Market sell,2025-03-01 16:00:00,US0378331005,AAPL,Apple,10,250.00,USD,0.00,A2
            """;

        var primeira = await cliente.PostAsJsonAsync("/api/investimentos/importar", new PedidoImportacao("trading212", csv));
        var segunda = await cliente.PostAsJsonAsync("/api/investimentos/importar", new PedidoImportacao("trading212", csv));
        var relatorio = await cliente.GetFromJsonAsync<RelatorioIrs>("/api/investimentos/relatorio/2025");

        Assert.Equal(2, (await primeira.Content.ReadFromJsonAsync<Resultado>())!.Importadas);
        Assert.Equal(0, (await segunda.Content.ReadFromJsonAsync<Resultado>())!.Importadas);
        var linha = Assert.Single(relatorio!.MaisValias);
        Assert.Equal(1000m, linha.ValorAquisicao);  // 1 100 USD / 1,10 (câmbio falso)
        Assert.Equal(2000m, linha.ValorRealizacao); // 2 500 USD / 1,25
    }

    private sealed record Resultado(int Importadas, List<string> Avisos);
}

/// <summary>Câmbios fixos nos testes: 1 EUR = 1,10 USD em 2024 e 1,25 USD depois.</summary>
public sealed class CambiosFalsos : ICambios
{
    public Task<decimal?> TaxaAsync(string moeda, DateOnly data, CancellationToken ct) =>
        Task.FromResult<decimal?>(moeda switch { "EUR" => 1m, "USD" => data.Year == 2024 ? 1.10m : 1.25m, _ => null });
}
