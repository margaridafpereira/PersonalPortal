using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Portal.Modules.Carro;

public sealed record PostoCarregamento(string Id, string Morada, string Operador, string Tipo, decimal PotenciaKw, decimal CustoOperador, IReadOnlyList<string> Tarifas);

/// <param name="Energia">kWh do carregamento de referência usado na comparação.</param>
public sealed record CarregamentoConcelho(string Concelho, int NumeroPostos, decimal Energia, IReadOnlyList<PostoCarregamento> MaisBaratos);

public interface IPostosCarregamento
{
    Task<CarregamentoConcelho?> NoConcelhoAsync(string concelho, CancellationToken ct);
}

/// <summary>
/// Postos da rede Mobi.E e a tarifa de cada operador (OPC), do ficheiro público de tarifas da Mobi.E (CSV com cerca de 50 mil tomadas).
/// Só o custo do posto: a energia do comercializador (CEME, o cartão de carregamento) e os impostos somam-se a isto.
/// </summary>
public sealed class PostosMobiE(HttpClient http, IMemoryCache cache, ILogger<PostosMobiE> log) : IPostosCarregamento
{
    public const string Url = "https://www.mobie.pt/documents/42032/106470/Tarifas";

    /// <summary>Carregamento de referência para comparar postos com tarifas diferentes (por kWh, por minuto, por carregamento).</summary>
    public const decimal EnergiaReferencia = 20m;

    public async Task<CarregamentoConcelho?> NoConcelhoAsync(string concelho, CancellationToken ct)
    {
        try
        {
            var porConcelho = await cache.GetOrCreateAsync("mobie:postos", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
                return Interpretar(await http.GetStringAsync(Url, ct));
            });
            return porConcelho!.TryGetValue(Normalizar(concelho), out var postos) && postos.Count > 0
                ? new CarregamentoConcelho(postos[0].Concelho, postos.Count, EnergiaReferencia, postos.OrderBy(p => p.Posto.CustoOperador).Take(5).Select(p => p.Posto).ToList())
                : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(e, "Não foi possível ler as tarifas da Mobi.E.");
            return null;
        }
    }

    /// <summary>"€ 0.04 /min após 0 min até 45 min", "€ 0.261 /charge", "€ 0.1 /kWh".</summary>
    private static readonly Regex Tarifa = new(
        @"€\s*(?<valor>[\d.,]+)\s*/\s*(?<unidade>charge|kwh|min)(?:\s*após\s*(?<apos>\d+)\s*min)?(?:\s*até\s*(?<ate>\d+)\s*min)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Dictionary<string, List<(string Concelho, PostoCarregamento Posto)>> Interpretar(string csv)
    {
        var linhas = csv.TrimStart('﻿').Split('\n');
        var cabecalho = linhas[0].TrimEnd('\r').Split(';');
        int Col(string nome) => Array.IndexOf(cabecalho, nome);
        int id = Col("ID"), tomada = Col("UID_TOMADA"), tipo = Col("TIPO_POSTO"), municipio = Col("MUNICIPIO"), morada = Col("MORADA"),
            operador = Col("OPERADOR"), tarifa = Col("TARIFA"), potencia = Col("POTENCIA_TOMADA");

        // Tarifas por tomada; o custo de um posto é o da sua tomada mais barata.
        var tomadas = new Dictionary<string, (string[] Campos, List<string> Tarifas)>();
        foreach (var linha in linhas.Skip(1))
        {
            var c = linha.TrimEnd('\r').Split(';');
            if (c.Length <= Math.Max(potencia, tarifa) || c[tomada].Length == 0)
                continue;
            if (!tomadas.TryGetValue(c[tomada], out var t))
                tomadas[c[tomada]] = t = (c, []);
            t.Tarifas.Add(c[tarifa].Trim());
        }

        var postos = new Dictionary<string, (string Concelho, PostoCarregamento Posto)>();
        foreach (var (campos, tarifas) in tomadas.Values)
        {
            if (!decimal.TryParse(campos[potencia].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var kw) || kw <= 0)
                continue;
            var custo = CustoOperador(tarifas, kw, EnergiaReferencia);
            var posto = new PostoCarregamento(campos[id], campos[morada].Trim(), campos[operador].Trim(), campos[tipo].Trim(), kw, custo, tarifas);
            if (!postos.TryGetValue(campos[id], out var atual) || custo < atual.Posto.CustoOperador)
                postos[campos[id]] = (campos[municipio].Trim(), posto);
        }

        return postos.Values.GroupBy(p => Normalizar(p.Concelho)).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Custo do operador para carregar <paramref name="energia"/> kWh numa tomada de <paramref name="potenciaKw"/>
    /// (limitada a 50 kW, o que um carro comum aceita), com as condições de tempo ("até 45 min", "após 240 min").
    /// </summary>
    public static decimal CustoOperador(IEnumerable<string> tarifas, decimal potenciaKw, decimal energia)
    {
        var minutos = energia / Math.Min(potenciaKw, 50m) * 60m;
        decimal total = 0;
        foreach (var t in tarifas)
        {
            if (Tarifa.Match(t) is not { Success: true } m
                || !decimal.TryParse(m.Groups["valor"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor))
                continue;
            var de = m.Groups["apos"].Success ? decimal.Parse(m.Groups["apos"].Value, CultureInfo.InvariantCulture) : 0m;
            var ate = m.Groups["ate"].Success ? decimal.Parse(m.Groups["ate"].Value, CultureInfo.InvariantCulture) : decimal.MaxValue;
            var minutosNoIntervalo = Math.Max(0, Math.Min(minutos, ate) - de);

            total += m.Groups["unidade"].Value.ToLowerInvariant() switch
            {
                "charge" => valor,
                "kwh" => valor * energia * (minutos == 0 ? 1 : minutosNoIntervalo / minutos),
                _ => valor * minutosNoIntervalo,
            };
        }
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static string Normalizar(string nome) =>
        new string(nome.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
}
