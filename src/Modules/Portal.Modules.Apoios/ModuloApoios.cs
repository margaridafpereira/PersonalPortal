using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portal.Core.Prazos;
using Portal.Core.Dados;
using Portal.Core.Modulos;
using Portal.Core.Perfil;
using static Portal.Core.Idioma;
using Portal.Modules.Apoios.Prazos;
using Portal.Modules.Apoios.Regras;

namespace Portal.Modules.Apoios;

public sealed class ModuloApoios : IModulo
{
    public string Id => "apoios";
    public string Nome => T("Radar de apoios e prazos", "Benefits and deadlines");
    public string Descricao => T("Apoios públicos a que provavelmente tens direito e os prazos que não podes falhar.", "Public benefits you are probably entitled to, and the deadlines you cannot miss.");

    public IReadOnlyCollection<string> CamposPerfil { get; } =
    [
        Portal.Core.Perfil.CamposPerfil.DataNascimento,
        Portal.Core.Perfil.CamposPerfil.ResidenteFiscal,
        Portal.Core.Perfil.CamposPerfil.Dependente,
        Portal.Core.Perfil.CamposPerfil.CategoriaRendimento,
        Portal.Core.Perfil.CamposPerfil.RendimentoAnualAgregado,
        Portal.Core.Perfil.CamposPerfil.SituacaoHabitacao,
    ];

    public void RegistarServicos(IServiceCollection servicos, IConfiguration configuracao) =>
        servicos.AddSingleton<IFonteIndexantes, IndexantesEmbebidos>();

    public IReadOnlyList<FerramentaAssistente> FerramentasAssistente { get; } =
    [
        new("apoios_elegiveis",
            "Apoios públicos (IRS Jovem, Porta 65 Jovem, IMT Jovem, garantia pública, abono de família…) avaliados com o perfil da pessoa: "
            + "estado de elegibilidade, condições cumpridas, falhadas ou por saber, estimativa do valor, como pedir e fonte oficial.",
            [],
            (ctx, _, _) => Task.FromResult<object?>(ctx.Servicos.GetRequiredService<IFonteIndexantes>().Obter(ctx.Hoje.Year) is { } ix
                ? MotorApoios.Avaliar(ctx.Perfil, ix, ctx.Hoje).Select(r => new
                {
                    r.Apoio.Nome, r.Apoio.Descricao, r.Estado,
                    Condicoes = r.Condicoes.Select(c => new { c.Resultado, c.Descricao }),
                    r.Estimativa, r.Apoio.ComoPedir, r.Apoio.Prazo, r.Apoio.Aviso, r.Apoio.FonteOficial, r.Apoio.VerificadoEm,
                }).ToList()
                : new { Erro = $"Ainda não há indexantes para {ctx.Hoje.Year}." })),
        new("prazos_fiscais",
            "Próximos prazos fiscais e da Segurança Social que se aplicam à pessoa nos próximos 12 meses (IRS, IMI, declarações trimestrais…), com os dias em falta.",
            [],
            (ctx, _, _) => Task.FromResult<object?>(CalendarioPrazos.Proximos(ctx.Perfil, ctx.Hoje)
                .Select(p => new { p.Data, p.Titulo, p.Descricao, p.Categoria, DiasEmFalta = p.DiasEmFalta(ctx.Hoje), p.PorConfirmar, p.Link }).ToList())),
    ];

