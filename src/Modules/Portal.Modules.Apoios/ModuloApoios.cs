using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portal.Core.Modulos;
using Portal.Core.Perfil;

namespace Portal.Modules.Apoios;

public sealed class ModuloApoios : IModulo
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    public string Id => "apoios";
    public string Nome => "Radar de apoios e prazos";
    public string Descricao => "Apoios públicos a que provavelmente tens direito e os prazos que não podes falhar.";

    public IReadOnlyCollection<string> CamposPerfil { get; } =
    [
        Portal.Core.Perfil.CamposPerfil.DataNascimento,
        Portal.Core.Perfil.CamposPerfil.ResidenteFiscal,
        Portal.Core.Perfil.CamposPerfil.Dependente,
        Portal.Core.Perfil.CamposPerfil.CategoriaRendimento,
        Portal.Core.Perfil.CamposPerfil.RendimentoAnualAgregado,
        Portal.Core.Perfil.CamposPerfil.SituacaoHabitacao,
    ];

    public void RegistarServicos(IServiceCollection servicos, IConfiguration configuracao) =>
        servicos.AddSingleton<IFonteIndexantes, IndexantesEmbebidos>();

    public void MapearEndpoints(IEndpointRouteBuilder rotas) =>
        rotas.MapGet("/api/apoios/indexantes/{ano:int}", (int ano, IFonteIndexantes fonte) =>
            fonte.Obter(ano) is { } i ? Results.Ok(i) : Results.NotFound());

    public Task<CartaoPainel> ObterCartaoAsync(ContextoUtilizador contexto, CancellationToken ct)
    {
        var emFalta = Portal.Core.Perfil.CamposPerfil.EmFalta(contexto.Perfil, CamposPerfil);
        var itens = new List<ItemCartao>();

        // Pré-visualização até o motor de regras chegar (fase 2): só a condição de idade.
        var idade = contexto.Perfil.IdadeEm(contexto.Hoje);
        var valores = contexto.Servicos.GetRequiredService<IFonteIndexantes>().Obter(contexto.Hoje.Year);
        if (idade is { } anos && valores is not null)
        {
            var maxJovem = (int)valores[ChavesIndexantes.IdadeMaximaJovem];
            if (anos >= 18 && anos <= maxJovem)
                itens.Add(new ItemCartao(
                    $"Com {anos} anos, estás dentro da idade do IRS Jovem, Porta 65 Jovem e IMT Jovem.",
                    "As outras condições são verificadas pelo motor de regras."));
        }

        if (valores is not null)
            itens.Add(new ItemCartao(
                $"IAS {valores.Ano}: {valores[ChavesIndexantes.Ias].ToString("C", Pt)}",
                $"Valores verificados a {valores.VerificadoEm:dd/MM/yyyy}"));

        var resumo = emFalta.Count > 0
            ? $"Completa {emFalta.Count} campo(s) do perfil para veres os teus apoios."
            : "Perfil completo. O motor de regras chega na próxima fase.";

        return Task.FromResult(new CartaoPainel(Id, Nome, resumo, itens, emFalta));
    }
}
