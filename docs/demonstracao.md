# Conta de demonstração

Quem chega ao portal sem convite (por exemplo, pelo portefólio) pode carregar em **Experimentar com dados de exemplo** no ecrã de entrada. Entra numa conta partilhada, com dados fictícios em todas as secções, sem se registar.

## Como funciona

- `POST /api/demo/entrar` cria a conta `demo@portal-pessoal.demo` na primeira vez, repõe os dados se ainda não foram repostos hoje e abre a sessão (`Demo.cs`).
- **Só de leitura:** qualquer pedido que não seja GET recebe 403, menos sair e o assistente (`/api/assistente/aceitar` e `/api/assistente/mensagens`), que não alteram dados de ninguém. Isto inclui mudar o email ou a palavra-passe.
- **Sem palavra-passe:** a conta é criada sem palavra-passe, por isso o login normal nunca entra nela.
- **Reposta todos os dias:** as datas (prazos, histórico de preços, revisão do carro) são calculadas a partir do dia da reposição, para acompanharem o calendário. As vendas e os dividendos ficam no ano que o cartão de IRS mostra.
- **Sem emails:** a conta tem os avisos desligados e o serviço de avisos ignora-a.
- O frontend mostra uma faixa no topo enquanto a sessão é a de demonstração (`GET /api/demo`).

## Dados de exemplo

| Secção | O que tem |
|---|---|
| Perfil | 29 anos, Porto, trabalho dependente, arrenda e procura casa até 280 000 € |
| Anúncios | 2 pesquisas guardadas e 3 imóveis seguidos, um deles com descida de preço |
| Carro | 1 carro a gasolina com seguro, revisão, inspeção e 4 abastecimentos |
| Investimentos | Compras, 2 vendas e 3 dividendos em 3 corretoras (Apple, Microsoft, VWCE) |

## Limites

- A conta é partilhada: o limite do assistente (`Assistente:PedidosPorHora`) vale para todos os visitantes juntos, o que protege a quota gratuita do fornecedor.
- Se um visitante aceitar a nota do assistente, os seguintes não a voltam a ver até à reposição do dia seguinte. A nota continua acessível no painel.

## Ligar e desligar

`Demo:Ativa` (`true` em `appsettings.json`). Na Azure, para desligar: `Demo__Ativa=false` nas definições da aplicação. Com a demonstração desligada, o botão desaparece e `POST /api/demo/entrar` responde 404.
