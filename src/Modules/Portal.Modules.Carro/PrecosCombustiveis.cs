using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Portal.Modules.Carro;

public sealed record Posto(string Nome, string Marca, string Morada, string Localidade, decimal Preco, string AtualizadoEm, double? Latitude, double? Longitude);

/// <param name="Localidades">As localidades dos postos do concelho (como a DGEG as indica), para filtrar.</param>
/// <param name="Localidade">A localidade filtrada, ou null para o concelho todo.</param>
public sealed record PrecosConcelho(string Concelho, Combustivel Combustivel, decimal Minimo, decimal Media, int NumeroPostos, IReadOnlyList<Posto> MaisBaratos,
    IReadOnlyList<string>? Localidades = null, string? Localidade = null);

public interface IPrecosCombustiveis
{
    /// <param name="localidade">Só os postos desta localidade (normalmente a freguesia ou a vila); null = o concelho todo.</param>
    Task<PrecosConcelho?> NoConcelhoAsync(string concelho, Combustivel combustivel, CancellationToken ct, string? localidade = null);

    /// <summary>Os concelhos que a DGEG conhece, para escolher onde procurar.</summary>
    Task<IReadOnlyList<string>> ConcelhosAsync(CancellationToken ct);
}

/// <summary>
/// Preços dos postos publicados pela DGEG (precoscombustiveis.dgeg.gov.pt), atualizados diariamente pelos postos.
/// A lista de concelhos fica em cache 24 h; os preços, 1 h.
/// </summary>
public sealed class PrecosDgeg(HttpClient http, IMemoryCache cache, ILogger<PrecosDgeg> log) : IPrecosCombustiveis
{
    private sealed record Municipio(int IdDistrito, int Id, string Nome);

    public async Task<IReadOnlyList<string>> ConcelhosAsync(CancellationToken ct)
    {
        try
        {
            var municipios = await MunicipiosAsync(ct);
            return municipios.Values.Select(m => m.Nome).Order(StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true)).ToList();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning(e, "Não foi possível ler a lista de concelhos da DGEG.");
            return [];
        }
    }

    private async Task<Dictionary<string, Municipio>> MunicipiosAsync(CancellationToken ct) =>
        (await cache.GetOrCreateAsync("dgeg:municipios", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            return await CarregarMunicipiosAsync(ct);
        }))!;

    public async Task<PrecosConcelho?> NoConcelhoAsync(string concelho, Combustivel combustivel, CancellationToken ct, string? localidade = null)
    {
        if (combustivel == Combustivel.Eletrico)
            return null;

        try
        {
            var municipios = await MunicipiosAsync(ct);
            if (!municipios.TryGetValue(Normalizar(concelho), out var m))
                return null;

            // Guarda todos os postos do concelho: filtrar por localidade não obriga a pedir outra vez à DGEG.
            var postos = await cache.GetOrCreateAsync($"dgeg:postos:{m.Id}:{(int)combustivel}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                var json = await http.GetStringAsync(
                    $"api/PrecoComb/PesquisarPostos?idsTiposComb={(int)combustivel}&idDistrito={m.IdDistrito}&idsMunicipios={m.Id}&qtdPorPagina=200&pagina=1", ct);
                return LerPostos(json);
            });
            return Resumir(m.Nome, combustivel, postos!, localidade);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning(e, "Não foi possível ler os preços da DGEG para {Concelho}.", concelho);
            return null;
        }
    }

    private async Task<Dictionary<string, Municipio>> CarregarMunicipiosAsync(CancellationToken ct)
    {
        using var distritos = JsonDocument.Parse(await http.GetStringAsync("api/PrecoComb/GetDistritos", ct));
        var resultado = new Dictionary<string, Municipio>();
        foreach (var d in distritos.RootElement.GetProperty("resultado").EnumerateArray())
        {
            var idDistrito = d.GetProperty("Id").GetInt32();
            using var municipios = JsonDocument.Parse(await http.GetStringAsync($"api/PrecoComb/GetMunicipios?idDistrito={idDistrito}", ct));
            foreach (var m in municipios.RootElement.GetProperty("resultado").EnumerateArray())
            {
                var nome = m.GetProperty("Descritivo").GetString() ?? "";
                resultado[Normalizar(nome)] = new Municipio(idDistrito, m.GetProperty("Id").GetInt32(), nome);
            }
        }
        return resultado;
    }

    /// <summary>Converte a resposta de PesquisarPostos e resume o concelho todo.</summary>
    public static PrecosConcelho? Interpretar(string json, string concelho, Combustivel combustivel) =>
        Resumir(concelho, combustivel, LerPostos(json), null);

    /// <summary>Mínimo, média e os 5 mais baratos, no concelho todo ou só numa localidade. Null se não houver postos.</summary>
    public static PrecosConcelho? Resumir(string concelho, Combustivel combustivel, IReadOnlyList<Posto> todos, string? localidade)
    {
        var localidades = todos.Select(p => p.Localidade).Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true)).ToList();
        var postos = string.IsNullOrWhiteSpace(localidade) ? todos
            : todos.Where(p => Normalizar(p.Localidade) == Normalizar(localidade)).ToList();
        if (postos.Count == 0)
            return null;

        var ordenados = postos.OrderBy(p => p.Preco).ToList();
        return new PrecosConcelho(concelho, combustivel, ordenados[0].Preco, Math.Round(postos.Average(p => p.Preco), 3, MidpointRounding.AwayFromZero),
            postos.Count, ordenados.Take(5).ToList(), localidades, string.IsNullOrWhiteSpace(localidade) ? null : ordenados[0].Localidade);
    }

    /// <summary>Os postos da resposta de PesquisarPostos. Os preços vêm como texto: "1,336 €".</summary>
    public static List<Posto> LerPostos(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var postos = new List<Posto>();
        foreach (var p in doc.RootElement.GetProperty("resultado").EnumerateArray())
        {
            var texto = (p.GetProperty("Preco").GetString() ?? "").Replace("€", "").Trim();
            if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-PT"), out var preco) || preco <= 0)
                continue;
            postos.Add(new Posto(
                Texto(p, "Nome"), Texto(p, "Marca"), Texto(p, "Morada"), Texto(p, "Localidade"), preco, Texto(p, "DataAtualizacao"),
                p.TryGetProperty("Latitude", out var lat) && lat.ValueKind == JsonValueKind.Number ? lat.GetDouble() : null,
                p.TryGetProperty("Longitude", out var lon) && lon.ValueKind == JsonValueKind.Number ? lon.GetDouble() : null));
        }

        return postos;
    }

    private static string Texto(JsonElement e, string campo) =>
        e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()!.Trim() : "";

    private static string Normalizar(string nome) =>
        new string(nome.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
}
