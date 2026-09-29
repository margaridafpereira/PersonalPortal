using System.IO.Compression;
using System.Text;
using Portal.Modules.Investimentos;

namespace Portal.Tests;

/// <summary>
/// Leitores das corretoras, testados com linhas dos ficheiros de exemplo públicos do projeto Export-To-Ghostfolio
/// (github.com/dickwolff/Export-To-Ghostfolio, pasta samples/). Ver docs/investimentos/02-formatos-corretoras.md.
/// </summary>
public class CorretorasTests
{
    [Fact]
    public void Trading212_ficheiro_real_de_exemplo()
    {
        const string csv = """
            Action,Time,ISIN,Ticker,Name,No. of shares,Price / share,Currency (Price / share),Exchange rate,Result,Currency (Result),Total,Currency (Total),Withholding tax,Currency (Withholding tax),Notes,ID,Currency conversion fee,Currency (Currency conversion fee)
            Deposit,2023-12-18 11:45:06.326,,,,,,,,,,31.00,"EUR",,,"Transaction ID: PB6XHFHSNDT97F32",30c841b3-068d-44f0-9809-e75638e211cd,,
            Market buy,2023-12-18 14:30:03.613,US17275R1023,CSCO,"Cisco Systems",0.0290530000,49.96,USD,1.09303,,"EUR",1.33,"EUR",,,,EOF7504196256,,
            Market sell,2023-12-26 14:30:05.104,US04634X2027,ASTR,"Astra Space",0.6125400000,1.26,USD,1.10231,-3.08,"EUR",0.70,"EUR",,,,EOF7802023054,,
            Dividend (Dividend),2024-01-12 14:14:14,IE00B1XNHC34,INRG,"iShares Global Clean Energy UCITS ETF",0.0280492000,630.11,GBX,Not available,,,17.67,"EUR",15.02,USD,,,,
            """;

        Assert.Equal("trading212", Importacao.Detetar(csv));
        var r = Importacao.Importar("auto", csv);

        Assert.Equal(3, r.Operacoes.Count);
        var compra = r.Operacoes[0];
        Assert.Equal((TipoOperacao.Compra, 0.029053m, 49.96m, "USD"), (compra.Tipo, compra.Quantidade, compra.PrecoUnitario, compra.Moeda));
        Assert.Equal(TipoOperacao.Venda, r.Operacoes[1].Tipo);
        var dividendo = r.Operacoes[2];
        Assert.Equal(("GBP", 6.3011m), (dividendo.Moeda, dividendo.PrecoUnitario)); // 630,11 pence
        Assert.Equal((15.02m, "USD"), (dividendo.RetencaoFonte, dividendo.MoedaRetencao));
    }

