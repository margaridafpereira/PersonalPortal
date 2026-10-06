import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { api, type EstadoAssistente, type MensagemConversa } from '../api'
import { t } from '../i18n'
import { Icone } from './Icone'

type Mensagem = MensagemConversa & { consultas?: string[] }

const sugestoes = (): Record<string, string[]> => ({
  painel: [t('O que tenho de tratar nas próximas semanas?', 'What do I need to deal with in the coming weeks?'), t('A que apoios tenho direito?', 'Which benefits am I entitled to?'), t('Que dados me faltam no perfil e para quê?', 'What is missing from my profile, and why does it matter?')],
  apoios: [t('Porque é que tenho (ou não) direito ao IRS Jovem?', 'Why do I (or do I not) qualify for IRS Jovem?'), t('Que prazos fiscais tenho nos próximos meses?', 'What tax deadlines do I have in the coming months?')],
  anuncios: [t('Algum imóvel que sigo baixou de preço?', 'Has any listing I follow dropped in price?'), t('Os imóveis que sigo estão acima da mediana do concelho?', 'Are the listings I follow above the municipal median?')],
  carro: [t('Quando é a próxima inspeção e o IUC?', 'When are my next inspection and road tax due?'), t('Onde está o combustível mais barato no meu concelho?', 'Where is the cheapest fuel in my municipality?')],
  investimentos: [t('Quanto vou pagar de IRS pelos investimentos?', 'How much IRS will I pay on my investments?'), t('Explica-me o que vai em cada quadro do meu Anexo J.', 'Explain what goes in each table of my Annex J.'), t('Que títulos ainda não têm ISIN?', 'Which holdings still have no ISIN?')],
  perfil: [t('Que dados do perfil me faltam e o que ganho em preenchê-los?', 'What is missing from my profile, and what do I gain by filling it in?')],
})

const chaveConversa = 'assistente.conversa'

function lerConversa(): Mensagem[] {
  try {
    const guardada = sessionStorage.getItem(chaveConversa)
    return guardada ? (JSON.parse(guardada) as Mensagem[]) : []
  } catch {
    return []
  }
}

function guardarConversa(conversa: Mensagem[]) {
  try { sessionStorage.setItem(chaveConversa, JSON.stringify(conversa)) } catch { /* sem armazenamento: a conversa só não sobrevive a um refresh */ }
}