    public void MapearEndpoints(IEndpointRouteBuilder rotas)
    {
        rotas.MapGet("/api/apoios/indexantes/{ano:int}", (int ano, IFonteIndexantes fonte) =>
            fonte.Obter(ano) is { } i ? Results.Ok(i) : Results.NotFound());

        var grupo = rotas.MapGroup("/api/apoios").RequireAuthorization();

        grupo.MapGet("/", async (ClaimsPrincipal user, PortalDbContext db, IFonteIndexantes fonte, TimeProvider relogio, CancellationToken ct) =>
        {
            var (perfil, hoje) = await CarregarAsync(user, db, relogio, ct);
            var indexantes = fonte.Obter(hoje.Year);
            return indexantes is null
                ? Results.Problem(T($"Ainda não há indexantes para {hoje.Year}.", $"There are no reference values for {hoje.Year} yet."), statusCode: 503)
                : Results.Ok(MotorApoios.Avaliar(perfil, indexantes, hoje));
        });

        grupo.MapGet("/prazos", async (ClaimsPrincipal user, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            var (perfil, hoje) = await CarregarAsync(user, db, relogio, ct);
            return CalendarioPrazos.Proximos(perfil, hoje).Select(p => new { p.Data, p.Titulo, p.Descricao, p.Categoria, p.Link, p.PorConfirmar, DiasEmFalta = p.DiasEmFalta(hoje) });
        });

        grupo.MapGet("/prazos.ics", async (ClaimsPrincipal user, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            var (perfil, hoje) = await CarregarAsync(user, db, relogio, ct);
            var prefs = await db.Preferencias.FindAsync([UtilizadorId(user)], ct);
            var ics = Calendario.ParaIcs(CalendarioPrazos.Proximos(perfil, hoje).Select(p => p.ParaEvento()), prefs?.DiasAntecedencia ?? [14, 3], relogio.GetUtcNow());
            return Results.File(Encoding.UTF8.GetBytes(ics), "text/calendar", "prazos.ics");
        });
    }

    public Task<IReadOnlyList<AvisoPrazo>> ObterAvisosAsync(ContextoUtilizador contexto, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AvisoPrazo>>(CalendarioPrazos.Proximos(contexto.Perfil, contexto.Hoje)
            .Select(p => new AvisoPrazo($"apoios:{p.Categoria}:{p.Titulo}:{p.Data:yyyy-MM-dd}", p.Data, p.Titulo, p.Descricao, p.Link))
            .ToList());

    public Task<CartaoPainel> ObterCartaoAsync(ContextoUtilizador contexto, CancellationToken ct)
    {
        var emFalta = Portal.Core.Perfil.CamposPerfil.EmFalta(contexto.Perfil, CamposPerfil);
        var itens = new List<ItemCartao>();
        var indicadores = new List<Indicador>();

        if (contexto.Servicos.GetRequiredService<IFonteIndexantes>().Obter(contexto.Hoje.Year) is { } ix)
        {
            var resultados = MotorApoios.Avaliar(contexto.Perfil, ix, contexto.Hoje);
            var provaveis = resultados.Where(r => r.Estado == EstadoElegibilidade.Provavel).ToList();
            var porConfirmar = resultados.Count(r => r.Estado == EstadoElegibilidade.FaltaInformacao);

            indicadores.Add(new Indicador(provaveis.Count.ToString(), provaveis.Count == 1 ? T("apoio provável", "likely benefit") : T("apoios prováveis", "likely benefits"), "positivo"));
            if (porConfirmar > 0)
                indicadores.Add(new Indicador(porConfirmar.ToString(), T("precisam de mais dados", "need more data"), "aviso"));

            itens.AddRange(provaveis.Take(3).Select(r => new ItemCartao(r.Apoio.Nome, r.Estimativa)));
        }

        var proximo = CalendarioPrazos.Proximos(contexto.Perfil, contexto.Hoje).FirstOrDefault();
        if (proximo is not null)
        {
            var dias = proximo.DiasEmFalta(contexto.Hoje);
            indicadores.Add(new Indicador(dias.ToString(), T($"dias até: {proximo.Titulo}", $"days until: {proximo.Titulo}"), dias <= 14 ? "aviso" : "neutro"));
        }

        var resumo = emFalta.Count > 0
            ? T($"Completa {emFalta.Count} campo(s) do perfil para resultados mais certos.", $"Fill in {emFalta.Count} more profile field(s) for more accurate results.")
            : T("Com base no teu perfil.", "Based on your profile.");

        return Task.FromResult(new CartaoPainel(Id, Nome, resumo, itens, emFalta) { Indicadores = indicadores });
    }

    private static async Task<(PerfilUtilizador, DateOnly)> CarregarAsync(ClaimsPrincipal user, PortalDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var perfil = await db.Perfis.FindAsync([UtilizadorId(user)], ct) ?? new PerfilUtilizador();
        return (perfil, DateOnly.FromDateTime(relogio.GetLocalNow().DateTime));
    }

    private static string UtilizadorId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
