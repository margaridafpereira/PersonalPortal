using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Portal.Core.Dados;
using Portal.Core.Perfil;
using Portal.Modules.Anuncios.Mercado;
using Portal.Modules.Apoios;

namespace Portal.Modules.Anuncios;

public sealed record NovaPesquisa(string? Nome, Negocio Negocio, TipoImovel Tipo, string Distrito, string Concelho, decimal? PrecoMaximo, int? QuartosMinimo);

public sealed record NovoFavorito(string Url, string? Titulo, TipoImovel Tipo, string? Tipologia, decimal? AreaM2, string? Concelho, decimal? Preco, string? Notas);

public sealed record AlteracaoFavorito(string Titulo, string? Tipologia, decimal? AreaM2, string? Concelho, EstadoFavorito Estado, string? Notas);

public sealed record NovoPreco(decimal Preco);

public sealed record EventoPreco(DateOnly Data, decimal De, decimal Para, decimal VariacaoPercent);

public sealed record FavoritoVista(
    Guid Id, string Url, string Portal, string Titulo, TipoImovel Tipo, string? Tipologia, decimal? AreaM2, string? Concelho,
    EstadoFavorito Estado, string? Notas, DateTimeOffset CriadoEm, int DiasASeguir,
    decimal? PrecoAtual, decimal? PrecoInicial, decimal? VariacaoPercent, decimal? EurM2,
    MedianaConcelho? Mediana, decimal? DiferencaMedianaPercent,
    IReadOnlyList<RegistoPreco> Precos, IReadOnlyList<EventoPreco> Eventos, IReadOnlyList<string> Etiquetas);

