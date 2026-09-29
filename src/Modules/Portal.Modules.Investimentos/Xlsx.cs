using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Portal.Modules.Investimentos;

/// <summary>
/// Leitor mínimo de ficheiros Excel (.xlsx), sem dependências: um .xlsx é um ZIP com XML (Office Open XML).
/// Chega para os extratos das corretoras (XTB, eToro, Revolut…): texto, números e datas, sem fórmulas.
/// </summary>
public static class Xlsx
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace NsPkg = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>Formatos de data incorporados no Excel (ECMA-376, 18.8.30).</summary>
    private static readonly HashSet<int> FormatosDataIncorporados = [14, 15, 16, 17, 18, 19, 20, 21, 22, 45, 46, 47];

    public sealed record Folha(string Nome, List<string[]> Linhas);

    /// <summary>
    /// Converte o Excel em CSV para o resto da importação. Escolhe a folha que tem um formato conhecido
    /// (ex.: "Account Activity" da eToro) ou, sem nenhuma, a que tem mais linhas; ignora as linhas de título antes do cabeçalho.
    /// </summary>
    public static string ParaCsv(byte[] ficheiro)
    {
        var candidatas = Folhas(ficheiro).Select(f => ParaCsv(SemTitulos(f.Linhas))).Where(t => t.Length > 0).ToList();
        return candidatas.FirstOrDefault(t => Importacao.Detetar(t) != "universal")
            ?? candidatas.OrderByDescending(t => t.Count(c => c == '\n')).FirstOrDefault()
            ?? "";
    }

    public static List<Folha> Folhas(byte[] ficheiro)
    {
        using var zip = new ZipArchive(new MemoryStream(ficheiro), ZipArchiveMode.Read);
        var partilhados = TextosPartilhados(zip);
        var estilosData = EstilosData(zip);

        var livro = Ler(zip, "xl/workbook.xml") ?? throw new InvalidDataException("Não é um ficheiro .xlsx.");
        var relacoes = Ler(zip, "xl/_rels/workbook.xml.rels")?.Root?.Elements(NsPkg + "Relationship")
            .ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!) ?? [];

        var folhas = new List<Folha>();
        foreach (var folha in livro.Root!.Element(Ns + "sheets")?.Elements(Ns + "sheet") ?? [])
        {
            var id = (string?)folha.Attribute(NsRel + "id");
            if (id is null || !relacoes.TryGetValue(id, out var alvo))
                continue;
            var caminho = alvo.StartsWith('/') ? alvo.TrimStart('/') : "xl/" + alvo;
            if (Ler(zip, caminho) is { } xml)
                folhas.Add(new Folha((string?)folha.Attribute("name") ?? "", LerLinhas(xml, partilhados, estilosData)));
        }
        return folhas;
    }

    private static List<string[]> LerLinhas(XDocument folha, List<string> partilhados, HashSet<int> estilosData)
    {
        var linhas = new List<string[]>();
        foreach (var linha in folha.Root!.Element(Ns + "sheetData")?.Elements(Ns + "row") ?? [])
        {
            var celulas = new SortedDictionary<int, string>();
            var seguinte = 0;
            foreach (var c in linha.Elements(Ns + "c"))
            {
                // A referência (ex.: "C7") diz a coluna; células vazias não aparecem no XML.
                var coluna = (string?)c.Attribute("r") is { } r ? Coluna(r) : seguinte;
                seguinte = coluna + 1;
                celulas[coluna] = Valor(c, partilhados, estilosData);
            }
            var valores = new string[celulas.Count == 0 ? 0 : celulas.Keys.Max() + 1];
            Array.Fill(valores, "");
            foreach (var (i, v) in celulas)
                valores[i] = v;
            if (valores.Any(v => v.Length > 0))
                linhas.Add(valores);
        }
        return linhas;
    }

    private static string Valor(XElement c, List<string> partilhados, HashSet<int> estilosData)
    {
        var tipo = (string?)c.Attribute("t");
        var v = (string?)c.Element(Ns + "v");
        switch (tipo)
        {
            case "s":
                return int.TryParse(v, out var i) && i < partilhados.Count ? partilhados[i] : "";
            case "inlineStr":
                return string.Concat(c.Element(Ns + "is")?.Descendants(Ns + "t").Select(t => t.Value) ?? []);
            case "b":
                return v == "1" ? "TRUE" : "FALSE";
            case "str" or "e":
                return v ?? "";
        }
        if (v is null)
            return "";
        if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero))
            return v;

        // As datas no Excel são números (dias desde 1899-12-30) com um estilo de data.
        if (int.TryParse((string?)c.Attribute("s"), out var estilo) && estilosData.Contains(estilo) && numero is > 0 and < 2958466)
        {
            var data = DateTime.FromOADate(numero);
            return data.TimeOfDay == TimeSpan.Zero ? data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : data.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        // Números sem notação científica ("1E-05" não seria lido como número mais à frente).
        return decimal.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d.ToString(CultureInfo.InvariantCulture)
            : numero.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    private static List<string> TextosPartilhados(ZipArchive zip) =>
        Ler(zip, "xl/sharedStrings.xml")?.Root?.Elements(Ns + "si")
            .Select(si => string.Concat(si.Descendants(Ns + "t").Where(t => t.Parent?.Name != Ns + "rPh").Select(t => t.Value)))
            .ToList() ?? [];

    /// <summary>Índices dos estilos de célula (cellXfs) que formatam datas.</summary>
    private static HashSet<int> EstilosData(ZipArchive zip)
    {
        var estilos = Ler(zip, "xl/styles.xml")?.Root;
        if (estilos is null)
            return [];

        var formatosData = new HashSet<int>(FormatosDataIncorporados);
        foreach (var f in estilos.Element(Ns + "numFmts")?.Elements(Ns + "numFmt") ?? [])
            if (int.TryParse((string?)f.Attribute("numFmtId"), out var id) && EFormatoData((string?)f.Attribute("formatCode") ?? ""))
                formatosData.Add(id);

        var resultado = new HashSet<int>();
        var i = 0;
        foreach (var xf in estilos.Element(Ns + "cellXfs")?.Elements(Ns + "xf") ?? [])
        {
            if (int.TryParse((string?)xf.Attribute("numFmtId"), out var id) && formatosData.Contains(id))
                resultado.Add(i);
            i++;
        }
        return resultado;
    }

    /// <summary>Um formato é de data se, sem texto entre aspas e sem [cores], tiver dia, ano ou hora.</summary>
    private static bool EFormatoData(string formato)
    {
        var sb = new StringBuilder();
        var entreAspas = false;
        var entreParenteses = false;
        foreach (var ch in formato)
        {
            if (ch == '"') entreAspas = !entreAspas;
            else if (!entreAspas && ch == '[') entreParenteses = true;
            else if (!entreAspas && ch == ']') entreParenteses = false;
            else if (!entreAspas && !entreParenteses) sb.Append(char.ToLowerInvariant(ch));
        }
        var limpo = sb.ToString();
        return limpo != "general" && limpo.IndexOfAny(['d', 'y', 'h']) >= 0;
    }

    /// <summary>"C7" → 2 (colunas a partir de 0).</summary>
    private static int Coluna(string referencia)
    {
        var n = 0;
        foreach (var ch in referencia.TakeWhile(char.IsAsciiLetter))
            n = n * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        return n - 1;
    }

    /// <summary>
    /// Tira as linhas de título que alguns extratos têm antes do cabeçalho (ex.: nome do cliente, período).
    /// O cabeçalho é a primeira linha com pelo menos metade das colunas preenchidas.
    /// </summary>
    private static List<string[]> SemTitulos(List<string[]> linhas)
    {
        if (linhas.Count == 0)
            return linhas;
        var maximo = linhas.Max(l => l.Count(v => v.Length > 0));
        var inicio = linhas.FindIndex(l => l.Count(v => v.Length > 0) >= Math.Max(2, (maximo + 1) / 2));
        return inicio <= 0 ? linhas : linhas[inicio..];
    }

    private static string ParaCsv(List<string[]> linhas)
    {
        if (linhas.Count < 2)
            return "";
        var colunas = linhas.Max(l => l.Length);
        var sb = new StringBuilder();
        foreach (var linha in linhas)
        {
            for (var i = 0; i < colunas; i++)
            {
                if (i > 0) sb.Append(',');
                var v = i < linha.Length ? linha[i] : "";
                sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static XDocument? Ler(ZipArchive zip, string caminho)
    {
        var entrada = zip.GetEntry(caminho);
        if (entrada is null)
            return null;
        using var fluxo = entrada.Open();
        return XDocument.Load(fluxo);
    }
}
