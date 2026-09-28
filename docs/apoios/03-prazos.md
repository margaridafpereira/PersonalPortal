# Radar de apoios: prazos

Calendário para particulares e trabalhadores independentes em 2026. A fonte oficial é a **Agenda Fiscal do Portal das Finanças**. Esta tabela serve de ponto de partida e deve ser confirmada linha a linha antes de entrar em `prazos-2026.json`.

## Particulares

| Obrigação | Prazo | Para quem (campo do perfil) |
|---|---|---|
| Validar faturas no e-Fatura | Até 25 de fevereiro *(a confirmar)* | Todos com IRS |
| Entrega da declaração de IRS | 1 de abril a 30 de junho | Todos com rendimentos |
| Pagamento do IMI | Maio; ou maio e novembro; ou maio, agosto e novembro, consoante o valor | Proprietários |
| Pagamento do IUC | No mês da matrícula do veículo | Donos de veículo |

**Regra das prestações do IMI** *(a confirmar)*:
- até 100 €: uma prestação em maio;
- de 100 € a 500 €: maio e novembro;
- acima de 500 €: maio, agosto e novembro.

## Trabalhadores independentes (categoria B)

| Obrigação | Prazo |
|---|---|
| Declaração trimestral à Segurança Social | Até 31 jan (out a dez), 30 abr (jan a mar), 31 jul (abr a jun), 31 out (jul a set) |
| Pagamento das contribuições | Mensal *(dia a confirmar)* |
| Declaração periódica do IVA, regime trimestral | Até ao dia 20 do 2.º mês seguinte ao trimestre, segundo a fonte consultada *(confirmar na Agenda Fiscal)* |

Notas:
- A declaração trimestral é obrigatória mesmo sem rendimentos. Sem ela aplica-se uma contribuição mínima de 20 €/mês e coimas de 50 € a 250 €.
- A declaração trimestral pode ser corrigida até 15 dias depois do fim do prazo.

## Prazos de apoios

| Apoio | Prazo |
|---|---|
| IMT Jovem e garantia pública | Escrituras até 31/12/2026 |
| Porta 65 Jovem | Candidaturas todo o ano |
| E-Lar | Encerrado; vigiar a reabertura |

## Como os prazos entram no portal
1. Todos os anos, em janeiro, transcrever a Agenda Fiscal para `prazos-AAAA.json`.
2. Cada prazo tem uma condição sobre o perfil (por exemplo `trabalho.categoria == "B"`), para cada pessoa ver só os seus.
3. Lembretes 14 dias e 3 dias antes, por email ou Telegram, e exportação `.ics` para o calendário pessoal.
4. Quando o prazo cai ao fim de semana ou num feriado, passa para o dia útil seguinte; o motor tem de tratar isto.

## Fontes
- [Calendário fiscal 2026 (Doutor Finanças)](https://www.doutorfinancas.pt/impostos/calendario-fiscal-de-2026-as-datas-que-nao-pode-mesmo-falhar/)
- [Agenda Fiscal da AT para 2026: obrigações declarativas](https://economiafinancas.com/2026/agenda-fiscal-da-at-para-2026-obrigacoes-declarativas/)
- [Declaração trimestral à Segurança Social em 2026](https://crncontabilidade.pt/blog/recibos-verdes-qual-e-o-prazo-da-declaracao-trimestral/)
- [gov.pt: declaração trimestral](https://www2.gov.pt/-/texto-declara%C3%A7%C3%A3o-trimestral-para-a-seguran%C3%A7a-social)
