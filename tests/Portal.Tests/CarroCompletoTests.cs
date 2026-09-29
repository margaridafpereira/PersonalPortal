using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Portal.Api.Avisos;
using Portal.Core.Dados;
using Portal.Core.Perfil;
using Portal.Core.Prazos;
using Portal.Modules.Carro;

namespace Portal.Tests;

public class FeriadosTests
{
    [Fact]
    public void Pascoa_e_feriados_moveis()
    {
        Assert.Equal(new DateOnly(2026, 4, 5), Calendario.Pascoa(2026));
        Assert.Equal(new DateOnly(2025, 4, 20), Calendario.Pascoa(2025));
        Assert.True(Calendario.EFeriado(new DateOnly(2026, 4, 3)));  // Sexta-feira Santa
        Assert.True(Calendario.EFeriado(new DateOnly(2026, 6, 4)));  // Corpo de Deus
        Assert.False(Calendario.EFeriado(new DateOnly(2026, 2, 17))); // Carnaval é facultativo
    }

    [Theory]
    [InlineData("2026-12-08", "2026-12-09")] // feriado numa terça
    [InlineData("2026-10-03", "2026-10-06")] // sábado, e a segunda é 5 de Outubro
    [InlineData("2026-12-25", "2026-12-28")] // Natal numa sexta
    [InlineData("2026-09-29", "2026-09-29")] // dia útil
    public void Prazo_num_feriado_passa_para_o_dia_util_seguinte(string data, string esperado) =>
        Assert.Equal(DateOnly.Parse(esperado), Calendario.DiaUtil(DateOnly.Parse(data)));
}

public class IucTests
{
    private static Veiculo Carro(string matricula, Combustivel combustivel, int? cc, int? co2, NormaEmissoes? norma = null) =>
        new() { Nome = "X", DataPrimeiraMatricula = DateOnly.Parse(matricula), Combustivel = combustivel, Cilindrada = cc, EmissoesCo2 = co2, NormaCo2 = norma };

    [Fact]
    public void Categoria_b_gasolina() =>
        // (31,77 + 65,15) × 1,15
        Assert.Equal(111.46m, Iuc.Estimar(Carro("2018-11-27", Combustivel.Gasolina95, 1199, 130, NormaEmissoes.Wltp)).Valor);

    [Fact]
    public void Categoria_b_gasoleo_tem_adicional()
    {
        // (63,74 + 65,15) × 1,15 + 10,07
        var r = Iuc.Estimar(Carro("2012-05-10", Combustivel.GasoleoSimples, 1560, 104, NormaEmissoes.Nedc));

        Assert.Equal(158.29m, r.Valor);
        Assert.Contains(r.Detalhe, d => d.Contains("gasóleo"));
    }

    [Fact]
    public void Adicional_de_co2_so_para_matriculas_desde_2017()
    {
        var antes = Iuc.Estimar(Carro("2016-06-01", Combustivel.Gasolina98, 1998, 190, NormaEmissoes.Nedc)).Valor;
        var depois = Iuc.Estimar(Carro("2017-06-01", Combustivel.Gasolina98, 1998, 190, NormaEmissoes.Nedc)).Valor;

        Assert.Equal(31.77m, depois - antes);
    }

    [Fact]
    public void Categoria_a_e_eletricos_e_dados_em_falta()
    {
        Assert.Equal(62.40m, Iuc.Estimar(Carro("2003-03-01", Combustivel.Gasolina95, 1400, null)).Valor);
        Assert.Equal(0m, Iuc.Estimar(Carro("2022-01-01", Combustivel.Eletrico, null, null)).Valor);
        Assert.Equal(["cilindrada", "emissões de CO2"], Iuc.Estimar(Carro("2020-01-01", Combustivel.Gasolina95, null, null)).EmFalta);
    }
}

