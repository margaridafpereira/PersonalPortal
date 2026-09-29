using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Portal.Core.Avisos;
using Portal.Core.Dados;
using Portal.Core.Modulos;
using Portal.Core.Perfil;
using Portal.Modules.Perfil;

namespace Portal.Api.Avisos;

/// <summary>Um prazo a avisar: faltam <see cref="DiasEmFalta"/> dias e cumpriu-se a antecedência <see cref="Antecedencia"/>.</summary>
public sealed record AvisoPendente(AvisoPrazo Prazo, string Seccao, int DiasEmFalta, int Antecedencia);

/// <summary>
/// Avisos de prazos por email. Uma vez por dia, para cada pessoa com os alertas por email ligados, junta os prazos de todas
/// as secções ativas e envia um só email com os que chegaram a uma das antecedências escolhidas (ex.: 14 e 3 dias antes).
/// Cada aviso vai uma vez por antecedência: fica registado em <see cref="AvisoEnviado"/>.
/// </summary>
public sealed class ServicoAvisos(PortalDbContext db, IEnumerable<IModulo> modulos, IEnviadorEmail email,
    IOptions<ConfiguracaoEmail> opcoes, IServiceProvider servicos, ILogger<ServicoAvisos> log)
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    /// <summary>Envia os avisos do dia. Devolve quantos emails foram enviados.</summary>
    public async Task<int> ExecutarAsync(DateTimeOffset agora, CancellationToken ct)
    {
        var hoje = DateOnly.FromDateTime(agora.LocalDateTime);
        // Todas as contas menos as que desligaram os alertas: quem nunca abriu as preferências tem-nos ligados por omissão.
        var comAlertas = await db.Users.Select(u => u.Id)
            .Where(id => !db.Preferencias.Any(p => p.UtilizadorId == id && !p.AlertasEmail))
            .ToListAsync(ct);
        var enviados = 0;
        foreach (var id in comAlertas)
        {
            try
            {
                if (await EnviarParaAsync(id, hoje, agora, teste: false, ct) > 0)
                    enviados++;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning(e, "Não foi possível enviar os avisos de {Utilizador}.", id);
            }
        }
        return enviados;
    }

    /// <summary>
    /// Prazos por avisar desta pessoa. Com <paramref name="teste"/>, inclui todos os prazos dentro da maior antecedência,
    /// mesmo os já avisados, e não regista nada.
    /// </summary>
    public async Task<IReadOnlyList<AvisoPendente>> PendentesAsync(string id, DateOnly hoje, bool teste, CancellationToken ct)
    {
        var perfil = await db.Perfis.FindAsync([id], ct) ?? new PerfilUtilizador { UtilizadorId = id };
        var prefs = await PerfilEndpoints.ObterPreferenciasAsync(db, id, modulos, ct);
        var antecedencias = prefs.DiasAntecedencia.Count > 0 ? prefs.DiasAntecedencia.Order().ToList() : [14, 3];
        var contexto = new ContextoUtilizador(id, perfil, hoje, servicos);
        var jaEnviados = teste ? [] : (await db.AvisosEnviados.Where(a => a.UtilizadorId == id)
            .Select(a => new { a.Chave, a.Dias }).ToListAsync(ct)).Select(a => (a.Chave, a.Dias)).ToHashSet();

        var pendentes = new List<AvisoPendente>();
        foreach (var modulo in modulos.Where(m => prefs.SeccoesAtivas.Contains(m.Id)))
            foreach (var prazo in await modulo.ObterAvisosAsync(contexto, ct))
            {
                var dias = prazo.Data.DayNumber - hoje.DayNumber;
                // A antecedência mais apertada já atingida: a 2 dias do prazo, com 14 e 3, é a de 3.
                // Se o serviço esteve parado e passou a de 14, não se manda a de 14 atrasada.
                if (dias < 0)
                    continue;
                var antecedencia = antecedencias.FirstOrDefault(d => d >= dias, -1);
                if (antecedencia < 0)
                    continue;
                if (!jaEnviados.Contains((prazo.Chave, antecedencia)))
                    pendentes.Add(new AvisoPendente(prazo, modulo.Nome, dias, antecedencia));
            }
        return pendentes.OrderBy(p => p.Prazo.Data).ToList();
    }

    /// <summary>Envia o email desta pessoa. Devolve quantos prazos levou (0 = não havia nada para avisar, nada enviado).</summary>
    public async Task<int> EnviarParaAsync(string id, DateOnly hoje, DateTimeOffset agora, bool teste, CancellationToken ct)
    {
        var endereco = await db.Users.Where(u => u.Id == id).Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(endereco))
            return 0;
        var pendentes = await PendentesAsync(id, hoje, teste, ct);
        if (pendentes.Count == 0 && !teste)
            return 0;

        var nome = (await db.Perfis.FindAsync([id], ct))?.Nome;
        await email.EnviarAsync(Compor(endereco, nome, pendentes, teste), ct);

        if (!teste)
        {
            db.AvisosEnviados.AddRange(pendentes.Select(p => new AvisoEnviado
            {
                UtilizadorId = id, Chave = p.Prazo.Chave, Dias = p.Antecedencia, EnviadoEm = agora,
            }));
            await db.SaveChangesAsync(ct);
        }
        return Math.Max(pendentes.Count, 1);
    }

    private Email Compor(string para, string? nome, IReadOnlyList<AvisoPendente> pendentes, bool teste)
    {
        var url = opcoes.Value.UrlPortal.TrimEnd('/');
        var assunto = pendentes.Count switch
        {
            0 => "Portal pessoal: email de teste",
            1 => $"Prazo a chegar: {pendentes[0].Prazo.Titulo} ({Quando(pendentes[0].DiasEmFalta)})",
            _ => $"{pendentes.Count} prazos a chegar, o primeiro {Quando(pendentes[0].DiasEmFalta)}",
        };

        var html = new StringBuilder();
        var texto = new StringBuilder();
        var saudacao = string.IsNullOrWhiteSpace(nome) ? "Olá," : $"Olá, {nome.Trim()},";
        html.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:15px;color:#111827;max-width:600px\">");
        html.Append($"<p>{WebUtility.HtmlEncode(saudacao)}</p>");
        texto.AppendLine(saudacao).AppendLine();

        if (pendentes.Count == 0)
        {
            const string semPrazos = "Este é um email de teste: os avisos estão a funcionar. Neste momento não há prazos dentro da antecedência que escolheste.";
            html.Append($"<p>{semPrazos}</p>");
            texto.AppendLine(semPrazos);
        }
        else
        {
            var intro = teste ? "Email de teste com os prazos que tens pela frente:" : "Estes prazos estão a chegar:";
            html.Append($"<p>{intro}</p><ul style=\"padding-left:18px\">");
            texto.AppendLine(intro).AppendLine();
            foreach (var p in pendentes)
            {
                var data = p.Prazo.Data.ToString("dddd, d 'de' MMMM", Pt);
                html.Append("<li style=\"margin-bottom:12px\">")
                    .Append($"<strong>{WebUtility.HtmlEncode(p.Prazo.Titulo)}</strong> — {WebUtility.HtmlEncode(data)} ({Quando(p.DiasEmFalta)})<br>")
                    .Append($"<span style=\"color:#5f6b7a\">{WebUtility.HtmlEncode(p.Prazo.Descricao)}</span>");
                if (p.Prazo.Link is { } link)
                    html.Append($"<br><a href=\"{WebUtility.HtmlEncode(link)}\">Mais informação</a>");
                html.Append("</li>");
                texto.AppendLine($"- {p.Prazo.Titulo}: {data} ({Quando(p.DiasEmFalta)})").AppendLine($"  {p.Prazo.Descricao}");
                if (p.Prazo.Link is { } l)
                    texto.AppendLine($"  {l}");
            }
            html.Append("</ul>");
        }

        html.Append($"<p><a href=\"{url}\">Abrir o portal</a></p>")
            .Append($"<p style=\"color:#5f6b7a;font-size:13px\">Recebes este email porque tens os avisos por email ligados. Podes desligá-los ou mudar os dias de antecedência em <a href=\"{url}/#/perfil\">Perfil → Preferências</a>.</p></div>");
        texto.AppendLine().AppendLine($"Abrir o portal: {url}").AppendLine("Para desligar os avisos ou mudar os dias de antecedência: Perfil → Preferências.");
        return new Email(para, assunto, html.ToString(), texto.ToString());
    }

    private static string Quando(int dias) => dias switch { 0 => "hoje", 1 => "amanhã", _ => $"daqui a {dias} dias" };
}

