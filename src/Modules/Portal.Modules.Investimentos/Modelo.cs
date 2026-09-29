using System.Text.Json.Serialization;

namespace Portal.Modules.Investimentos;

[JsonConverter(typeof(JsonStringEnumConverter<TipoOperacao>))]
public enum TipoOperacao { Compra, Venda, Dividendo }

/// <summary>Define o código do Anexo J: ações G01; ETF e fundos G20.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TipoAtivo>))]
public enum TipoAtivo { Acao, Etf, Fundo }

/// <summary>
/// Uma operação numa corretora. Os valores ficam na moeda original; a conversão para euros
/// é feita no cálculo, ao câmbio de referência do dia (Banco de Portugal).
/// </summary>
public class Operacao
{
    public Guid Id { get; set; }
    public string UtilizadorId { get; set; } = "";
    public TipoOperacao Tipo { get; set; }

    /// <summary>Data e hora da operação; a hora desempata compras e vendas no mesmo dia (FIFO).</summary>
    public DateTime Momento { get; set; }

    /// <summary>ISIN (ex.: US0378331005). Identifica o ativo no FIFO e dá o país da fonte.</summary>
    public string Ativo { get; set; } = "";
    public string Nome { get; set; } = "";

    /// <summary>Sugerido pelo nome na importação; a pessoa pode corrigir por título.</summary>
    public TipoAtivo TipoAtivo { get; set; }

    /// <summary>Compra/venda: número de títulos. Dividendo: títulos que deram direito ao dividendo.</summary>
    public decimal Quantidade { get; set; }

    /// <summary>Compra/venda: preço por título. Dividendo: dividendo bruto por título.</summary>
    public decimal PrecoUnitario { get; set; }
    public string Moeda { get; set; } = "EUR";

    /// <summary>Comissões e taxas da corretora, na moeda <see cref="MoedaComissoes"/> (convertidas no cálculo, como o preço).</summary>
    public decimal Comissoes { get; set; }

    /// <summary>null = euros. A IBKR, por exemplo, cobra na moeda da bolsa (USD, CHF…).</summary>
    public string? MoedaComissoes { get; set; }

    /// <summary>Imposto retido no estrangeiro (dividendos), na moeda indicada.</summary>
    public decimal RetencaoFonte { get; set; }
    public string? MoedaRetencao { get; set; }

    public string? Corretora { get; set; }

    /// <summary>Id da operação na corretora, para não importar a mesma operação duas vezes.</summary>
    public string? IdExterno { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
}
