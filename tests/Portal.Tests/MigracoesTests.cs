using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Portal.Core.Dados;
using Portal.Core.Modulos;
using Portal.Core.Perfil;
using Portal.Modules.Anuncios;
using Portal.Modules.Apoios;
using Portal.Modules.Carro;
using Portal.Modules.Investimentos;
using Portal.Modules.Perfil;

namespace Portal.Tests;

public class MigracoesTests
{
    private static readonly IModulo[] Modulos = [new ModuloApoios(), new ModuloAnuncios(), new ModuloCarro(), new ModuloInvestimentos()];

    private static PortalDbContext Contexto(SqliteConnection ligacao) =>
        new(new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlite(ligacao, b => b.MigrationsAssembly("Portal.Api"))
            .Options, Modulos);

    [Fact]
    public async Task Conta_antiga_mantem_ocultas_as_seccoes_que_ocultou()
    {
        using var ligacao = new SqliteConnection("Data Source=:memory:");
        await ligacao.OpenAsync();

        // Base como estava antes da correção: a conta ocultou Apoios e ficou com SeccoesVistas = "[]".
        await using (var db = Contexto(ligacao))
        {
            await db.GetService<IMigrator>().MigrateAsync("20260928154537_NomeETipoAtivo");
            // SQL direto: o modelo atual tem colunas que esta versão da base ainda não tem.
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO AspNetUsers (Id, UserName, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
                VALUES ('u1', 'u1@exemplo.pt', 0, 0, 0, 0, 0);
                INSERT INTO preferencias (UtilizadorId, SeccoesAtivas, ZonasInteresse, AlertasEmail, AlertasTelegram, DiasAntecedencia, AtualizadoEm)
                VALUES ('u1', '["anuncios"]', '[]', 1, 0, '[14,3]', '2026-09-01 00:00:00+00:00');
                """);
        }

        await using (var db = Contexto(ligacao))
        {
            await db.Database.MigrateAsync();
            var prefs = await PerfilEndpoints.ObterPreferenciasAsync(db, "u1", Modulos, CancellationToken.None);

            Assert.Equal(["anuncios", "carro", "investimentos"], prefs.SeccoesAtivas);
            Assert.Equal(["apoios", "anuncios", "carro", "investimentos"], prefs.SeccoesVistas);
        }
    }
}