    [Fact]
    public void Degiro_extrato_de_conta_em_qualquer_lingua()
    {
        const string csv = """
            Datum,Tijd,Valutadatum,Product,ISIN,Omschrijving,FX,Mutatie,,Saldo,,Order Id
            19-12-2022,18:33,19-12-2022,FLATEX EURO BANKACCOUNT,NLFLATEXACNT,Degiro Cash Sweep Transfer,,EUR,"26,45",EUR,"27,80",
            16-12-2022,09:05,15-12-2022,COCA-COLA COMPANY (THE,US1912161007,Dividend,,USD,"0,44",USD,"1,36",
            16-12-2022,09:05,15-12-2022,COCA-COLA COMPANY (THE,US1912161007,Dividendbelasting,,USD,"-0,07",USD,"0,92",
            15-12-2022,16:55,15-12-2022,VICI PROPERTIES INC. C,US9256521090,"Koop 1 @ 33,9 USD",,USD,"-33,90",USD,"-33,90",5925d76b-eb36-46e3-b017-a61a6d03c3e7
            15-12-2022,16:55,15-12-2022,VICI PROPERTIES INC. C,US9256521090,DEGIRO Transactiekosten en/of kosten van derden,,EUR,"-1,00",EUR,"31,98",5925d76b-eb36-46e3-b017-a61a6d03c3e7
            10-08-2021,16:18,10-08-2021,BANK NOVA SCOTIA HALIF,CA0641491075,"Verkoop 1 @ 63,97 USD",,USD,"63,97",USD,"63,97",4046d050-7530-40c3-8544-4d0580cd1cea
            01-08-2023,10:03,01-08-2023,ISHARES NASDAQ US BIOTECHNOLOGY ETF,IE00BYXG2H39,"Verkoop 1 @ 5,416 EUR",,EUR,5.42,EUR,6.54,4c29dd81-bb01-40fa-8dda-aae3d05dae79
            11-03-2024,10:39,11-03-2024,QT GROUP OYJ,FI4000198031,"Achat 6 QT GROUP OYJ@79,96 EUR (FI4000198031)",,EUR,-479.76,EUR,38.43,cce1bd4c-9404-49b0-b69a-43a5c307d3c5
            18-03-2024,08:38,15-03-2024,REALTY INCOME CORP,US7561091049,Dividende,,USD,0.51,USD,0.36,
            19-09-2024,20:20,19-09-2024,APPLE INC,US0378331005,Levantamento de divisa,1.1191,USD,-457.52,USD,0.00,711b1ad7-a370-4c88-bc86-074aada4a278
            """;

        Assert.Equal("degiro", Importacao.Detetar(csv));
        var r = Importacao.Importar("auto", csv);

        var vici = r.Operacoes.Single(o => o.Ativo == "US9256521090");
        Assert.Equal((TipoOperacao.Compra, 1m, 33.9m, "USD"), (vici.Tipo, vici.Quantidade, vici.PrecoUnitario, vici.Moeda));
        Assert.Equal((1m, (string?)null), (vici.Comissoes, vici.MoedaComissoes)); // 1 € de custos da mesma ordem
        Assert.Equal(TipoOperacao.Venda, r.Operacoes.Single(o => o.Ativo == "CA0641491075").Tipo);
        Assert.Equal(5.416m, r.Operacoes.Single(o => o.Ativo == "IE00BYXG2H39").PrecoUnitario);
        var qt = r.Operacoes.Single(o => o.Ativo == "FI4000198031"); // descrição em francês, com o nome antes do @
        Assert.Equal((TipoOperacao.Compra, 6m, 79.96m), (qt.Tipo, qt.Quantidade, qt.PrecoUnitario));

        var coca = r.Operacoes.Single(o => o.Ativo == "US1912161007");
        Assert.Equal((TipoOperacao.Dividendo, 0.44m, 0.07m), (coca.Tipo, coca.PrecoUnitario, coca.RetencaoFonte));
        Assert.Equal((0.51m, 0m), (r.Operacoes.Single(o => o.Ativo == "US7561091049").PrecoUnitario, r.Operacoes.Single(o => o.Ativo == "US7561091049").RetencaoFonte));
        Assert.Equal(6, r.Operacoes.Count); // 4 negócios e 2 dividendos; sem o cash sweep nem o câmbio
    }

    [Fact]
    public void Revolut_so_tem_ticker_e_valores_com_simbolo_de_moeda()
    {
        const string csv = """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2019-11-15T23:15:55.878985Z,,CASH TOP-UP,,,$5.22,USD,1.1055
            2023-09-22T13:30:10.514Z,O,BUY - MARKET,1.63453043,$52.07,$85.11,USD,1.0665
            2023-07-14T13:30:00.797Z,MA,SELL - MARKET,0.1998348,$402.13,$80.34,USD,1.1241
            2019-12-13T08:40:00.835101Z,MSFT,DIVIDEND,,,$0.08,USD,1.1179
            2022-08-25T08:27:46.419568Z,TSLA,STOCK SPLIT,0.16431924,,$0,USD,0.0947
            2025-06-05T07:26:04.809Z,TSLA,BUY - MARKET,0.56217674,€88.94,€50,EUR,1.0000
            """;

        Assert.Equal("revolut", Importacao.Detetar(csv));
        var r = Importacao.Importar("auto", csv);

        Assert.Equal(4, r.Operacoes.Count);
        var o = r.Operacoes[0];
        Assert.Equal(("O", TipoOperacao.Compra, 1.63453043m, 52.07m, "USD"), (o.Ativo, o.Tipo, o.Quantidade, o.PrecoUnitario, o.Moeda));
        Assert.Equal(new DateTime(2023, 9, 22, 13, 30, 10, 514), o.Momento);
        Assert.Equal((TipoOperacao.Dividendo, 0.08m), (r.Operacoes[2].Tipo, r.Operacoes[2].PrecoUnitario));
        Assert.Equal((88.94m, "EUR"), (r.Operacoes[3].PrecoUnitario, r.Operacoes[3].Moeda));
        Assert.Contains(r.Avisos, a => a.Contains("stock split"));
    }

