using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Portal.Core.Modulos;

/// <summary>
/// Contrato que cada secção do portal implementa. A plataforma descobre os módulos,
/// regista os seus serviços e endpoints e pede-lhes um cartão para o painel inicial.
/// Uma secção nova só tem de implementar esta interface.
/// </summary>
public interface IModulo
{
    /// <summary>Identificador estável, usado nas preferências e nas rotas (ex.: "apoios").</summary>
    string Id { get; }

    string Nome { get; }

    string Descricao { get; }

    /// <summary>Campos do perfil de que o módulo precisa (nomes de <see cref="Perfil.CamposPerfil"/>).</summary>
    IReadOnlyCollection<string> CamposPerfil { get; }

    void RegistarServicos(IServiceCollection servicos, IConfiguration configuracao) { }

    void ConfigurarModelo(ModelBuilder modelo) { }

    void MapearEndpoints(IEndpointRouteBuilder rotas) { }

    Task<CartaoPainel> ObterCartaoAsync(ContextoUtilizador contexto, CancellationToken ct);

    /// <summary>
    /// Consultas que o assistente de IA pode fazer a esta secção. Uma secção nova traz as suas e o assistente
    /// passa a saber responder sobre ela, sem mudar nada no módulo do assistente.
    /// </summary>
    IReadOnlyList<FerramentaAssistente> FerramentasAssistente => [];

    /// <summary>
    /// Prazos desta secção para os avisos por email. A plataforma decide quando avisar (dias de antecedência nas preferências)
    /// e não repete um aviso já enviado; a <see cref="AvisoPrazo.Chave"/> identifica o prazo entre execuções.
    /// </summary>
    Task<IReadOnlyList<AvisoPrazo>> ObterAvisosAsync(ContextoUtilizador contexto, CancellationToken ct) => Task.FromResult<IReadOnlyList<AvisoPrazo>>([]);
}
