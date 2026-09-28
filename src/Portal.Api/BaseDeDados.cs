using Microsoft.EntityFrameworkCore;
using Portal.Core.Dados;

namespace Portal.Api;

public static class BaseDeDados
{
    /// <summary>
    /// "Sqlite" para desenvolvimento local sem instalar nada; "Postgres" para uma base alojada
    /// (Neon, Supabase…) ou local. Escolhido em <c>BaseDeDados:Fornecedor</c>.
    /// </summary>
    public static IServiceCollection AddBaseDeDados(this IServiceCollection servicos, IConfiguration config)
    {
        var fornecedor = config["BaseDeDados:Fornecedor"] ?? "Sqlite";
        var ligacao = config.GetConnectionString("Portal")
            ?? throw new InvalidOperationException("Falta a ConnectionStrings:Portal.");

        servicos.AddDbContext<PortalDbContext>(o => _ = fornecedor switch
        {
            "Sqlite" => o.UseSqlite(ligacao, b => b.MigrationsAssembly(typeof(BaseDeDados).Assembly.GetName().Name)),
            "Postgres" => o.UseNpgsql(ligacao),
            _ => throw new InvalidOperationException($"Fornecedor de base de dados desconhecido: {fornecedor}"),
        });
        return servicos;
    }

    /// <summary>
    /// Aplica as migrações pendentes no arranque. As migrações em Migrations/ são para SQLite;
    /// quando a base PostgreSQL entrar, terá o seu próprio conjunto (ver docs/desenvolvimento.md).
    /// </summary>
    public static async Task PrepararBaseDeDadosAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        await db.Database.MigrateAsync();
    }
}
