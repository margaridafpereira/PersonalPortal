namespace Portal.Modules.Carro;

/// <param name="ConsumoReal">L/100 km entre depósitos cheios (ou kWh/100 km num elétrico); null sem dois depósitos cheios.</param>
/// <param name="CustoPorKm">€/km no mesmo intervalo.</param>
/// <param name="GastoMensal">Média por mês nos últimos 12 meses com abastecimentos.</param>
public sealed record EstatisticasConsumo(
    int Abastecimentos, decimal? ConsumoReal, decimal? CustoPorKm, decimal? GastoMensal, decimal? PrecoMedioLitro, int? QuilometrosMedidos);

/// <summary>
/// Consumo real pelo método de depósito cheio a depósito cheio: os litros postos depois de um depósito cheio
/// (até ao cheio seguinte, inclusive) foram gastos nos quilómetros entre os dois.
/// </summary>
public static class Consumo
{
    public static EstatisticasConsumo Calcular(IEnumerable<Abastecimento> abastecimentos, DateOnly hoje)
    {
        var lista = abastecimentos.OrderBy(a => a.Quilometros).ThenBy(a => a.Data).ToList();
        if (lista.Count == 0)
            return new(0, null, null, null, null, null);

        decimal litros = 0, valor = 0;
        var quilometros = 0;
        int? kmCheioAnterior = null;
        decimal litrosDesdeCheio = 0, valorDesdeCheio = 0;
        foreach (var a in lista)
        {
            if (kmCheioAnterior is not null)
            {
                litrosDesdeCheio += a.Litros;
                valorDesdeCheio += a.ValorTotal;
            }
            if (!a.DepositoCheio)
                continue;
            if (kmCheioAnterior is { } km && a.Quilometros > km)
            {
                litros += litrosDesdeCheio;
                valor += valorDesdeCheio;
                quilometros += a.Quilometros - km;
            }
            kmCheioAnterior = a.Quilometros;
            litrosDesdeCheio = valorDesdeCheio = 0;
        }

        var desde = hoje.AddMonths(-12);
        var recentes = lista.Where(a => a.Data > desde).ToList();
        decimal? gastoMensal = null;
        if (recentes.Count > 0)
        {
            // Meses desde o primeiro abastecimento do período (pelo menos 1), para não dividir um mês de dados por 12.
            var meses = Math.Max(1, (hoje.Year - recentes.Min(a => a.Data).Year) * 12 + hoje.Month - recentes.Min(a => a.Data).Month + 1);
            gastoMensal = Math.Round(recentes.Sum(a => a.ValorTotal) / Math.Min(12, meses), 2);
        }

        var totalLitros = lista.Sum(a => a.Litros);
        return new(
            lista.Count,
            quilometros > 0 ? Math.Round(litros / quilometros * 100, 1) : null,
            quilometros > 0 ? Math.Round(valor / quilometros, 3) : null,
            gastoMensal,
            totalLitros > 0 ? Math.Round(lista.Sum(a => a.ValorTotal) / totalLitros, 3) : null,
            quilometros > 0 ? quilometros : null);
    }
}
