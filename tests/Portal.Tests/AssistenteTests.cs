using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Portal.Core.Modulos;
using Portal.Modules.Assistente;

namespace Portal.Tests;

public class ModeloCompativelOpenAITests
{
    private static readonly FerramentaAssistente SemParametros = new("perfil", "Perfil", [], (_, _, _) => Task.FromResult<object?>(null));
    private static readonly FerramentaAssistente ComParametros = new("relatorio", "Relatório",
        [new("ano", "integer", "Ano", Obrigatorio: true), new("tipo", "string", "Tipo", Valores: ["a", "b"])], (_, _, _) => Task.FromResult<object?>(null));

    [Fact]
    public void Pedido_segue_o_formato_de_chat_da_openai()
    {
        MensagemModelo[] mensagens =
        [
            new(Papel.Sistema, "instruções"),
            new(Papel.Utilizador, "pergunta"),
            new(Papel.Assistente, null, [new ChamadaFerramenta("c1", "relatorio", "{\"ano\":2025}")]),
            new(Papel.Ferramenta, "{\"saldo\":10}", IdChamada: "c1"),
        ];

        var corpo = ModeloCompativelOpenAI.Corpo("gemini-2.5-flash", mensagens, [SemParametros, ComParametros]);
        var json = JsonDocument.Parse(corpo.ToJsonString()).RootElement;

        Assert.Equal("gemini-2.5-flash", json.GetProperty("model").GetString());
        var msgs = json.GetProperty("messages");
        Assert.Equal(["system", "user", "assistant", "tool"], msgs.EnumerateArray().Select(m => m.GetProperty("role").GetString()));
        Assert.Equal("relatorio", msgs[2].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("c1", msgs[3].GetProperty("tool_call_id").GetString());

        var ferramentas = json.GetProperty("tools");
        Assert.False(ferramentas[0].GetProperty("function").TryGetProperty("parameters", out _)); // sem parâmetros, sem esquema
        var parametros = ferramentas[1].GetProperty("function").GetProperty("parameters");
        Assert.Equal("integer", parametros.GetProperty("properties").GetProperty("ano").GetProperty("type").GetString());
        Assert.Equal(["a", "b"], parametros.GetProperty("properties").GetProperty("tipo").GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(["ano"], parametros.GetProperty("required").EnumerateArray().Select(v => v.GetString()));
    }

    [Fact]
    public void Resposta_com_pedidos_de_ferramentas_e_lida()
    {
        const string json = """
            {"choices":[{"message":{"role":"assistant","content":null,
              "tool_calls":[{"id":"x1","type":"function","function":{"name":"relatorio","arguments":"{\"ano\":2025}"}}]}}]}
            """;

        var r = ModeloCompativelOpenAI.Interpretar(json);

        Assert.Null(r.Texto);
        var chamada = Assert.Single(r.Chamadas);
        Assert.Equal(("x1", "relatorio", "{\"ano\":2025}"), (chamada.Id, chamada.Nome, chamada.Argumentos));
    }

    [Fact]
    public void Assinatura_do_gemini_volta_no_pedido_seguinte()
    {
        const string json = """
            {"choices":[{"message":{"role":"assistant","tool_calls":[{"extra_content":{"google":{"thought_signature":"Ev4CCvsC"}},
              "id":"f1","type":"function","function":{"name":"veiculos_e_prazos","arguments":"{}"}}]}}]}
            """;

        var resposta = ModeloCompativelOpenAI.Interpretar(json);
        var corpo = ModeloCompativelOpenAI.Corpo("gemini-3.8-flash",
            [new(Papel.Utilizador, "IUC?"), new(Papel.Assistente, null, resposta.Chamadas), new(Papel.Ferramenta, "{}", IdChamada: "f1")], []);

        var chamada = JsonDocument.Parse(corpo.ToJsonString()).RootElement.GetProperty("messages")[1].GetProperty("tool_calls")[0];
        Assert.Equal("Ev4CCvsC", chamada.GetProperty("extra_content").GetProperty("google").GetProperty("thought_signature").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "limite")]
    [InlineData(HttpStatusCode.Unauthorized, "chave")]
    [InlineData(HttpStatusCode.NotFound, "modelo")]
    public async Task Erros_do_fornecedor_viram_mensagens_para_a_pessoa(HttpStatusCode estado, string esperado)
    {
        var modelo = new ModeloCompativelOpenAI(new HttpClient(new RespostaFixa(estado)),
            Options.Create(new ConfiguracaoAssistente { Chave = "k" }), NullLogger<ModeloCompativelOpenAI>.Instance);

        var erro = await Assert.ThrowsAsync<ErroFornecedorIa>(() => modelo.ConversarAsync([new(Papel.Utilizador, "olá")], [], CancellationToken.None));

        Assert.Contains(esperado, erro.Message);
    }

    [Fact]
    public async Task Modelo_sobrecarregado_passa_ao_seguinte_e_fica_com_ele()
    {
        var respostas = new SequenciaDeRespostas(
            (HttpStatusCode.ServiceUnavailable, "{}"),
            (HttpStatusCode.OK, """{"choices":[{"message":{"content":null,"tool_calls":[{"id":"a","function":{"name":"perfil","arguments":"{}"}}]}}]}"""),
            (HttpStatusCode.OK, """{"choices":[{"message":{"content":"Pronto."}}]}"""));
        var modelo = new ModeloCompativelOpenAI(new HttpClient(respostas),
            Options.Create(new ConfiguracaoAssistente { Chave = "k", Modelo = "novo", ModelosAlternativos = ["estavel"] }), NullLogger<ModeloCompativelOpenAI>.Instance);

        await modelo.ConversarAsync([new(Papel.Utilizador, "olá")], [], CancellationToken.None);
        await modelo.ConversarAsync([new(Papel.Utilizador, "olá")], [], CancellationToken.None);

        Assert.Equal(["novo", "estavel", "estavel"], respostas.Modelos);
        Assert.Equal("estavel", modelo.ModeloEmUso);
    }

    [Fact]
    public void Modelo_local_nao_precisa_de_chave()
    {
        Assert.True(new ConfiguracaoAssistente { Url = "http://localhost:11434/v1/" }.Configurado);
        Assert.False(new ConfiguracaoAssistente().Configurado);
    }

    private sealed class SequenciaDeRespostas(params (HttpStatusCode Estado, string Corpo)[] respostas) : HttpMessageHandler
    {
        private int _i;
        public List<string> Modelos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Modelos.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.GetProperty("model").GetString()!);
            var (estado, corpo) = respostas[_i++];
            return new HttpResponseMessage(estado) { Content = new StringContent(corpo) };
        }
    }