    [Fact]
    public void Xtb_operacoes_de_caixa()
    {
        const string csv = """
            ID;Type;Time;Symbol;Comment;Amount
            530692719;Stocks/ETF purchase;12.04.2024 13:01:45;SPYL.DE;OPEN BUY 34/42.5658 @ 11.7480;-399.43
            524303821;Stocks/ETF sale;02.04.2024 11:42:37;ZAL.DE;CLOSE BUY 1 @ 26.280;26.28
            579214169;Dividend;12.07.2024 11:00:27;SPYL.DE;SPYL.DE USD 0.6600/ SHR;1.21
            623424924;Withholding tax;26.09.2024 11:00:23;FB.US;FB.US USD WHT 15%;-0.33
            623424925;Dividend;26.09.2024 11:00:22;FB.US;FB.US USD 0.5000/ SHR;2.20
            530691593;Deposit;12.04.2024 13:00:22;;JP_MORGAN deposit, JP_MORGAN provider tran;500
            """;

        Assert.Equal("xtb", Importacao.Detetar(csv));
        var r = Importacao.Importar("auto", csv);

        var compra = r.Operacoes.Single(o => o.IdExterno == "xtb:530692719");
        Assert.Equal((TipoOperacao.Compra, 34m, "EUR", "SPYL.DE"), (compra.Tipo, compra.Quantidade, compra.Moeda, compra.Ativo));
        Assert.Equal(399.43m, Math.Round(compra.Quantidade * compra.PrecoUnitario, 2)); // o valor que saiu da conta
        Assert.Equal((TipoOperacao.Venda, 1m, 26.28m), (r.Operacoes.Single(o => o.Ativo == "ZAL.DE").Tipo, 1m, r.Operacoes.Single(o => o.Ativo == "ZAL.DE").PrecoUnitario));
        var fb = r.Operacoes.Single(o => o.Ativo == "FB.US");
        Assert.Equal((2.20m, 0.33m), (fb.PrecoUnitario, fb.RetencaoFonte)); // retenção junta ao dividendo do mesmo dia
        Assert.Contains(r.Avisos, a => a.Contains("Deposit"));
    }

    [Fact]
    public void EToro_atividade_da_conta()
    {
        const string csv = """
            Date,Type,Details,Amount,Units,Realized Equity Change,Realized Equity,Balance,Position ID,Asset type,NWA
            01/01/2024 05:50:54,Interest Payment,,0.08,-,0.08,"4,581.91",0.00,-,,0.00
            02/01/2024 00:10:33,Dividend,NKE/USD,0.17,-,0.17,"4,581.91",99.60,2272508626,Stocks,0.00
            09/01/2024 15:30:40,Position closed,OLED/USD,18.43,0.102626,7.37,"4,581.91",0.00,2355395242,Stocks,0.00
            09/01/2024 15:37:16,Open Position,AMD/USD,49.88,0.337209,0.00,"4,581.91",0.00,2596572937,Stocks,0.00
            15/04/2020 00:46:41,Overnight fee,Daily,"(0,10)",-,"(0,10)"," 212,77 "," 12,77 ",1074146905,CFD," 0,00 "
            20/05/2020 01:31:04,Open Position,BTC/USD,100,0.01,0,0,0,999,Crypto,0
            """;

        Assert.Equal("etoro", Importacao.Detetar(csv));
        var r = Importacao.Importar("auto", csv);

        Assert.Equal(3, r.Operacoes.Count);
        var amd = r.Operacoes.Single(o => o.Ativo == "AMD");
        Assert.Equal((TipoOperacao.Compra, 0.337209m, "USD"), (amd.Tipo, amd.Quantidade, amd.Moeda));
        Assert.Equal(49.88m, Math.Round(amd.Quantidade * amd.PrecoUnitario, 2));
        Assert.Equal(TipoOperacao.Venda, r.Operacoes.Single(o => o.Ativo == "OLED").Tipo);
        Assert.Equal((TipoOperacao.Dividendo, 0.17m), (r.Operacoes.Single(o => o.Ativo == "NKE").Tipo, r.Operacoes.Single(o => o.Ativo == "NKE").PrecoUnitario));
        Assert.Contains(r.Avisos, a => a.Contains("CFD ou cripto"));
    }

