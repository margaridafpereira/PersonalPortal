using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;

namespace Portal.Api;

/// <summary>
/// O que muda quando o portal está publicado (Azure App Service ou outro servidor): uma só aplicação que serve
/// o frontend, registo só para emails convidados, chaves de sessão e base de dados numa pasta que sobrevive a reinícios.
/// Tudo por configuração; em desenvolvimento nada disto é obrigatório. Ver docs/publicar-na-azure.md.
/// </summary>
public static class Producao
{
    public static IServiceCollection AddProducao(this IServiceCollection servicos, IConfiguration config)
    {
        // As chaves que cifram o cookie de sessão: sem pasta permanente, cada reinício obrigava a entrar outra vez.
        if (config["DataProtection:Pasta"] is { Length: > 0 } pasta)
            servicos.AddDataProtection().SetApplicationName("portal-pessoal").PersistKeysToFileSystem(Directory.CreateDirectory(pasta));

        // A Azure recebe o HTTPS e passa o pedido à aplicação; estes cabeçalhos dizem-lhe que o pedido original era seguro.
        servicos.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
        });
        return servicos;
    }

    /// <summary>A pasta da base de dados SQLite tem de existir antes das migrações (ex.: /home/data na Azure).</summary>
    public static void CriarPastaDaBaseDeDados(IConfiguration config)
    {
        if (config["BaseDeDados:Fornecedor"] is not (null or "Sqlite") || config.GetConnectionString("Portal") is not { } ligacao)
            return;
        var ficheiro = new SqliteConnectionStringBuilder(ligacao).DataSource;
        if (Path.GetDirectoryName(Path.GetFullPath(ficheiro)) is { Length: > 0 } pasta)
            Directory.CreateDirectory(pasta);
    }

    public static WebApplication UseProducao(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.Use(RegistoSoParaConvidados(app.Configuration, app.Environment));
        app.UseDefaultFiles();
        app.UseStaticFiles();
        return app;
    }

    /// <summary>O frontend (web/dist, copiado para wwwroot na publicação) responde a todos os endereços que não são da API.</summary>
    public static WebApplication MapFrontend(this WebApplication app)
    {
        app.MapGet("/api/saude", () => Results.Ok(new { Estado = "ok" })).AllowAnonymous();
        app.MapFallbackToFile("index.html");
        return app;
    }

    /// <summary>
    /// O portal guarda dados financeiros e pessoais: só os emails em <c>Registo:EmailsPermitidos</c> podem criar conta.
    /// Em produção, sem lista, ninguém se regista (falha fechado); em desenvolvimento, sem lista, o registo fica aberto.
    /// </summary>
    private static Func<HttpContext, RequestDelegate, Task> RegistoSoParaConvidados(IConfiguration config, IWebHostEnvironment ambiente) =>
        async (ctx, next) =>
        {
            if (!HttpMethods.IsPost(ctx.Request.Method) || !ctx.Request.Path.Equals("/api/auth/register", StringComparison.OrdinalIgnoreCase))
            {
                await next(ctx);
                return;
            }

            var permitidos = config.GetSection("Registo:EmailsPermitidos").Get<string[]>() ?? [];
            if (permitidos.Length == 0 && ambiente.IsDevelopment())
            {
                await next(ctx);
                return;
            }

            ctx.Request.EnableBuffering();
            string? email = null;
            try
            {
                using var corpo = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted);
                email = corpo.RootElement.TryGetProperty("email", out var e) ? e.GetString()?.Trim() : null;
            }
            catch (JsonException)
            {
                // Corpo inválido: o endpoint de registo responde com o erro próprio.
            }
            ctx.Request.Body.Position = 0;

            if (email is null || !permitidos.Contains(email, StringComparer.OrdinalIgnoreCase))
            {
                await Results.Problem("Este portal é privado: só se podem registar pessoas convidadas. Pede acesso a quem gere o portal.",
                    statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(ctx);
                return;
            }
            await next(ctx);
        };
}
