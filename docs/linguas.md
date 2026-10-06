# Português e inglês

O portal tem um seletor **PT | EN** no topo e no ecrã de entrada. A escolha fica no browser; sem escolha, segue a língua do browser (português para quem o tem em português, inglês para os outros).

## Como funciona

- **Frontend** (`web/src/i18n.ts`): os textos estão lado a lado no código, `t('Entrar', 'Sign in')`, sem ficheiros de chaves. Ao mudar de língua, a aplicação volta a montar as páginas, que pedem os dados outra vez.
- **Servidor** (`Portal.Core/Idioma.cs`): cada pedido leva a língua no cabeçalho `X-Idioma` (ou `?idioma=en` nos links, como os `.ics`). Os textos gerados no servidor (cartões do painel, apoios e condições, prazos, IUC, avisos do IRS, mensagens de erro, assistente) usam `T("pt", "en")`.
- O valor fica num `AsyncLocal`, não na cultura do sistema: sem cabeçalho, é sempre português, seja qual for a língua do Windows ou do Linux onde o servidor corre.

## O que fica igual nas duas línguas

- **Números, euros e datas numéricas** mantêm o formato português (`29 542 €`, `31/12/2026`): os valores são de Portugal. Os nomes dos meses seguem a língua.
- **Nomes oficiais** (IRS Jovem, Porta 65 Jovem, IMT Jovem, Anexo J, Portal das Finanças) mantêm-se, com a explicação na língua escolhida.
- **Emails de avisos**: sempre em português, também o de teste.
- **Dados introduzidos pela pessoa** e os dados de exemplo da demonstração (títulos de anúncios, notas).
- As descrições das ferramentas do assistente para o modelo de IA estão em português; o modelo responde na língua pedida.

## Acrescentar texto

Qualquer texto novo que a pessoa vê leva as duas línguas: `t(...)` no frontend, `T(...)` no servidor (com `using static Portal.Core.Idioma;`). Constantes ao nível do módulo que tenham texto têm de ser funções ou getters, para serem lidas na língua do momento.