    [Fact]
    public void Ibkr_negocios_com_comissoes_na_moeda_da_bolsa()
    {
        const string csv = """
            "Buy/Sell","TradeDate","ISIN","Quantity","TradePrice","TradeMoney","CurrencyPrimary","IBCommission","IBCommissionCurrency"
            "BUY","20230522","CH0111762537","7","282.7","1978.9","CHF","-5","CHF"
            "BUY","20230522","US9220427424","95","93.78","8909.1","USD","-1","USD"
            "SELL","20230522","","-8000","1.1173","-8938.4","USD","-1.79924","CHF"
            """;

        Assert.Equal("ibkr", Importacao.Detetar(csv));
        var r = Importacao.Importar("auto", csv);

        Assert.Equal(2, r.Operacoes.Count); // a conversão de moeda (sem ISIN) fica de fora
        var ch = r.Operacoes[0];
        Assert.Equal((7m, 282.7m, "CHF", 5m, "CHF"), (ch.Quantidade, ch.PrecoUnitario, ch.Moeda, ch.Comissoes, ch.MoedaComissoes));
        Assert.Contains(r.Avisos, a => a.Contains("sem ISIN"));
    }

    [Fact]
    public void Ibkr_dividendos_juntam_a_retencao_e_ignoram_reembolso_de_capital()
    {
        const string csv = """
            "Type","SettleDate","ISIN","Description","Amount","CurrencyPrimary"
            "Dividends","20230623","US9220427424","VT(US9220427424) CASH DIVIDEND USD 0.6504 PER SHARE (Ordinary Dividend)","137.23","USD"
            "Withholding Tax","20230623","US9220427424","VT(US9220427424) CASH DIVIDEND USD 0.6504 PER SHARE - US TAX","-20.58","USD"
            "Dividends","20230913","CH0111762537","SMMCHA(CH0111762537) CASH DIVIDEND CHF 0.69 PER SHARE (Return of Capital)","4.83","CHF"
            """;

        Assert.Equal("ibkr-dividendos", Importacao.Detetar(csv));
        var r = Importacao.Importar("auto", csv);

        var vt = Assert.Single(r.Operacoes);
        Assert.Equal(("VT", 137.23m, 20.58m, "USD"), (vt.Nome, vt.PrecoUnitario, vt.RetencaoFonte, vt.Moeda));
        Assert.Contains(r.Avisos, a => a.Contains("Return of Capital"));
    }

    [Theory]
    [InlineData("trading212.csv", "trading212", 7)]
    [InlineData("degiro-extrato-de-conta.csv", "degiro", 7)]
    [InlineData("revolut.csv", "revolut", 4)]
    [InlineData("xtb-operacoes-de-caixa.csv", "xtb", 4)]
    [InlineData("etoro-atividade-da-conta.csv", "etoro", 3)]
    [InlineData("ibkr-negocios.csv", "ibkr", 4)]
    [InlineData("ibkr-dividendos.csv", "ibkr-dividendos", 1)]
    [InlineData("demonstracao-anexo-j.csv", "modelo", 7)]
    public void Ficheiros_de_exemplo_da_documentacao_sao_reconhecidos(string ficheiro, string formato, int operacoes)
    {
        var conteudo = File.ReadAllText(Path.Combine(RaizDoRepositorio(), "docs", "exemplos-corretoras", ficheiro));

        Assert.Equal(formato, Importacao.Detetar(conteudo));
        Assert.Equal(operacoes, Importacao.Importar("auto", conteudo).Operacoes.Count);
    }

    /// <summary>A partir do caminho deste ficheiro de código: funciona qualquer que seja a pasta onde os testes foram compilados.</summary>
    private static string RaizDoRepositorio([System.Runtime.CompilerServices.CallerFilePath] string ficheiro = "")
    {
        var pasta = new DirectoryInfo(Path.GetDirectoryName(ficheiro)!);
        while (!File.Exists(Path.Combine(pasta.FullName, "Portal.sln")))
            pasta = pasta.Parent ?? throw new DirectoryNotFoundException("Não encontrei o Portal.sln.");
        return pasta.FullName;
    }

    [Fact]
    public void Demonstracao_preenche_o_anexo_j_de_2025()
    {
        var ops = Importacao.Importar("auto", File.ReadAllText(Path.Combine(RaizDoRepositorio(), "docs", "exemplos-corretoras", "demonstracao-anexo-j.csv"))).Operacoes;

        var r = CalculoIrs.Calcular(ops, 2025, (moeda, _) => moeda == "USD" ? 1.10m : 1m);

        Assert.Equal(3, r.MaisValias.Count);                               // Apple em 2 linhas (FIFO) + o ETF
        Assert.Contains(r.MaisValias, l => l.DetidoMenosDe365Dias);        // as 2 Apple de fevereiro de 2025
        Assert.Equal("G20", r.MaisValias.Single(l => l.Ativo.StartsWith("IE")).Codigo);
        Assert.True(r.Dividendos.Single().RetencaoNaoCreditada > 0);       // 30% retidos nos EUA
        Assert.True(r.SaldoMaisValias > 0);
    }

