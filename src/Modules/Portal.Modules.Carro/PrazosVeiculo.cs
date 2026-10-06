using static Portal.Core.Idioma;
using System.Text.Json.Serialization;
using Portal.Core.Perfil;
using Portal.Core.Prazos;

namespace Portal.Modules.Carro;

[JsonConverter(typeof(JsonStringEnumConverter<TipoPrazoVeiculo>))]
public enum TipoPrazoVeiculo { Inspecao, Iuc, Seguro, Revisao, CartaConducao }

public sealed record PrazoVeiculo(Guid VeiculoId, string Veiculo, TipoPrazoVeiculo Tipo, DateOnly Data, string Titulo, string Descricao, string? Link)
{
    public int DiasEmFalta(DateOnly hoje) => Data.DayNumber - hoje.DayNumber;

    // O id do veículo entra no UID: dois carros com o mesmo nome não se sobrepõem no calendário.
    public EventoCalendario ParaEvento() => new(Data, Titulo, Descricao, Link, $"{VeiculoId:N}-{Tipo}");
}

/// <summary>
/// Prazos de cada veículo. Regras verificadas a 28/09/2026 (docs/carro/01-fontes-e-regras.md):
/// inspeção de ligeiros de passageiros aos 4, 6 e 8 anos e depois anual; IUC no mês da matrícula.
/// </summary>
public static class PrazosVeiculo
{
    public static IReadOnlyList<PrazoVeiculo> Proximos(Veiculo v, DateOnly hoje, int meses = 12)
    {
        var fim = hoje.AddMonths(meses);
        var prazos = new List<PrazoVeiculo>();

        if (ProximaInspecao(v, hoje) is { } inspecao && inspecao <= fim)
            prazos.Add(new(v.Id, v.Nome, TipoPrazoVeiculo.Inspecao, inspecao, T($"Inspeção: {v.Nome}", $"Inspection: {v.Nome}"),
                T($"Data-limite da inspeção periódica. Podes fazê-la até 3 meses antes sem mudar o ciclo. Marca a partir de {inspecao.AddMonths(-3):dd/MM/yyyy}.", $"Deadline for the periodic inspection. You can do it up to 3 months early without changing the cycle. Book from {inspecao.AddMonths(-3):dd/MM/yyyy}."),
                "https://www.gov.pt/servicos/levar-o-carro-a-inspecao"));

        // IUC: paga-se durante o mês da matrícula, todos os anos.
        for (var ano = hoje.Year; ano <= fim.Year; ano++)
        {
            var iuc = Calendario.DiaUtil(Calendario.UltimoDiaDoMes(ano, v.DataPrimeiraMatricula.Month));
            if (iuc >= hoje && iuc <= fim && ano > v.DataPrimeiraMatricula.Year)
                prazos.Add(new(v.Id, v.Nome, TipoPrazoVeiculo.Iuc, iuc, $"IUC: {v.Nome}",
                    T("Pagamento do Imposto Único de Circulação, durante o mês da matrícula.", "Annual road tax (IUC), due during the registration month."), "https://www.portaldasfinancas.gov.pt/"));
        }

        if (v.RenovacaoSeguro is { } seguro)
        {
            var renovacao = ProximoAniversario(seguro, hoje);
            var atual = v.Seguradora is { Length: > 0 } s ? T(" Atualmente: ", " Currently: ") + $"{s}{(v.ValorSeguroAnual is { } valor ? $", {valor:0.00} € {T("por ano", "a year")}" : "")}." : "";
            if (renovacao <= fim)
                prazos.Add(new(v.Id, v.Nome, TipoPrazoVeiculo.Seguro, renovacao, T($"Renovação do seguro: {v.Nome}", $"Insurance renewal: {v.Nome}"),
                    T("O seguro renova automaticamente nesta data se não o cancelares.", "The policy renews automatically on this date unless you cancel it.") + atual, null));
        }

        if (v.ProximaRevisao is { } revisao && revisao >= hoje && revisao <= fim)
            prazos.Add(new(v.Id, v.Nome, TipoPrazoVeiculo.Revisao, revisao, T($"Revisão: {v.Nome}", $"Service: {v.Nome}"), T("Revisão marcada ou prevista.", "Booked or planned service."), null));

        return prazos.OrderBy(p => p.Data).ToList();
    }

