using System.Text;
using System.Text.Json.Serialization;
using Portal.Core.Perfil;
using Portal.Modules.Apoios.Regras;

namespace Portal.Modules.Apoios.Prazos;

[JsonConverter(typeof(JsonStringEnumConverter<CategoriaPrazo>))]
public enum CategoriaPrazo { Impostos, SegurancaSocial, Apoios }

public sealed record Prazo(DateOnly Data, string Titulo, string Descricao, CategoriaPrazo Categoria, string? Link, bool PorConfirmar = false)
{
    public int DiasEmFalta(DateOnly hoje) => Data.DayNumber - hoje.DayNumber;
}

/// <summary>
/// Prazos pessoais: só aparecem os que se aplicam ao perfil. Fonte: docs/apoios/03-prazos.md.
/// Prazos que caem ao fim de semana passam para o dia útil seguinte (feriados ainda não são tratados).
/// </summary>
public static class CalendarioPrazos
{
    private const string AgendaFiscal = "https://info.portaldasfinancas.gov.pt/pt/apoio_contribuinte/calendario_fiscal/Pages/default.aspx";

    public static IReadOnlyList<Prazo> Proximos(PerfilUtilizador p, DateOnly hoje, int meses = 12)
    {
        var fim = hoje.AddMonths(meses);
        return Enumerable.Range(hoje.Year, fim.Year - hoje.Year + 1)
            .SelectMany(ano => DoAno(p, ano, hoje))
            .Select(pr => pr with { Data = DiaUtil(pr.Data) })
            .Where(pr => pr.Data >= hoje && pr.Data <= fim)
            .OrderBy(pr => pr.Data)
            .ToList();
    }

    private static IEnumerable<Prazo> DoAno(PerfilUtilizador p, int ano, DateOnly hoje)
    {
        if (p.CategoriaRendimento != CategoriaRendimento.Nenhum)
        {
            yield return new Prazo(new DateOnly(ano, 2, 25), "Validar faturas no e-Fatura",
                "Confirma as despesas que contam para as deduções do IRS.", CategoriaPrazo.Impostos, "https://faturas.portaldasfinancas.gov.pt/", PorConfirmar: true);
            yield return new Prazo(new DateOnly(ano, 6, 30), "Entregar o IRS",
                $"Prazo de entrega da declaração de IRS (de 1 de abril a 30 de junho de {ano}).", CategoriaPrazo.Impostos, AgendaFiscal);
        }

        if (p.CategoriaRendimento is CategoriaRendimento.TrabalhoIndependente or CategoriaRendimento.Ambos)
        {
            (int mes, string trimestre)[] trimestral = [(1, "out. a dez."), (4, "jan. a mar."), (7, "abr. a jun."), (10, "jul. a set.")];
            foreach (var (mes, trimestre) in trimestral)
                yield return new Prazo(UltimoDia(ano, mes), "Declaração trimestral à Segurança Social",
                    $"Rendimentos de {trimestre}. Obrigatória mesmo sem rendimentos.", CategoriaPrazo.SegurancaSocial, "https://www.seg-social.pt/");
        }

        if (p.ProprietarioImovel == true)
        {
            int[] meses = p.ValorImi switch
            {
                null or <= 100 => [5],
                <= 500 => [5, 11],
                _ => [5, 8, 11],
            };
            for (var i = 0; i < meses.Length; i++)
                yield return new Prazo(UltimoDia(ano, meses[i]), meses.Length == 1 ? "Pagar o IMI" : $"Pagar o IMI ({i + 1}.ª prestação)",
                    p.ValorImi is null ? "O número de prestações depende do valor do IMI; indica-o no perfil." : "Pagamento no Portal das Finanças ou no multibanco.",
                    CategoriaPrazo.Impostos, AgendaFiscal, PorConfirmar: true);
        }

        if (p.TemVeiculo == true && p.MesMatricula is >= 1 and <= 12)
            yield return new Prazo(UltimoDia(ano, p.MesMatricula.Value), "Pagar o IUC",
                "O IUC paga-se no mês da matrícula do veículo.", CategoriaPrazo.Impostos, AgendaFiscal);

        var compraJovem = (p.ProcuraComprarCasa == true || p.SituacaoHabitacao == SituacaoHabitacao.ProcuraComprar)
            && p.IdadeEm(hoje) is <= 35;
        if (compraJovem && ano == MotorApoios.FimRegimesJovemCompra.Year)
            yield return new Prazo(MotorApoios.FimRegimesJovemCompra, "Fim do IMT Jovem e da garantia pública",
                "Última data para a escritura e o contrato de crédito beneficiarem dos regimes para jovens.", CategoriaPrazo.Apoios,
                "https://info.portaldasfinancas.gov.pt/pt/apoio_contribuinte/IMT_Jovem/Pages/default.aspx");
    }

    private static DateOnly UltimoDia(int ano, int mes) => new(ano, mes, DateTime.DaysInMonth(ano, mes));

    public static DateOnly DiaUtil(DateOnly data) => data.DayOfWeek switch
    {
        DayOfWeek.Saturday => data.AddDays(2),
        DayOfWeek.Sunday => data.AddDays(1),
        _ => data,
    };

    /// <summary>Ficheiro iCalendar para importar no Google Calendar, Outlook ou telemóvel.</summary>
    public static string ParaIcs(IEnumerable<Prazo> prazos, IEnumerable<int> diasAntecedencia, DateTimeOffset agora)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Portal pessoal//Prazos//PT\r\nCALSCALE:GREGORIAN\r\n");
        foreach (var p in prazos)
        {
            sb.Append("BEGIN:VEVENT\r\n");
            // UID estável: reimportar o ficheiro atualiza os eventos em vez de os duplicar.
            sb.Append($"UID:{p.Data:yyyyMMdd}-{new string(p.Titulo.Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant()}@portal-pessoal\r\n");
            sb.Append($"DTSTAMP:{agora.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}\r\n");
            sb.Append($"DTSTART;VALUE=DATE:{p.Data:yyyyMMdd}\r\n");
            sb.Append($"DTEND;VALUE=DATE:{p.Data.AddDays(1):yyyyMMdd}\r\n");
            sb.Append($"SUMMARY:{Escapar(p.Titulo)}\r\n");
            sb.Append($"DESCRIPTION:{Escapar(p.Descricao)}\r\n");
            if (p.Link is not null)
                sb.Append($"URL:{p.Link}\r\n");
            foreach (var dias in diasAntecedencia)
                sb.Append($"BEGIN:VALARM\r\nACTION:DISPLAY\r\nDESCRIPTION:{Escapar(p.Titulo)}\r\nTRIGGER:-P{dias}D\r\nEND:VALARM\r\n");
            sb.Append("END:VEVENT\r\n");
        }
        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    private static string Escapar(string texto) =>
        texto.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
}
