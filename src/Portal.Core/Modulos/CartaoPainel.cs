using Portal.Core.Perfil;

namespace Portal.Core.Modulos;

/// <summary>Resumo que um módulo mostra no painel inicial.</summary>
public sealed record CartaoPainel(
    string ModuloId,
    string Titulo,
    string Resumo,
    IReadOnlyList<ItemCartao> Itens,
    IReadOnlyList<string> CamposPerfilEmFalta);

public sealed record ItemCartao(string Texto, string? Detalhe = null, string? Link = null);

/// <summary>O que um módulo sabe sobre quem está a usar o portal.</summary>
/// <remarks>Os módulos são criados antes do contentor de DI; usam <see cref="Servicos"/> para obter o que registaram.</remarks>
public sealed record ContextoUtilizador(string UtilizadorId, PerfilUtilizador Perfil, DateOnly Hoje, IServiceProvider Servicos);
