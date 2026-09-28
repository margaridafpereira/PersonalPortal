# Visão geral

## Conceito

Uma **área pessoal personalizável**. Cada pessoa entra com a sua conta, preenche o perfil e escolhe as secções que quer ver. A plataforma guarda as escolhas e usa-as em todas as secções.

- **Perfil:** idade, localização, agregado, rendimento, situação da habitação…
- **Preferências:** secções ativas e a sua ordem, zonas de interesse, alertas (email ou Telegram, antecedência)
- **Secções (módulos):**
  - V1: **Radar de apoios e prazos**
  - V1: **Radar de anúncios** (casas, apartamentos, terrenos)
  - Mais tarde, outras secções que se acrescentam sem mexer nas existentes (ideias em baixo)

## Plataforma modular

Cada secção é um módulo que se liga à plataforma através de um contrato comum:

| O módulo declara | Exemplo (radar de apoios) |
|---|---|
| Os campos do perfil que usa | idade, rendimento, agregado |
| As suas preferências | "avisar 14 dias antes" |
| Um cartão para o painel inicial | "3 apoios prováveis, 1 prazo esta semana" |
| As notificações que gera | "IRS: faltam 3 dias" |

O painel inicial mostra os cartões das secções ativas, pela ordem que a pessoa escolheu. Uma secção nova só tem de implementar este contrato.

**Ideias para secções futuras:** eletricidade (ideia 5), IRS de investimentos (ideia 1), A minha inflação (ideia 2), carro (IUC, inspeção, seguro), documentos e validades (cartão de cidadão, passaporte, carta de condução).

## As duas primeiras secções

```
                 ┌──────────────────────────┐
                 │   Portal (login, perfil)  │
                 └────────────┬─────────────┘
              ┌───────────────┴───────────────┐
   ┌──────────▼──────────┐         ┌──────────▼──────────┐
   │  Radar de apoios    │         │  Radar de casas      │
   │  - catálogo         │◄────────┤  - imóveis seguidos  │
   │  - motor de regras  │  regras │  - histórico preços  │
   │  - prazos/alertas   │  IMT,   │  - mercado (INE)     │
   └─────────────────────┘ garantia└──────────────────────┘
```

## Perfil partilhado

Dados que o utilizador preenche uma vez e que as duas áreas usam:

| Campo | Usado em |
|---|---|
| Data de nascimento | IRS Jovem, Porta 65, IMT Jovem, garantia pública |
| Residência fiscal em Portugal | IRS Jovem, IMT Jovem |
| É dependente para efeitos fiscais? | IRS Jovem |
| Tipo de rendimento (categoria A ou B) | IRS Jovem, prazos da Segurança Social e do IVA |
| Rendimento anual do agregado | Porta 65, apoio à renda, abono de família |
| Composição do agregado (adultos, filhos e idades) | Abono de família, Porta 65 (casal) |
| Situação da habitação (arrenda, proprietário, procura) | Apoios à renda, radar de casas |
| Renda mensal e data do contrato | Apoio extraordinário à renda |
| Procura casa para comprar? Orçamento, zonas | Radar de casas, IMT Jovem, garantia pública |
| Tem veículo? Mês da matrícula | Prazo do IUC |
| É proprietário de imóvel? Valor do IMI | Prazos do IMI |

Os dados são sensíveis (rendimentos, agregado), o que exige cifra em repouso, nenhuma partilha e a possibilidade de apagar tudo. Isto fica documentado no README e nos termos de utilização.

## Arquitetura proposta (a confirmar)

| Camada | Escolha | Porquê |
|---|---|---|
| Backend | .NET (C#), monólito modular: `Portal.Perfil`, `Portal.Apoios`, `Portal.Casas` | Stack já conhecida; módulos separados sem a complexidade de microserviços |
| Motor de regras | Regras em JSON avaliadas por código próprio | Explicável ("tens direito porque…") e fácil de testar |
| Base de dados | PostgreSQL + PostGIS | Uma só base; PostGIS para freguesias e mapas |
| Frontend | React + TypeScript | Uma aplicação com duas secções |
| Jobs agendados | Hangfire ou Quartz.NET | Lembretes de prazos, atualização dos dados do INE |
| Notificações | Email e bot de Telegram | Alertas de prazos e de imóveis |
| Infraestrutura | Docker, GitHub Actions, fly.io ou Railway | Deploy simples e barato |

## Princípio que orienta as fontes de dados

**Só dados oficiais, abertos ou fornecidos pelo próprio utilizador.** O radar de casas não faz scraping aos grandes portais. As razões estão em [casas/02-questoes-legais.md](casas/02-questoes-legais.md).
