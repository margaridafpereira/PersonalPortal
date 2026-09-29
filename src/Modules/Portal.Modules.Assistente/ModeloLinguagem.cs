using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Core.Modulos;

namespace Portal.Modules.Assistente;

/// <summary>
/// Fornecedor de IA, em "Assistente" na configuração. Qualquer serviço compatível com a API de chat da OpenAI serve:
/// Google Gemini (por omissão), Groq, OpenRouter, Mistral, ou um modelo local com Ollama. A chave nunca fica no código:
/// <c>dotnet user-secrets set "Assistente:Chave" "…" --project src/Portal.Api</c>.
/// </summary>
public sealed class ConfiguracaoAssistente
{
    public string Fornecedor { get; set; } = "Google Gemini";
    public string Url { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";
    /// <summary>A Google retira modelos antigos às contas novas; confirma os disponíveis em GET {Url}models.</summary>
    /// <remarks>
    /// Os "lite" são o ponto de partida: no plano gratuito os "flash" normais passam muito tempo sobrecarregados (503),
    /// e para resumir o que as consultas devolvem os "lite" chegam e respondem em poucos segundos.
    /// </remarks>
    public string Modelo { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>
    /// Modelos a tentar, por ordem, quando o anterior está sobrecarregado (HTTP 503). No plano gratuito da Gemini os
    /// modelos mais recentes ficam muitas vezes sem capacidade; com alternativas, o assistente continua a responder.
    /// </summary>
    public List<string> ModelosAlternativos { get; set; } = [];
    public string? Chave { get; set; }

    /// <summary>No plano gratuito, o fornecedor pode usar os dados enviados para melhorar os modelos: a nota diz isso.</summary>
    public bool PlanoGratuito { get; set; } = true;

    /// <summary>Termos do fornecedor sobre os dados, para a nota.</summary>
    public string? TermosDados { get; set; } = "https://ai.google.dev/gemini-api/terms";

    public int PedidosPorHora { get; set; } = 30;

    /// <summary>Com um modelo local (Ollama, LM Studio) não há chave.</summary>
    public bool Configurado => !string.IsNullOrWhiteSpace(Chave) || Uri.TryCreate(Url, UriKind.Absolute, out var u) && u.IsLoopback;
}

public enum Papel { Sistema, Utilizador, Assistente, Ferramenta }

/// <param name="Extra">
/// Campo "extra_content" do fornecedor, devolvido tal e qual no pedido seguinte. Os modelos Gemini 3 põem aqui a
/// "thought_signature" e recusam a conversa se ela não voltar.
/// </param>
public sealed record ChamadaFerramenta(string Id, string Nome, string Argumentos, string? Extra = null);

/// <summary>Uma mensagem da conversa com o modelo. Mensagens do assistente podem pedir ferramentas; as de ferramenta respondem a esse pedido.</summary>
public sealed record MensagemModelo(Papel Papel, string? Texto, IReadOnlyList<ChamadaFerramenta>? Chamadas = null, string? IdChamada = null);

public sealed record RespostaModelo(string? Texto, IReadOnlyList<ChamadaFerramenta> Chamadas);

/// <summary>Erro do fornecedor, com uma mensagem que se pode mostrar à pessoa.</summary>
public sealed class ErroFornecedorIa(string mensagem, Exception? causa = null) : Exception(mensagem, causa);

public interface IModeloLinguagem
{
    Task<RespostaModelo> ConversarAsync(IReadOnlyList<MensagemModelo> mensagens, IReadOnlyList<FerramentaAssistente> ferramentas, CancellationToken ct);
}

/// <summary>
/// Cliente da API de chat compatível com a OpenAI (<c>POST {Url}/chat/completions</c>), com chamadas de ferramentas.
/// Uma instância serve uma pergunta: o modelo que respondeu primeiro fica para as voltas seguintes, porque as
/// assinaturas das ferramentas (Gemini 3) só valem para o modelo que as criou.
/// </summary>
public sealed class ModeloCompativelOpenAI(HttpClient http, IOptions<ConfiguracaoAssistente> opcoes, ILogger<ModeloCompativelOpenAI> log) : IModeloLinguagem
{
    private string? _modeloEmUso;

    /// <summary>O modelo que está a responder a esta pergunta (null antes da primeira resposta).</summary>
    public string? ModeloEmUso => _modeloEmUso;

    public async Task<RespostaModelo> ConversarAsync(IReadOnlyList<MensagemModelo> mensagens, IReadOnlyList<FerramentaAssistente> ferramentas, CancellationToken ct)
    {
        var cfg = opcoes.Value;
        string[] candidatos = _modeloEmUso is { } fixado ? [fixado] : [cfg.Modelo, .. cfg.ModelosAlternativos.Where(m => m != cfg.Modelo)];
        foreach (var modelo in candidatos)
        {
            if (await TentarAsync(modelo, mensagens, ferramentas, ct) is { } resposta)
            {
                _modeloEmUso = modelo;
                return resposta;
            }
        }
        throw new ErroFornecedorIa($"{cfg.Fornecedor} está sobrecarregado neste momento (acontece no plano gratuito). Tenta daqui a um minuto.");
    }

    /// <summary>A resposta do modelo, ou null se ele estiver sobrecarregado (503) e valer a pena tentar outro.</summary>
    private async Task<RespostaModelo?> TentarAsync(string modelo, IReadOnlyList<MensagemModelo> mensagens, IReadOnlyList<FerramentaAssistente> ferramentas, CancellationToken ct)
    {
        var cfg = opcoes.Value;
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(cfg.Url.TrimEnd('/') + "/"), "chat/completions"))
        {
            Content = new StringContent(Corpo(modelo, mensagens, ferramentas).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(cfg.Chave))
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg.Chave);

        HttpResponseMessage resposta;
        try
        {
            resposta = await http.SendAsync(pedido, ct);
        }
        catch (HttpRequestException e)
        {
            throw new ErroFornecedorIa($"Não foi possível contactar {cfg.Fornecedor}. Confirma a ligação à internet (e o proxy, numa rede de empresa).", e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ErroFornecedorIa($"{cfg.Fornecedor} demorou demasiado a responder. Tenta outra vez.", e);
        }

        using (resposta)
        {
            var json = await resposta.Content.ReadAsStringAsync(ct);
            if (resposta.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                log.LogInformation("{Modelo} está sobrecarregado; a tentar o modelo seguinte.", modelo);
                return null;
            }
            if (!resposta.IsSuccessStatusCode)
            {
                log.LogWarning("{Fornecedor} respondeu {Estado}: {Corpo}", cfg.Fornecedor, (int)resposta.StatusCode, json.Length > 500 ? json[..500] : json);
                throw new ErroFornecedorIa(resposta.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"A chave de {cfg.Fornecedor} não foi aceite. Confirma a configuração do assistente.",
                    HttpStatusCode.TooManyRequests => $"Atingiste o limite de pedidos de {cfg.Fornecedor} (no plano gratuito é por minuto e por dia). Tenta daqui a pouco.",
                    HttpStatusCode.NotFound => $"{cfg.Fornecedor} não conhece o modelo \"{modelo}\" (a Google retira modelos antigos). Confirma o nome na configuração.",
                    _ => $"{cfg.Fornecedor} devolveu um erro ({(int)resposta.StatusCode}). Tenta outra vez.",
                });
            }
            return Interpretar(json);
        }
    }

