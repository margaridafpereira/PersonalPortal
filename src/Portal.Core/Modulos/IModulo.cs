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
}