public class ConsumoTests
{
    [Fact]
    public void Consumo_real_entre_depositos_cheios()
    {
        Abastecimento[] a =
        [
            new() { Data = new(2026, 6, 1), Quilometros = 1000, Litros = 40, ValorTotal = 70, DepositoCheio = true },
            new() { Data = new(2026, 6, 10), Quilometros = 1500, Litros = 10, ValorTotal = 17, DepositoCheio = false },
            new() { Data = new(2026, 6, 20), Quilometros = 1800, Litros = 30, ValorTotal = 51, DepositoCheio = true },
        ];

        var r = Consumo.Calcular(a, new DateOnly(2026, 6, 30));

        Assert.Equal(5.0m, r.ConsumoReal);    // 40 L em 800 km
        Assert.Equal(0.085m, r.CustoPorKm);   // 68 € em 800 km
        Assert.Equal(138m, r.GastoMensal);    // tudo em junho
    }

    [Fact]
    public void Sem_dois_depositos_cheios_nao_ha_consumo()
    {
        var r = Consumo.Calcular([new Abastecimento { Data = new(2026, 6, 1), Quilometros = 1000, Litros = 40, ValorTotal = 70, DepositoCheio = true }], new DateOnly(2026, 6, 30));

        Assert.Null(r.ConsumoReal);
        Assert.Equal(1, r.Abastecimentos);
    }
}

public class MobiETests
{
    [Fact]
    public void Custo_do_operador_com_tarifas_por_carregamento_kwh_e_minuto() =>
        // 20 kWh a 7,4 kW = 162,2 min: 0,261 + 20 × 0,1 + 162,2 × 0,015
        Assert.Equal(4.69m, PostosMobiE.CustoOperador(["€ 0.261 /charge", "€ 0.1 /kWh", "€ 0.015 /min"], 7.4m, 20));

    [Fact]
    public void Tarifa_por_minuto_com_limite() =>
        // 20 kWh a 22 kW = 54,5 min, mas só se pagam os primeiros 45
        Assert.Equal(1.80m, PostosMobiE.CustoOperador(["€ 0.04 /min até 45 min"], 22, 20));

    [Fact]
    public void Ficheiro_de_tarifas_real_agrupa_por_concelho()
    {
        const string csv = """
            ID;UID_TOMADA;TIPO_POSTO;MUNICIPIO;MORADA;OPERADOR;MOBICHARGER;NIVELTENSAO;TIPO_TARIFARIO;TIPO_TARIFA;TARIFA;TIPO_TOMADA;FORMATO_TOMADA;POTENCIA_TOMADA
            ABF-00009;PT-EDP-EABF-00009-1-1;Semirrápido;Albufeira;Vila Gale Cerro Alagoa;EDP;False;MT;;FLAT;€ 0.261 /charge;MENNEKES;SOCKET;7,4
            ABF-00009;PT-EDP-EABF-00009-1-1;Semirrápido;Albufeira;Vila Gale Cerro Alagoa;EDP;False;MT;;ENERGY;€ 0.1 /kWh;MENNEKES;SOCKET;7,4
            ABF-00009;PT-EDP-EABF-00009-1-1;Semirrápido;Albufeira;Vila Gale Cerro Alagoa;EDP;False;MT;;TIME;€ 0.015 /min;MENNEKES;SOCKET;7,4
            ABF-00011;ABF-00011-01-01;Semirrápido;Albufeira;Praia Maria Luísa;BLU;False;BTE;REGULAR;FLAT;€ 0.2608 /charge;MENNEKES;SOCKET;22
            ABF-00011;ABF-00011-01-01;Semirrápido;Albufeira;Praia Maria Luísa;BLU;False;BTE;REGULAR;TIME;€ 0.04 /min;MENNEKES;SOCKET;22
            LRS-90023;LRS-90023-01-01;Semirrápido;Lisboa;Alameda dos Oceanos;GLP;False;BTN;REGULAR;FLAT;€ 0 /charge;MENNEKES;SOCKET;22
            """;

        var r = PostosMobiE.Interpretar(csv);

        Assert.Equal(2, r["albufeira"].Count);
        Assert.Equal(0m, Assert.Single(r["lisboa"]).Posto.CustoOperador);
    }
}