    public static JsonObject Corpo(string modelo, IReadOnlyList<MensagemModelo> mensagens, IReadOnlyList<FerramentaAssistente> ferramentas)
    {
        var corpo = new JsonObject
        {
            ["model"] = modelo,
            ["messages"] = new JsonArray([.. mensagens.Select(Mensagem)]),
        };
        if (ferramentas.Count > 0)
            corpo["tools"] = new JsonArray([.. ferramentas.Select(Ferramenta)]);
        return corpo;
    }

    private static JsonNode Mensagem(MensagemModelo m) => m.Papel switch
    {
        Papel.Sistema => new JsonObject { ["role"] = "system", ["content"] = m.Texto },
        Papel.Utilizador => new JsonObject { ["role"] = "user", ["content"] = m.Texto },
        Papel.Ferramenta => new JsonObject { ["role"] = "tool", ["tool_call_id"] = m.IdChamada, ["content"] = m.Texto },
        _ when m.Chamadas is { Count: > 0 } chamadas => new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = m.Texto,
            ["tool_calls"] = new JsonArray([.. chamadas.Select(c =>
            {
                var chamada = new JsonObject
                {
                    ["id"] = c.Id,
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = c.Nome, ["arguments"] = c.Argumentos },
                };
                if (c.Extra is not null)
                    chamada["extra_content"] = JsonNode.Parse(c.Extra);
                return (JsonNode)chamada;
            })]),
        },
        _ => new JsonObject { ["role"] = "assistant", ["content"] = m.Texto },
    };

    private static JsonNode Ferramenta(FerramentaAssistente f)
    {
        var funcao = new JsonObject { ["name"] = f.Nome, ["description"] = f.Descricao };
        // Sem parâmetros, o esquema fica de fora: alguns fornecedores recusam um objeto sem propriedades.
        if (f.Parametros.Count > 0)
        {
            var propriedades = new JsonObject();
            foreach (var p in f.Parametros)
            {
                var esquema = new JsonObject { ["type"] = p.Tipo, ["description"] = p.Descricao };
                if (p.Valores is { Count: > 0 } valores)
                    esquema["enum"] = new JsonArray([.. valores.Select(v => (JsonNode)v)]);
                propriedades[p.Nome] = esquema;
            }
            funcao["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = propriedades,
                ["required"] = new JsonArray([.. f.Parametros.Where(p => p.Obrigatorio).Select(p => (JsonNode)p.Nome)]),
            };
        }
        return new JsonObject { ["type"] = "function", ["function"] = funcao };
    }

    public static RespostaModelo Interpretar(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var mensagem = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
            var texto = mensagem.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            var chamadas = new List<ChamadaFerramenta>();
            if (mensagem.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
                foreach (var chamada in tc.EnumerateArray())
                {
                    var funcao = chamada.GetProperty("function");
                    var argumentos = funcao.TryGetProperty("arguments", out var a)
                        ? a.ValueKind == JsonValueKind.String ? a.GetString() ?? "{}" : a.GetRawText()
                        : "{}";
                    var id = chamada.TryGetProperty("id", out var i) && i.GetString() is { Length: > 0 } s ? s : $"chamada-{chamadas.Count + 1}";
                    var extra = chamada.TryGetProperty("extra_content", out var e) && e.ValueKind == JsonValueKind.Object ? e.GetRawText() : null;
                    chamadas.Add(new ChamadaFerramenta(id, funcao.GetProperty("name").GetString() ?? "", argumentos, extra));
                }
            return new RespostaModelo(texto, chamadas);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            throw new ErroFornecedorIa("A resposta do fornecedor de IA veio num formato inesperado.", e);
        }
    }
}
