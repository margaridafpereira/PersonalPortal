using System.Text.Json.Serialization;

namespace Portal.Modules.Carro;

[JsonConverter(typeof(JsonStringEnumConverter<CategoriaVeiculo>))]
public enum CategoriaVeiculo { LigeiroPassageiros, LigeiroMercadorias }

/// <summary>Os valores são os ids dos tipos de combustível na API da DGEG.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Combustivel>))]
public enum Combustivel
{
    GasoleoSimples = 2101,
    GasoleoEspecial = 2105,
    Gasolina95 = 3201,
    Gasolina95Especial = 3205,
    Gasolina98 = 3400,
    Gpl = 1120,
    Eletrico = 0,
}

/// <summary>Como foram medidas as emissões de CO2 (campo V.7 do certificado de matrícula): muda os escalões do IUC.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NormaEmissoes>))]
public enum NormaEmissoes { Nedc, Wltp }

public class Veiculo
{
    public Guid Id { get; set; }
    public string UtilizadorId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Matricula { get; set; }
    public CategoriaVeiculo Categoria { get; set; }
    public Combustivel Combustivel { get; set; }

    /// <summary>Define os prazos da inspeção e do IUC.</summary>
    public DateOnly DataPrimeiraMatricula { get; set; }

    /// <summary>Cilindrada em cm³ (campo P.1 do certificado de matrícula), para estimar o IUC.</summary>
    public int? Cilindrada { get; set; }

    /// <summary>Emissões de CO2 em g/km (campo V.7), para estimar o IUC dos carros matriculados desde julho de 2007.</summary>
    public int? EmissoesCo2 { get; set; }

    public NormaEmissoes? NormaCo2 { get; set; }

    /// <summary>Uma data de renovação qualquer; o prazo repete-se todos os anos.</summary>
    public DateOnly? RenovacaoSeguro { get; set; }

    public string? Seguradora { get; set; }
    public string? ApoliceSeguro { get; set; }

    /// <summary>Prémio anual, para comparar com as propostas antes da renovação.</summary>
    public decimal? ValorSeguroAnual { get; set; }

    public DateOnly? ProximaRevisao { get; set; }

    /// <summary>Consumo médio (L/100 km) indicado pela pessoa; com abastecimentos registados, o portal calcula o real.</summary>
    public decimal? ConsumoLitros100Km { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
}

/// <summary>Um abastecimento. Com dois depósitos cheios seguidos, dá o consumo real entre eles.</summary>
public class Abastecimento
{
    public Guid Id { get; set; }
    public Guid VeiculoId { get; set; }
    public string UtilizadorId { get; set; } = "";
    public DateOnly Data { get; set; }

    /// <summary>Quilómetros no conta-quilómetros no momento do abastecimento.</summary>
    public int Quilometros { get; set; }

    /// <summary>Litros (ou kWh, num elétrico).</summary>
    public decimal Litros { get; set; }

    public decimal ValorTotal { get; set; }

    /// <summary>Encheu o depósito? Só entre depósitos cheios se sabe quanto se gastou.</summary>
    public bool DepositoCheio { get; set; } = true;

    public string? Posto { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
}
