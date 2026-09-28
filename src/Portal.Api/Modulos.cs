using System.Security.Claims;
using Portal.Core.Dados;
using Portal.Core.Modulos;
using Portal.Modules.Anuncios;
using Portal.Modules.Apoios;
using Portal.Modules.Perfil;

namespace Portal.Api;

public static class Modulos
{
    /// <summary>Todas as secções disponíveis. Uma secção nova entra aqui.</summary>
    private static readonly IModulo[] Todos = [new ModuloApoios(), new ModuloAnuncios()];

    public static IServiceCollection AddModulos(this IServiceCollection servicos, IConfiguration config)
    {
        foreach (var modulo in Todos)
        {
            servicos.AddSingleton(modulo);
            modulo.RegistarServicos(servicos, config);
        }
        return servicos;
    }

    public static IEndpointRouteBuilder MapModulos(this IEndpointRouteBuilder rotas)
    {
        var modulos = rotas.ServiceProvider.GetServices<IModulo>().ToList();

        rotas.MapGet("/api/modulos", () =>
            modulos.Select(m => new { m.Id, m.Nome, m.Descricao, m.CamposPerfil }))
            .RequireAuthorization();

        foreach (var modulo in modulos)
            modulo.MapearEndpoints(rotas);

        return rotas;
    }

    /// <summary>Painel inicial: um cartão por secção ativa, pela ordem das preferências.</summary>
    public static IEndpointRouteBuilder MapPainel(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet("/api/painel", async (ClaimsPrincipal user, PortalDbContext db, IEnumerable<IModulo> modulos,
            TimeProvider relogio, IServiceProvider servicos, CancellationToken ct) =>
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var perfil = await PerfilEndpoints.ObterPerfilAsync(db, id, ct);
            var prefs = await PerfilEndpoints.ObterPreferenciasAsync(db, id, modulos, ct);
            var contexto = new ContextoUtilizador(id, perfil, DateOnly.FromDateTime(relogio.GetLocalNow().DateTime), servicos);

            var porId = modulos.ToDictionary(m => m.Id);
            var cartoes = new List<CartaoPainel>();
            foreach (var seccao in prefs.SeccoesAtivas)
                if (porId.TryGetValue(seccao, out var modulo))
                    cartoes.Add(await modulo.ObterCartaoAsync(contexto, ct));

            return cartoes;
        }).RequireAuthorization();

        return rotas;
    }
}
