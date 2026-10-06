using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portal.Core.Dados;
using Portal.Core.Perfil;
using Portal.Modules.Anuncios;
using Portal.Modules.Carro;
using Portal.Modules.Investimentos;

namespace Portal.Api;

/// <summary>
/// Conta de demonstração: quem chega pelo portefólio entra sem registo, numa conta com dados de exemplo.
/// A conta é só de leitura (pedidos que alteram dados recebem 403), não tem palavra-passe (não se entra nela pelo login
/// normal) e os dados são repostos uma vez por dia, para que as datas acompanhem o calendário.
/// Liga-se com <c>Demo:Ativa</c>. Ver docs/demonstracao.md.
/// </summary>
public static class Demo
{
    public const string Email = "demo@portal-pessoal.demo";

    /// <summary>Pedidos que não alteram dados de ninguém e que a conta de demonstração pode fazer.</summary>
    private static readonly string[] PermitidosEmDemo =
    [
        "/api/auth/logout",
        "/api/assistente/aceitar",
        "/api/assistente/mensagens",
    ];

    private static readonly SemaphoreSlim Reposicao = new(1, 1);

    public static bool EmDemo(ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name, Email, StringComparison.OrdinalIgnoreCase);

    public static WebApplication UseDemoSoLeitura(this WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            var leitura = HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method);
            if (!leitura && EmDemo(ctx.User) && !PermitidosEmDemo.Any(p => ctx.Request.Path.Equals(p, StringComparison.OrdinalIgnoreCase)))
            {
                await Results.Problem(Portal.Core.Idioma.T("Esta é a conta de demonstração: podes ver tudo, mas não alterar. Os dados são de exemplo.", "This is the demo account: you can look at everything but not change it. The data is fictitious."),
                    statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(ctx);
                return;
            }
            await next(ctx);
        });
        return app;
    }

    public static IEndpointRouteBuilder MapDemo(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet("/api/demo", (ClaimsPrincipal user, IConfiguration config) =>
            new { Ativa = config.GetValue("Demo:Ativa", false), EmDemo = EmDemo(user) })
            .AllowAnonymous();

        rotas.MapPost("/api/demo/entrar", async (IConfiguration config, UserManager<Utilizador> contas, SignInManager<Utilizador> entrada,
            PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            if (!config.GetValue("Demo:Ativa", false))
                return Results.NotFound();

            var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);
            var conta = await PrepararAsync(contas, db, hoje, relogio.GetUtcNow(), ct);
            await entrada.SignInAsync(conta, isPersistent: false);
            return Results.NoContent();
        }).AllowAnonymous();

        return rotas;
    }

    /// <summary>Cria a conta na primeira vez e repõe os dados se ainda não foram repostos hoje.</summary>
    public static async Task<Utilizador> PrepararAsync(UserManager<Utilizador> contas, PortalDbContext db, DateOnly hoje, DateTimeOffset agora, CancellationToken ct)
    {
        await Reposicao.WaitAsync(ct);
        try
        {
            var conta = await contas.FindByEmailAsync(Email);
            if (conta is null)
            {
                // Sem palavra-passe: o login normal nunca entra nesta conta.
                conta = new Utilizador { UserName = Email, Email = Email, EmailConfirmed = true };
                var criada = await contas.CreateAsync(conta);
                if (!criada.Succeeded)
                    throw new InvalidOperationException("Não foi possível criar a conta de demonstração: " +
                        string.Join(" ", criada.Errors.Select(e => e.Description)));
            }

            var perfil = await db.Perfis.FindAsync([conta.Id], ct);
            if (perfil is null || DateOnly.FromDateTime(perfil.AtualizadoEm.LocalDateTime) < hoje)
                await ReporDadosAsync(db, conta.Id, hoje, agora, ct);
            return conta;
        }
        finally
        {
            Reposicao.Release();
        }
    }

    private static async Task ReporDadosAsync(PortalDbContext db, string id, DateOnly hoje, DateTimeOffset agora, CancellationToken ct)
    {
        await db.Set<Operacao>().Where(o => o.UtilizadorId == id).ExecuteDeleteAsync(ct);
        await db.Set<Abastecimento>().Where(a => a.UtilizadorId == id).ExecuteDeleteAsync(ct);
        await db.Set<Veiculo>().Where(v => v.UtilizadorId == id).ExecuteDeleteAsync(ct);
        await db.Set<ImovelFavorito>().Where(f => f.UtilizadorId == id).ExecuteDeleteAsync(ct);
        await db.Set<PesquisaGuardada>().Where(p => p.UtilizadorId == id).ExecuteDeleteAsync(ct);
        await db.AvisosEnviados.Where(a => a.UtilizadorId == id).ExecuteDeleteAsync(ct);
        await db.Preferencias.Where(p => p.UtilizadorId == id).ExecuteDeleteAsync(ct);
        await db.Perfis.Where(p => p.UtilizadorId == id).ExecuteDeleteAsync(ct);
        // O perfil antigo pode estar carregado; as linhas já foram apagadas diretamente na base.
        db.ChangeTracker.Clear();

        db.Perfis.Add(new PerfilUtilizador
        {
            UtilizadorId = id,
            Nome = "Ana (demo)",
            DataNascimento = hoje.AddYears(-29).AddMonths(-4),
            Concelho = "Porto",
            Freguesia = "Bonfim",
            ResidenteFiscal = true,
            Dependente = false,
            CategoriaRendimento = CategoriaRendimento.TrabalhoDependente,
            RendimentoAnualAgregado = 31_500,
            NumeroAdultos = 1,
            SituacaoHabitacao = SituacaoHabitacao.Arrenda,
            RendaMensal = 850,
            DataContratoArrendamento = new DateOnly(hoje.Year - 2, 3, 1),
            ProcuraComprarCasa = true,
            OrcamentoCompra = 280_000,
            ValidadeCartaConducao = hoje.AddMonths(5),
            ProprietarioImovel = false,
            AtualizadoEm = agora,
        });

        db.Preferencias.Add(new PreferenciasUtilizador
        {
            UtilizadorId = id,
            SeccoesAtivas = ["apoios", "anuncios", "carro", "investimentos"],
            SeccoesVistas = ["apoios", "anuncios", "carro", "investimentos"],
            ZonasInteresse = ["Porto", "Matosinhos"],
            AlertasEmail = false,
            AtualizadoEm = agora,
        });

        db.Set<PesquisaGuardada>().AddRange(
            new PesquisaGuardada { Id = Guid.NewGuid(), UtilizadorId = id, Nome = "T2 no Porto", Negocio = Negocio.Comprar, Tipo = TipoImovel.Apartamento,
                Distrito = "Porto", Concelho = "Porto", PrecoMaximo = 280_000, QuartosMinimo = 2, CriadaEm = agora.AddDays(-60) },
            new PesquisaGuardada { Id = Guid.NewGuid(), UtilizadorId = id, Nome = "Moradia em Matosinhos", Negocio = Negocio.Comprar, Tipo = TipoImovel.Moradia,
                Distrito = "Porto", Concelho = "Matosinhos", PrecoMaximo = 350_000, QuartosMinimo = 3, CriadaEm = agora.AddDays(-30) });

        ImovelFavorito Imovel(string titulo, string tipologia, decimal area, string concelho, EstadoFavorito estado, string? notas, params (int DiasAtras, decimal Preco)[] precos) => new()
        {
            Id = Guid.NewGuid(),
            UtilizadorId = id,
            Url = $"https://www.idealista.pt/comprar-casas/{(concelho == "Porto" ? "porto" : "matosinhos")}/",
            Titulo = titulo,
            Tipo = TipoImovel.Apartamento,
            Tipologia = tipologia,
            AreaM2 = area,
            Concelho = concelho,
            Estado = estado,
            Notas = notas,
            CriadoEm = agora.AddDays(-precos.Max(p => p.DiasAtras)),
            Precos = [.. precos.Select(p => new RegistoPreco { Data = hoje.AddDays(-p.DiasAtras), Preco = p.Preco })],
        };
        db.Set<ImovelFavorito>().AddRange(
            Imovel("T2 com varanda, Bonfim", "T2", 78, "Porto", EstadoFavorito.Ativo, "Anúncio de exemplo.", (75, 279_000), (40, 269_000), (10, 259_000)),
            Imovel("T2 renovado, Paranhos", "T2", 85, "Porto", EstadoFavorito.Visitado, "Visita de exemplo: precisa de obras na cozinha.", (50, 265_000), (20, 265_000)),
            Imovel("T3 perto do metro, Matosinhos Sul", "T3", 110, "Matosinhos", EstadoFavorito.Ativo, null, (30, 310_000), (5, 318_000)));

        var carro = new Veiculo
        {
            Id = Guid.NewGuid(),
            UtilizadorId = id,
            Nome = "Clio (demo)",
            Matricula = "AA-00-AA",
            Categoria = CategoriaVeiculo.LigeiroPassageiros,
            Combustivel = Combustivel.Gasolina95,
            DataPrimeiraMatricula = hoje.AddYears(-6).AddMonths(2),
            Cilindrada = 999,
            EmissoesCo2 = 118,
            NormaCo2 = NormaEmissoes.Wltp,
            RenovacaoSeguro = hoje.AddDays(45),
            Seguradora = "Seguradora de exemplo",
            ValorSeguroAnual = 320,
            ProximaRevisao = hoje.AddDays(25),
            ConsumoLitros100Km = 5.8m,
            CriadoEm = agora.AddDays(-90),
        };
        db.Set<Veiculo>().Add(carro);
        db.Set<Abastecimento>().AddRange(
            new[] { (60, 41_200, 38.5m, 66.10m), (40, 41_850, 37.9m, 64.80m), (20, 42_520, 39.2m, 67.40m), (3, 43_170, 38.1m, 65.90m) }
                .Select(a => new Abastecimento
                {
                    Id = Guid.NewGuid(), VeiculoId = carro.Id, UtilizadorId = id, Data = hoje.AddDays(-a.Item1),
                    Quilometros = a.Item2, Litros = a.Item3, ValorTotal = a.Item4, DepositoCheio = true, Posto = "Posto de exemplo", CriadoEm = agora,
                }));

        // Operações de exemplo: compras em anos anteriores; vendas e dividendos no ano que o cartão do painel mostra
        // (até junho, o ano anterior; depois, o atual), sempre entre fevereiro e junho, para nunca ficarem no futuro.
        var ano = hoje.Month <= 6 ? hoje.Year - 1 : hoje.Year;
        Operacao Op(TipoOperacao tipo, DateTime momento, string isin, string nome, TipoAtivo tipoAtivo, decimal qtd, decimal preco, string moeda,
            decimal comissoes, string corretora, decimal retencao = 0) => new()
        {
            Id = Guid.NewGuid(), UtilizadorId = id, Tipo = tipo, Momento = momento, Ativo = isin, Nome = nome, TipoAtivo = tipoAtivo,
            Quantidade = qtd, PrecoUnitario = preco, Moeda = moeda, Comissoes = comissoes, RetencaoFonte = retencao,
            MoedaRetencao = retencao > 0 ? moeda : null, Corretora = corretora, IdExterno = $"demo-{Guid.NewGuid():N}", CriadoEm = agora,
        };
        db.Set<Operacao>().AddRange(
            Op(TipoOperacao.Compra, new DateTime(ano - 2, 3, 14, 15, 32, 0), "US0378331005", "Apple Inc.", TipoAtivo.Acao, 10, 172.40m, "USD", 1m, "DEGIRO"),
            Op(TipoOperacao.Compra, new DateTime(ano - 1, 1, 22, 16, 5, 0), "US0378331005", "Apple Inc.", TipoAtivo.Acao, 5, 192.10m, "USD", 1m, "DEGIRO"),
            Op(TipoOperacao.Venda, new DateTime(ano, 4, 10, 17, 12, 0), "US0378331005", "Apple Inc.", TipoAtivo.Acao, 8, 228.50m, "USD", 1m, "DEGIRO"),
            Op(TipoOperacao.Compra, new DateTime(ano - 2, 6, 3, 9, 41, 0), "IE00BK5BQT80", "Vanguard FTSE All-World UCITS ETF (USD) Acc", TipoAtivo.Etf, 20, 112.30m, "EUR", 0m, "Trading 212"),
            Op(TipoOperacao.Venda, new DateTime(ano, 5, 18, 10, 3, 0), "IE00BK5BQT80", "Vanguard FTSE All-World UCITS ETF (USD) Acc", TipoAtivo.Etf, 6, 131.80m, "EUR", 0m, "Trading 212"),
            Op(TipoOperacao.Compra, new DateTime(ano - 1, 9, 9, 15, 50, 0), "US5949181045", "Microsoft Corp.", TipoAtivo.Acao, 4, 405.20m, "USD", 1m, "Interactive Brokers"),
            Op(TipoOperacao.Dividendo, new DateTime(ano, 3, 13, 12, 0, 0), "US5949181045", "Microsoft Corp.", TipoAtivo.Acao, 4, 0.83m, "USD", 0m, "Interactive Brokers", retencao: 0.49m),
            Op(TipoOperacao.Dividendo, new DateTime(ano, 6, 12, 12, 0, 0), "US5949181045", "Microsoft Corp.", TipoAtivo.Acao, 4, 0.83m, "USD", 0m, "Interactive Brokers", retencao: 0.49m),
            Op(TipoOperacao.Dividendo, new DateTime(ano, 5, 15, 12, 0, 0), "US0378331005", "Apple Inc.", TipoAtivo.Acao, 15, 0.26m, "USD", 0m, "DEGIRO", retencao: 0.58m));

        await db.SaveChangesAsync(ct);
    }
}
