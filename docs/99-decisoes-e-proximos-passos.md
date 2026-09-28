# Decisões e próximos passos

Atualizado a 28/09/2026.

## Decisões tomadas

| # | Decisão | Motivo |
|---|---|---|
| D1 | Um portal com perfil partilhado e duas áreas | As áreas ligam-se: idade e rendimento → IMT Jovem e garantia pública nos imóveis |
| D2 | Não fazer scraping aos portais imobiliários | Termos de uso, direito das bases de dados (C-762/19) e RGPD; ver [casas/02-questoes-legais.md](casas/02-questoes-legais.md) |
| D3 | Regras dos apoios escritas à mão na versão 1 | Não há API oficial de elegibilidade |
| D4 | Mercado imobiliário a partir da API do INE | Testada: gratuita e com dados por freguesia |
| D5 | Plataforma modular: conta, perfil, preferências e secções ativáveis | Permite acrescentar secções sem mexer nas existentes |
| D6 | Anúncios em 4 níveis: links de pesquisa, anúncios guardados, alertas de email, API oficial | Ver [casas/03-como-ver-anuncios.md](casas/03-como-ver-anuncios.md) |
| D7 | Favoritos com histórico: descida, subida, desaparecimento. Fontes: emails dos portais e extensão do browser; verificação automática só onde os termos o permitam | Idem |
| D8 | Backend em .NET, não em Go | Ver a comparação abaixo |

### .NET vs Go (D8)

| Critério | .NET | Go |
|---|---|---|
| Tipo de aplicação (CRUD, contas, perfil, regras, jobs) | Tem tudo incluído: ASP.NET Core Identity, EF Core, Hangfire, validação | Mais peças para escolher e ligar à mão (auth, ORM, jobs) |
| Motor de regras e domínio complexo | Tipos ricos, records, pattern matching, LINQ | Mais verboso para lógica de domínio |
| Workers leves e muito concorrentes, binários pequenos | Bom | **Excelente** |
| Mercado de trabalho em Portugal (banca, seguros, consultoras) | **Muito procurado** | Nicho (startups, infraestrutura, cloud) |
| Curva de aprendizagem | Já conhecido | Linguagem nova (é aprendizagem, não produtividade) |

Conclusão: sem scraping deixa de haver a carga concorrente pesada em que o Go brilha, e este produto é sobretudo contas, perfil, regras e jobs, o terreno natural do .NET. O Go pode entrar mais tarde num componente isolado (por exemplo, o leitor de emails de alertas), se quiseres aprendê-lo.

## Decisões em aberto

| # | Pergunta | Recomendação |
|---|---|---|
| A1 | Stack: .NET + React, ou outra? | .NET + React + PostgreSQL/PostGIS |
| A2 | Portal só para ti ou aberto a outros? | Começar só para ti (menos RGPD e menos risco), preparado para vários utilizadores |
| A3 | Como entram os anúncios? | V1: introdução manual. V2: alertas de email. Em paralelo, pedir a API do Idealista |
| A4 | Por que área começar? | **Apoios**: tem menos dependências externas e as regras alimentam depois a área de casas |

## Por confirmar (pesquisa pendente)

- [ ] URLs exatos dos feeds RSS do Diário da República (1.ª série)
- [ ] Escalonamento exato da isenção do IRS Jovem por ano
- [ ] Rendas máximas admitidas do Porta 65 por concelho
- [ ] Garantia pública: limite de rendimento e percentagem garantida
- [ ] Valores mensais do abono de família por escalão
- [ ] Prazos exatos do IVA trimestral, do e-Fatura e das prestações do IMI na Agenda Fiscal 2026
- [ ] Validar no browser os padrões de URL de pesquisa de cada portal
- [ ] Perguntar ao Casa Yes (APEMIP) se tem API ou programa de parceiros
- [ ] Termos de uso do Imovirtual, Casa Sapo, Supercasa, e-Leilões e portais da banca
- [ ] Se o indicador de rendas trimestral do INE (`0012571`) continua a ser publicado
- [ ] Tabela de correspondência entre os códigos geográficos do INE (NUTS 2024) e da CAOP (DICOFRE)

## Plano

**Fase 0: estudo** ✅ (este documento)

**Fase 1: fundações (1 a 2 semanas)**
- Repositório, solução .NET com módulos, Docker, CI
- Autenticação, perfil e preferências
- Contrato de módulo e painel inicial com cartões
- Ficheiro de indexantes 2026

**Fase 2: radar de apoios, V1 (2 a 3 semanas)**
- Motor de regras com os 7 apoios do catálogo e testes por perfis-tipo
- Página "os teus apoios", com o motivo de cada um
- Prazos 2026 e exportação `.ics`

**Fase 3: radar de casas, V1 (2 a 3 semanas)**
- Pesquisas guardadas com links para cada portal (casas e terrenos)
- Import trimestral do INE e mapa das freguesias (CAOP)
- Imóveis seguidos, com introdução manual, histórico e comparação com a mediana da freguesia
- Custo real da compra, ligado ao perfil (IMT Jovem, garantia)

**Fase 4: evolução**
- Alertas de email como fonte de anúncios
- Monitorização do DR com LLM e revisão humana
- Lembretes por Telegram
