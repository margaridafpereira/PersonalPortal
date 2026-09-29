# IRS de investimentos: fontes e regras

Pesquisa e testes feitos a 28/09/2026. **O resultado é uma estimativa indicativa e não substitui um contabilista.**

## Regras fiscais aplicadas

| Regra | Como está implementada | Fonte |
|---|---|---|
| **FIFO** obrigatório: os títulos vendidos são os adquiridos há mais tempo | Por ISIN, sobre todo o histórico; só as vendas do ano entram no relatório | [CIRS, art. 43.º](https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/cirs_rep/Pages/irs43.aspx) |
| Saldo entre mais-valias e menos-valias do ano | Soma dos resultados das linhas | CIRS, art. 43.º |
| Taxa autónoma de **28%** sobre o saldo positivo, sem englobamento | `CalculoIrs.TaxaAutonoma` | [Doutor Finanças](https://www.doutorfinancas.pt/impostos/irs/imposto-mais-valias-como-declarar/) |
| Englobamento obrigatório para títulos detidos menos de 365 dias, se o rendimento coletável chegar ao último escalão | Só um aviso; não é calculado | [CGD: declarar investimentos](https://www.cgd.pt/Site/Saldo-Positivo/leis-e-impostos/Pages/declarar-investimentos-no-IRS.aspx) |
| Conversão para euros ao câmbio do dia de cada operação | Câmbio de referência do BdP; se não houver publicação nesse dia, usa-se o último dia anterior | Prática corrente; *confirmar* |
| Comissões como "despesas e encargos", separadas | Repartidas pela quantidade vendida (da compra e da venda) | Anexo J, quadro 9.2A |
| Coeficientes de desvalorização monetária | **Não se aplicam** a valores mobiliários (art. 50.º só abrange as alíneas a), c) e d) do art. 10.º) | CIRS, art. 50.º; *confirmar* |

## Onde declarar (Anexo J)

| Rendimento | Quadro | Código | Campos |
|---|---|---|---|
| Venda de ações | **9.2A** | **G01** | País da fonte, datas e valores de realização e aquisição, despesas |
| Venda de ETF e fundos | **9.2A** | **G20** | Idem |
| Dividendos | **8A** | **E11** | País da fonte, rendimento bruto, imposto pago no estrangeiro |

- **País da fonte:** as duas primeiras letras do ISIN (US, IE, …), convertidas no código numérico ISO 3166 (EUA = 840, Irlanda = 372). **Confirmar na tabela de países da declaração.**
- **ETF e fundos:** código **G20** ("resgate ou alienação de unidades de participação em fundos de investimento"), segundo a maioria dos guias ([Literacia Financeira](https://www.literaciafinanceira.pt/artigos/declarar-investimentos-irs), [Fórum do Investidor](https://forumdoinvestidor.pt/viewtopic.php?t=200)). Cada título tem um tipo (Ação / ETF / Fundo), sugerido pelo nome (iShares, Vanguard, UCITS, Acc…) e corrigível na página.
- **Regimes fiscais mais favoráveis** (Portaria n.º 150/2004): quando o ISIN é de um território da lista (KY, BM, JE, GG…), o relatório avisa que pode aplicar-se 35%. *Lista parcial; confirmar.*
- **Dividendos dos EUA:** a convenção prevê 15% de retenção; só esse valor conta como crédito. O cálculo usa o valor retido como crédito até ao limite do imposto português (28%) e mostra um aviso.

Fontes: [Literacia Financeira: declarar investimentos](https://www.literaciafinanceira.pt/artigos/declarar-investimentos-irs), [DECO: declarar ações](https://www.deco.proteste.pt/investe/investimentos/impostos/dossie/fiscalidade-acoes/como-declarar-acoes-irs), [OCC: guia prático de mais-valias](https://www.occ.pt/sites/default/files/public/2025-04/Guia_Pratico_MAIS-VALIASMd2.pdf).

## Câmbios: Banco de Portugal (BPstat) ✅ testada

A API do BCE (`data-api.ecb.europa.eu`) está bloqueada na rede da empresa. O Banco de Portugal publica os mesmos câmbios de referência do euro.

- Endpoint: `https://bpstat.bportugal.pt/data/v1/domains/29/datasets/23e0cdd56bddb4ad3016a9c3ad63a539/?lang=PT&series_ids={serie}&obs_since=AAAA-MM-DD`
- Resposta em JSON-stat: `value` alinhado com `dimension.reference_date.category.index`.
- Valores: 1 EUR = X unidades da moeda.

| Moeda | Série | Moeda | Série |
|---|---|---|---|
| USD | 12531971 | GBP | 12531970 |
| CHF | 12531968 | JPY | 12531951 |
| CAD | 12531936 | AUD | 12531935 |
| DKK | 12531942 | SEK | 12531967 |
| NOK | 12531960 | HKD | 12531945 |

A lista completa (20 moedas) está em `CambiosBancoDePortugal.Series`.

## Importação

| Corretora | Estado | Como exportar |
|---|---|---|
| **Trading 212** | ✅ | Menu → Histórico → Exportar CSV. Colunas usadas: `Action`, `Time`, `ISIN`, `Name`, `No. of shares`, `Price / share`, `Currency (Price / share)`, `Withholding tax`, `Currency (Withholding tax)`, `Currency conversion fee`, `ID` |
| **Qualquer outra (universal)** | ✅ | Qualquer CSV: o portal deteta o separador (`,` `;` tab), o formato dos números (`1.234,56` ou `1,234.56`), a data e as colunas pelo nome (PT, EN, DE, FR, ES). A pessoa confirma o mapeamento e o significado de cada tipo ("BUY", "Compra", "Kauf"…). Sem coluna de tipo, quantidade negativa = venda (Degiro). Sem coluna de id, cada linha tem um id calculado, para não duplicar |
| Modelo do portal | ✅ | Para corretoras que só dão PDF: `data,tipo,isin,nome,quantidade,preco,moeda,comissoes_eur,retencao,moeda_retencao` |

O importador universal foi testado com ficheiros ao estilo da Degiro e de corretoras com colunas em inglês; ainda não com exportações reais de cada corretora.

- A importação da Trading 212 usa o `ID` de cada operação para não duplicar quando o mesmo período é importado duas vezes.
- As linhas "Deposit", "Withdrawal", "Interest on cash" e semelhantes são ignoradas, com um aviso. Os juros sobre dinheiro também são rendimento tributável; *por fazer*.

## Em aberto

- [ ] Confirmar a tabela de códigos de país do Anexo J e a lista completa de regimes fiscais favoráveis
- [ ] Testar o importador universal com exportações reais (Degiro, IBKR, XTB, Trade Republic, Revolut)
- [ ] Dividendos em que a corretora só dá o valor total (não por título)
- [ ] Juros (Trading 212 "Interest on cash") e criptoativos (Anexo G; isenção a partir de 365 dias)
- [ ] Simulação do englobamento com os restantes rendimentos do perfil
