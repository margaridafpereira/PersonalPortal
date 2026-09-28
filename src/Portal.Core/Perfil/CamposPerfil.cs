namespace Portal.Core.Perfil;

/// <summary>Nomes dos campos do perfil, usados pelos módulos para declarar o que precisam.</summary>
public static class CamposPerfil
{
    public const string DataNascimento = nameof(PerfilUtilizador.DataNascimento);
    public const string Concelho = nameof(PerfilUtilizador.Concelho);
    public const string Freguesia = nameof(PerfilUtilizador.Freguesia);
    public const string ResidenteFiscal = nameof(PerfilUtilizador.ResidenteFiscal);
    public const string Dependente = nameof(PerfilUtilizador.Dependente);
    public const string CategoriaRendimento = nameof(PerfilUtilizador.CategoriaRendimento);
    public const string RendimentoAnualAgregado = nameof(PerfilUtilizador.RendimentoAnualAgregado);
    public const string NumeroAdultos = nameof(PerfilUtilizador.NumeroAdultos);
    public const string SituacaoHabitacao = nameof(PerfilUtilizador.SituacaoHabitacao);
    public const string RendaMensal = nameof(PerfilUtilizador.RendaMensal);
    public const string ProcuraComprarCasa = nameof(PerfilUtilizador.ProcuraComprarCasa);
    public const string OrcamentoCompra = nameof(PerfilUtilizador.OrcamentoCompra);

    private static readonly Dictionary<string, Func<PerfilUtilizador, bool>> Preenchido = new()
    {
        [DataNascimento] = p => p.DataNascimento is not null,
        [Concelho] = p => !string.IsNullOrWhiteSpace(p.Concelho),
        [Freguesia] = p => !string.IsNullOrWhiteSpace(p.Freguesia),
        [ResidenteFiscal] = p => p.ResidenteFiscal is not null,
        [Dependente] = p => p.Dependente is not null,
        [CategoriaRendimento] = p => p.CategoriaRendimento is not null,
        [RendimentoAnualAgregado] = p => p.RendimentoAnualAgregado is not null,
        [NumeroAdultos] = p => p.NumeroAdultos is not null,
        [SituacaoHabitacao] = p => p.SituacaoHabitacao is not null,
        [RendaMensal] = p => p.RendaMensal is not null,
        [ProcuraComprarCasa] = p => p.ProcuraComprarCasa is not null,
        [OrcamentoCompra] = p => p.OrcamentoCompra is not null,
    };

    public static IReadOnlyList<string> EmFalta(PerfilUtilizador perfil, IEnumerable<string> campos) =>
        campos.Where(c => !Preenchido.TryGetValue(c, out var teste) || !teste(perfil)).ToList();
}
