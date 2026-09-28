# Decisões e próximos passos

Atualizado a 28/09/2026.

## Decisões tomadas

| # | Decisão | Motivo |
|---|---|---|
| D1 | Um portal com perfil partilhado e duas áreas | As áreas ligam-se: idade e rendimento → IMT Jovem e garantia pública nos imóveis |
| D2 | Não fazer scraping aos portais imobiliários | Termos de uso, direito das bases de dados (C-762/19) e RGPD; ver [casas/02-questoes-legais.md](casas/02-questoes-legais.md) |
| D3 | Regras dos apoios escritas à mão, em C# (`MotorApoios`), com testes | Não há API oficial de elegibilidade; em código são verificadas pelo compilador e pelos testes. Uma DSL em JSON só se justifica se alguém sem programar tiver de as manter |
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

**Fase 1: fundações** 🟡 em curso
- [x] Repositório e solução .NET com módulos
- [x] Autenticação, perfil e preferências
- [x] Contrato de módulo e painel inicial com cartões
- [x] Ficheiro de indexantes 2026
- [x] Frontend: entrar/registar, painel, perfil, preferências
- [x] Frontend a correr neste computador (Vite 5 com binários da cache do npm)
- [ ] Repositório no GitHub pessoal, CI (GitHub Actions), Dockerfile
- [ ] PostgreSQL alojado (Neon ou Supabase) e migrações EF

**Fase 2: radar de apoios, V1** ✅
- [x] Motor de regras com os 7 apoios do catálogo, condição a condição, e testes por perfis-tipo
- [x] Página de apoios agrupada por estado (provável, falta informação, não elegível, encerrado)
- [x] Prazos pessoais nos próximos 12 meses (IRS, e-Fatura, IMI, IUC, Segurança Social, fim do IMT Jovem) e exportação `.ics`
- [ ] Feriados nacionais no cálculo do dia útil

**Fase 3: radar de anúncios, V1** 🟡
- [x] Pesquisas guardadas com links para 5 portais (casas e terrenos, comprar e arrendar)
- [x] Imóveis seguidos: histórico de preços, variação, €/m² e comparação com a mediana do INE **por concelho** (em tempo real, com cache de 12 h)
- [x] Etiquetas IMT Jovem e garantia pública, a partir do perfil
- [ ] Validar no browser os URLs do Idealista, Casa Yes, Casa Sapo e Supercasa
- [ ] Mediana por freguesia e mapa (CAOP)
- [ ] Custo total da compra: valor do IMT e do Imposto do Selo, prestação do crédito

**Fase 4: evolução**
- Alertas de email como fonte de anúncios
- Monitorização do DR com LLM e revisão humana
- Lembretes por Telegram
