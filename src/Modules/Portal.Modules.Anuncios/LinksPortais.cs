using System.Globalization;
using System.Text;

namespace Portal.Modules.Anuncios;

/// <param name="Verificado">true quando o formato do URL foi testado (docs/casas/03-como-ver-anuncios.md).</param>
public sealed record LinkPortal(string Portal, string Url, bool Verificado);

/// <summary>
/// Traduz os critérios de uma pesquisa para o URL de pesquisa de cada portal.
/// Os portais mudam os URLs sem aviso: os que não estão verificados abrem pelo menos a zona certa.
/// </summary>
public static class LinksPortais
{
    public static IReadOnlyList<LinkPortal> Para(PesquisaGuardada p)
    {
        var concelho = Slug(p.Concelho);
        var distrito = Slug(p.Distrito);
        var comprar = p.Negocio == Negocio.Comprar;
        var preco = p.PrecoMaximo is { } v ? ((long)v).ToString(CultureInfo.InvariantCulture) : null;

        var tipoImovirtual = p.Tipo switch { TipoImovel.Apartamento => "apartamento", TipoImovel.Moradia => "moradia", _ => "terreno" };
        var imovirtual = $"https://www.imovirtual.com/pt/resultados/{(comprar ? "comprar" : "arrendar")}/{tipoImovirtual}/{distrito}/{concelho}"
            + (preco is null ? "" : $"?priceMax={preco}");

        var idealista = $"https://www.idealista.pt/{(comprar ? "comprar" : "arrendar")}-{(p.Tipo == TipoImovel.Terreno ? "terrenos" : "casas")}/{concelho}/"
            + (preco is null ? "" : $"com-preco-max_{preco}/");

        var casaYes = $"https://casayes.pt/pt/{(comprar ? "comprar" : "arrendar")}/{tipoImovirtual}/{distrito}/{concelho}";

        var tipoSapo = p.Tipo switch { TipoImovel.Apartamento => "apartamentos", TipoImovel.Moradia => "moradias", _ => "terrenos" };
        var casaSapo = $"https://casa.sapo.pt/{(comprar ? "comprar" : "alugar")}-{tipoSapo}/{concelho}/";

        var supercasa = $"https://supercasa.pt/{(comprar ? "comprar" : "arrendar")}-{(p.Tipo == TipoImovel.Terreno ? "terrenos" : "casas")}/{concelho}";

        return
        [
            new("Imovirtual", imovirtual, Verificado: true),
            new("Idealista", idealista, Verificado: false),
            new("Casa Yes", casaYes, Verificado: false),
            new("Casa Sapo", casaSapo, Verificado: false),
            new("Supercasa", supercasa, Verificado: false),
        ];
    }

    /// <summary>"Vila Nova de Gaia" → "vila-nova-de-gaia"; "Setúbal" → "setubal".</summary>
    public static string Slug(string texto)
    {
        var sb = new StringBuilder();
        foreach (var c in texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsAsciiLetterOrDigit(c))
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }
        return sb.ToString().TrimEnd('-');
    }

    /// <summary>Nome do portal a partir do URL de um anúncio, para mostrar na lista de favoritos.</summary>
    public static string PortalDe(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host switch
            {
                var h when h.EndsWith("idealista.pt") => "Idealista",
                var h when h.EndsWith("imovirtual.com") => "Imovirtual",
                var h when h.EndsWith("casayes.pt") => "Casa Yes",
                var h when h.EndsWith("casa.sapo.pt") => "Casa Sapo",
                var h when h.EndsWith("supercasa.pt") => "Supercasa",
                var h when h.EndsWith("e-leiloes.pt") => "e-Leilões",
                var h => h.StartsWith("www.") ? h[4..] : h,
            }
            : "Outro";
}
