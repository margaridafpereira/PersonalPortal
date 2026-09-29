using System.Net.Http.Json;
using Portal.Modules.Carro;

namespace Portal.Tests;

public class CarroTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);

    private static Veiculo Carro(DateOnly matricula, CategoriaVeiculo categoria = CategoriaVeiculo.LigeiroPassageiros) =>
        new() { Id = Guid.NewGuid(), Nome = "Clio", DataPrimeiraMatricula = matricula, Categoria = categoria, Combustivel = Combustivel.GasoleoSimples };

    [Theory]
    [InlineData("2024-03-10", "2028-03-10")] // 2 anos: primeira inspeção aos 4
    [InlineData("2022-11-05", "2026-11-05")] // faz 4 anos
    [InlineData("2020-10-01", "2026-10-01")] // faz 6 anos
    [InlineData("2018-09-30", "2026-09-30")] // faz 8 anos
    [InlineData("2015-01-20", "2027-01-20")] // mais de 8 anos: anual
    public void Inspecao_de_ligeiros_de_passageiros(string matricula, string esperado)
    {
        var v = Carro(DateOnly.Parse(matricula));

        Assert.Equal(DateOnly.Parse(esperado), PrazosVeiculo.ProximaInspecao(v, Hoje));
    }

    [Fact]
    public void Inspecao_de_ligeiros_de_mercadorias_aos_2_anos_e_depois_anual()
    {
        Assert.Equal(new DateOnly(2027, 5, 1), PrazosVeiculo.ProximaInspecao(Carro(new DateOnly(2025, 5, 1), CategoriaVeiculo.LigeiroMercadorias), Hoje));
        Assert.Equal(new DateOnly(2027, 2, 1), PrazosVeiculo.ProximaInspecao(Carro(new DateOnly(2022, 2, 1), CategoriaVeiculo.LigeiroMercadorias), Hoje));
    }

    [Fact]
    public void Iuc_no_ultimo_dia_util_do_mes_da_matricula()
    {
        var prazos = PrazosVeiculo.Proximos(Carro(new DateOnly(2019, 1, 15)), Hoje);

        var iuc = Assert.Single(prazos, p => p.Tipo == TipoPrazoVeiculo.Iuc);
        Assert.Equal(new DateOnly(2027, 2, 1), iuc.Data); // 31/01/2027 é domingo
    }

    [Fact]
    public void Seguro_repete_todos_os_anos()
    {
        var v = Carro(new DateOnly(2019, 1, 15));
        v.RenovacaoSeguro = new DateOnly(2023, 3, 1);

        var seguro = Assert.Single(PrazosVeiculo.Proximos(v, Hoje), p => p.Tipo == TipoPrazoVeiculo.Seguro);

        Assert.Equal(new DateOnly(2027, 3, 1), seguro.Data);
    }

    [Fact]
    public void Precos_da_dgeg_sao_lidos_e_ordenados()
    {
        const string json = """
            {"status":true,"resultado":[
              {"Id":1,"Nome":"Posto A","Marca":"GALP","Preco":"1,366 €","Morada":"Rua 1","Localidade":"Ermesinde","DataAtualizacao":"2026-09-28 07:40","Latitude":41.2,"Longitude":-8.5},
              {"Id":2,"Nome":"Posto B","Marca":"PRIO","Preco":"1,299 €","Morada":"Rua 2","Localidade":"Valongo","DataAtualizacao":"2026-09-28 06:45","Latitude":41.1,"Longitude":-8.4},
              {"Id":3,"Nome":"Posto C","Marca":"BP","Preco":"--","Morada":"","Localidade":"","DataAtualizacao":""}
            ]}
            """;

        var r = PrecosDgeg.Interpretar(json, "Valongo", Combustivel.GasoleoSimples)!;

        Assert.Equal(2, r.NumeroPostos);
        Assert.Equal(1.299m, r.Minimo);
        Assert.Equal(1.333m, r.Media);
        Assert.Equal("Posto B", r.MaisBaratos[0].Nome);
        Assert.Equal(["Ermesinde", "Valongo"], r.Localidades);
    }

    [Fact]
    public void Filtra_os_postos_pela_localidade()
    {
        Posto[] postos =
        [
            new("A", "GALP", "Rua 1", "Ermesinde", 1.366m, "", null, null),
            new("B", "PRIO", "Rua 2", "Valongo", 1.299m, "", null, null),
            new("C", "BP", "Rua 3", "Ermesinde", 1.350m, "", null, null),
        ];

        var r = PrecosDgeg.Resumir("Valongo", Combustivel.GasoleoSimples, postos, "ermesinde")!;

        Assert.Equal((2, 1.350m, "Ermesinde"), (r.NumeroPostos, r.Minimo, r.Localidade));
        Assert.Equal(["Ermesinde", "Valongo"], r.Localidades); // a lista é sempre a do concelho todo
        Assert.Null(PrecosDgeg.Resumir("Valongo", Combustivel.GasoleoSimples, postos, "Alfena"));
    }

    [Fact]
    public void Dois_carros_com_o_mesmo_nome_tem_eventos_distintos_no_calendario()
    {
        var a = Carro(new DateOnly(2022, 11, 5));
        var b = Carro(new DateOnly(2022, 11, 5));

        var ics = Portal.Core.Prazos.Calendario.ParaIcs(
            new[] { a, b }.SelectMany(v => PrazosVeiculo.Proximos(v, Hoje)).Select(p => p.ParaEvento()), [], DateTimeOffset.UnixEpoch);

        var uids = ics.Split("\r\n").Where(l => l.StartsWith("UID:")).ToList();
        Assert.Equal(uids.Count, uids.Distinct().Count());
        Assert.Contains(uids, u => u.Contains(a.Id.ToString("N")));
    }
}

