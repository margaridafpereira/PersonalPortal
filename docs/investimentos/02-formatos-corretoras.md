# Formatos dos ficheiros das corretoras

Estado a 29/09/2026. Os leitores próprios (`Importacao.Corretoras.cs`) foram feitos a partir de **ficheiros de exemplo públicos** e de conversores open source, não de ficheiros de utilizadores do portal. Cada importação avisa a pessoa para comparar as primeiras operações com o extrato. Quando houver um ficheiro real de uma corretora, junta-se aos testes (`tests/Portal.Tests/CorretorasTests.cs`) e esta tabela passa a "verificado".

Fonte principal: [Export-To-Ghostfolio](https://github.com/dickwolff/Export-To-Ghostfolio), com os ficheiros de exemplo em `samples/` e os conversores em `src/converters/`.

| Corretora | Ficheiro | Formato | Reconhecido por | Estado |
|---|---|---|---|---|
| Trading 212 | Histórico → Exportar | CSV | colunas `Action`, `Time`, `No. of shares`, `Price / share`… | Confirmado com o exemplo público |
| Degiro | Caixa de entrada → Extrato de conta (Account.csv) | CSV | estrutura: `ISIN` na 5.ª coluna, colunas 9 e 11 sem nome (os nomes mudam com a língua) | Exemplo público (NL, FR, PT) |
| Revolut | Investir → Mais → Documentos → Extrato de conta | CSV ou Excel | `Ticker`, `Type`, `Quantity`, `Price per share`, `Total Amount` | Exemplo público |
| XTB | Histórico da conta → Operações de caixa → Exportar | CSV (`;`) ou Excel | `ID`, `Type`, `Time`, `Symbol`, `Comment`, `Amount` | Exemplo público |
| eToro | Histórico → Extrato de conta → Excel, folha "Account Activity" | Excel | `Date`, `Type`, `Details`, `Amount`, `Units`, `Position ID` | Exemplo público |
| Interactive Brokers | Flex Query "Trades" | CSV | `Buy/Sell`, `ISIN`, `Quantity`, `TradePrice`, `CurrencyPrimary` | Exemplo público |
| Interactive Brokers | Flex Query "Cash Transactions" (dividendos) | CSV | `Type`, `ISIN`, `Description`, `Amount`, `CurrencyPrimary` | Exemplo público |
| Trade Republic | Perfil → Extratos → Exportação de transações (desde abril de 2026) | CSV | — (leitura por colunas) | Sem exemplo público do formato novo |

## Decisões e limitações

- **Degiro.** As compras e vendas vêm na descrição ("Koop 1 @ 33,9 USD", "Compra…", "Achat 6 NOME@79,96 EUR"). A quantidade e o preço saem daí. Os custos da ordem ligam-se pelo `Order Id` e ficam na primeira execução. Dividendos e retenção estão em linhas separadas e juntam-se por título, dia e moeda.
- **XTB.** O ficheiro não diz a moeda da conta; o portal assume EUR e avisa. O preço é `Amount / quantidade`, ou seja, o valor que saiu da conta, já com o câmbio aplicado pela XTB. Da descrição "OPEN BUY 34/42.5658 @ …" usa-se 34, a parte executada.
- **eToro.** Os valores estão na moeda da conta (USD). CFD e cripto ficam de fora: têm regras fiscais próprias.
- **Revolut, XTB e eToro** só dão o ticker. Sem ISIN, o país da fonte fica "??" e a pessoa indica o ISIN no separador Operações (`PUT /api/investimentos/ativos/{ativo}/isin`).
- **Dividendos da Revolut e da eToro** podem vir já sem a retenção; o portal avisa para confirmar o bruto.
- **Desdobramentos de ações** (stock split) ainda não são tratados; a Revolut marca-os e o portal avisa.
- **IBKR.** As comissões ficam na moeda em que foram cobradas (`MoedaComissoes`) e são convertidas no cálculo. As linhas sem ISIN (conversões de moeda) são ignoradas. "Return of Capital" não é dividendo e fica de fora, com aviso.
- **Excel (.xlsx).** Lido sem bibliotecas: um `.xlsx` é um ZIP com XML (`Xlsx.cs`). Escolhe a folha com formato conhecido (ou a maior), salta as linhas de título e converte as datas. O formato antigo `.xls` não é suportado.
- **PDF.** Não suportado. Quase todas as corretoras dão CSV ou Excel; para as que só dão PDF existe o modelo CSV do portal.
