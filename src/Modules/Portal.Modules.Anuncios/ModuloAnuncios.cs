using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portal.Core.Dados;
using Portal.Core.Modulos;
using Portal.Core.Perfil;
using Portal.Modules.Anuncios.Mercado;
using Portal.Modules.Apoios;

namespace Portal.Modules.Anuncios;

public sealed class ModuloAnuncios : IModulo
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    public string Id => "anuncios";
    public string Nome => "Radar de anúncios";
    public string Descricao => "Casas e terrenos: pesquisas guardadas nos vários portais e os anúncios que segues.";

    public IReadOnlyCollection<string> CamposPerfil { get; } =
    [
        Portal.Core.Perfil.CamposPerfil.Concelho,
        Portal.Core.Perfil.CamposPerfil.ProcuraComprarCasa,
        Portal.Core.Perfil.CamposPerfil.OrcamentoCompra,
    ];

    public void RegistarServicos(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AddMemoryCache();
        servicos.AddHttpClient<IMercadoImobiliario, MercadoIne>(c =>
        {
            c.BaseAddress = new Uri(configuracao["Ine:Url"] ?? "https://www.ine.pt/");
            c.Timeout = TimeSpan.FromSeconds(60);
        });
    }

    public void ConfigurarModelo(ModelBuilder modelo)
    {
        modelo.Entity<PesquisaGuardada>(e =>
        {
            e.ToTable("pesquisas");
            e.HasIndex(p => p.UtilizadorId);
            e.HasOne<Utilizador>().WithMany().HasForeignKey(p => p.UtilizadorId).OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.Negocio).HasConversion<string>();
            e.Property(p => p.Tipo).HasConversion<string>();
        });

        modelo.Entity<ImovelFavorito>(e =>
        {
            e.ToTable("favoritos");
            e.HasIndex(f => f.UtilizadorId);
            e.HasOne<Utilizador>().WithMany().HasForeignKey(f => f.UtilizadorId).OnDelete(DeleteBehavior.Cascade);
            e.Property(f => f.Tipo).HasConversion<string>();
            e.Property(f => f.Estado).HasConversion<string>();
            e.OwnsMany(f => f.Precos, p =>
            {
                p.ToTable("favoritos_precos");
                p.WithOwner().HasForeignKey("FavoritoId");
                p.Property<int>("Id");
                p.HasKey("Id");
            });
        });
    }

    public void MapearEndpoints(IEndpointRouteBuilder rotas) => EndpointsAnuncios.Mapear(rotas);

    public async Task<CartaoPainel> ObterCartaoAsync(ContextoUtilizador contexto, CancellationToken ct)
    {
        var perfil = contexto.Perfil;
        var db = contexto.Servicos.GetRequiredService<PortalDbContext>();
        var emFalta = Portal.Core.Perfil.CamposPerfil.EmFalta(perfil, CamposPerfil);
        var itens = new List<ItemCartao>();
        var indicadores = new List<Indicador>();

        var pesquisas = await db.Set<PesquisaGuardada>().CountAsync(p => p.UtilizadorId == contexto.UtilizadorId, ct);
        var favoritos = await db.Set<ImovelFavorito>().Where(f => f.UtilizadorId == contexto.UtilizadorId && f.Estado != EstadoFavorito.Descartado).ToListAsync(ct);

        indicadores.Add(new Indicador(favoritos.Count.ToString(), favoritos.Count == 1 ? "imóvel seguido" : "imóveis seguidos", "positivo"));
        indicadores.Add(new Indicador(pesquisas.ToString(), pesquisas == 1 ? "pesquisa guardada" : "pesquisas guardadas"));

        var descidas = favoritos.Count(f => f.Precos.Count >= 2 && f.Precos.OrderBy(p => p.Data).Last().Preco < f.Precos.OrderBy(p => p.Data).First().Preco);
        if (descidas > 0)
            indicadores.Add(new Indicador(descidas.ToString(), descidas == 1 ? "baixou de preço" : "baixaram de preço", "aviso"));

        if (!string.IsNullOrWhiteSpace(perfil.Concelho))
        {
            var mediana = await contexto.Servicos.GetRequiredService<IMercadoImobiliario>().MedianaVendasAsync(perfil.Concelho, ct);
            if (mediana?.Total is { } m)
                itens.Add(new ItemCartao($"Mediana de venda em {mediana.Concelho}: {m.ToString("N0", Pt)} €/m²", $"INE, {mediana.Periodo}"));
        }

        if (perfil.ProcuraComprarCasa == true && perfil.OrcamentoCompra is { } orcamento)
        {
            var onde = string.IsNullOrWhiteSpace(perfil.Concelho) ? "" : $" em {perfil.Concelho}";
            itens.Add(new ItemCartao($"Procuras casa{onde} até {orcamento.ToString("C0", Pt)}."));
        }

        var resumo = favoritos.Count == 0 && pesquisas == 0
            ? "Guarda uma pesquisa e abre-a em todos os portais com um clique."
            : "As tuas pesquisas e os imóveis que segues.";

        return new CartaoPainel(Id, Nome, resumo, itens, emFalta) { Indicadores = indicadores };
    }
}

/// <summary>Etiquetas que ligam um imóvel aos apoios do perfil (garantia pública, IMT Jovem).</summary>
internal static class EtiquetasApoios
{
    public static IReadOnlyList<string> Para(decimal? preco, TipoImovel tipo, PerfilUtilizador perfil, Indexantes? ix, DateOnly hoje)
    {
        if (preco is not { } valor || ix is null || tipo == TipoImovel.Terreno)
            return [];

        var idade = perfil.IdadeEm(hoje);
        if (idade is null || idade > (int)ix[ChavesIndexantes.IdadeMaximaJovem] || hoje > Portal.Modules.Apoios.Regras.MotorApoios.FimRegimesJovemCompra)
            return [];

        var etiquetas = new List<string>();
        if (valor <= ix[ChavesIndexantes.ImtJovemIsencaoTotal])
            etiquetas.Add("IMT Jovem: isenção total");
        else if (valor <= ix[ChavesIndexantes.ImtJovemIsencaoParcial])
            etiquetas.Add("IMT Jovem: isenção parcial");
        if (valor <= ix[ChavesIndexantes.GarantiaPublicaValorMaximo])
            etiquetas.Add("Garantia pública");
        return etiquetas;
    }
}