    private sealed class RespostaFixa(HttpStatusCode estado) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(estado) { Content = new StringContent("{}") });
    }
}

public class AssistenteApiTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    [Fact]
    public async Task So_responde_depois_de_aceitar_a_nota_e_usa_os_dados_da_pessoa()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);
        const string csv = """
            Date,Type,ISIN,Name,Quantity,Price,Currency
            2024-02-03,Buy,IE00B4L5Y983,iShares Core MSCI World,3,80,EUR
            """;
        await cliente.PostAsJsonAsync("/api/investimentos/importar", new { formato = "auto", conteudo = csv });
        var pergunta = new { conversa = new[] { new { papel = "utilizador", texto = "Que títulos tenho?" } }, seccao = "investimentos" };

        var estado = await cliente.GetFromJsonAsync<JsonElement>("/api/assistente");
        var antes = await cliente.PostAsJsonAsync("/api/assistente/mensagens", pergunta);
        await cliente.PostAsync("/api/assistente/aceitar", null);
        var depois = await cliente.PostAsJsonAsync("/api/assistente/mensagens", pergunta);

        Assert.True(estado.GetProperty("configurado").GetBoolean());
        Assert.Contains("Google Gemini", estado.GetProperty("nota").GetString());
        Assert.Equal(HttpStatusCode.Conflict, antes.StatusCode);
        var resposta = await depois.Content.ReadFromJsonAsync<RespostaAssistente>();
        Assert.Contains("IE00B4L5Y983", resposta!.Texto); // o modelo falso devolve o que a consulta encontrou
        Assert.Equal(["Carteira"], resposta.Consultas);
    }

    [Fact]
    public async Task Conversa_tem_de_acabar_numa_pergunta()
    {
        var cliente = await ApiTests.ClienteAutenticadoAsync(factory);
        await cliente.PostAsync("/api/assistente/aceitar", null);

        var r = await cliente.PostAsJsonAsync("/api/assistente/mensagens",
            new { conversa = new[] { new { papel = "assistente", texto = "olá" } }, seccao = "painel" });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Todas_as_seccoes_dao_ferramentas_ao_assistente()
    {
        using var scope = factory.Services.CreateScope();
        var nomes = scope.ServiceProvider.GetRequiredService<ServicoAssistente>().Ferramentas().Select(f => f.Nome).ToList();

        Assert.Equal(nomes.Count, nomes.Distinct().Count());
        Assert.Contains("apoios_elegiveis", nomes);
        Assert.Contains("imoveis_seguidos", nomes);
        Assert.Contains("veiculos_e_prazos", nomes);
        Assert.Contains("relatorio_irs_investimentos", nomes);
        Assert.Contains("perfil", nomes);
        await Task.CompletedTask;
    }
}

/// <summary>
/// Modelo de teste: à pergunta responde pedindo a carteira; com o resultado da consulta, devolve-o como texto.
/// Assim os testes verificam o ciclo pergunta → consulta → resposta sem chamar nenhum fornecedor.
/// </summary>
public sealed class ModeloFalso : IModeloLinguagem
{
    public Task<RespostaModelo> ConversarAsync(IReadOnlyList<MensagemModelo> mensagens, IReadOnlyList<FerramentaAssistente> ferramentas, CancellationToken ct)
    {
        var ultima = mensagens[^1];
        return Task.FromResult(ultima.Papel == Papel.Ferramenta
            ? new RespostaModelo($"Segundo o portal: {ultima.Texto}", [])
            : new RespostaModelo(null, [new ChamadaFerramenta("c1", "carteira_investimentos", "{}")]));
    }
}