public class PrazosNovosCarroTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);

    [Fact]
    public void Renovacao_do_seguro_diz_a_seguradora_e_o_valor()
    {
        var v = new Veiculo { Id = Guid.NewGuid(), Nome = "Clio", DataPrimeiraMatricula = new(2019, 1, 15), RenovacaoSeguro = new(2023, 12, 1), Seguradora = "Fidelidade", ValorSeguroAnual = 320 };

        var seguro = Assert.Single(PrazosVeiculo.Proximos(v, Hoje), p => p.Tipo == TipoPrazoVeiculo.Seguro);

        Assert.Equal(new DateOnly(2026, 12, 1), seguro.Data);
        Assert.Contains("Fidelidade, 320,00 €", seguro.Descricao.Replace('.', ','));
    }

    [Fact]
    public void Carta_de_conducao_pela_validade_ou_pela_idade()
    {
        var comValidade = PrazosVeiculo.CartaConducao(new PerfilUtilizador { ValidadeCartaConducao = new(2027, 3, 1) }, Hoje);
        Assert.Equal(new DateOnly(2027, 3, 1), comValidade!.Data);
        Assert.Contains("01/09/2026", comValidade.Descricao); // pode revalidar 6 meses antes

        Assert.Equal(new DateOnly(2036, 5, 10), PrazosVeiculo.ProximaRevalidacaoPorIdade(new DateOnly(1996, 5, 10), Hoje)); // já fez 30: próxima aos 40
        Assert.Equal(new DateOnly(2028, 2, 1), PrazosVeiculo.ProximaRevalidacaoPorIdade(new DateOnly(1956, 2, 1), Hoje));  // depois dos 70, de 2 em 2 anos
        Assert.Null(PrazosVeiculo.CartaConducao(new PerfilUtilizador(), Hoje));
    }
}

public class CarroApiNovidadesTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    [Fact]
    public async Task Veiculo_com_iuc_abastecimentos_e_edicao()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);
        var criado = await (await cliente.PostAsJsonAsync("/api/carro/veiculos", new DadosVeiculo(
            "DS", "BS-60-MP", CategoriaVeiculo.LigeiroPassageiros, Combustivel.Gasolina95, new(2018, 11, 27), null, null, 6,
            Cilindrada: 1199, EmissoesCo2: 130, NormaCo2: NormaEmissoes.Wltp))).Content.ReadFromJsonAsync<Veiculo>();

        var alteracao = await cliente.PutAsJsonAsync($"/api/carro/veiculos/{criado!.Id}", new DadosVeiculo(
            "DS 3", "BS-60-MP", CategoriaVeiculo.LigeiroPassageiros, Combustivel.Gasolina95, new(2018, 11, 27), new(2024, 12, 1), null, 6,
            Cilindrada: 1199, EmissoesCo2: 130, NormaCo2: NormaEmissoes.Wltp, Seguradora: "Tranquilidade", ValorSeguroAnual: 280));
        await cliente.PostAsJsonAsync($"/api/carro/veiculos/{criado.Id}/abastecimentos", new NovoAbastecimento(new(2026, 9, 1), 50000, 40, 70, true, "Intermarché"));
        await cliente.PostAsJsonAsync($"/api/carro/veiculos/{criado.Id}/abastecimentos", new NovoAbastecimento(new(2026, 9, 15), 50600, 36, 64, true, null));
        var lista = await cliente.GetFromJsonAsync<List<VistaVeiculo>>("/api/carro/veiculos");

        Assert.Equal(HttpStatusCode.OK, alteracao.StatusCode);
        var v = Assert.Single(lista!);
        Assert.Equal(("DS 3", "Tranquilidade"), (v.Veiculo.Nome, v.Veiculo.Seguradora));
        Assert.Equal(111.46m, v.Iuc.Valor);
        Assert.Equal(6.0m, v.Consumo.ConsumoReal); // 36 L em 600 km
        Assert.Equal(2, v.Abastecimentos.Count);
    }

    [Fact]
    public async Task Carta_de_conducao_fica_no_perfil()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);

        await cliente.PutAsJsonAsync("/api/carro/carta", new DadosCarta(new DateOnly(2030, 1, 15)));
        var carta = await cliente.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/carro/carta");

        Assert.Equal("2030-01-15", carta.GetProperty("validade").GetString());
        Assert.Equal("CartaConducao", carta.GetProperty("prazo").GetProperty("tipo").GetString());
    }

    private sealed record VistaVeiculo(Veiculo Veiculo, EstimativaIuc Iuc, EstatisticasConsumo Consumo, List<Abastecimento> Abastecimentos);
}

