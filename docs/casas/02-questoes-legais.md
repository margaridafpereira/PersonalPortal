# Radar de casas: questões legais

Isto não é aconselhamento jurídico. É o levantamento que justifica as decisões técnicas.

## Conclusão

**Não fazer scraping ao Idealista nem aos outros grandes portais.** Há três obstáculos independentes, e basta um deles para o impedir:

1. **Os termos de uso proíbem-no expressamente.**
2. **Existe um direito sobre bases de dados**, e o Tribunal de Justiça da UE já o aplicou a um caso muito parecido.
3. **O RGPD**, porque os anúncios de particulares têm nomes e telefones.

Para um projeto de portefólio, que recrutadores vão ver, um scraper que viola os termos de uso é um **sinal negativo**. Uma solução que respeita estas regras mostra maturidade.

## 1. Termos de uso

**Idealista.** As condições gerais proíbem:
- aceder, monitorizar ou copiar informação com robots, spiders, scrapers "ou outros processos automáticos ou manuais" sem autorização escrita;
- contornar os cabeçalhos de exclusão de robots ou outras medidas que limitem o acesso;
- reproduzir ou incorporar conteúdos noutros sites.

O Idealista usa captchas e deteção de bots. Fonte: [Condições gerais do idealista.pt](https://www.idealista.pt/apoioutilizador/artigos/aviso-legal-e-condicoes-gerais/?lang=en)

**Imovirtual.** Tem uma [Política de Uso Aceitável](https://ajuda.imovirtual.com/imovirtualhelp/s/article/poltica-de-uso-aceitvel-V7IMO) que limita o serviço aos fins para que foi criado. O texto completo não foi lido; **falta confirmar**.

**Casa Sapo, Supercasa, CustoJusto.** Termos por ler. Os `robots.txt` foram consultados, mas o resultado não foi conclusivo; **falta confirmar**.

## 2. Direito sui generis sobre bases de dados

- **Diretiva 96/9/CE**, transposta em Portugal pelo **Decreto-Lei 122/2000**. Protege o investimento de quem cria uma base de dados, como um portal de anúncios, contra a extração e reutilização de partes substanciais.
- **Caso C-762/19, CV-Online Latvia vs Melons (TJUE, 3/6/2021).** Um motor de pesquisa especializado copiava e indexava anúncios de emprego de outro site. O Tribunal decidiu que isso é "extração" e "reutilização", e que pode ser proibido **quando prejudica o investimento** do dono da base de dados, por exemplo ao desviar visitas e receitas. Um agregador de anúncios de casas está exatamente neste cenário.
  - [Bird & Bird: análise](https://www.twobirds.com/en/insights/2021/uk/cv-online-latvia-cjeu-complicates-the-enforcement-of-database-rights)
  - [IPCuria: C-762/19](https://ipcuria.eu/case?reference=C-762%2F19)

## 3. RGPD

- Os anúncios de particulares incluem nome, telefone e às vezes fotos da casa com pessoas. Guardá-los é tratamento de dados pessoais, que exige fundamento legal, minimização e prazos de conservação.
- **Mitigação:** não guardar contactos, guardar só dados do imóvel (preço, área, tipologia, localização aproximada, link) e apagar os imóveis que já não são seguidos.

## O que é seguro fazer

| Abordagem | Risco | Nota |
|---|---|---|
| Dados do INE, DGT, GEO API | Nenhum | Dados abertos; citar a fonte |
| API oficial do Idealista, se aprovada | Baixo | Cumprir os termos da API |
| Introdução manual pelo utilizador | Nenhum | O utilizador fica com os seus próprios apontamentos |
| Ler os alertas de email do próprio utilizador | Baixo, se o uso for privado | Não republicar; só para o próprio |
| Scraping dos portais | **Alto** | Não fazer |
| e-Leilões e portais da banca | A avaliar | Ler os termos primeiro; volumes pequenos |

## Consequência para o produto

Com o scraping fora de hipótese, a área de casas deixa de ser um "agregador de portais" e passa a ser **um caderno inteligente de procura de casa**:
- os imóveis que o utilizador segue, com histórico e notas;
- o contexto de mercado oficial (INE) por freguesia;
- o custo real da compra (IMT, IS, garantia, prestação), ligado ao perfil;
- riscos do local (inundação e incêndio, via GEO API).

Este produto não depende da boa vontade de terceiros e não tem risco legal.
