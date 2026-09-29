using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Portal.Core.Avisos;
using Portal.Core.Modulos;
using Portal.Core.Perfil;

namespace Portal.Core.Dados;

public class Utilizador : IdentityUser;

/// <summary>
/// Base de dados única do portal. As tabelas comuns (contas, perfil, preferências) são
/// definidas aqui; cada módulo acrescenta as suas em <see cref="IModulo.ConfigurarModelo"/>.
/// </summary>
public class PortalDbContext(DbContextOptions<PortalDbContext> opcoes, IEnumerable<IModulo> modulos)
    : IdentityDbContext<Utilizador>(opcoes)
{
    public DbSet<PerfilUtilizador> Perfis => Set<PerfilUtilizador>();
    public DbSet<PreferenciasUtilizador> Preferencias => Set<PreferenciasUtilizador>();
    public DbSet<AvisoEnviado> AvisosEnviados => Set<AvisoEnviado>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        base.OnModelCreating(modelo);

        modelo.Entity<PerfilUtilizador>(e =>
        {
            e.ToTable("perfis");
            e.HasKey(p => p.UtilizadorId);
            e.HasOne<Utilizador>().WithOne().HasForeignKey<PerfilUtilizador>(p => p.UtilizadorId).OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.CategoriaRendimento).HasConversion<string>();
            e.Property(p => p.SituacaoHabitacao).HasConversion<string>();
            e.Property(p => p.Nome).HasMaxLength(100);
            e.Property(p => p.Concelho).HasMaxLength(100);
            e.Property(p => p.Freguesia).HasMaxLength(150);
        });

        modelo.Entity<PreferenciasUtilizador>(e =>
        {
            e.ToTable("preferencias");
            e.HasKey(p => p.UtilizadorId);
            e.HasOne<Utilizador>().WithOne().HasForeignKey<PreferenciasUtilizador>(p => p.UtilizadorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelo.Entity<AvisoEnviado>(e =>
        {
            e.ToTable("avisos_enviados");
            e.HasIndex(a => new { a.UtilizadorId, a.Chave, a.Dias }).IsUnique();
            e.HasOne<Utilizador>().WithMany().HasForeignKey(a => a.UtilizadorId).OnDelete(DeleteBehavior.Cascade);
            e.Property(a => a.Chave).HasMaxLength(300);
        });

        foreach (var modulo in modulos)
            modulo.ConfigurarModelo(modelo);
    }
}