public class AvisosTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    [Fact]
    public async Task Aviso_vai_uma_vez_por_antecedencia()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);
        var hoje = DateOnly.FromDateTime(DateTime.Now);
        var nome = $"Carro {Guid.NewGuid():N}"[..20];
        await cliente.PostAsJsonAsync("/api/carro/veiculos", new DadosVeiculo(
            nome, null, CategoriaVeiculo.LigeiroPassageiros, Combustivel.Gasolina95, hoje.AddYears(-1), null, hoje.AddDays(2), null));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var id = (await db.Set<Veiculo>().SingleAsync(v => v.Nome == nome)).UtilizadorId;
        var email = await db.Users.Where(u => u.Id == id).Select(u => u.Email).SingleAsync();
        var avisos = scope.ServiceProvider.GetRequiredService<ServicoAvisos>();

        await avisos.ExecutarAsync(DateTimeOffset.Now, CancellationToken.None);
        await avisos.ExecutarAsync(DateTimeOffset.Now, CancellationToken.None); // no mesmo dia, nada de novo

        var meus = factory.Emails.Enviados.Where(e => e.Para == email).ToList();
        // Um só email por pessoa e por dia, com os prazos de todas as secções (aqui também os fiscais dos Apoios).
        var enviado = Assert.Single(meus);
        Assert.Contains($"- Revisão: {nome}", enviado.Texto);
        Assert.Contains("(daqui a 2 dias)", enviado.Texto);
    }

    [Fact]
    public async Task Email_de_teste_mostra_os_proximos_prazos_sem_os_marcar()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);
        var hoje = DateOnly.FromDateTime(DateTime.Now);
        await cliente.PostAsJsonAsync("/api/carro/veiculos", new DadosVeiculo(
            "Teste", null, CategoriaVeiculo.LigeiroPassageiros, Combustivel.Gasolina95, hoje.AddYears(-1), null, hoje.AddDays(5), null));

        var estado = await cliente.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/avisos");
        var r = await cliente.PostAsync("/api/avisos/teste", null);
        var outra = await cliente.PostAsync("/api/avisos/teste", null);

        Assert.True(estado.GetProperty("ativos").GetBoolean());
        Assert.Contains(estado.GetProperty("proximos").EnumerateArray(), p => p.GetProperty("titulo").GetString() == "Revisão: Teste");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(HttpStatusCode.OK, outra.StatusCode); // o teste não gasta o aviso
    }
}

/// <summary>Guarda os emails em vez de os enviar.</summary>
public sealed class EmailsFalsos : IEnviadorEmail
{
    public ConcurrentBag<Email> Enviados { get; } = [];

    public string Destino => "guardados pelos testes";

    public Task EnviarAsync(Email email, CancellationToken ct)
    {
        Enviados.Add(email);
        return Task.CompletedTask;
    }
}
