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
            "Sqlite" => o.UseSqlite(ligacao),
            "Postgres" => o.UseNpgsql(ligacao),
            _ => throw new InvalidOperationException($"Fornecedor de base de dados desconhecido: {fornecedor}"),
        });
        return servicos;
    }

    /// <summary>
    /// Fase 1: cria o esquema se não existir. As migrações EF entram quando o esquema estabilizar
    /// e a base de produção (PostgreSQL) estiver escolhida.
    /// </summary>
    public static async Task PrepararBaseDeDadosAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
