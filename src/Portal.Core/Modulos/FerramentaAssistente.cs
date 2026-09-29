using System.Text.Json;

namespace Portal.Core.Modulos;

/// <summary>Um parâmetro de uma ferramenta. Tipos do JSON Schema: "string", "integer", "number" ou "boolean".</summary>
public sealed record ParametroFerramenta(string Nome, string Tipo, string Descricao, bool Obrigatorio = false, IReadOnlyList<string>? Valores = null);

/// <summary>
/// Uma consulta que o assistente de IA pode fazer aos dados da pessoa numa secção (ex.: o relatório de IRS).
/// Só de leitura: o assistente responde com os números reais do portal, mas não altera nada.
/// O resultado é serializado em JSON e enviado ao fornecedor de IA, por isso deve ter só o necessário para responder.
/// </summary>
public sealed record FerramentaAssistente(
    string Nome,
    string Descricao,
    IReadOnlyList<ParametroFerramenta> Parametros,
    Func<ContextoUtilizador, ArgumentosFerramenta, CancellationToken, Task<object?>> Executar);

/// <summary>Os argumentos que o modelo escolheu, lidos com tolerância: um valor em falta ou do tipo errado dá null.</summary>
public sealed class ArgumentosFerramenta(IReadOnlyDictionary<string, JsonElement> valores)
{
    public static readonly ArgumentosFerramenta Vazios = new(new Dictionary<string, JsonElement>());

    public string? Texto(string nome) =>
        valores.TryGetValue(nome, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

    public int? Inteiro(string nome) =>
        valores.TryGetValue(nome, out var v) ? v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(v.GetString(), out var i) => i,
            _ => null,
        } : null;
}
