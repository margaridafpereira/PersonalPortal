namespace Portal.Modules.Investimentos;

/// <summary>Classificação dos títulos e dos países para o Anexo J.</summary>
public static class Classificacao
{
    private static readonly string[] SinaisEtf =
    [
        "ETF", "UCITS", "ISHARES", "VANGUARD", "XTRACKERS", "AMUNDI", "SPDR", "INVESCO", "LYXOR", "WISDOMTREE",
        "VANECK", "HSBC MSCI", "FRANKLIN FTSE", "GLOBAL X", "ACC", "DIST",
    ];

    /// <summary>
    /// Sugere o tipo a partir do nome. Só sugere: "ACC"/"DIST" e as marcas de ETF acertam na maioria dos casos,
    /// mas a pessoa confirma na página (e o relatório diz quais foram sugeridos).
    /// </summary>
    public static TipoAtivo Sugerir(string nome)
    {
        var palavras = nome.ToUpperInvariant().Split([' ', '-', '(', ')', ',', '.'], StringSplitOptions.RemoveEmptyEntries);
        var texto = " " + string.Join(' ', palavras) + " ";
        return SinaisEtf.Any(s => texto.Contains(" " + s + " ", StringComparison.Ordinal)) ? TipoAtivo.Etf : TipoAtivo.Acao;
    }

    public static string CodigoAnexoJ(TipoAtivo tipo) => tipo == TipoAtivo.Acao ? "G01" : "G20";

    /// <summary>
    /// Territórios da lista de regimes fiscais mais favoráveis (Portaria n.º 150/2004) que aparecem
    /// com frequência em ISIN. Lista parcial: confirmar a versão em vigor.
    /// </summary>
    public static readonly IReadOnlySet<string> RegimesFiscaisFavoraveis = new HashSet<string>
    {
        "KY", "BM", "JE", "GG", "IM", "VG", "BS", "PA", "GI", "AI", "AG", "BB", "BZ", "MU", "SC", "LB",
    };
}
