using Portal.Core.Perfil;

namespace Portal.Core.Modulos;

/// <summary>Resumo que um módulo mostra no painel inicial.</summary>
public sealed record CartaoPainel(
    string ModuloId,
    string Titulo,
    string Resumo,
    IReadOnlyList<ItemCartao> Itens,
    IReadOnlyList<string> CamposPerfilEmFalta)
{
    /// <summary>Números em destaque no cartão (ex.: "3 apoios prováveis").</summary>
    public IReadOnlyList<Indicador> Indicadores { get; init; } = [];
}

public sealed record ItemCartao(string Texto, string? Detalhe = null, string? Link = null);

/// <param name="Tom">"positivo", "aviso" ou "neutro": o frontend escolhe a cor.</param>
public sealed record Indicador(string Valor, string Rotulo, string Tom = "neutro");

/// <summary>O que um módulo sabe sobre quem está a usar o portal.</summary>
/// <remarks>Os módulos são criados antes do contentor de DI; usam <see cref="Servicos"/> para obter o que registaram.</remarks>
public sealed record ContextoUtilizador(string UtilizadorId, PerfilUtilizador Perfil, DateOnly Hoje, IServiceProvider Servicos);

/// <summary>Um prazo que pode dar origem a um aviso por email. <paramref name="Chave"/> é estável entre dias (ex.: "carro:{id}:Iuc:2026-11-30").</summary>
public sealed record AvisoPrazo(string Chave, DateOnly Data, string Titulo, string Descricao, string? Link);