    /// <summary>Idades a que a carta do grupo 1 (inclui a categoria B) se revalida; depois dos 70, de 2 em 2 anos (IMT).</summary>
    private static readonly int[] IdadesRevalidacao = [30, 40, 50, 60, 65, 70];

    /// <summary>
    /// Revalidação da carta de condução. Com a data de validade da carta (campo 4b) usa-a; sem ela, estima pela idade.
    /// Pode pedir-se até 6 meses antes, no IMT Online.
    /// </summary>
    public static PrazoVeiculo? CartaConducao(PerfilUtilizador perfil, DateOnly hoje, int meses = 12)
    {
        DateOnly data;
        string descricao;
        if (perfil.ValidadeCartaConducao is { } validade)
        {
            data = validade;
            descricao = T($"A carta de condução caduca nesta data (campo 4b). Podes revalidá-la no IMT Online a partir de {validade.AddMonths(-6):dd/MM/yyyy}.", $"Your driving licence expires on this date (field 4b). You can renew it at IMT Online from {validade.AddMonths(-6):dd/MM/yyyy}.");
        }
        else if (perfil.DataNascimento is { } nascimento && ProximaRevalidacaoPorIdade(nascimento, hoje) is { } estimada)
        {
            data = estimada;
            descricao = T("Data provável, calculada pela idade (revalidação aos 30, 40, 50, 60, 65 e 70 anos). Confirma no campo 4b da tua carta e indica a validade no portal.", "Likely date, worked out from your age (renewal at 30, 40, 50, 60, 65 and 70). Check field 4b of your licence and enter the expiry date in the portal.");
        }
        else
            return null;

        if (data < hoje.AddMonths(-1) || data > hoje.AddMonths(meses))
            return null;
        return new(Guid.Empty, T("Carta de condução", "Driving licence"), TipoPrazoVeiculo.CartaConducao, data, T("Revalidar a carta de condução", "Renew your driving licence"), descricao,
            "https://www.imt-ip.pt/condutores/informacoes-gerais/quero-ser-condutor/revalidacao-da-carta-de-conducao/");
    }

    public static DateOnly? ProximaRevalidacaoPorIdade(DateOnly nascimento, DateOnly hoje)
    {
        for (var idade = 18; idade <= 110; idade++)
        {
            var revalida = IdadesRevalidacao.Contains(idade) || (idade > 70 && idade % 2 == 0);
            var data = nascimento.AddYears(idade);
            if (revalida && data >= hoje)
                return data;
        }
        return null;
    }

    /// <summary>
    /// Próxima data-limite da inspeção periódica (aniversário da primeira matrícula).
    /// Ligeiros de passageiros: 4, 6 e 8 anos, depois anual. Ligeiros de mercadorias: 2 anos, depois anual.
    /// </summary>
    public static DateOnly? ProximaInspecao(Veiculo v, DateOnly hoje)
    {
        for (var anos = 1; anos <= 100; anos++)
        {
            var obrigatoria = v.Categoria switch
            {
                CategoriaVeiculo.LigeiroPassageiros => anos is 4 or 6 or >= 8,
                _ => anos >= 2,
            };
            var data = v.DataPrimeiraMatricula.AddYears(anos);
            if (obrigatoria && data >= hoje)
                return data;
        }
        return null;
    }

    private static DateOnly ProximoAniversario(DateOnly data, DateOnly hoje)
    {
        var candidato = data.AddYears(Math.Max(0, hoje.Year - data.Year));
        return candidato >= hoje ? candidato : candidato.AddYears(1);
    }
}
