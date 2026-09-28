using System.Text.Json.Serialization;

namespace Portal.Modules.Anuncios;

[JsonConverter(typeof(JsonStringEnumConverter<Negocio>))]
public enum Negocio { Comprar, Arrendar }

[JsonConverter(typeof(JsonStringEnumConverter<TipoImovel>))]
public enum TipoImovel { Apartamento, Moradia, Terreno }

[JsonConverter(typeof(JsonStringEnumConverter<EstadoFavorito>))]
public enum EstadoFavorito { Ativo, Visitado, Descartado, SaiuDoPortal }

/// <summary>
/// Uma pesquisa que o utilizador quer repetir. Guardamos os critérios, nunca os anúncios:
/// os links abrem a pesquisa no site de cada portal (ver docs/casas/03-como-ver-anuncios.md).
/// </summary>
public class PesquisaGuardada
{
    public Guid Id { get; set; }
    public string UtilizadorId { get; set; } = "";
    public string Nome { get; set; } = "";
    public Negocio Negocio { get; set; }
    public TipoImovel Tipo { get; set; }
    public string Distrito { get; set; } = "";
    public string Concelho { get; set; } = "";
    public decimal? PrecoMaximo { get; set; }
    public int? QuartosMinimo { get; set; }
    public DateTimeOffset CriadaEm { get; set; }
}

/// <summary>Um anúncio que o utilizador segue. Os dados são introduzidos por ele.</summary>
public class ImovelFavorito
{
    public Guid Id { get; set; }
    public string UtilizadorId { get; set; } = "";
    public string Url { get; set; } = "";
    public string Titulo { get; set; } = "";
    public TipoImovel Tipo { get; set; }
    public string? Tipologia { get; set; }
    public decimal? AreaM2 { get; set; }
    public string? Concelho { get; set; }
    public EstadoFavorito Estado { get; set; }
    public string? Notas { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public List<RegistoPreco> Precos { get; set; } = [];
}

public class RegistoPreco
{
    public DateOnly Data { get; set; }
    public decimal Preco { get; set; }
}
