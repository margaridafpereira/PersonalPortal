using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Portal.Modules.Investimentos;

public interface ICambios
{
    /// <summary>
    /// Câmbio de referência do euro (1 EUR = X unidades da moeda) no dia, ou no último dia útil anterior
    /// com publicação. Devolve 1 para EUR e null se a moeda não for suportada ou não houver dados.
    /// </summary>
    Task<decimal?> TaxaAsync(string moeda, DateOnly data, CancellationToken ct);
}

/// <summary>
/// Câmbios de referência do euro publicados pelo Banco de Portugal (BPstat, domínio 29 "Taxas de câmbio").
/// Documentação: docs/investimentos/01-fontes-e-regras.md.
/// </summary>
public sealed class CambiosBancoDePortugal(HttpClient http, IMemoryCache cache, ILogger<CambiosBancoDePortugal> log) : ICambios
{
    private const string Dataset = "23e0cdd56bddb4ad3016a9c3ad63a539";

    /// <summary>Séries diárias "Moeda face ao Euro" no BPstat.</summary>
    public static readonly IReadOnlyDictionary<string, int> Series = new Dictionary<string, int>
    {
        ["USD"] = 12531971, ["GBP"] = 12531970, ["CHF"] = 12531968, ["JPY"] = 12531951, ["CAD"] = 12531936,
        ["AUD"] = 12531935, ["DKK"] = 12531942, ["SEK"] = 12531967, ["NOK"] = 12531960, ["HKD"] = 12531945,
        ["CNY"] = 12531938, ["PLN"] = 12531973, ["CZK"] = 12531941, ["HUF"] = 12531946, ["BRL"] = 12531976,
        ["SGD"] = 12531963, ["NZD"] = 12531959, ["ZAR"] = 12531966, ["MXN"] = 12531958, ["ILS"] = 12531950,
    };

    public async Task<decimal?> TaxaAsync(string moeda, DateOnly data, CancellationToken ct)
    {
        moeda = moeda.Trim().ToUpperInvariant();
        if (moeda == "EUR")
            return 1m;
        if (!Series.TryGetValue(moeda, out var serie))
            return null;

        // Uma série por moeda e por ano, com um mês antes para cobrir o início de janeiro.
        var taxas = await cache.GetOrCreateAsync($"bdp:{moeda}:{data.Year}", async e =>
        {
            var dados = await CarregarAsync(serie, new DateOnly(data.Year - 1, 12, 1), ct);
            e.AbsoluteExpirationRelativeToNow = dados is null ? TimeSpan.FromMinutes(10) : TimeSpan.FromHours(12);
            return dados;
        });

        if (taxas is null)
            return null;

        // O câmbio aplicável é o do dia ou, sem publicação (fim de semana, feriado), o do último dia anterior.
        decimal? taxa = null;
        foreach (var (dia, valor) in taxas)
        {
            if (dia > data)
                break;
            taxa = valor;
        }
        return taxa;
    }

    private async Task<SortedList<DateOnly, decimal>?> CarregarAsync(int serie, DateOnly desde, CancellationToken ct)
    {
        try
        {
            var json = await http.GetStringAsync(
                $"data/v1/domains/29/datasets/{Dataset}/?lang=PT&series_ids={serie}&obs_since={desde:yyyy-MM-dd}", ct);
            return Interpretar(json);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning(e, "Não foi possível ler os câmbios do Banco de Portugal (série {Serie}).", serie);
            return null;
        }
    }

    /// <summary>Converte a resposta JSON-stat do BPstat: "value" alinhado com as datas de "reference_date".</summary>
    public static SortedList<DateOnly, decimal> Interpretar(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        var datas = raiz.GetProperty("dimension").GetProperty("reference_date").GetProperty("category").GetProperty("index")
            .EnumerateArray().Select(d => DateOnly.ParseExact(d.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList();
        var valores = raiz.GetProperty("value").EnumerateArray().ToList();

        var resultado = new SortedList<DateOnly, decimal>();
        for (var i = 0; i < datas.Count && i < valores.Count; i++)
            if (valores[i].ValueKind == JsonValueKind.Number)
                resultado[datas[i]] = valores[i].GetDecimal();
        return resultado;
    }
}
