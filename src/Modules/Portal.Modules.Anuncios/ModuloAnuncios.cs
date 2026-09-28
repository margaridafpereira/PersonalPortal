using System.Globalization;
using Portal.Core.Modulos;
using Portal.Core.Perfil;

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

    public Task<CartaoPainel> ObterCartaoAsync(ContextoUtilizador contexto, CancellationToken ct)
    {
        var perfil = contexto.Perfil;
        var emFalta = Portal.Core.Perfil.CamposPerfil.EmFalta(perfil, CamposPerfil);
        var itens = new List<ItemCartao>();

        if (perfil.ProcuraComprarCasa == true && perfil.OrcamentoCompra is { } orcamento)
        {
            var onde = string.IsNullOrWhiteSpace(perfil.Concelho) ? "" : $" em {perfil.Concelho}";
            itens.Add(new ItemCartao($"Procuras casa{onde} até {orcamento.ToString("C0", Pt)}."));
        }

        var resumo = emFalta.Count > 0
            ? $"Completa {emFalta.Count} campo(s) do perfil para personalizar as pesquisas."
            : "Pesquisas guardadas e favoritos chegam na fase 3.";

        return Task.FromResult(new CartaoPainel(Id, Nome, resumo, itens, emFalta));
    }
}
