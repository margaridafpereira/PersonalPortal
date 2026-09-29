using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portal.Core.Dados;
using Portal.Core.Modulos;

namespace Portal.Modules.Investimentos;

public sealed record NovaOperacao(TipoOperacao Tipo, DateTime Momento, string Ativo, string? Nome, decimal Quantidade, decimal PrecoUnitario,
    string Moeda, decimal Comissoes, decimal RetencaoFonte, string? MoedaRetencao, string? Corretora, TipoAtivo? TipoAtivo = null);

/// <summary>O ficheiro vem como texto (CSV) ou, se for Excel, em <see cref="Xlsx"/> (base64).</summary>
public sealed record PedidoImportacao(string Formato, string? Conteudo, Mapeamento? Mapeamento = null, string? Xlsx = null);

public sealed record PedidoAnalise(string? Conteudo, string? Xlsx = null);

public sealed record NovoTipoAtivo(TipoAtivo Tipo);

public sealed record NovoIsin(string Isin);

public sealed class ModuloInvestimentos : IModulo
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    public string Id => "investimentos";
    public string Nome => "IRS de investimentos";
    public string Descricao => "Mais-valias e dividendos de corretoras estrangeiras, prontos para o Anexo J.";

    public IReadOnlyCollection<string> CamposPerfil { get; } = [Portal.Core.Perfil.CamposPerfil.ResidenteFiscal];

    public void RegistarServicos(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AddMemoryCache();
        servicos.AddHttpClient<ICambios, CambiosBancoDePortugal>(c =>
        {
            c.BaseAddress = new Uri(configuracao["BancoDePortugal:Url"] ?? "https://bpstat.bportugal.pt/");
            c.Timeout = TimeSpan.FromSeconds(60);
        });
    }

    public IReadOnlyList<FerramentaAssistente> FerramentasAssistente { get; } =
    [
        new("relatorio_irs_investimentos",
            "Estimativa do Anexo J de um ano: linhas de mais-valias (FIFO, em euros ao câmbio do Banco de Portugal), dividendos por país, "
            + "imposto retido lá fora que conta, imposto estimado e avisos (títulos sem ISIN, vendas sem compras, etc.).",
            [new("ano", "integer", "Ano dos rendimentos. Se omitido, o ano que se declara agora (até junho, o ano anterior).")],
            async (ctx, argumentos, ct) =>
            {
                var operacoes = await ctx.Servicos.GetRequiredService<PortalDbContext>().Set<Operacao>()
                    .Where(o => o.UtilizadorId == ctx.UtilizadorId).ToListAsync(ct);
                var ano = argumentos.Inteiro("ano") ?? (ctx.Hoje.Month <= 6 ? ctx.Hoje.Year - 1 : ctx.Hoje.Year);
                return await RelatorioAsync(operacoes, ano, ctx.Servicos.GetRequiredService<ICambios>(), ct);
            }),
        new("carteira_investimentos",
            "Títulos que a pessoa tem hoje, pelas operações importadas: quantidade, tipo (ação, ETF, fundo), corretora e datas da primeira e última operação.",
            [],
            async (ctx, _, ct) =>
            {
                var operacoes = await ctx.Servicos.GetRequiredService<PortalDbContext>().Set<Operacao>()
                    .Where(o => o.UtilizadorId == ctx.UtilizadorId && o.Tipo != TipoOperacao.Dividendo).ToListAsync(ct);
                return operacoes.GroupBy(o => o.Ativo).Select(g => new
                {
                    Ativo = g.Key, g.First().Nome, g.First().TipoAtivo,
                    Quantidade = g.Sum(o => o.Tipo == TipoOperacao.Compra ? o.Quantidade : -o.Quantidade),
                    Corretoras = g.Select(o => o.Corretora).Distinct(),
                    Primeira = g.Min(o => o.Momento), Ultima = g.Max(o => o.Momento),
                }).Where(t => t.Quantidade > 0).ToList();
            }),
    ];

    public void ConfigurarModelo(ModelBuilder modelo) =>
        modelo.Entity<Operacao>(e =>
        {
            e.ToTable("operacoes_investimento");
            e.HasIndex(o => o.UtilizadorId);
            e.HasIndex(o => new { o.UtilizadorId, o.IdExterno });
            e.HasOne<Utilizador>().WithMany().HasForeignKey(o => o.UtilizadorId).OnDelete(DeleteBehavior.Cascade);
            e.Property(o => o.Tipo).HasConversion<string>();
            e.Property(o => o.TipoAtivo).HasConversion<string>();
            e.Property(o => o.Ativo).HasMaxLength(20);
            e.Property(o => o.Moeda).HasMaxLength(3);
            e.Property(o => o.MoedaRetencao).HasMaxLength(3);
            e.Property(o => o.MoedaComissoes).HasMaxLength(3);
        });

    public void MapearEndpoints(IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/investimentos").RequireAuthorization();

        grupo.MapGet("/operacoes", async (ClaimsPrincipal user, PortalDbContext db, CancellationToken ct) =>
            (await Operacoes(db, user).ToListAsync(ct)).OrderByDescending(o => o.Momento));

        grupo.MapPost("/operacoes", async (ClaimsPrincipal user, NovaOperacao d, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(d.Ativo) || d.Ativo.Trim().Length > 20 || d.Quantidade <= 0 || d.PrecoUnitario < 0)
                return Problema("Ativo", "Indica o ISIN, a quantidade e o preço.");
            if (!MoedaValida(d.Moeda) || (d.MoedaRetencao is not null && !MoedaValida(d.MoedaRetencao)))
                return Problema("Moeda", "Indica a moeda com o código de 3 letras (ex.: EUR, USD).");
            if (d.Comissoes < 0 || d.RetencaoFonte < 0)
                return Problema("Comissoes", "As comissões e o imposto retido não podem ser negativos.");

            var op = new Operacao
            {
                Id = Guid.NewGuid(), UtilizadorId = UtilizadorId(user), Tipo = d.Tipo, Momento = d.Momento,
                Ativo = d.Ativo.Trim().ToUpperInvariant(), Nome = string.IsNullOrWhiteSpace(d.Nome) ? d.Ativo.Trim() : d.Nome.Trim(),
                TipoAtivo = d.TipoAtivo ?? Classificacao.Sugerir(d.Nome ?? ""),
                Quantidade = d.Quantidade, PrecoUnitario = d.PrecoUnitario, Moeda = d.Moeda.Trim().ToUpperInvariant(),
                Comissoes = d.Comissoes, RetencaoFonte = d.RetencaoFonte, MoedaRetencao = d.MoedaRetencao?.Trim().ToUpperInvariant(),
                Corretora = d.Corretora, CriadoEm = relogio.GetUtcNow(),
            };
            db.Add(op);
            await db.SaveChangesAsync(ct);
            return Results.Ok(op);
        });

        grupo.MapDelete("/operacoes/{id:guid}", async (ClaimsPrincipal user, Guid id, PortalDbContext db, CancellationToken ct) =>
            await Operacoes(db, user).Where(o => o.Id == id).ExecuteDeleteAsync(ct) == 0 ? Results.NotFound() : Results.NoContent());

        grupo.MapDelete("/operacoes", async (ClaimsPrincipal user, PortalDbContext db, CancellationToken ct) =>
            Results.Ok(new { Apagadas = await Operacoes(db, user).ExecuteDeleteAsync(ct) }));

        // O ficheiro vem no corpo JSON (lido no browser): evita multipart e antiforgery.
        grupo.MapPost("/importar", async (ClaimsPrincipal user, PedidoImportacao pedido, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            if (LerFicheiro(pedido.Conteudo, pedido.Xlsx, out var texto) is { } erro)
                return erro;

            var resultado = Importacao.Importar(pedido.Formato, texto, pedido.Mapeamento);
            var id = UtilizadorId(user);
            var existentes = (await Operacoes(db, user).Where(o => o.IdExterno != null).Select(o => o.IdExterno!).ToListAsync(ct)).ToHashSet();

            var novas = resultado.Operacoes.Where(o => o.IdExterno is null || !existentes.Contains(o.IdExterno)).ToList();
            foreach (var op in novas)
            {
                op.Id = Guid.NewGuid();
                op.UtilizadorId = id;
                op.CriadoEm = relogio.GetUtcNow();
            }
            db.AddRange(novas);
            await db.SaveChangesAsync(ct);

            var repetidas = resultado.Operacoes.Count - novas.Count;
            var avisos = repetidas > 0 ? [.. resultado.Avisos, $"{repetidas} operação(ões) já estavam importadas e foram ignoradas."] : resultado.Avisos;
            return Results.Ok(new { Importadas = novas.Count, Avisos = avisos });
        });

        // Lê o cabeçalho de um ficheiro de qualquer corretora e sugere que coluna é o quê.
        grupo.MapPost("/analisar", (PedidoAnalise pedido) =>
            LerFicheiro(pedido.Conteudo, pedido.Xlsx, out var texto) ?? Results.Ok(Importacao.Analisar(texto)));

        // Corretoras que só dão o ticker (Revolut, XTB, eToro): a pessoa indica o ISIN, que dá o país da fonte no Anexo J.
        grupo.MapPut("/ativos/{ativo}/isin", async (ClaimsPrincipal user, string ativo, NovoIsin dados, PortalDbContext db, CancellationToken ct) =>
        {
            var isin = dados.Isin.Trim().ToUpperInvariant();
            if (!Paises.IsinValido(isin))
                return Problema("Isin", "O ISIN tem 12 caracteres: 2 letras do país, 9 letras ou números e 1 dígito (ex.: US0378331005).");
            var atual = ativo.Trim().ToUpperInvariant();
            var alteradas = await Operacoes(db, user).Where(o => o.Ativo == atual)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Ativo, isin), ct);
            return alteradas == 0 ? Results.NotFound() : Results.Ok(new { Alteradas = alteradas });
        });

        // Ação, ETF ou fundo: muda o código do Anexo J (G01 ou G20) em todas as operações do título.
        grupo.MapPut("/ativos/{ativo}/tipo", async (ClaimsPrincipal user, string ativo, NovoTipoAtivo dados, PortalDbContext db, CancellationToken ct) =>
        {
            var isin = ativo.Trim().ToUpperInvariant();
            var alteradas = await Operacoes(db, user).Where(o => o.Ativo == isin)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.TipoAtivo, dados.Tipo), ct);
            return alteradas == 0 ? Results.NotFound() : Results.Ok(new { Alteradas = alteradas });
        });

        grupo.MapGet("/modelo.csv", () => Results.File(Encoding.UTF8.GetBytes(
            Importacao.CabecalhoModelo + "\n" +
            "2024-03-15,compra,US0378331005,Apple,10,172.50,USD,1.00,,\n" +
            "2025-06-02,venda,US0378331005,Apple,4,201.30,USD,1.00,,\n" +
            "2025-05-15,dividendo,US0378331005,Apple,10,0.26,USD,,0.39,USD\n"), "text/csv", "modelo-investimentos.csv"));

        grupo.MapGet("/relatorio/{ano:int}", async (ClaimsPrincipal user, int ano, PortalDbContext db, ICambios cambios, CancellationToken ct) =>
            await RelatorioAsync(await Operacoes(db, user).ToListAsync(ct), ano, cambios, ct));
    }

    /// <summary>Obtém primeiro todos os câmbios necessários (assíncrono) e depois calcula (síncrono e testável).</summary>
    public static async Task<RelatorioIrs> RelatorioAsync(IReadOnlyList<Operacao> operacoes, int ano, ICambios cambios, CancellationToken ct)
    {
        var pedidos = operacoes
            .SelectMany(o => new[]
            {
                (o.Moeda, DateOnly.FromDateTime(o.Momento)),
                (o.MoedaRetencao ?? o.Moeda, DateOnly.FromDateTime(o.Momento)),
                (o.MoedaComissoes ?? "EUR", DateOnly.FromDateTime(o.Momento)),
            })
            .Distinct();
        var taxas = new Dictionary<(string, DateOnly), decimal?>();
        foreach (var (moeda, data) in pedidos)
            taxas[(moeda, data)] = await cambios.TaxaAsync(moeda, data, ct);

        return CalculoIrs.Calcular(operacoes, ano, (moeda, data) => taxas.GetValueOrDefault((moeda, data)));
    }

    public async Task<CartaoPainel> ObterCartaoAsync(ContextoUtilizador contexto, CancellationToken ct)
    {
        var db = contexto.Servicos.GetRequiredService<PortalDbContext>();
        var operacoes = await db.Set<Operacao>().Where(o => o.UtilizadorId == contexto.UtilizadorId).ToListAsync(ct);
        var emFalta = Portal.Core.Perfil.CamposPerfil.EmFalta(contexto.Perfil, CamposPerfil);

        if (operacoes.Count == 0)
            return new CartaoPainel(Id, Nome, "Importa o histórico da tua corretora para preparar o Anexo J.", [], emFalta);

        // Até ao fim do prazo de entrega (junho), o ano que interessa é o anterior.
        var ano = contexto.Hoje.Month <= 6 ? contexto.Hoje.Year - 1 : contexto.Hoje.Year;
        var r = await RelatorioAsync(operacoes, ano, contexto.Servicos.GetRequiredService<ICambios>(), ct);

        var indicadores = new List<Indicador>
        {
            new(r.SaldoMaisValias.ToString("C0", Pt), $"saldo de mais-valias {ano}", r.SaldoMaisValias >= 0 ? "positivo" : "aviso"),
            new((r.ImpostoMaisValias + r.ImpostoDividendos).ToString("C0", Pt), "imposto estimado", "neutro"),
        };
        var itens = new List<ItemCartao>
        {
            new($"{r.MaisValias.Count} linha(s) no quadro 9.2A e {r.Dividendos.Count} país(es) no quadro 8A"),
            new($"{operacoes.Count} operações registadas"),
        };
        if (r.Avisos.Count > 0)
            itens.Add(new ItemCartao($"{r.Avisos.Count} aviso(s) para rever"));

        return new CartaoPainel(Id, Nome, $"Estimativa para o IRS de {ano}. Não substitui um contabilista.", itens, emFalta) { Indicadores = indicadores };
    }

    /// <summary>Devolve o ficheiro como texto CSV (um Excel é convertido aqui), ou o erro a mostrar.</summary>
    private static IResult? LerFicheiro(string? conteudo, string? xlsx, out string texto)
    {
        texto = "";
        if (!string.IsNullOrEmpty(xlsx))
        {
            if (xlsx.Length > 7_000_000) // 5 MB em base64
                return Problema("Conteudo", "O ficheiro é demasiado grande (máximo 5 MB).");
            try
            {
                texto = Xlsx.ParaCsv(Convert.FromBase64String(xlsx));
                return texto.Length == 0 ? Problema("Conteudo", "O ficheiro Excel não tem dados.") : null;
            }
            catch (Exception e) when (e is FormatException or InvalidDataException or System.Xml.XmlException)
            {
                return Problema("Conteudo", "Não consegui ler o ficheiro Excel. Guarda-o como .xlsx (ou exporta em CSV) e tenta outra vez.");
            }
        }
        if (string.IsNullOrEmpty(conteudo))
            return Problema("Conteudo", "O ficheiro está vazio.");
        if (conteudo.Length > 5_000_000)
            return Problema("Conteudo", "O ficheiro é demasiado grande (máximo 5 MB).");
        texto = conteudo;
        return null;
    }

    private static bool MoedaValida(string? moeda) => moeda?.Trim() is { Length: 3 } m && m.All(char.IsAsciiLetter);

    private static IResult Problema(string campo, string mensagem) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [campo] = [mensagem] });

    private static IQueryable<Operacao> Operacoes(PortalDbContext db, ClaimsPrincipal user)
    {
        var id = UtilizadorId(user);
        return db.Set<Operacao>().Where(o => o.UtilizadorId == id);
    }

    private static string UtilizadorId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
