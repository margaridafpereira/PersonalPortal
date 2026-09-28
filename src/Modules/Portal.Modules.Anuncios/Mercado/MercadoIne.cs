using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Portal.Modules.Anuncios.Mercado;

/// <summary>Valor mediano das vendas de alojamentos familiares num concelho, em €/m² (INE).</summary>
public sealed record MedianaConcelho(string Concelho, string Periodo, decimal? Total, decimal? Novos, decimal? Existentes);

public interface IMercadoImobiliario
{
    Task<MedianaConcelho?> MedianaVendasAsync(string concelho, CancellationToken ct);
}

/// <summary>
/// Lê o indicador 0012234 do INE ("Valor mediano das vendas... nos últimos 12 meses, €/m²", trimestral)
/// e guarda o último trimestre publicado em memória durante 12 horas.
/// Documentação: docs/casas/01-fontes-de-dados.md.
/// </summary>
public sealed class MercadoIne(HttpClient http, IMemoryCache cache, TimeProvider relogio, ILogger<MercadoIne> log) : IMercadoImobiliario
{
    public const string Indicador = "0012234";

    public async Task<MedianaConcelho?> MedianaVendasAsync(string concelho, CancellationToken ct)
    {
        var tabela = await cache.GetOrCreateAsync("ine:" + Indicador, async entrada =>
        {
            var dados = await CarregarUltimoTrimestreAsync(ct);
            // Se o INE falhar, tenta outra vez daqui a pouco em vez de ficar 12 horas sem dados.
            entrada.AbsoluteExpirationRelativeToNow = dados is null ? TimeSpan.FromMinutes(10) : TimeSpan.FromHours(12);
            return dados;
        });

        return tabela?.GetValueOrDefault(Normalizar(concelho));
    }

    private async Task<Dictionary<string, MedianaConcelho>?> CarregarUltimoTrimestreAsync(CancellationToken ct)
    {
        // O INE publica com cerca de 3 a 6 meses de atraso: tenta do trimestre atual para trás.
        var data = relogio.GetLocalNow();
        var (ano, trimestre) = (data.Year, (data.Month - 1) / 3 + 1);
        for (var tentativa = 0; tentativa < 6; tentativa++)
        {
            var periodo = $"S5A{ano}{trimestre}";
            try
            {
                using var resposta = await http.GetAsync($"ine/json_indicador/pindica.jsp?op=2&varcd={Indicador}&Dim1={periodo}&lang=PT", ct);
                resposta.EnsureSuccessStatusCode();
                var tabela = Interpretar(await resposta.Content.ReadAsStringAsync(ct));
                if (tabela is not null)
                    return tabela;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
            {
                log.LogWarning(e, "Não foi possível ler o INE ({Periodo}).", periodo);
                return null;
            }

            (ano, trimestre) = trimestre == 1 ? (ano - 1, 4) : (ano, trimestre - 1);
        }
        return null;
    }

    /// <summary>Converte a resposta do INE numa tabela por concelho. Devolve null se o período não existir.</summary>
    public static Dictionary<string, MedianaConcelho>? Interpretar(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement[0];
        if (!raiz.TryGetProperty("Dados", out var dados))
            return null;

        var periodo = dados.EnumerateObject().First();
        var valores = new Dictionary<string, Dictionary<string, decimal?>>();
        var nomes = new Dictionary<string, string>();

        foreach (var linha in periodo.Value.EnumerateArray())
        {
            // Os concelhos têm códigos de 7 caracteres (ex.: 1A01106); freguesias têm 9 e regiões menos.
            var geocod = linha.GetProperty("geocod").GetString() ?? "";
            if (geocod.Length != 7)
                continue;

            nomes[geocod] = linha.GetProperty("geodsg").GetString() ?? "";
            var categoria = linha.GetProperty("dim_3").GetString() ?? "";
            decimal? valor = linha.TryGetProperty("valor", out var v) && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

            if (!valores.TryGetValue(geocod, out var porCategoria))
                valores[geocod] = porCategoria = [];
            porCategoria[categoria] = valor;
        }

        return valores.ToDictionary(
            kv => Normalizar(nomes[kv.Key]),
            kv => new MedianaConcelho(nomes[kv.Key], periodo.Name, kv.Value.GetValueOrDefault("H1"), kv.Value.GetValueOrDefault("H11"), kv.Value.GetValueOrDefault("H12")));
    }

    private static string Normalizar(string nome) =>
        new string(nome.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
}
