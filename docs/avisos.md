# Avisos por email

Uma vez por dia, a partir das 8h, o portal junta os prazos de todas as secções ativas de cada pessoa e envia **um só email** com os que chegaram a uma das antecedências escolhidas em Perfil → Preferências (por omissão 14 e 3 dias antes).

- Cada prazo é avisado uma vez por antecedência: fica registado na tabela `avisos_enviados`. Correr o serviço várias vezes no mesmo dia, ou reiniciar a API, não repete avisos.
- Se a API esteve parada e passou a antecedência de 14 dias, a pessoa recebe só a seguinte que ainda se aplica (ex.: a de 3 dias), não uma atrasada.
- Quem desligar "Por email" nas preferências não recebe nada. Contas que nunca abriram as preferências têm os avisos ligados por omissão.
- Em Preferências há o botão **Enviar email de teste**, que mostra os prazos que tens pela frente sem os marcar como avisados.

## De onde vêm os prazos

Cada secção dá os seus em `IModulo.ObterAvisosAsync` (`Portal.Core`). Uma secção nova só tem de implementar este método.

| Secção | Prazos |
|---|---|
| Apoios | Prazos fiscais e da Segurança Social que se aplicam ao perfil (IRS, IMI, declarações trimestrais…) |
| Carro | Inspeção, IUC, renovação do seguro, revisão, carta de condução |

Os prazos que calham ao fim de semana ou num feriado nacional passam para o dia útil seguinte (`Calendario.DiaUtil`).

## Configuração (`appsettings.json`, secção `Email`)

Por omissão, o modo é `Pasta`: os emails ficam gravados como ficheiros `.eml` em `src/Portal.Api/emails-enviados/`, que abrem no Outlook. Serve para testar sem servidor de email.

Para receber os emails de verdade, muda para `Smtp`. A palavra-passe vai para os user-secrets:

```json
"Email": {
  "Modo": "Smtp",
  "Servidor": "smtp.gmail.com",
  "Porta": 587,
  "Ssl": true,
  "Utilizador": "o.teu.email@gmail.com",
  "Remetente": "o.teu.email@gmail.com",
  "UrlPortal": "http://localhost:5173"
}
```

```bash
dotnet user-secrets set "Email:PalavraPasse" "A_PALAVRA_PASSE_DE_APLICACAO" --project src/Portal.Api
```

- **Gmail:** é preciso a verificação em dois passos e uma "palavra-passe de aplicação" (Conta Google → Segurança). A palavra-passe normal não funciona.
- **Outlook.com / Microsoft 365:** `smtp.office365.com`, porta 587. Em contas de empresa o SMTP pode estar desligado pelo administrador.
- **Brevo** e serviços parecidos: têm planos gratuitos com SMTP próprio; usa os dados que dão.

Numa rede de empresa, o servidor de email externo pode estar bloqueado pela firewall; o modo `Pasta` funciona sempre.

Para desligar o serviço automático (ex.: em testes): `"Email": { "AvisosAutomaticos": false }`.