public class CarroApiTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    [Fact]
    public async Task Veiculo_novo_aparece_com_prazos_e_no_painel()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);

        var r = await cliente.PostAsJsonAsync("/api/carro/veiculos",
            new DadosVeiculo("Clio", "aa-00-bb", CategoriaVeiculo.LigeiroPassageiros, Combustivel.GasoleoSimples, new DateOnly(2018, 5, 10), null, null, 5.5m));
        r.EnsureSuccessStatusCode();

        var lista = await cliente.GetFromJsonAsync<List<VeiculoComPrazos>>("/api/carro/veiculos");
        var item = Assert.Single(lista!);
        Assert.Equal("AA-00-BB", item.Veiculo.Matricula);
        Assert.Contains(item.Prazos, p => p.Tipo == TipoPrazoVeiculo.Inspecao);

        var painel = await cliente.GetFromJsonAsync<List<Portal.Core.Modulos.CartaoPainel>>("/api/painel");
        Assert.Contains(painel!, c => c.ModuloId == "carro");
    }

    [Fact]
    public async Task Veiculo_sem_data_de_matricula_e_rejeitado()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);

        var r = await cliente.PostAsJsonAsync("/api/carro/veiculos",
            new DadosVeiculo("Clio", null, CategoriaVeiculo.LigeiroPassageiros, Combustivel.Gasolina95, default, null, null, null));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
    }

    private sealed record PrazoResposta(TipoPrazoVeiculo Tipo, DateOnly Data);

    private sealed record VeiculoComPrazos(Veiculo Veiculo, List<PrazoResposta> Prazos);
}

/// <summary>Substitui a API da DGEG nos testes.</summary>
public sealed class PrecosFalsos : IPrecosCombustiveis
{
    public Task<PrecosConcelho?> NoConcelhoAsync(string concelho, Combustivel combustivel, CancellationToken ct, string? localidade = null) =>
        Task.FromResult<PrecosConcelho?>(new PrecosConcelho(concelho, combustivel, 1.299m, 1.35m, 2,
            [new Posto("Posto B", "PRIO", "Rua 2", "Valongo", 1.299m, "2026-09-28 06:45", null, null)]));

    public Task<IReadOnlyList<string>> ConcelhosAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(["Porto", "Valongo"]);
}

public sealed class CarregamentoFalso : IPostosCarregamento
{
    public Task<CarregamentoConcelho?> NoConcelhoAsync(string concelho, CancellationToken ct) =>
        Task.FromResult<CarregamentoConcelho?>(new CarregamentoConcelho(concelho, 1, PostosMobiE.EnergiaReferencia,
            [new PostoCarregamento("VLG-00001", "Rua 1", "EDP", "Semirrápido", 22, 2.5m, ["€ 0.1 /kWh"])]));
}
