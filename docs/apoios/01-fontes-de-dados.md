# Radar de apoios: fontes de dados

Pesquisa feita a 28/09/2026.

## Resumo

Não existe uma API oficial que diga "a que apoios tem direito esta pessoa". As regras estão espalhadas por legislação e portais, e na primeira versão **são compiladas à mão**. As fontes automáticas servem para **detetar alterações**, não para gerar as regras.

| Fonte | Para quê | Formato | Acesso automático? |
|---|---|---|---|
| Diário da República (diariodarepublica.pt) | Legislação nova ou alterada (portarias com valores, novos programas) | Web e RSS | Sim, via RSS |
| Portal das Finanças: Agenda Fiscal | Prazos fiscais do ano | Página web e PDF anual | Não; transcrição manual uma vez por ano |
| Segurança Social (seg-social.pt) | Abono de família, prestações, obrigações dos independentes | Web | Não |
| Portal da Habitação (IHRU) | Porta 65, Arrendamento Acessível, 1.º Direito, IHRU Arrenda | Web | Não |
| Fundo Ambiental | Apoios de eficiência energética (E-Lar e outros) | Web; abre e fecha por avisos | Não; monitorizar a página |
| gov.pt / ePortugal | Descrição de serviços públicos e links para os pedir | Web | Não |
| dados.gov.pt | Portal de dados abertos (cerca de 10 000 datasets) | API (CKAN/udata) | Sim, mas sem catálogo de apoios |
| Guias de bancos e da DECO | Resumos acessíveis, úteis para validar | Web | Não usar como fonte oficial |

## Detalhe por fonte

### Diário da República
- **RSS:** o DR permite subscrever os sumários diários da 1.ª e 2.ª série e criar feeds a partir de pesquisas. Página: <https://diariodarepublica.pt/dr/geral/notificacoes/rss>
- **API:** não encontrei nenhuma API oficial de dados abertos. Existem projetos da comunidade, por exemplo [DRE-RSS](https://github.com/damiaocode/DRE-RSS) e [dre.tretas.org](https://dre.tretas.org/dre/rss/help/).
- **Uso previsto (fase 2):** um job lê o sumário diário da 1.ª série, filtra por palavras-chave (IAS, arrendamento, IRS Jovem, Porta 65, abono…) e um LLM resume o que mudou. **Revisão humana antes de alterar qualquer regra.**
- **A confirmar:** URLs exatos dos feeds (a página não carregou sem browser) e condições de reutilização.

### Portal das Finanças: Agenda Fiscal
- Tem filtros por tipo de contribuinte, imposto e mês, e publica um PDF anual com as obrigações declarativas e de pagamento.
- Uso: transcrever uma vez por ano para `prazos-AAAA.json`. Ver [03-prazos.md](03-prazos.md).

### Valores de referência que mudam todos os anos
Muitas regras dependem de indexantes. Ficam num ficheiro próprio (`indexantes-AAAA.json`) para não estarem espalhados pelas regras:

| Indexante | 2026 | Fonte |
|---|---|---|
| IAS (Indexante dos Apoios Sociais) | 537,13 € | Portaria anual, DR |
| Limite do 6.º escalão de IRS | 43 090 € | Código do IRS / OE |
| Limite da isenção total de IMT Jovem | 330 539 € | Tabelas do IMT, OE |
| Limite da isenção parcial de IMT Jovem | 660 982 € | Tabelas do IMT, OE |

### dados.gov.pt
- API documentada em <https://github.com/amagovpt/docs.dados.gov.pt>. A leitura é pública; a escrita exige uma chave no cabeçalho `X-API-KEY`.
- Não tem um catálogo de apoios. Pode servir para dados auxiliares.
- Lista útil de APIs públicas portuguesas: <https://github.com/devpt-org/public-data-portugal>

## Riscos
- **Regras mudam a meio do ano.** Exemplo: em fevereiro de 2026 o Governo anunciou a intenção de revogar o apoio extraordinário à renda; o E-Lar 2.ª fase fechou a 24/03/2026. Cada apoio precisa de um campo `estado` (ativo, suspenso, encerrado, anunciado) e de uma data de última verificação.
- **Responsabilidade.** O resultado é sempre indicativo e aponta para a fonte oficial.

## Fontes
- [Subscrever RSS | DR](https://diariodarepublica.pt/dr/geral/notificacoes/rss)
- [dados.gov.pt: documentação](https://github.com/amagovpt/docs.dados.gov.pt)
- [Calendário Fiscal 2026 (CRN Contabilidade)](https://crncontabilidade.pt/blog/calendario-fiscal-2026/)
- [Portal da Habitação](https://www.portaldahabitacao.pt/)
- [Programa E-Lar: Fundo Ambiental](https://www.fundoambiental.pt/apoios-prr/c13-eficiencia-energetica-em-edificios/11c13-i012025-programa-e-lar-2-fase.aspx)
