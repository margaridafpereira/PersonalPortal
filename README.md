# Portal pessoal

Uma área pessoal personalizável: cada pessoa tem conta, perfil e as secções que escolher. Começa com duas secções e cresce com mais:

| Área | O que faz | Origem |
|---|---|---|
| **Radar de apoios e prazos** | Diz a que apoios públicos o utilizador provavelmente tem direito e avisa antes de cada prazo | Ideia 4 |
| **Radar de anúncios** | Casas e terrenos: pesquisas guardadas em vários portais, anúncios seguidos, mercado da zona | Ideia 3 |
| **Carro** | Inspeção, IUC, seguro e revisão; combustível mais barato no concelho (DGEG) | Nova |
| **IRS de investimentos** | Mais-valias (FIFO) e dividendos para o Anexo J, com câmbios do Banco de Portugal | Ideia 1 |

As duas áreas ligam-se através do perfil. Quem tem até 35 anos e procura casa vê no radar de casas se um imóvel tem isenção de IMT e garantia pública, informação que vem das regras do radar de apoios.

## Documentação

Estado: **fase 1 (fundações)**: backend com contas, perfil, preferências e painel modular; frontend React. Estudo das fontes de dados feito a 28/09/2026.

```bash
dotnet run --project src/Portal.Api --launch-profile http   # API
cd web && npm install && npm run dev                        # frontend
dotnet test                                                 # 170 testes
```

| Documento | Conteúdo |
|---|---|
| [docs/00-visao-geral.md](docs/00-visao-geral.md) | Conceito, arquitetura proposta, perfil partilhado |
| [docs/apoios/01-fontes-de-dados.md](docs/apoios/01-fontes-de-dados.md) | Onde está a informação oficial sobre apoios e prazos |
| [docs/apoios/02-catalogo-inicial.md](docs/apoios/02-catalogo-inicial.md) | Primeiros apoios e respetivas regras de elegibilidade |
| [docs/apoios/03-prazos.md](docs/apoios/03-prazos.md) | Calendário de prazos fiscais e da Segurança Social |
| [docs/casas/01-fontes-de-dados.md](docs/casas/01-fontes-de-dados.md) | Portais, INE, dados geográficos e alternativas (APIs testadas) |
| [docs/casas/02-questoes-legais.md](docs/casas/02-questoes-legais.md) | Termos de uso, direito das bases de dados e RGPD |
| [docs/casas/03-como-ver-anuncios.md](docs/casas/03-como-ver-anuncios.md) | Como ver anúncios do Idealista, Imovirtual, Casa Yes… sem scraping |
| [docs/carro/01-fontes-e-regras.md](docs/carro/01-fontes-e-regras.md) | API da DGEG, regras da inspeção e do IUC |
| [docs/investimentos/01-fontes-e-regras.md](docs/investimentos/01-fontes-e-regras.md) | Regras fiscais, Anexo J, câmbios do BdP, importação |
| [docs/investimentos/02-formatos-corretoras.md](docs/investimentos/02-formatos-corretoras.md) | Ficheiros de cada corretora, de onde vem cada formato e limitações |
| [docs/publicar-na-azure.md](docs/publicar-na-azure.md) | Publicar o portal na Azure (plano gratuito): configuração, GitHub, avisos diários |
| [docs/avisos.md](docs/avisos.md) | Avisos de prazos por email: como funcionam e como ligar um servidor de email |
| [docs/exemplos-corretoras/](docs/exemplos-corretoras/) | Ficheiros de exemplo de cada corretora, para experimentar a importação |
| [docs/assistente.md](docs/assistente.md) | Assistente de IA: ferramentas, fornecedor, nota sobre os dados |
| [docs/demonstracao.md](docs/demonstracao.md) | Conta de demonstração: entrar sem convite, só de leitura, reposta todos os dias |
| [docs/99-decisoes-e-proximos-passos.md](docs/99-decisoes-e-proximos-passos.md) | Decisões em aberto e plano |
| [docs/desenvolvimento.md](docs/desenvolvimento.md) | Estrutura do código, como correr, como acrescentar uma secção |
