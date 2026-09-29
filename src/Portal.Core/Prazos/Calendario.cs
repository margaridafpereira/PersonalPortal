using System.Text;

namespace Portal.Core.Prazos;

/// <summary>
/// Um evento de dia inteiro que qualquer secção pode exportar para o calendário.
/// <paramref name="Chave"/> distingue eventos com o mesmo título no mesmo dia (ex.: o id do veículo).
/// </summary>
public sealed record EventoCalendario(DateOnly Data, string Titulo, string Descricao, string? Link, string? Chave = null);

public static class Calendario
{
    /// <summary>
    /// Prazos que caem ao fim de semana ou num feriado nacional passam para o dia útil seguinte
    /// (Código Civil, art. 279.º, al. e); CPPT, art. 20.º). Os feriados municipais não contam: variam de concelho para concelho.
    /// </summary>
    public static DateOnly DiaUtil(DateOnly data)
    {
        while (data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || EFeriado(data))
            data = data.AddDays(1);
        return data;
    }

    public static bool EFeriado(DateOnly data) => FeriadosNacionais(data.Year).Contains(data);

    /// <summary>Feriados nacionais obrigatórios (Código do Trabalho, art. 234.º). O Carnaval é facultativo e não entra.</summary>
    public static IReadOnlySet<DateOnly> FeriadosNacionais(int ano)
    {
        var pascoa = Pascoa(ano);
        return new HashSet<DateOnly>
        {
            new(ano, 1, 1),        // Ano Novo
            pascoa.AddDays(-2),    // Sexta-feira Santa
            pascoa,                // Páscoa
            new(ano, 4, 25),       // Dia da Liberdade
            new(ano, 5, 1),        // Dia do Trabalhador
            pascoa.AddDays(60),    // Corpo de Deus
            new(ano, 6, 10),       // Dia de Portugal
            new(ano, 8, 15),       // Assunção de Nossa Senhora
            new(ano, 10, 5),       // Implantação da República
            new(ano, 11, 1),       // Todos os Santos
            new(ano, 12, 1),       // Restauração da Independência
            new(ano, 12, 8),       // Imaculada Conceição
            new(ano, 12, 25),      // Natal
        };
    }

    /// <summary>Domingo de Páscoa (algoritmo de Meeus/Jones/Butcher, calendário gregoriano).</summary>
    public static DateOnly Pascoa(int ano)
    {
        int a = ano % 19, b = ano / 100, c = ano % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(ano, mes, dia);
    }

    public static DateOnly UltimoDiaDoMes(int ano, int mes) => new(ano, mes, DateTime.DaysInMonth(ano, mes));

    /// <summary>Ficheiro iCalendar (RFC 5545) para importar no Google Calendar, Outlook ou telemóvel.</summary>
    public static string ParaIcs(IEnumerable<EventoCalendario> eventos, IEnumerable<int> diasAntecedencia, DateTimeOffset agora)
    {
        var dias = diasAntecedencia.ToList();
        var sb = new StringBuilder();
        void Linha(string texto) => sb.Append(Dobrar(texto)).Append("\r\n");

        Linha("BEGIN:VCALENDAR");
        Linha("VERSION:2.0");
        Linha("PRODID:-//Portal pessoal//Prazos//PT");
        Linha("CALSCALE:GREGORIAN");
        foreach (var e in eventos)
        {
            Linha("BEGIN:VEVENT");
            // UID estável: reimportar o ficheiro atualiza os eventos em vez de os duplicar.
            var chave = e.Chave is null ? "" : "-" + new string(e.Chave.Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant();
            Linha($"UID:{e.Data:yyyyMMdd}-{new string(e.Titulo.Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant()}{chave}@portal-pessoal");
            Linha($"DTSTAMP:{agora.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}");
            Linha($"DTSTART;VALUE=DATE:{e.Data:yyyyMMdd}");
            Linha($"DTEND;VALUE=DATE:{e.Data.AddDays(1):yyyyMMdd}");
            Linha($"SUMMARY:{Escapar(e.Titulo)}");
            Linha($"DESCRIPTION:{Escapar(e.Descricao)}");
            if (e.Link is not null)
                Linha($"URL:{e.Link}");
            foreach (var d in dias)
            {
                Linha("BEGIN:VALARM");
                Linha("ACTION:DISPLAY");
                Linha($"DESCRIPTION:{Escapar(e.Titulo)}");
                Linha($"TRIGGER:-P{d}D");
                Linha("END:VALARM");
            }
            Linha("END:VEVENT");
        }
        Linha("END:VCALENDAR");
        return sb.ToString();
    }

    private static string Escapar(string texto) =>
        texto.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\n").Replace("\r", "").Replace("\n", "\\n");

    /// <summary>
    /// Linhas com mais de 75 bytes em UTF-8 continuam na linha seguinte, começada por um espaço (RFC 5545, 3.1).
    /// Corta entre caracteres, nunca a meio de um carácter acentuado.
    /// </summary>
    public static string Dobrar(string linha)
    {
        if (Encoding.UTF8.GetByteCount(linha) <= 75)
            return linha;

        var sb = new StringBuilder();
        var bytes = 0;
        var limite = 75;
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(linha);
        while (e.MoveNext())
        {
            var elemento = e.GetTextElement();
            var tamanho = Encoding.UTF8.GetByteCount(elemento);
            if (bytes + tamanho > limite)
            {
                sb.Append("\r\n ");
                bytes = 0;
                limite = 74; // o espaço inicial conta
            }
            sb.Append(elemento);
            bytes += tamanho;
        }
        return sb.ToString();
    }
}
