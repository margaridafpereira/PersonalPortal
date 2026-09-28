using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Portal.Core.Dados;
using Portal.Core.Modulos;
using Portal.Core.Perfil;

namespace Portal.Modules.Perfil;

/// <summary>
/// Perfil e preferências não são uma secção do painel: são a base que todas as secções usam.
/// </summary>
public static class PerfilEndpoints
{
    public static IEndpointRouteBuilder MapPerfil(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api").RequireAuthorization();

        grupo.MapGet("/perfil", async (ClaimsPrincipal user, PortalDbContext db, CancellationToken ct) =>
            await ObterPerfilAsync(db, Id(user), ct));

        grupo.MapPut("/perfil", async (ClaimsPrincipal user, PerfilUtilizador dados, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            var id = Id(user);
            var perfil = await db.Perfis.FindAsync([id], ct);
            if (perfil is null)
            {
                perfil = new PerfilUtilizador { UtilizadorId = id };
                db.Perfis.Add(perfil);
            }

            db.Entry(perfil).CurrentValues.SetValues(dados);
            perfil.UtilizadorId = id;
            perfil.IdadesFilhos = dados.IdadesFilhos ?? [];
            perfil.AtualizadoEm = relogio.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Results.Ok(perfil);
        });

        grupo.MapGet("/preferencias", async (ClaimsPrincipal user, PortalDbContext db, IEnumerable<IModulo> modulos, CancellationToken ct) =>
            await ObterPreferenciasAsync(db, Id(user), modulos, ct));

        grupo.MapPut("/preferencias", async (ClaimsPrincipal user, PreferenciasUtilizador dados, PortalDbContext db,
            IEnumerable<IModulo> modulos, TimeProvider relogio, CancellationToken ct) =>
        {
            var validos = modulos.Select(m => m.Id).ToHashSet();
            var desconhecidos = dados.SeccoesAtivas.Where(s => !validos.Contains(s)).ToList();
            if (desconhecidos.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(dados.SeccoesAtivas)] = [$"Secções desconhecidas: {string.Join(", ", desconhecidos)}"],
                });

            var prefs = await ObterPreferenciasAsync(db, Id(user), modulos, ct);
            prefs.SeccoesAtivas = dados.SeccoesAtivas.Distinct().ToList();
            prefs.ZonasInteresse = dados.ZonasInteresse ?? [];
            prefs.AlertasEmail = dados.AlertasEmail;
            prefs.AlertasTelegram = dados.AlertasTelegram;
            prefs.DiasAntecedencia = (dados.DiasAntecedencia ?? []).Where(d => d is > 0 and <= 90).Distinct().OrderDescending().ToList();
            prefs.AtualizadoEm = relogio.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Results.Ok(prefs);
        });

        return rotas;
    }

    /// <summary>Devolve o perfil, criando-o vazio na primeira vez.</summary>
    public static async Task<PerfilUtilizador> ObterPerfilAsync(PortalDbContext db, string id, CancellationToken ct)
    {
        var perfil = await db.Perfis.FindAsync([id], ct);
        if (perfil is not null)
            return perfil;

        perfil = new PerfilUtilizador { UtilizadorId = id };
        db.Perfis.Add(perfil);
        await db.SaveChangesAsync(ct);
        return perfil;
    }

    /// <summary>Devolve as preferências; na primeira vez ficam ativas todas as secções.</summary>
    public static async Task<PreferenciasUtilizador> ObterPreferenciasAsync(PortalDbContext db, string id, IEnumerable<IModulo> modulos, CancellationToken ct)
    {
        var prefs = await db.Preferencias.FindAsync([id], ct);
        if (prefs is not null)
            return prefs;

        prefs = new PreferenciasUtilizador { UtilizadorId = id, SeccoesAtivas = modulos.Select(m => m.Id).ToList() };
        db.Preferencias.Add(prefs);
        await db.SaveChangesAsync(ct);
        return prefs;
    }

    private static string Id(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Utilizador sem id.");
}
