namespace Portal.Core.Perfil;

/// <summary>Escolhas da pessoa sobre a própria plataforma: secções, ordem e alertas.</summary>
public class PreferenciasUtilizador
{
    public string UtilizadorId { get; set; } = "";

    /// <summary>Ids dos módulos ativos, pela ordem em que aparecem no painel.</summary>
    public List<string> SeccoesAtivas { get; set; } = [];

    public List<string> ZonasInteresse { get; set; } = [];

    public bool AlertasEmail { get; set; } = true;
    public bool AlertasTelegram { get; set; }

    /// <summary>Com quantos dias de antecedência avisar antes de um prazo.</summary>
    public List<int> DiasAntecedencia { get; set; } = [14, 3];

    public DateTimeOffset AtualizadoEm { get; set; }
}
