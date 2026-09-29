# Assistente de IA

Um assistente geral, disponível em todas as páginas (botão "Assistente", em baixo à direita). Responde sobre qualquer secção com os dados da própria pessoa: consulta o portal com ferramentas só de leitura e diz sempre o que consultou.

## Como funciona

1. A pessoa escreve uma pergunta. O frontend envia a conversa (até 20 mensagens) e a secção onde está.
2. O servidor (`ServicoAssistente`) envia ao modelo as instruções, a conversa e a lista de ferramentas.
3. Se o modelo pedir uma ferramenta (ex.: `relatorio_irs_investimentos`), o servidor corre-a com os dados de quem pergunta e devolve o resultado ao modelo. São no máximo 6 voltas.
4. A resposta volta com a lista do que foi consultado ("Consultou: Relatório de IRS").

O assistente **não altera nada**: as ferramentas só leem. Os resultados das ferramentas são tratados como dados, e as instruções do modelo dizem para ignorar qualquer instrução que venha dentro deles.

## Ferramentas

Cada secção declara as suas em `IModulo.FerramentasAssistente` (`Portal.Core`). Uma secção nova traz as suas e o assistente passa a responder sobre ela, sem mudar o módulo do assistente.

| Secção | Ferramentas |
|---|---|
| Geral (módulo Assistente) | `perfil`, `seccoes_do_portal`, `resumo_do_painel` |
| Apoios | `apoios_elegiveis`, `prazos_fiscais` |
| Anúncios | `imoveis_seguidos`, `mediana_precos_casas` |
| Carro | `veiculos_e_prazos`, `preco_combustivel` |
| Investimentos | `relatorio_irs_investimentos`, `carteira_investimentos` |

## Fornecedor

Serve qualquer fornecedor compatível com a API de chat da OpenAI (`POST {Url}/chat/completions`, com `tools`). A configuração fica em `appsettings.json`, secção `Assistente`; a chave vai para os user-secrets, nunca para o código:

```bash
dotnet user-secrets set "Assistente:Chave" "A_TUA_CHAVE" --project src/Portal.Api
```

| Fornecedor | `Url` | `Modelo` (exemplo) | Chave |
|---|---|---|---|
| Google Gemini (por omissão) | `https://generativelanguage.googleapis.com/v1beta/openai/` | `gemini-3.5-flash-lite` | grátis em https://aistudio.google.com/apikey |
| Groq | `https://api.groq.com/openai/v1/` | `openai/gpt-oss-120b` | grátis em https://console.groq.com/keys |
| OpenRouter | `https://openrouter.ai/api/v1/` | um modelo `:free` | https://openrouter.ai/keys |
| Mistral | `https://api.mistral.ai/v1/` | `mistral-small-latest` | https://console.mistral.ai |
| Ollama (local, sem chave) | `http://localhost:11434/v1/` | `qwen3:8b` | — |

Os nomes dos modelos mudam: se o fornecedor responder "modelo não encontrado", confirma o nome na documentação dele. A Google retira os modelos antigos às contas novas (em 29/09/2026, o `gemini-2.5-flash` já não estava disponível para uma chave nova); a lista do que a tua chave pode usar está em `GET {Url}models`.

Em 29/09/2026 os "flash" normais (3.5, 3.7, 3.8, "latest") responderam 503 durante longos períodos, enquanto os "lite" respondiam em 1 a 4 segundos e usavam bem as ferramentas; por isso o portal começa pelo `gemini-3.5-flash-lite` e tenta os outros a seguir. No plano gratuito, os modelos mais recentes ficam muitas vezes sobrecarregados (HTTP 503: "high demand"). `ModelosAlternativos` lista os modelos a tentar a seguir; dentro de uma pergunta, o assistente fica com o modelo que respondeu primeiro.

Os modelos Gemini 3 devolvem uma `thought_signature` em `extra_content` de cada pedido de ferramenta e exigem-na de volta na mensagem seguinte; o cliente (`ModeloCompativelOpenAI`) guarda-a e reenvia-a.

## Nota sobre os dados

Antes da primeira pergunta, a pessoa lê e aceita a nota (fica registado em `PreferenciasUtilizador.AssistenteAceiteEm`). A nota está sempre acessível no painel, em "Para onde vão os meus dados?". Diz que:

- as perguntas e os dados consultados (perfil, apoios, veículos, investimentos, imóveis) são enviados para o fornecedor;
- **no plano gratuito, o fornecedor pode guardar os dados, usá-los para melhorar os modelos, e revisores humanos podem lê-los** (`PlanoGratuito: true`);
- o assistente só consulta e pode enganar-se.

Com um plano pago ou um modelo local, põe `PlanoGratuito: false` e a nota deixa de ter essa frase.

## Limites

- 30 perguntas por pessoa e por hora (`PedidosPorHora`), para proteger a quota do plano gratuito.
- Cada resultado de ferramenta é cortado aos 30 000 caracteres.
- A conversa fica só no browser (sessionStorage) e desaparece ao fechar o separador; o servidor não a guarda.

## Rede de empresa

Um proxy que pede autenticação (erro 407) bloqueia os serviços externos. Com `Rede:ProxyComCredenciaisWindows: true` (por omissão), a API usa as credenciais do Windows no proxy do sistema; sem proxy, isto não faz nada.