/** Negrito (**texto**) e listas (- item): o suficiente para as respostas, sem HTML vindo do modelo. */
function Texto({ texto }: { texto: string }) {
  const inline = (linha: string): ReactNode[] =>
    linha.split(/(\*\*[^*]+\*\*)/g).map((parte, i) =>
      parte.startsWith('**') && parte.endsWith('**') ? <strong key={i}>{parte.slice(2, -2)}</strong> : parte)

  const blocos: ReactNode[] = []
  let lista: ReactNode[] = []
  const fecharLista = () => {
    if (lista.length) blocos.push(<ul key={`l${blocos.length}`}>{lista}</ul>)
    lista = []
  }
  texto.split('\n').forEach((linha, i) => {
    const item = linha.match(/^\s*(?:[-*•]|\d+[.)])\s+(.*)$/)
    if (item) lista.push(<li key={i}>{inline(item[1])}</li>)
    else {
      fecharLista()
      if (linha.trim()) blocos.push(<p key={i}>{inline(linha.replace(/^#+\s*/, ''))}</p>)
    }
  })
  fecharLista()
  return <>{blocos}</>
}

/**
 * Assistente geral do portal, em todas as páginas. Responde com os dados da pessoa (consultados pelo servidor)
 * e mostra sempre o que consultou e para onde vão os dados.
 */
export function Assistente({ seccao }: { seccao: string }) {
  const [aberto, setAberto] = useState(false)
  const [estado, setEstado] = useState<EstadoAssistente | null>(null)
  const [conversa, setConversa] = useState<Mensagem[]>(lerConversa)
  const [pergunta, setPergunta] = useState('')
  const [aPensar, setAPensar] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const fim = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (aberto && !estado) api.assistente().then(setEstado, (e: Error) => setErro(e.message))
  }, [aberto, estado])

  useEffect(() => { guardarConversa(conversa) }, [conversa])
  useEffect(() => { fim.current?.scrollIntoView({ block: 'end' }) }, [conversa, aPensar])

  const enviar = async (texto: string) => {
    const limpa = texto.trim()
    if (!limpa || aPensar) return
    const nova: Mensagem[] = [...conversa, { papel: 'utilizador', texto: limpa }]
    setConversa(nova)
    setPergunta('')
    setErro(null)
    setAPensar(true)
    try {
      // Só as últimas mensagens: o servidor aceita até 20 e o resto raramente importa para a pergunta.
      const r = await api.perguntar(nova.slice(-20).map(({ papel, texto }) => ({ papel, texto })), seccao)
      setConversa([...nova, { papel: 'assistente', texto: r.texto, consultas: r.consultas }])
    } catch (e) {
      setErro((e as Error).message)
    } finally {
      setAPensar(false)
    }
  }

  const aceitar = async () => {
    await api.aceitarAssistente()
    setEstado(await api.assistente())
  }

  const submeter = (e: FormEvent) => { e.preventDefault(); enviar(pergunta) }

  if (!aberto)
    return (
      <button className="assistente-botao" onClick={() => setAberto(true)} aria-label={t('Abrir o assistente', 'Open the assistant')}>
        <Icone nome="conversa" tamanho={22} /> <span>{t('Assistente', 'Assistant')}</span>
      </button>
    )

  return (
    <aside className="assistente" aria-label={t('Assistente', 'Assistant')}>
      <header>
        <div>
          <strong>{t('Assistente', 'Assistant')}</strong>
          {estado?.configurado && <span className="suave pequeno">{estado.fornecedor} · {estado.modelo}</span>}
        </div>
        {conversa.length > 0 && <button className="ligacao pequeno" onClick={() => { setConversa([]); setErro(null) }}>{t('Nova conversa', 'New conversation')}</button>}
        <button className="icone-botao" onClick={() => setAberto(false)} aria-label={t('Fechar o assistente', 'Close the assistant')}><Icone nome="errado" tamanho={18} /></button>
      </header>

      {!estado ? <p className="assistente-corpo">{erro ?? t('A carregar…', 'Loading…')}</p>
        : !estado.configurado ? (
          <div className="assistente-corpo">
            <p><strong>{t('O assistente ainda não está ligado a um fornecedor de IA.', 'The assistant is not connected to an AI provider yet.')}</strong></p>
            <ol className="pequeno">
              <li>{t('Cria uma chave gratuita no', 'Create a free key at')} <a href="https://aistudio.google.com/apikey" target="_blank" rel="noreferrer">Google AI Studio <Icone nome="externo" tamanho={12} /></a>.</li>
              <li>{t('Na pasta do projeto, corre:', 'In the project folder, run:')}<br /><code>dotnet user-secrets set "Assistente:Chave" "A_TUA_CHAVE" --project src/Portal.Api</code></li>
              <li>{t('Reinicia a API.', 'Restart the API.')}</li>
            </ol>
            <p className="suave pequeno">{t('Para usar outro fornecedor (Groq, OpenRouter, Mistral) ou um modelo local (Ollama), muda "Assistente" no appsettings.json: vê docs/assistente.md.', 'To use another provider (Groq, OpenRouter, Mistral) or a local model (Ollama), change "Assistente" in appsettings.json: see docs/assistente.md.')}</p>
          </div>
        ) : !estado.aceiteEm ? (
          <div className="assistente-corpo">
            <p><strong>{t('Antes de começares', 'Before you start')}</strong></p>
            <p className="aviso pequeno"><Icone nome="alerta" tamanho={14} /> {estado.nota}</p>
            {estado.termosDados && <p className="pequeno"><a href={estado.termosDados} target="_blank" rel="noreferrer">{t(`Termos de ${estado.fornecedor} sobre os dados`, `${estado.fornecedor} data terms`)} <Icone nome="externo" tamanho={12} /></a></p>}
            <button onClick={aceitar}>{t('Percebi, quero usar o assistente', 'Understood, I want to use the assistant')}</button>
          </div>
        ) : (
          <>
            <div className="assistente-corpo assistente-conversa">
              <details className="explicacao">
                <summary><Icone nome="duvida" tamanho={14} /> {t('Para onde vão os meus dados?', 'Where does my data go?')}</summary>
                <div className="pequeno">
                  <p>{estado.nota}</p>
                  {estado.termosDados && <p><a href={estado.termosDados} target="_blank" rel="noreferrer">{t(`Termos de ${estado.fornecedor}`, `${estado.fornecedor} terms`)}</a></p>}
                </div>
              </details>

              {conversa.length === 0 && (
                <div className="sugestoes">
                  <p className="suave pequeno">{t('Pergunta sobre qualquer secção do portal. Por exemplo:', 'Ask about any section of the portal. For example:')}</p>
                  {(sugestoes()[seccao] ?? sugestoes().painel).map(s => <button key={s} className="sugestao" onClick={() => enviar(s)}>{s}</button>)}
                </div>
              )}

              {conversa.map((m, i) => (
                <div key={i} className={`mensagem mensagem-${m.papel}`}>
                  {m.papel === 'assistente' ? <Texto texto={m.texto} /> : m.texto}
                  {m.consultas && m.consultas.length > 0 && (
                    <div className="consultas" title={t('O que o assistente consultou no portal para responder', 'What the assistant looked up in the portal to answer')}>
                      {t('Consultou:', 'Looked up:')} {m.consultas.map(c => <span key={c} className="etiqueta">{c}</span>)}
                    </div>
                  )}
                </div>
              ))}
              {aPensar && <div className="mensagem mensagem-assistente suave">{t('A consultar os teus dados…', 'Looking up your data…')}</div>}
              {erro && <p className="erro pequeno">{erro}</p>}
              <div ref={fim} />
            </div>

            <form className="assistente-pergunta" onSubmit={submeter}>
              <textarea rows={2} maxLength={4000} placeholder={t('Escreve a tua pergunta…', 'Type your question…')} value={pergunta}
                onChange={e => setPergunta(e.target.value)}
                onKeyDown={e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); enviar(pergunta) } }} />
              <button type="submit" disabled={aPensar || !pergunta.trim()} aria-label={t('Enviar', 'Send')}><Icone nome="enviar" tamanho={18} /></button>
            </form>
          </>
        )}
    </aside>
  )
}