    [Fact]
    public void Ticker_nao_e_confundido_com_isin()
    {
        Assert.Equal("??", Paises.Iso("AAPL"));
        Assert.Equal("??", Paises.Iso("SPYL.DE"));
        Assert.Equal("US", Paises.Iso("US0378331005"));
    }

    [Fact]
    public void Comissoes_noutra_moeda_sao_convertidas_no_calculo()
    {
        Operacao[] ops =
        [
            new() { Tipo = TipoOperacao.Compra, Momento = new(2024, 1, 10), Ativo = "US0378331005", Nome = "Apple", Quantidade = 1, PrecoUnitario = 100, Moeda = "EUR", Comissoes = 2, MoedaComissoes = "USD" },
            new() { Tipo = TipoOperacao.Venda, Momento = new(2025, 1, 10), Ativo = "US0378331005", Nome = "Apple", Quantidade = 1, PrecoUnitario = 150, Moeda = "EUR" },
        ];

        var r = CalculoIrs.Calcular(ops, 2025, (moeda, _) => moeda == "USD" ? 2m : 1m);

        Assert.Equal(1m, Assert.Single(r.MaisValias).Despesas); // 2 USD a 2 USD/EUR
    }

    [Fact]
    public void Excel_escolhe_a_folha_conhecida_salta_o_titulo_e_le_datas()
    {
        var data = new DateTime(2024, 1, 9, 15, 37, 16).ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var xlsx = CriarXlsx(
            ("Resumo", "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Resumo da conta</t></is></c></row>"),
            ("Account Activity",
                "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Account Activity</t></is></c></row>" +
                "<row r=\"2\">" + string.Concat(new[] { "Date", "Type", "Details", "Amount", "Units", "Realized Equity Change", "Realized Equity", "Balance", "Position ID", "Asset type", "NWA" }
                    .Select((c, i) => $"<c r=\"{(char)('A' + i)}2\" t=\"inlineStr\"><is><t>{c}</t></is></c>")) + "</row>" +
                $"<row r=\"3\"><c r=\"A3\" s=\"1\"><v>{data}</v></c><c r=\"B3\" t=\"s\"><v>0</v></c><c r=\"C3\" t=\"inlineStr\"><is><t>AMD/USD</t></is></c>" +
                "<c r=\"D3\"><v>49.88</v></c><c r=\"E3\"><v>0.337209</v></c><c r=\"I3\"><v>2596572937</v></c><c r=\"J3\" t=\"inlineStr\"><is><t>Stocks</t></is></c></row>"));

        var csv = Xlsx.ParaCsv(xlsx);
        var r = Importacao.Importar("auto", csv);

        Assert.Equal("etoro", Importacao.Detetar(csv));
        var amd = Assert.Single(r.Operacoes);
        Assert.Equal(new DateTime(2024, 1, 9, 15, 37, 16), amd.Momento);
        Assert.Equal(0.337209m, amd.Quantidade);
    }

    /// <summary>Um .xlsx mínimo: livro, relações, estilos (o estilo 1 é uma data) e textos partilhados.</summary>
    private static byte[] CriarXlsx(params (string Nome, string Linhas)[] folhas)
    {
        const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Escrever(string caminho, string xml)
            {
                using var w = new StreamWriter(zip.CreateEntry(caminho).Open(), new UTF8Encoding(false));
                w.Write(xml);
            }

            Escrever("xl/workbook.xml",
                $"<workbook xmlns=\"{Main}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" +
                string.Concat(folhas.Select((f, i) => $"<sheet name=\"{f.Nome}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>")) + "</sheets></workbook>");
            Escrever("xl/_rels/workbook.xml.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                string.Concat(folhas.Select((_, i) => $"<Relationship Id=\"rId{i + 1}\" Type=\"worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>")) + "</Relationships>");
            Escrever("xl/styles.xml",
                $"<styleSheet xmlns=\"{Main}\"><numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy hh:mm:ss\"/></numFmts>" +
                "<cellXfs count=\"2\"><xf numFmtId=\"0\"/><xf numFmtId=\"164\"/></cellXfs></styleSheet>");
            Escrever("xl/sharedStrings.xml", $"<sst xmlns=\"{Main}\"><si><t>Open Position</t></si></sst>");
            for (var i = 0; i < folhas.Length; i++)
                Escrever($"xl/worksheets/sheet{i + 1}.xml", $"<worksheet xmlns=\"{Main}\"><sheetData>{folhas[i].Linhas}</sheetData></worksheet>");
        }
        return ms.ToArray();
    }
}