/// <summary>Corre os avisos uma vez por dia, a partir das 8h (hora local). Os avisos já enviados nunca se repetem.</summary>
public sealed class AvisosDiarios(IServiceScopeFactory scopes, TimeProvider relogio, ILogger<AvisosDiarios> log) : BackgroundService
{
    public const int Hora = 8;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        DateOnly? ultimaExecucao = null;
        using var temporizador = new PeriodicTimer(TimeSpan.FromMinutes(15), relogio);
        do
        {
            var agora = relogio.GetLocalNow();
            var hoje = DateOnly.FromDateTime(agora.DateTime);
            if (agora.Hour < Hora || ultimaExecucao == hoje)
                continue;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var enviados = await scope.ServiceProvider.GetRequiredService<ServicoAvisos>().ExecutarAsync(relogio.GetUtcNow(), ct);
                ultimaExecucao = hoje;
                if (enviados > 0)
                    log.LogInformation("Avisos do dia: {Enviados} email(s) enviado(s).", enviados);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning(e, "Os avisos do dia falharam; tenta outra vez daqui a 15 minutos.");
            }
        }
        while (await temporizador.WaitForNextTickAsync(ct));
    }
}

public static class AvisosEndpoints
{
    public static IServiceCollection AddAvisos(this IServiceCollection servicos, IConfiguration config)
    {
        servicos.Configure<ConfiguracaoEmail>(config.GetSection("Email"));
        servicos.AddSingleton<IEnviadorEmail, EnviadorSmtp>();
        servicos.AddScoped<ServicoAvisos>();
        if (config.GetValue("Email:AvisosAutomaticos", true))
            servicos.AddHostedService<AvisosDiarios>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapAvisos(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/avisos").RequireAuthorization();

        grupo.MapGet("/", async (ClaimsPrincipal user, PortalDbContext db, ServicoAvisos avisos, IEnviadorEmail email,
            IEnumerable<IModulo> modulos, TimeProvider relogio, CancellationToken ct) =>
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);
            var prefs = await PerfilEndpoints.ObterPreferenciasAsync(db, id, modulos, ct);
            var proximos = await avisos.PendentesAsync(id, hoje, teste: true, ct);
            return new
            {
                Ativos = prefs.AlertasEmail,
                prefs.DiasAntecedencia,
                Email = await db.Users.Where(u => u.Id == id).Select(u => u.Email).FirstOrDefaultAsync(ct),
                Destino = email.Destino,
                HoraEnvio = AvisosDiarios.Hora,
                Proximos = proximos.Select(p => new { p.Prazo.Titulo, p.Prazo.Data, p.Seccao, p.DiasEmFalta }),
            };
        });

        grupo.MapPost("/teste", async (ClaimsPrincipal user, ServicoAvisos avisos, IEnviadorEmail email, TimeProvider relogio, CancellationToken ct) =>
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var prazos = await avisos.EnviarParaAsync(id, DateOnly.FromDateTime(relogio.GetLocalNow().DateTime), relogio.GetUtcNow(), teste: true, ct);
                return prazos == 0
                    ? Results.Problem("A tua conta não tem email.", statusCode: 400)
                    : Results.Ok(new { Mensagem = $"Email de teste {email.Destino}." });
            }
            catch (Exception e) when (e is System.Net.Mail.SmtpException or InvalidOperationException or IOException)
            {
                return Results.Problem($"O email não foi enviado: {e.Message}", statusCode: 502);
            }
        });

        return rotas;
    }
}
