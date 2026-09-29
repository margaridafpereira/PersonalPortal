# Publicar o portal na Azure (plano gratuito)

O portal corre numa **Web App** da Azure no plano **Free F1**: uma só aplicação que serve a API e o frontend, com a base de dados SQLite numa pasta permanente. O código vem do repositório privado no GitHub e cada envio (`git push`) publica uma versão nova.

## Limitações do plano gratuito

- **Adormece** quando ninguém o usa: o primeiro acesso do dia demora 10 a 30 segundos.
- Por isso os **avisos das 8h** não correm sozinhos: uma tarefa do GitHub (`.github/workflows/avisos-diarios.yml`) acorda o portal todos os dias e pede-lhe para os enviar.
- 60 minutos de processador por dia e 1 GB de espaço: chega para uso pessoal.
- Endereço `*.azurewebsites.net` com HTTPS; domínio próprio só nos planos pagos.

## 1. Criar a Web App (feito uma vez)

portal.azure.com → **Criar um recurso** → **Aplicação Web**:

| Campo | Valor |
|---|---|
| Grupo de recursos | novo, ex.: `personal-portal_group` |
| Nome | ex.: `personal-portal` |
| Publicar | Código |
| Pilha de runtime | .NET 9 (passar para .NET 10 antes de 10/11/2026, quando o .NET 9 deixa de ter suporte) |
| Sistema operativo | Linux |
| Região | uma europeia que aceite subscrições novas: North Europe, Sweden Central, France Central, Spain Central… (West Europe pode recusar: "not accepting new customers") |
| Plano de preços | **Free F1** |
| Database | desmarcado (o portal usa SQLite) |
| Implementação contínua | desligada (liga-se no passo 3) |
| Application Insights / Defender | desligados (são pagos) |

## 2. Configuração da Web App

Web App → **Definições → Variáveis de ambiente** → **Definições da aplicação**, acrescentar (no Linux, `__` separa as secções):

| Nome | Valor | Para quê |
|---|---|---|
| `ConnectionStrings__Portal` | `Data Source=/home/data/portal.db` | Base de dados na pasta permanente (`/home` sobrevive a reinícios e publicações) |
| `DataProtection__Pasta` | `/home/data/chaves` | Chaves da sessão: sem isto, cada reinício obrigava a entrar outra vez |
| `Registo__EmailsPermitidos__0` | o teu email | **Só estes emails podem criar conta.** Para dar acesso a outra pessoa: `Registo__EmailsPermitidos__1`, `__2`… Sem nenhum, ninguém se regista |
| `Assistente__Chave` | a chave da Gemini | Assistente de IA |
| `Email__UrlPortal` | `https://<o-teu-endereço>.azurewebsites.net` | Link nos emails |
| `Email__ChaveExecucao` | uma frase secreta longa (20+ caracteres, ex.: gerada num gestor de palavras-passe) | Protege o endereço que dispara os avisos |
| `TZ` | `Europe/Lisbon` | "Hoje" e as 8h em hora portuguesa |
| `Rede__ProxyComCredenciaisWindows` | `false` | Só faz sentido na rede da empresa |

Para enviar emails de verdade (em vez de os gravar numa pasta), acrescentar também os de `docs/avisos.md` (`Email__Modo=Smtp`, `Email__Servidor`, `Email__Utilizador`, `Email__PalavraPasse`, `Email__Remetente`…).

Carregar em **Aplicar**: a Web App reinicia.

## 3. Ligar ao GitHub (publicação automática)

O ficheiro `.github/workflows/publicar-azure.yml` compila o frontend, corre os testes e publica a API com o frontend dentro, a cada envio para `main`. Entra na Azure com o **perfil de publicação**:

1. Web App → **Definições → Configuração → Definições gerais** → **SCM Basic Auth Publishing Credentials: On** → Guardar.
2. Web App → **Descrição geral** → **Transferir perfil de publicação** (ficheiro `.PublishSettings`).
3. GitHub → repositório → **Settings → Secrets and variables → Actions → New repository secret**: nome `AZURE_WEBAPP_PUBLISH_PROFILE`, valor = o conteúdo todo do ficheiro.
4. GitHub → **Actions → Publicar na Azure → Run workflow** (ou qualquer `git push`).

O perfil de publicação é uma palavra-passe: não o ponhas no código. Se o reiniciares na Azure ("Reset publish profile"), atualiza o segredo.

Nota: o Centro de Implementação da Azure com "Identidade atribuída pelo utilizador" falhou em 29/09/2026 com "We couldn't verify how GitHub Actions issues OIDC tokens for this repository"; por isso o portal usa o perfil de publicação. Se o nome da Web App for outro que não `personal-portal`, muda `NOME_WEB_APP` no ficheiro.

## 4. Avisos diários

No GitHub: repositório → **Settings → Secrets and variables → Actions**:
- **Variables → New repository variable:** `PORTAL_URL` = `https://<o-teu-endereço>.azurewebsites.net` (sem `/` no fim).
- **Secrets → New repository secret:** `AVISOS_CHAVE` = a mesma frase de `Email__ChaveExecucao`.

Para testar: **Actions → Avisos diários → Run workflow**.

## 5. Primeiro acesso

Abrir o endereço da Web App, **Criar conta** com o email que está em `Registo__EmailsPermitidos__0` e preencher o perfil. A base de dados publicada começa vazia: os dados do portátil não vão para lá.

## Cópias de segurança

A base de dados é o ficheiro `/home/data/portal.db`. Para a descarregar: Web App → **Ferramentas de desenvolvimento → Ferramentas avançadas (Kudu)** → **Debug console** → pasta `/home/data`.
