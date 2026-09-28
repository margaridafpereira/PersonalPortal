using System.Text.Json.Serialization;

namespace Portal.Core.Perfil;

/// <summary>
/// Perfil partilhado por todas as secções. Todos os campos são opcionais:
/// cada módulo diz de quais precisa e o painel mostra o que falta preencher.
/// </summary>
public class PerfilUtilizador
{
    public string UtilizadorId { get; set; } = "";

    public DateOnly? DataNascimento { get; set; }
    public string? Concelho { get; set; }
    public string? Freguesia { get; set; }
    public bool? ResidenteFiscal { get; set; }
    public bool? Dependente { get; set; }
    public CategoriaRendimento? CategoriaRendimento { get; set; }

    public decimal? RendimentoAnualAgregado { get; set; }
    public int? NumeroAdultos { get; set; }
    public List<int> IdadesFilhos { get; set; } = [];

    public SituacaoHabitacao? SituacaoHabitacao { get; set; }
    public decimal? RendaMensal { get; set; }
    public DateOnly? DataContratoArrendamento { get; set; }

    public bool? ProcuraComprarCasa { get; set; }
    public decimal? OrcamentoCompra { get; set; }

    public bool? TemVeiculo { get; set; }
    public int? MesMatricula { get; set; }
    public bool? ProprietarioImovel { get; set; }
    public decimal? ValorImi { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }

    /// <summary>Idade completa numa data. Devolve null se a data de nascimento não estiver preenchida.</summary>
    public int? IdadeEm(DateOnly data)
    {
        if (DataNascimento is not { } nascimento)
            return null;

        var idade = data.Year - nascimento.Year;
        if (data < nascimento.AddYears(idade))
            idade--;
        return idade;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<CategoriaRendimento>))]
public enum CategoriaRendimento
{
    Nenhum,
    TrabalhoDependente,   // categoria A
    TrabalhoIndependente, // categoria B
    Ambos,
}

[JsonConverter(typeof(JsonStringEnumConverter<SituacaoHabitacao>))]
public enum SituacaoHabitacao
{
    Arrenda,
    Proprietario,
    ProcuraArrendar,
    ProcuraComprar,
    CasaDeFamilia,
}
