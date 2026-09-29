# Ficheiros de exemplo das corretoras

Para experimentar a importação em **Investimentos → Operações → Escolher ficheiro**. Cada ficheiro tem o formato real da corretora, com poucas linhas.

O Anexo J de um ano só leva as **vendas** e os **dividendos** desse ano; as compras servem para calcular o custo. Os ficheiros das corretoras têm poucas linhas e anos diferentes, por isso o relatório pode ficar a zero no ano que estiver escolhido: usa o ficheiro de demonstração para ver um Anexo J preenchido.

Os dados são de exemplo, não de ninguém (o de demonstração foi feito para o portal): vêm dos ficheiros públicos do projeto [Export-To-Ghostfolio](https://github.com/dickwolff/Export-To-Ghostfolio) (pasta `samples/`), os mesmos usados nos testes do portal (`tests/Portal.Tests/CorretorasTests.cs`).

| Ficheiro | Corretora | O que mostra |
|---|---|---|
| `demonstracao-anexo-j.csv` | Modelo do portal | **Para ver o Anexo J com números: escolhe 2025.** Duas compras de Apple e uma venda de 12 (o FIFO divide-a pelas duas compras, uma com menos de 365 dias), um ETF vendido com lucro (código G20) e dividendos dos EUA com 30% retidos (só 15% contam) |
| `trading212.csv` | Trading 212 | Compras, uma venda, dividendos (um deles em pence, GBX); o depósito é ignorado |
| `degiro-extrato-de-conta.csv` | Degiro | Descrições em várias línguas, custos ligados à ordem, dividendo com retenção em linha separada |
| `revolut.csv` | Revolut | Só tickers (sem ISIN), valores com símbolo de moeda, aviso de stock split |
| `xtb-operacoes-de-caixa.csv` | XTB | Separador `;`, preço tirado do valor que saiu da conta, retenção junta ao dividendo |
| `etoro-atividade-da-conta.csv` | eToro | Posições abertas e fechadas, dividendo, CFD ignorado |
| `ibkr-negocios.csv` | Interactive Brokers | Comissões em CHF e USD; a conversão de moeda (sem ISIN) é ignorada |
| `ibkr-dividendos.csv` | Interactive Brokers | Dividendo com retenção dos EUA; "Return of Capital" ignorado |

**Atenção:** as operações importadas ficam na tua conta e entram no relatório do IRS. Depois de experimentar, apaga-as em Operações → "Apagar todas" (ou usa uma conta de teste).
