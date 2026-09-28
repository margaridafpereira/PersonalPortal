using System.Text.Json.Serialization;

namespace Portal.Modules.Apoios.Regras;

[JsonConverter(typeof(JsonStringEnumConverter<EstadoElegibilidade>))]
public enum EstadoElegibilidade
{
    Provavel,
    FaltaInformacao,
    NaoElegivel,
    Encerrado,
}

[JsonConverter(typeof(JsonStringEnumConverter<ResultadoCondicao>))]
public enum ResultadoCondicao
{
    Cumpre,
    NaoCumpre,
    Desconhecido,
}

public sealed record Condicao(string Descricao, ResultadoCondicao Resultado);

public sealed record Apoio(
    string Id,
    string Nome,
    string Descricao,
    string Categoria,
    string ComoPedir,
    string FonteOficial,
    DateOnly VerificadoEm,
    string? Prazo = null,
    string? Aviso = null);

public sealed record ResultadoApoio(
    Apoio Apoio,
    EstadoElegibilidade Estado,
    IReadOnlyList<Condicao> Condicoes,
    string? Estimativa);

/// <summary>Junta as condições de um apoio e deduz o estado: basta uma falhar para não ser elegível.</summary>
internal sealed class Avaliacao(Apoio apoio)
{
    private readonly List<Condicao> _condicoes = [];
    private string? _estimativa;

    /// <param name="cumpre">null quando o perfil não tem a informação necessária.</param>
    public Avaliacao Condicao(string descricao, bool? cumpre)
    {
        _condicoes.Add(new Condicao(descricao, cumpre switch
        {
            true => ResultadoCondicao.Cumpre,
            false => ResultadoCondicao.NaoCumpre,
            null => ResultadoCondicao.Desconhecido,
        }));
        return this;
    }

    public Avaliacao Estimativa(string? texto)
    {
        _estimativa = texto;
        return this;
    }

    public ResultadoApoio Resultado(bool encerrado = false)
    {
        var estado = encerrado ? EstadoElegibilidade.Encerrado
            : _condicoes.Any(c => c.Resultado == ResultadoCondicao.NaoCumpre) ? EstadoElegibilidade.NaoElegivel
            : _condicoes.Any(c => c.Resultado == ResultadoCondicao.Desconhecido) ? EstadoElegibilidade.FaltaInformacao
            : EstadoElegibilidade.Provavel;

        // A estimativa só faz sentido quando o apoio é provável.
        return new ResultadoApoio(apoio, estado, _condicoes, estado == EstadoElegibilidade.Provavel ? _estimativa : null);
    }
}
