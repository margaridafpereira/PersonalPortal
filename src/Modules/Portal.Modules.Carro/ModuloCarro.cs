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
using Portal.Core.Perfil;
using Portal.Core.Prazos;

namespace Portal.Modules.Carro;

public sealed record DadosVeiculo(
    string Nome, string? Matricula, CategoriaVeiculo Categoria, Combustivel Combustivel,
    DateOnly DataPrimeiraMatricula, DateOnly? RenovacaoSeguro, DateOnly? ProximaRevisao, decimal? ConsumoLitros100Km,
    int? Cilindrada = null, int? EmissoesCo2 = null, NormaEmissoes? NormaCo2 = null,
    string? Seguradora = null, string? ApoliceSeguro = null, decimal? ValorSeguroAnual = null);

public sealed record NovoAbastecimento(DateOnly Data, int Quilometros, decimal Litros, decimal ValorTotal, bool DepositoCheio, string? Posto);

public sealed record DadosCarta(DateOnly? Validade);

public sealed class ModuloCarro : IModulo
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    public string Id => "carro";
    public string Nome => "Carro";
    public string Descricao => "Inspeção, IUC, seguro e carta sem esquecer, quanto gastas, e onde abastecer ou carregar mais barato.";

    public IReadOnlyCollection<string> CamposPerfil { get; } = [Portal.Core.Perfil.CamposPerfil.Concelho];

    public void RegistarServicos(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AddMemoryCache();
        servicos.AddHttpClient<IPrecosCombustiveis, PrecosDgeg>(c =>
        {
            c.BaseAddress = new Uri(configuracao["Dgeg:Url"] ?? "https://precoscombustiveis.dgeg.gov.pt/");
            c.Timeout = TimeSpan.FromSeconds(60);
        });
        servicos.AddHttpClient<IPostosCarregamento, PostosMobiE>(c => c.Timeout = TimeSpan.FromSeconds(120));
    }

    public IReadOnlyList<FerramentaAssistente> FerramentasAssistente { get; } =
    [
        new("veiculos_e_prazos",
            "Veículos da pessoa (combustível, 1.ª matrícula, seguro), a estimativa do IUC com as parcelas, o consumo real e o gasto pelos abastecimentos, "
            + "e os próximos prazos (inspeção, IUC, seguro, revisão, carta de condução) com os dias em falta.",
            [],
            async (ctx, _, ct) =>
            {
                var db = ctx.Servicos.GetRequiredService<PortalDbContext>();
                var veiculos = await db.Set<Veiculo>().Where(v => v.UtilizadorId == ctx.UtilizadorId).ToListAsync(ct);
                var abastecimentos = await db.Set<Abastecimento>().Where(a => a.UtilizadorId == ctx.UtilizadorId).ToListAsync(ct);
                return new
                {
                    Veiculos = veiculos.Select(v => new
                    {
                        v.Nome, v.Matricula, v.Categoria, Combustivel = NomeCombustivel(v.Combustivel), v.DataPrimeiraMatricula,
                        v.Cilindrada, v.EmissoesCo2, v.Seguradora, v.ValorSeguroAnual, v.RenovacaoSeguro,
                        Iuc = Iuc.Estimar(v),
                        Consumo = Consumo.Calcular(abastecimentos.Where(a => a.VeiculoId == v.Id), ctx.Hoje),
                        Prazos = PrazosVeiculo.Proximos(v, ctx.Hoje).Select(p => new { p.Tipo, p.Data, p.Titulo, p.Descricao, DiasEmFalta = p.DiasEmFalta(ctx.Hoje) }),
                    }),
                    CartaConducao = PrazosVeiculo.CartaConducao(ctx.Perfil, ctx.Hoje) is { } carta
                        ? new { carta.Data, carta.Descricao, DiasEmFalta = carta.DiasEmFalta(ctx.Hoje) }
                        : null,
                };
            }),
        new("preco_combustivel",
            "Preços do combustível num concelho (DGEG, atualizados pelos postos): mínimo, média e os postos mais baratos.",
            [
                new("concelho", "string", "Nome do concelho. Se omitido, usa o concelho do perfil."),
                new("localidade", "string", "Só os postos desta localidade ou freguesia (ex.: Ermesinde). Se omitido, o concelho todo."),
                new("combustivel", "string", "Tipo de combustível. Se omitido, usa o do primeiro veículo a combustível.",
                    Valores: [.. Enum.GetNames<Combustivel>().Where(n => n != nameof(Combustivel.Eletrico))]),
            ],
            async (ctx, argumentos, ct) =>
            {
                if ((argumentos.Texto("concelho") ?? ctx.Perfil.Concelho) is not { } concelho)
                    return new { Erro = "Não foi indicado nenhum concelho e o perfil não tem concelho." };
                Combustivel? combustivel = Enum.TryParse<Combustivel>(argumentos.Texto("combustivel"), true, out var c) ? c : null;
                combustivel ??= (await ctx.Servicos.GetRequiredService<PortalDbContext>().Set<Veiculo>()
                    .Where(v => v.UtilizadorId == ctx.UtilizadorId && v.Combustivel != Combustivel.Eletrico)
                    .OrderBy(v => v.CriadoEm).FirstOrDefaultAsync(ct))?.Combustivel ?? Combustivel.GasoleoSimples;
                return await ctx.Servicos.GetRequiredService<IPrecosCombustiveis>().NoConcelhoAsync(concelho, combustivel.Value, ct, argumentos.Texto("localidade"))
                    ?? (object)new { Erro = $"Sem preços da DGEG para {NomeCombustivel(combustivel.Value)} em {argumentos.Texto("localidade") ?? concelho}." };
            }),
        new("carregamento_eletrico",
            "Postos de carregamento da rede Mobi.E num concelho, ordenados pelo custo do operador do posto para carregar 20 kWh. "
            + "Só inclui a tarifa do operador; a energia do comercializador (cartão) e os impostos somam-se.",
            [new("concelho", "string", "Nome do concelho. Se omitido, usa o concelho do perfil.")],
            async (ctx, argumentos, ct) =>
            {
                if ((argumentos.Texto("concelho") ?? ctx.Perfil.Concelho) is not { } concelho)
                    return new { Erro = "Não foi indicado nenhum concelho e o perfil não tem concelho." };
                return await ctx.Servicos.GetRequiredService<IPostosCarregamento>().NoConcelhoAsync(concelho, ct)
                    ?? (object)new { Erro = $"Sem postos Mobi.E conhecidos em {concelho}." };
            }),
    ];

    public void ConfigurarModelo(ModelBuilder modelo)
    {
        modelo.Entity<Veiculo>(e =>
        {
            e.ToTable("veiculos");
            e.HasIndex(v => v.UtilizadorId);
            e.HasOne<Utilizador>().WithMany().HasForeignKey(v => v.UtilizadorId).OnDelete(DeleteBehavior.Cascade);
            e.Property(v => v.Categoria).HasConversion<string>();
            e.Property(v => v.Combustivel).HasConversion<string>();
            e.Property(v => v.NormaCo2).HasConversion<string>();
            e.Property(v => v.Nome).HasMaxLength(100);
            e.Property(v => v.Matricula).HasMaxLength(20);
            e.Property(v => v.Seguradora).HasMaxLength(100);
            e.Property(v => v.ApoliceSeguro).HasMaxLength(50);
        });
        modelo.Entity<Abastecimento>(e =>
        {
            e.ToTable("abastecimentos");
            e.HasIndex(a => new { a.UtilizadorId, a.VeiculoId });
            e.HasOne<Veiculo>().WithMany().HasForeignKey(a => a.VeiculoId).OnDelete(DeleteBehavior.Cascade);
            e.Property(a => a.Posto).HasMaxLength(100);
        });
    }

    public void MapearEndpoints(IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/carro").RequireAuthorization();

        grupo.MapGet("/veiculos", async (ClaimsPrincipal user, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            var hoje = Hoje(relogio);
            var id = UtilizadorId(user);
            var abastecimentos = await db.Set<Abastecimento>().Where(a => a.UtilizadorId == id).ToListAsync(ct);
            return (await Veiculos(db, user).ToListAsync(ct)).OrderBy(v => v.CriadoEm).Select(v =>
            {
                var doVeiculo = abastecimentos.Where(a => a.VeiculoId == v.Id).ToList();
                return new
                {
                    Veiculo = v,
                    Prazos = PrazosVeiculo.Proximos(v, hoje).Select(p => Vista(p, hoje)),
                    Iuc = Iuc.Estimar(v),
                    Consumo = Consumo.Calcular(doVeiculo, hoje),
                    Abastecimentos = doVeiculo.OrderByDescending(a => a.Data).ThenByDescending(a => a.Quilometros).Take(10),
                };
            });
        });

        grupo.MapPost("/veiculos", async (ClaimsPrincipal user, DadosVeiculo dados, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            if (Validar(dados, Hoje(relogio)) is { } erro)
                return erro;
            var v = new Veiculo { Id = Guid.NewGuid(), UtilizadorId = UtilizadorId(user), CriadoEm = relogio.GetUtcNow() };
            Aplicar(v, dados);
            db.Add(v);
            await db.SaveChangesAsync(ct);
            return Results.Ok(v);
        });

        grupo.MapPut("/veiculos/{id:guid}", async (ClaimsPrincipal user, Guid id, DadosVeiculo dados, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            if (Validar(dados, Hoje(relogio)) is { } erro)
                return erro;
            var v = await Veiculos(db, user).FirstOrDefaultAsync(v => v.Id == id, ct);
            if (v is null)
                return Results.NotFound();
            Aplicar(v, dados);
            await db.SaveChangesAsync(ct);
            return Results.Ok(v);
        });

        grupo.MapDelete("/veiculos/{id:guid}", async (ClaimsPrincipal user, Guid id, PortalDbContext db, CancellationToken ct) =>
            await Veiculos(db, user).Where(v => v.Id == id).ExecuteDeleteAsync(ct) == 0 ? Results.NotFound() : Results.NoContent());

        grupo.MapPost("/veiculos/{id:guid}/abastecimentos", async (ClaimsPrincipal user, Guid id, NovoAbastecimento dados, PortalDbContext db,
            TimeProvider relogio, CancellationToken ct) =>
        {
            if (!await Veiculos(db, user).AnyAsync(v => v.Id == id, ct))
                return Results.NotFound();
            if (dados.Litros <= 0 || dados.ValorTotal < 0 || dados.Quilometros < 0)
                return Problema("Litros", "Indica os litros (ou kWh), o valor pago e os quilómetros do conta-quilómetros.");
            if (dados.Data > Hoje(relogio))
                return Problema("Data", "A data não pode ser no futuro.");

            var a = new Abastecimento
            {
                Id = Guid.NewGuid(), VeiculoId = id, UtilizadorId = UtilizadorId(user), Data = dados.Data, Quilometros = dados.Quilometros,
                Litros = dados.Litros, ValorTotal = dados.ValorTotal, DepositoCheio = dados.DepositoCheio,
                Posto = string.IsNullOrWhiteSpace(dados.Posto) ? null : dados.Posto.Trim(), CriadoEm = relogio.GetUtcNow(),
            };
            db.Add(a);
            await db.SaveChangesAsync(ct);
            return Results.Ok(a);
        });

        grupo.MapDelete("/abastecimentos/{id:guid}", async (ClaimsPrincipal user, Guid id, PortalDbContext db, CancellationToken ct) =>
        {
            var utilizador = UtilizadorId(user);
            return await db.Set<Abastecimento>().Where(a => a.Id == id && a.UtilizadorId == utilizador).ExecuteDeleteAsync(ct) == 0
                ? Results.NotFound() : Results.NoContent();
        });

        // A validade da carta é da pessoa, não de um veículo: fica no perfil.
        grupo.MapGet("/carta", async (ClaimsPrincipal user, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            var hoje = Hoje(relogio);
            var perfil = await Perfil(db, user, ct);
            return new
            {
                Validade = perfil.ValidadeCartaConducao,
                Prazo = PrazosVeiculo.CartaConducao(perfil, hoje, meses: 1200) is { } p ? Vista(p, hoje) : null,
            };
        });

        grupo.MapPut("/carta", async (ClaimsPrincipal user, DadosCarta dados, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            var perfil = await Perfil(db, user, ct);
            if (db.Entry(perfil).State == EntityState.Detached)
                db.Perfis.Add(perfil);
            perfil.ValidadeCartaConducao = dados.Validade;
            perfil.AtualizadoEm = relogio.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        grupo.MapGet("/prazos.ics", async (ClaimsPrincipal user, PortalDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            var hoje = Hoje(relogio);
            var prazos = (await Veiculos(db, user).ToListAsync(ct)).SelectMany(v => PrazosVeiculo.Proximos(v, hoje)).ToList();
            if (PrazosVeiculo.CartaConducao(await Perfil(db, user, ct), hoje) is { } carta)
                prazos.Add(carta);
            var prefs = await db.Preferencias.FindAsync([UtilizadorId(user)], ct);
            var ics = Calendario.ParaIcs(prazos.Select(p => p.ParaEvento()), prefs?.DiasAntecedencia ?? [14, 3], relogio.GetUtcNow());
            return Results.File(Encoding.UTF8.GetBytes(ics), "text/calendar", "carro.ics");
        });

        grupo.MapGet("/combustiveis", async (string concelho, Combustivel combustivel, string? localidade, IPrecosCombustiveis precos, CancellationToken ct) =>
            await precos.NoConcelhoAsync(concelho, combustivel, ct, localidade) is { } r ? Results.Ok(r) : Results.NotFound());

        grupo.MapGet("/concelhos", (IPrecosCombustiveis precos, CancellationToken ct) => precos.ConcelhosAsync(ct));

        grupo.MapGet("/carregamento", async (string concelho, IPostosCarregamento postos, CancellationToken ct) =>
            await postos.NoConcelhoAsync(concelho, ct) is { } r ? Results.Ok(r) : Results.NotFound());
    }

    public async Task<CartaoPainel> ObterCartaoAsync(ContextoUtilizador contexto, CancellationToken ct)
    {
        var db = contexto.Servicos.GetRequiredService<PortalDbContext>();
        var veiculos = await db.Set<Veiculo>().Where(v => v.UtilizadorId == contexto.UtilizadorId).ToListAsync(ct);
        var emFalta = Portal.Core.Perfil.CamposPerfil.EmFalta(contexto.Perfil, CamposPerfil);
        var itens = new List<ItemCartao>();
        var indicadores = new List<Indicador>();

        if (veiculos.Count == 0)
            return new CartaoPainel(Id, Nome, "Adiciona o teu carro para receberes os prazos e os preços do combustível.", itens, emFalta);

        var prazos = TodosOsPrazos(veiculos, contexto.Perfil, contexto.Hoje);
        if (prazos.FirstOrDefault() is { } proximo)
        {
            var dias = proximo.DiasEmFalta(contexto.Hoje);
            indicadores.Add(new Indicador(dias.ToString(), $"dias até: {proximo.Titulo}", dias <= 30 ? "aviso" : "neutro"));
        }

        // O primeiro veículo a combustível (um elétrico não tem preço na DGEG), como na página do Carro.
        var principal = veiculos.OrderBy(v => v.CriadoEm).FirstOrDefault(v => v.Combustivel != Combustivel.Eletrico);
        if (!string.IsNullOrWhiteSpace(contexto.Perfil.Concelho) && principal is not null)
        {
            var precos = await contexto.Servicos.GetRequiredService<IPrecosCombustiveis>().NoConcelhoAsync(contexto.Perfil.Concelho, principal.Combustivel, ct);
            if (precos is not null)
            {
                indicadores.Add(new Indicador($"{precos.Minimo.ToString("0.000", Pt)} €", $"{NomeCombustivel(principal.Combustivel)} mais barato", "positivo"));
                var melhor = precos.MaisBaratos[0];
                itens.Add(new ItemCartao($"Mais barato em {precos.Concelho}: {melhor.Nome} ({melhor.Marca})",
                    $"{(precos.Media - precos.Minimo).ToString("0.000", Pt)} €/L abaixo da média do concelho"));
            }
        }

        itens.AddRange(prazos.Skip(1).Take(2).Select(p => new ItemCartao(p.Titulo, p.Data.ToString("dd/MM/yyyy"))));
        return new CartaoPainel(Id, Nome, $"{veiculos.Count} {(veiculos.Count == 1 ? "veículo" : "veículos")}.", itens, emFalta) { Indicadores = indicadores };
    }

    public async Task<IReadOnlyList<AvisoPrazo>> ObterAvisosAsync(ContextoUtilizador contexto, CancellationToken ct)
    {
        var veiculos = await contexto.Servicos.GetRequiredService<PortalDbContext>().Set<Veiculo>()
            .Where(v => v.UtilizadorId == contexto.UtilizadorId).ToListAsync(ct);
        return TodosOsPrazos(veiculos, contexto.Perfil, contexto.Hoje)
            .Select(p => new AvisoPrazo($"carro:{p.VeiculoId:N}:{p.Tipo}:{p.Data:yyyy-MM-dd}", p.Data, p.Titulo, p.Descricao, p.Link))
            .ToList();
    }

    private static List<PrazoVeiculo> TodosOsPrazos(IEnumerable<Veiculo> veiculos, PerfilUtilizador perfil, DateOnly hoje)
    {
        var prazos = veiculos.SelectMany(v => PrazosVeiculo.Proximos(v, hoje)).ToList();
        if (PrazosVeiculo.CartaConducao(perfil, hoje) is { } carta && carta.Data >= hoje)
            prazos.Add(carta);
        return prazos.OrderBy(p => p.Data).ToList();
    }

    public static string NomeCombustivel(Combustivel c) => c switch
    {
        Combustivel.GasoleoSimples => "Gasóleo simples",
        Combustivel.GasoleoEspecial => "Gasóleo especial",
        Combustivel.Gasolina95 => "Gasolina 95",
        Combustivel.Gasolina95Especial => "Gasolina 95 especial",
        Combustivel.Gasolina98 => "Gasolina 98",
        Combustivel.Gpl => "GPL",
        _ => "Elétrico",
    };

    private static object Vista(PrazoVeiculo p, DateOnly hoje) =>
        new { p.VeiculoId, p.Tipo, p.Data, p.Titulo, p.Descricao, p.Link, DiasEmFalta = p.DiasEmFalta(hoje) };

    private static IResult? Validar(DadosVeiculo d, DateOnly hoje) =>
        string.IsNullOrWhiteSpace(d.Nome) ? Problema("Nome", "Dá um nome ao veículo (ex.: \"Clio\").")
        : d.DataPrimeiraMatricula > hoje || d.DataPrimeiraMatricula.Year < 1950 ? Problema("DataPrimeiraMatricula", "Indica a data da primeira matrícula (está no documento único).")
        : d.Cilindrada is < 0 or > 10_000 ? Problema("Cilindrada", "A cilindrada vai de 0 a 10 000 cm³ (campo P.1 do certificado de matrícula).")
        : d.EmissoesCo2 is < 0 or > 1_000 ? Problema("EmissoesCo2", "As emissões de CO2 vão de 0 a 1000 g/km (campo V.7).")
        : d.ValorSeguroAnual is < 0 ? Problema("ValorSeguroAnual", "O valor do seguro não pode ser negativo.")
        : null;

    private static IResult Problema(string campo, string mensagem) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [campo] = [mensagem] });

    private static void Aplicar(Veiculo v, DadosVeiculo d)
    {
        v.Nome = d.Nome.Trim();
        v.Matricula = d.Matricula?.Trim().ToUpperInvariant();
        v.Categoria = d.Categoria;
        v.Combustivel = d.Combustivel;
        v.DataPrimeiraMatricula = d.DataPrimeiraMatricula;
        v.Cilindrada = d.Cilindrada;
        v.EmissoesCo2 = d.EmissoesCo2;
        v.NormaCo2 = d.NormaCo2;
        v.RenovacaoSeguro = d.RenovacaoSeguro;
        v.Seguradora = string.IsNullOrWhiteSpace(d.Seguradora) ? null : d.Seguradora.Trim();
        v.ApoliceSeguro = string.IsNullOrWhiteSpace(d.ApoliceSeguro) ? null : d.ApoliceSeguro.Trim();
        v.ValorSeguroAnual = d.ValorSeguroAnual;
        v.ProximaRevisao = d.ProximaRevisao;
        v.ConsumoLitros100Km = d.ConsumoLitros100Km;
    }

    private static IQueryable<Veiculo> Veiculos(PortalDbContext db, ClaimsPrincipal user)
    {
        var id = UtilizadorId(user);
        return db.Set<Veiculo>().Where(v => v.UtilizadorId == id);
    }

    private static async Task<PerfilUtilizador> Perfil(PortalDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var id = UtilizadorId(user);
        return await db.Perfis.FindAsync([id], ct) ?? new PerfilUtilizador { UtilizadorId = id };
    }

    private static DateOnly Hoje(TimeProvider relogio) => DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);

    private static string UtilizadorId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
