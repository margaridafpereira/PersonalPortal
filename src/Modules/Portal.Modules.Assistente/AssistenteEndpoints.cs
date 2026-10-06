using static Portal.Core.Idioma;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Portal.Core.Dados;
using Portal.Core.Modulos;
using Portal.Core.Perfil;

namespace Portal.Modules.Assistente;

public sealed record PedidoAssistente(IReadOnlyList<MensagemConversa>? Conversa, string? Seccao);

/// <summary>
/// O assistente não é uma secção do painel: está em todas as páginas, como o perfil.
/// Antes da primeira pergunta a pessoa aceita a nota sobre os dados enviados ao fornecedor de IA.
/// </summary>
public static class AssistenteEndpoints
{
    private const int MaxMensagens = 20;
    private const int MaxCaracteres = 4000;

    public static IServiceCollection AddAssistente(this IServiceCollection servicos, IConfiguration config)
    {
        servicos.Configure<ConfiguracaoAssistente>(config.GetSection("Assistente"));
        servicos.AddMemoryCache();
        servicos.AddHttpClient<IModeloLinguagem, ModeloCompativelOpenAI>(c => c.Timeout = TimeSpan.FromSeconds(90));
        servicos.AddScoped<ServicoAssistente>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapAssistente(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/assistente").RequireAuthorization();

        grupo.MapGet("/", async (ClaimsPrincipal user, PortalDbContext db, IOptions<ConfiguracaoAssistente> opcoes, CancellationToken ct) =>
        {
            var cfg = opcoes.Value;
            var prefs = await db.Preferencias.FindAsync([Id(user)], ct);
            return new
            {
                cfg.Configurado, cfg.Fornecedor, cfg.Modelo, cfg.PlanoGratuito, cfg.TermosDados,
                Nota = Nota(cfg),
                AceiteEm = prefs?.AssistenteAceiteEm,
            };
        });

        grupo.MapPost("/aceitar", async (ClaimsPrincipal user, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            var id = Id(user);
            var prefs = await db.Preferencias.FindAsync([id], ct);
            if (prefs is null)
            {
                prefs = new PreferenciasUtilizador { UtilizadorId = id };
                db.Preferencias.Add(prefs);
            }
            prefs.AssistenteAceiteEm = relogio.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        grupo.MapPost("/mensagens", async (ClaimsPrincipal user, PedidoAssistente pedido, PortalDbContext db, ServicoAssistente assistente,
            IOptions<ConfiguracaoAssistente> opcoes, IMemoryCache cache, TimeProvider relogio, IServiceProvider servicos, CancellationToken ct) =>
        {
            var cfg = opcoes.Value;
            var id = Id(user);
            if (!cfg.Configurado)
                return Results.Problem(T("O assistente ainda não está configurado: falta a chave do fornecedor de IA.", "The assistant is not set up yet: the AI provider key is missing."), statusCode: 503);
            if ((await db.Preferencias.FindAsync([id], ct))?.AssistenteAceiteEm is null)
                return Results.Problem(T("Lê e aceita a nota sobre os dados antes de usar o assistente.", "Read and accept the data notice before using the assistant."), statusCode: 409);
            if (Validar(pedido.Conversa) is { } erro)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Conversa"] = [erro] });

            // Limite por pessoa e por hora: protege a quota do plano gratuito, que é partilhada por todos.
            var chave = $"assistente:{id}:{relogio.GetUtcNow():yyyyMMddHH}";
            var pedidos = cache.GetOrCreate(chave, e => { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1); return 0; });
            if (pedidos >= cfg.PedidosPorHora)
                return Results.Problem(T($"Chegaste ao limite de {cfg.PedidosPorHora} perguntas por hora. Tenta daqui a pouco.", $"You have reached the limit of {cfg.PedidosPorHora} questions an hour. Try again shortly."), statusCode: 429);
            cache.Set(chave, pedidos + 1, TimeSpan.FromHours(1));

            var perfil = await db.Perfis.FindAsync([id], ct) ?? new PerfilUtilizador { UtilizadorId = id };
            var contexto = new ContextoUtilizador(id, perfil, DateOnly.FromDateTime(relogio.GetLocalNow().DateTime), servicos);
            try
            {
                return Results.Ok(await assistente.ResponderAsync(contexto, pedido.Conversa!, pedido.Seccao, ct));
            }
            catch (ErroFornecedorIa e)
            {
                return Results.Problem(e.Message, statusCode: 502);
            }
        });

        return rotas;
    }

    /// <summary>A nota mostrada antes da primeira utilização e sempre visível no assistente.</summary>
    public static string Nota(ConfiguracaoAssistente cfg) =>
        T($"As tuas perguntas e os dados do portal que o assistente consultar para responder (perfil, apoios, veículos, investimentos, imóveis) são enviados para {cfg.Fornecedor}.",
          $"Your questions and the portal data the assistant looks up to answer (profile, benefits, vehicles, investments, listings) are sent to {cfg.Fornecedor}.")
        + (cfg.PlanoGratuito
            ? T(" No plano gratuito, o fornecedor pode guardar estes dados, usá-los para melhorar os seus modelos e revisores humanos podem lê-los. Não escrevas nada que não queiras partilhar.",
                " On the free plan, the provider may keep this data, use it to improve its models, and human reviewers may read it. Do not write anything you would not want to share.")
            : "")
        + T(" O assistente só consulta, não altera nada no portal, e pode enganar-se: confirma os valores importantes.",
            " The assistant only looks things up, never changes anything in the portal, and can be wrong: check the important figures.");

    private static string? Validar(IReadOnlyList<MensagemConversa>? conversa)
    {
        if (conversa is not { Count: > 0 })
            return T("A conversa está vazia.", "The conversation is empty.");
        if (conversa.Count > MaxMensagens)
            return T($"A conversa tem mais de {MaxMensagens} mensagens: começa uma nova.", $"The conversation has more than {MaxMensagens} messages: start a new one.");
        if (conversa.Any(m => m.Papel is not ("utilizador" or "assistente") || string.IsNullOrWhiteSpace(m.Texto)))
            return T("Mensagem inválida.", "Invalid message.");
        if (conversa.Any(m => m.Texto.Length > MaxCaracteres))
            return T($"Cada mensagem pode ter até {MaxCaracteres} caracteres.", $"Each message can be up to {MaxCaracteres} characters.");
        if (conversa[^1].Papel != "utilizador")
            return T("A última mensagem tem de ser uma pergunta.", "The last message must be a question.");
        return null;
    }

    private static string Id(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