internal static class EndpointsAnuncios
{
    public static void Mapear(IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/anuncios").RequireAuthorization();

        // ---- Pesquisas guardadas ----

        grupo.MapGet("/pesquisas", async (ClaimsPrincipal user, PortalDbContext db, CancellationToken ct) =>
            // Ordenado em memória: o SQLite não ordena DateTimeOffset, e cada pessoa tem poucas pesquisas.
            (await db.Set<PesquisaGuardada>().Where(p => p.UtilizadorId == Id(user)).ToListAsync(ct))
                .OrderByDescending(p => p.CriadaEm)
                .Select(p => new { Pesquisa = p, Links = LinksPortais.Para(p) }));

        grupo.MapPost("/pesquisas", async (ClaimsPrincipal user, NovaPesquisa dados, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dados.Concelho) || string.IsNullOrWhiteSpace(dados.Distrito))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Concelho"] = ["Indica o distrito e o concelho."] });

            var pesquisa = new PesquisaGuardada
            {
                Id = Guid.NewGuid(),
                UtilizadorId = Id(user),
                Nome = string.IsNullOrWhiteSpace(dados.Nome) ? NomePorOmissao(dados) : dados.Nome.Trim(),
                Negocio = dados.Negocio,
                Tipo = dados.Tipo,
                Distrito = dados.Distrito.Trim(),
                Concelho = dados.Concelho.Trim(),
                PrecoMaximo = dados.PrecoMaximo,
                QuartosMinimo = dados.QuartosMinimo,
                CriadaEm = relogio.GetUtcNow(),
            };
            db.Add(pesquisa);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { Pesquisa = pesquisa, Links = LinksPortais.Para(pesquisa) });
        });

        grupo.MapDelete("/pesquisas/{id:guid}", async (ClaimsPrincipal user, Guid id, PortalDbContext db, CancellationToken ct) =>
        {
            var apagadas = await db.Set<PesquisaGuardada>().Where(p => p.Id == id && p.UtilizadorId == Id(user)).ExecuteDeleteAsync(ct);
            return apagadas == 0 ? Results.NotFound() : Results.NoContent();
        });

        // ---- Favoritos ----

        grupo.MapGet("/favoritos", async (ClaimsPrincipal user, PortalDbContext db, IMercadoImobiliario mercado,
            IFonteIndexantes fonte, TimeProvider relogio, CancellationToken ct) =>
        {
            var favoritos = (await db.Set<ImovelFavorito>().Where(f => f.UtilizadorId == Id(user)).ToListAsync(ct))
                .OrderByDescending(f => f.CriadoEm);
            var contexto = await ContextoAsync(user, db, fonte, relogio, ct);
            var vistas = new List<FavoritoVista>();
            foreach (var f in favoritos)
                vistas.Add(await VistaAsync(f, contexto, mercado, ct));
            return vistas;
        });

        grupo.MapPost("/favoritos", async (ClaimsPrincipal user, NovoFavorito dados, PortalDbContext db, IMercadoImobiliario mercado,
            IFonteIndexantes fonte, TimeProvider relogio, CancellationToken ct) =>
        {
            if (!Uri.TryCreate(dados.Url?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Url"] = ["Cola o link completo do anúncio (https://…)."] });

            var favorito = new ImovelFavorito
            {
                Id = Guid.NewGuid(),
                UtilizadorId = Id(user),
                Url = uri.ToString(),
                Titulo = string.IsNullOrWhiteSpace(dados.Titulo) ? $"{dados.Tipo} {dados.Tipologia} {dados.Concelho}".Trim() : dados.Titulo.Trim(),
                Tipo = dados.Tipo,
                Tipologia = dados.Tipologia?.Trim(),
                AreaM2 = dados.AreaM2,
                Concelho = dados.Concelho?.Trim(),
                Notas = dados.Notas,
                Estado = EstadoFavorito.Ativo,
                CriadoEm = relogio.GetUtcNow(),
            };
            if (dados.Preco is > 0)
                favorito.Precos.Add(new RegistoPreco { Data = Hoje(relogio), Preco = dados.Preco.Value });

            db.Add(favorito);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await VistaAsync(favorito, await ContextoAsync(user, db, fonte, relogio, ct), mercado, ct));
        });

        grupo.MapPut("/favoritos/{id:guid}", async (ClaimsPrincipal user, Guid id, AlteracaoFavorito dados, PortalDbContext db,
            IMercadoImobiliario mercado, IFonteIndexantes fonte, TimeProvider relogio, CancellationToken ct) =>
        {
            var f = await db.Set<ImovelFavorito>().FirstOrDefaultAsync(f => f.Id == id && f.UtilizadorId == Id(user), ct);
            if (f is null)
                return Results.NotFound();

            f.Titulo = string.IsNullOrWhiteSpace(dados.Titulo) ? f.Titulo : dados.Titulo.Trim();
            f.Tipologia = dados.Tipologia?.Trim();
            f.AreaM2 = dados.AreaM2;
            f.Concelho = dados.Concelho?.Trim();
            f.Estado = dados.Estado;
            f.Notas = dados.Notas;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await VistaAsync(f, await ContextoAsync(user, db, fonte, relogio, ct), mercado, ct));
        });

        // Regista o preço atual. No mesmo dia, substitui o registo em vez de acumular.
        grupo.MapPost("/favoritos/{id:guid}/precos", async (ClaimsPrincipal user, Guid id, NovoPreco dados, PortalDbContext db,
            IMercadoImobiliario mercado, IFonteIndexantes fonte, TimeProvider relogio, CancellationToken ct) =>
        {
            if (dados.Preco <= 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Preco"] = ["O preço tem de ser positivo."] });

            var f = await db.Set<ImovelFavorito>().FirstOrDefaultAsync(f => f.Id == id && f.UtilizadorId == Id(user), ct);
            if (f is null)
                return Results.NotFound();

            var hoje = Hoje(relogio);
            f.Precos.RemoveAll(p => p.Data == hoje);
            f.Precos.Add(new RegistoPreco { Data = hoje, Preco = dados.Preco });
            await db.SaveChangesAsync(ct);
            return Results.Ok(await VistaAsync(f, await ContextoAsync(user, db, fonte, relogio, ct), mercado, ct));
        });

        grupo.MapDelete("/favoritos/{id:guid}", async (ClaimsPrincipal user, Guid id, PortalDbContext db, CancellationToken ct) =>
        {
            var f = await db.Set<ImovelFavorito>().FirstOrDefaultAsync(f => f.Id == id && f.UtilizadorId == Id(user), ct);
            if (f is null)
                return Results.NotFound();
            db.Remove(f);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ---- Mercado ----

        grupo.MapGet("/mercado/{concelho}", async (string concelho, IMercadoImobiliario mercado, CancellationToken ct) =>
            await mercado.MedianaVendasAsync(concelho, ct) is { } m ? Results.Ok(m) : Results.NotFound());
    }

    private sealed record Contexto(PerfilUtilizador Perfil, Indexantes? Indexantes, DateOnly Hoje);

    private static async Task<Contexto> ContextoAsync(ClaimsPrincipal user, PortalDbContext db, IFonteIndexantes fonte, TimeProvider relogio, CancellationToken ct)
    {
        var hoje = Hoje(relogio);
        var perfil = await db.Perfis.FindAsync([Id(user)], ct) ?? new PerfilUtilizador();
        return new Contexto(perfil, fonte.Obter(hoje.Year), hoje);
    }

    private static async Task<FavoritoVista> VistaAsync(ImovelFavorito f, Contexto ctx, IMercadoImobiliario mercado, CancellationToken ct)
    {
        var precos = f.Precos.OrderBy(p => p.Data).ToList();
        var atual = precos.LastOrDefault()?.Preco;
        var inicial = precos.FirstOrDefault()?.Preco;

        var eventos = precos.Zip(precos.Skip(1))
            .Where(par => par.First.Preco != par.Second.Preco)
            .Select(par => new EventoPreco(par.Second.Data, par.First.Preco, par.Second.Preco, Percentagem(par.Second.Preco, par.First.Preco)))
            .ToList();

        decimal? eurM2 = atual is { } a && f.AreaM2 is > 0 ? Math.Round(a / f.AreaM2.Value) : null;
        var mediana = string.IsNullOrWhiteSpace(f.Concelho) || f.Tipo == TipoImovel.Terreno
            ? null
            : await mercado.MedianaVendasAsync(f.Concelho, ct);
        decimal? diferenca = eurM2 is { } e && mediana?.Total is { } m && m > 0 ? Percentagem(e, m) : null;

        return new FavoritoVista(f.Id, f.Url, LinksPortais.PortalDe(f.Url), f.Titulo, f.Tipo, f.Tipologia, f.AreaM2, f.Concelho,
            f.Estado, f.Notas, f.CriadoEm, ctx.Hoje.DayNumber - DateOnly.FromDateTime(f.CriadoEm.UtcDateTime).DayNumber,
            atual, inicial, atual is { } x && inicial is { } y && y > 0 && x != y ? Percentagem(x, y) : null,
            eurM2, mediana, diferenca, precos, eventos,
            EtiquetasApoios.Para(atual, f.Tipo, ctx.Perfil, ctx.Indexantes, ctx.Hoje));
    }

    private static decimal Percentagem(decimal novo, decimal antigo) => Math.Round((novo - antigo) / antigo * 100, 1);

    private static string NomePorOmissao(NovaPesquisa p)
    {
        var tipo = p.Tipo switch { TipoImovel.Apartamento => "Apartamentos", TipoImovel.Moradia => "Moradias", _ => "Terrenos" };
        var preco = p.PrecoMaximo is { } v ? $" até {v.ToString("N0", CultureInfo.GetCultureInfo("pt-PT"))} €" : "";
        return $"{tipo} {(p.Negocio == Negocio.Comprar ? "à venda" : "para arrendar")} em {p.Concelho.Trim()}{preco}";
    }

    private static DateOnly Hoje(TimeProvider relogio) => DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);

    private static string Id(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
