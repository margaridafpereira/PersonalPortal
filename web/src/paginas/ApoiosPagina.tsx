import { useEffect, useState } from 'react'
import { api, dataCurta, type EstadoElegibilidade, type Prazo, type ResultadoApoio } from '../api'
import { Icone } from '../componentes/Icone'

const grupos: { estado: EstadoElegibilidade; titulo: string; texto: string }[] = [
  { estado: 'Provavel', titulo: 'Provavelmente tens direito', texto: 'Cumpres todas as condições que conseguimos verificar.' },
  { estado: 'FaltaInformacao', titulo: 'Falta informação', texto: 'Completa o perfil para sabermos se tens direito.' },
  { estado: 'NaoElegivel', titulo: 'Não tens direito', texto: 'Pelo menos uma condição não é cumprida.' },
  { estado: 'Encerrado', titulo: 'Encerrados ou a vigiar', texto: 'Programas fechados que podem reabrir.' },
]

const categoriasPrazo = { Impostos: 'Impostos', SegurancaSocial: 'Segurança Social', Apoios: 'Apoios' }

export function ApoiosPagina() {
  const [separador, setSeparador] = useState<'apoios' | 'prazos'>('apoios')
  const [apoios, setApoios] = useState<ResultadoApoio[] | null>(null)
  const [prazos, setPrazos] = useState<Prazo[] | null>(null)

  useEffect(() => {
    api.apoios().then(setApoios)
    api.prazos().then(setPrazos)
  }, [])

  const provaveis = apoios?.filter(a => a.estado === 'Provavel').length ?? 0

  return (
    <>
      <header className="pagina-topo">
        <div>
          <h1>Apoios e prazos</h1>
          <p className="suave">Com base no teu perfil. O resultado é indicativo: confirma sempre na fonte oficial.</p>
        </div>
      </header>

      <div className="separadores" role="tablist">
        <button role="tab" aria-selected={separador === 'apoios'} onClick={() => setSeparador('apoios')}>
          <Icone nome="escudo" tamanho={18} /> Apoios <span className="contador">{provaveis}</span>
        </button>
        <button role="tab" aria-selected={separador === 'prazos'} onClick={() => setSeparador('prazos')}>
          <Icone nome="calendario" tamanho={18} /> Prazos <span className="contador">{prazos?.length ?? 0}</span>
        </button>
      </div>

      {separador === 'apoios' && (
        !apoios ? <p>A carregar…</p> : grupos.map(g => {
          const lista = apoios.filter(a => a.estado === g.estado)
          if (lista.length === 0) return null
          return (
            <details key={g.estado} className="grupo" open={g.estado !== 'NaoElegivel'}>
              <summary>
                <span className={`ponto estado-${g.estado}`} />
                <strong>{g.titulo}</strong> <span className="suave">({lista.length})</span>
                <span className="suave pequeno grupo-texto">{g.texto}</span>
              </summary>
              <div className="grelha">
                {lista.map(a => <CartaoApoio key={a.apoio.id} r={a} />)}
              </div>
            </details>
          )
        })
      )}

      {separador === 'prazos' && (
        !prazos ? <p>A carregar…</p> : (
          <>
            <div className="barra-acoes">
              <p className="suave">Os próximos 12 meses, só com o que se aplica a ti.</p>
              <a className="botao" href="/api/apoios/prazos.ics" download>
                <Icone nome="calendario" tamanho={18} /> Adicionar ao calendário
              </a>
            </div>
            {prazos.length === 0 && <p className="vazio">Sem prazos. Indica no perfil o tipo de rendimento, se tens casa ou carro.</p>}
            <ol className="linha-tempo">
              {prazos.map(p => {
                const d = new Date(p.data + 'T00:00:00')
                return (
                  <li key={p.data + p.titulo} className={p.diasEmFalta <= 14 ? 'urgente' : ''}>
                    <div className="data-bloco">
                      <strong>{d.getDate()}</strong>
                      <span>{d.toLocaleDateString('pt-PT', { month: 'short' }).replace('.', '')}</span>
                    </div>
                    <div className="prazo-corpo">
                      <div className="prazo-titulo">
                        <strong>{p.titulo}</strong>
                        <span className="etiqueta">{categoriasPrazo[p.categoria]}</span>
                        {p.porConfirmar && <span className="etiqueta etiqueta-aviso" title="Confirmar na Agenda Fiscal">a confirmar</span>}
                      </div>
                      <p className="suave pequeno">{p.descricao}</p>
                      {p.link && <a className="pequeno" href={p.link} target="_blank" rel="noreferrer">Fonte oficial <Icone nome="externo" tamanho={12} /></a>}
                    </div>
                    <div className="dias">
                      <strong>{p.diasEmFalta}</strong>
                      <span>{p.diasEmFalta === 1 ? 'dia' : 'dias'}</span>
                    </div>
                  </li>
                )
              })}
            </ol>
          </>
        )
      )}
    </>
  )
}

function CartaoApoio({ r }: { r: ResultadoApoio }) {
  const icones = { Cumpre: 'certo', NaoCumpre: 'errado', Desconhecido: 'duvida' } as const
  return (
    <article className={`cartao apoio estado-borda-${r.estado}`}>
      <div className="apoio-topo">
        <span className="etiqueta">{r.apoio.categoria}</span>
        {r.apoio.prazo && <span className="etiqueta etiqueta-aviso">{r.apoio.prazo}</span>}
      </div>
      <h3>{r.apoio.nome}</h3>
      <p className="suave pequeno">{r.apoio.descricao}</p>

      {r.estimativa && <p className="estimativa">{r.estimativa}</p>}

      {r.condicoes.length > 0 && (
        <ul className="condicoes">
          {r.condicoes.map(c => (
            <li key={c.descricao} className={`condicao-${c.resultado}`}>
              <Icone nome={icones[c.resultado]} tamanho={16} />
              <span>{c.descricao}</span>
            </li>
          ))}
        </ul>
      )}

      {r.apoio.aviso && (
        <p className="aviso pequeno"><Icone nome="alerta" tamanho={14} /> {r.apoio.aviso}</p>
      )}

      <p className="pequeno"><strong>Como pedir:</strong> {r.apoio.comoPedir}</p>
      <p className="pequeno suave">
        <a href={r.apoio.fonteOficial} target="_blank" rel="noreferrer">Fonte oficial <Icone nome="externo" tamanho={12} /></a>
        {' · '}verificado a {dataCurta(r.apoio.verificadoEm)}
      </p>
    </article>
  )
}
