using System.Text.Json;

namespace Portal.Modules.Apoios;

/// <summary>
/// Valores de referência que mudam todos os anos (IAS, escalões, limites).
/// Ficam num ficheiro por ano para que as regras nunca tenham números escritos à mão.
/// </summary>
public sealed record Indexantes(int Ano, DateOnly VerificadoEm, IReadOnlyDictionary<string, ValorIndexante> Valores)
{
    public decimal this[string chave] =>
        Valores.TryGetValue(chave, out var v) ? v.Valor : throw new KeyNotFoundException($"Indexante '{chave}' não existe em {Ano}.");
}

public sealed record ValorIndexante(decimal Valor, string Descricao, string Fonte);

public static class ChavesIndexantes
{
    public const string Ias = "ias";
    public const string LimiteEscalao6Irs = "limiteEscalao6Irs";
    public const string GarantiaPublicaValorMaximo = "garantiaPublicaValorMaximo";
    public const string IdadeMaximaJovem = "idadeMaximaJovem";
    public const string LimiteIsencaoIrsJovem = "limiteIsencaoIrsJovem";
    public const string ImtJovemIsencaoTotal = "imtJovemIsencaoTotal";
    public const string ImtJovemIsencaoParcial = "imtJovemIsencaoParcial";
    public const string IasPedidosNovosAbono = "iasPedidosNovosAbono";
    public const string ApoioRendaMaximoMensal = "apoioRendaMaximoMensal";
}

public interface IFonteIndexantes
{
    /// <summary>Indexantes do ano pedido, ou null se ainda não foram publicados no portal.</summary>
    Indexantes? Obter(int ano);
}

/// <summary>Lê os ficheiros <c>Dados/indexantes-AAAA.json</c> embebidos no assembly.</summary>
public sealed class IndexantesEmbebidos : IFonteIndexantes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<int, Indexantes> _porAno;

    public IndexantesEmbebidos()
    {
        var assembly = typeof(IndexantesEmbebidos).Assembly;
        _porAno = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".indexantes-", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                return JsonSerializer.Deserialize<Indexantes>(stream, Json)
                    ?? throw new InvalidOperationException($"Ficheiro de indexantes inválido: {n}");
            })
            .ToDictionary(i => i.Ano);
    }

    public Indexantes? Obter(int ano) => _porAno.GetValueOrDefault(ano);
}
