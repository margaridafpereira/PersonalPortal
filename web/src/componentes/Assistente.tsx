import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { api, type EstadoAssistente, type MensagemConversa } from '../api'
import { Icone } from './Icone'

type Mensagem = MensagemConversa & { consultas?: string[] }

const sugestoes: Record<string, string[]> = {
  painel: ['O que tenho de tratar nas próximas semanas?', 'A que apoios tenho direito?', 'Que dados me faltam no perfil e para quê?'],
  apoios: ['Porque é que tenho (ou não) direito ao IRS Jovem?', 'Que prazos fiscais tenho nos próximos meses?'],
  anuncios: ['Algum imóvel que sigo baixou de preço?', 'Os imóveis que sigo estão acima da mediana do concelho?'],
  carro: ['Quando é a próxima inspeção e o IUC?', 'Onde está o combustível mais barato no meu concelho?'],
  investimentos: ['Quanto vou pagar de IRS pelos investimentos?', 'Explica-me o que vai em cada quadro do meu Anexo J.', 'Que títulos ainda não têm ISIN?'],
  perfil: ['Que dados do perfil me faltam e o que ganho em preenchê-los?'],
}

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
      <button className="assistente-botao" onClick={() => setAberto(true)} aria-label="Abrir o assistente">
        <Icone nome="conversa" tamanho={22} /> <span>Assistente</span>
      </button>
    )

  return (
    <aside className="assistente" aria-label="Assistente">
      <header>
        <div>
          <strong>Assistente</strong>
          {estado?.configurado && <span className="suave pequeno">{estado.fornecedor} · {estado.modelo}</span>}
        </div>
        {conversa.length > 0 && <button className="ligacao pequeno" onClick={() => { setConversa([]); setErro(null) }}>Nova conversa</button>}
        <button className="icone-botao" onClick={() => setAberto(false)} aria-label="Fechar o assistente"><Icone nome="errado" tamanho={18} /></button>
      </header>

      {!estado ? <p className="assistente-corpo">{erro ?? 'A carregar…'}</p>
        : !estado.configurado ? (
          <div className="assistente-corpo">
            <p><strong>O assistente ainda não está ligado a um fornecedor de IA.</strong></p>
            <ol className="pequeno">
              <li>Cria uma chave gratuita no <a href="https://aistudio.google.com/apikey" target="_blank" rel="noreferrer">Google AI Studio <Icone nome="externo" tamanho={12} /></a>.</li>
              <li>Na pasta do projeto, corre:<br /><code>dotnet user-secrets set "Assistente:Chave" "A_TUA_CHAVE" --project src/Portal.Api</code></li>
              <li>Reinicia a API.</li>
            </ol>
            <p className="suave pequeno">Para usar outro fornecedor (Groq, OpenRouter, Mistral) ou um modelo local (Ollama), muda "Assistente" no appsettings.json: vê docs/assistente.md.</p>
          </div>
        ) : !estado.aceiteEm ? (
          <div className="assistente-corpo">
            <p><strong>Antes de começares</strong></p>
            <p className="aviso pequeno"><Icone nome="alerta" tamanho={14} /> {estado.nota}</p>
            {estado.termosDados && <p className="pequeno"><a href={estado.termosDados} target="_blank" rel="noreferrer">Termos de {estado.fornecedor} sobre os dados <Icone nome="externo" tamanho={12} /></a></p>}
            <button onClick={aceitar}>Percebi, quero usar o assistente</button>
          </div>
        ) : (
          <>
            <div className="assistente-corpo assistente-conversa">
              <details className="explicacao">
                <summary><Icone nome="duvida" tamanho={14} /> Para onde vão os meus dados?</summary>
                <div className="pequeno">
                  <p>{estado.nota}</p>
                  {estado.termosDados && <p><a href={estado.termosDados} target="_blank" rel="noreferrer">Termos de {estado.fornecedor}</a></p>}
                </div>
              </details>

              {conversa.length === 0 && (
                <div className="sugestoes">
                  <p className="suave pequeno">Pergunta sobre qualquer secção do portal. Por exemplo:</p>
                  {(sugestoes[seccao] ?? sugestoes.painel).map(s => <button key={s} className="sugestao" onClick={() => enviar(s)}>{s}</button>)}
                </div>
              )}

              {conversa.map((m, i) => (
                <div key={i} className={`mensagem mensagem-${m.papel}`}>
                  {m.papel === 'assistente' ? <Texto texto={m.texto} /> : m.texto}
                  {m.consultas && m.consultas.length > 0 && (
                    <div className="consultas" title="O que o assistente consultou no portal para responder">
                      Consultou: {m.consultas.map(c => <span key={c} className="etiqueta">{c}</span>)}
                    </div>
                  )}
                </div>
              ))}
              {aPensar && <div className="mensagem mensagem-assistente suave">A consultar os teus dados…</div>}
              {erro && <p className="erro pequeno">{erro}</p>}
              <div ref={fim} />
            </div>

            <form className="assistente-pergunta" onSubmit={submeter}>
              <textarea rows={2} maxLength={4000} placeholder="Escreve a tua pergunta…" value={pergunta}
                onChange={e => setPergunta(e.target.value)}
                onKeyDown={e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); enviar(pergunta) } }} />
              <button type="submit" disabled={aPensar || !pergunta.trim()} aria-label="Enviar"><Icone nome="enviar" tamanho={18} /></button>
            </form>
          </>
        )}
    </aside>
  )
}
