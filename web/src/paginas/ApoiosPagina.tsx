import { useEffect, useState } from 'react'
import { api, dataCurta, type EstadoElegibilidade, type Prazo, type ResultadoApoio } from '../api'
import { Icone } from '../componentes/Icone'
import { lingua, localeDatas, t } from '../i18n'

const grupos = (): { estado: EstadoElegibilidade; titulo: string; texto: string }[] => [
  { estado: 'Provavel', titulo: t('Provavelmente tens direito', 'You probably qualify'), texto: t('Cumpres todas as condições que conseguimos verificar.', 'You meet every condition we can check.') },
  { estado: 'FaltaInformacao', titulo: t('Falta informação', 'Missing information'), texto: t('Completa o perfil para sabermos se tens direito.', 'Complete your profile so we can tell whether you qualify.') },
  { estado: 'NaoElegivel', titulo: t('Não tens direito', 'You do not qualify'), texto: t('Pelo menos uma condição não é cumprida.', 'At least one condition is not met.') },
  { estado: 'Encerrado', titulo: t('Encerrados ou a vigiar', 'Closed or worth watching'), texto: t('Programas fechados que podem reabrir.', 'Closed programmes that may reopen.') },
]

const categoriasPrazo = () => ({ Impostos: t('Impostos', 'Taxes'), SegurancaSocial: t('Segurança Social', 'Social Security'), Apoios: t('Apoios', 'Benefits') })

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
          <h1>{t('Apoios e prazos', 'Benefits and deadlines')}</h1>
          <p className="suave">{t('Com base no teu perfil. O resultado é indicativo: confirma sempre na fonte oficial.', 'Based on your profile. Results are indicative: always confirm with the official source.')}</p>
        </div>
      </header>

      <div className="separadores" role="tablist">
        <button role="tab" aria-selected={separador === 'apoios'} onClick={() => setSeparador('apoios')}>
          <Icone nome="escudo" tamanho={18} /> {t('Apoios', 'Benefits')} <span className="contador">{provaveis}</span>
        </button>
        <button role="tab" aria-selected={separador === 'prazos'} onClick={() => setSeparador('prazos')}>
          <Icone nome="calendario" tamanho={18} /> {t('Prazos', 'Deadlines')} <span className="contador">{prazos?.length ?? 0}</span>
        </button>
      </div>

      {separador === 'apoios' && (
        !apoios ? <p>{t('A carregar…', 'Loading…')}</p> : grupos().map(g => {
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
        !prazos ? <p>{t('A carregar…', 'Loading…')}</p> : (
          <>
            <div className="barra-acoes">
              <p className="suave">{t('Os próximos 12 meses, só com o que se aplica a ti.', 'The next 12 months, only what applies to you.')}</p>
              <a className="botao" href={`/api/apoios/prazos.ics?idioma=${lingua()}`} download>
                <Icone nome="calendario" tamanho={18} /> {t('Adicionar ao calendário', 'Add to calendar')}
              </a>
            </div>
            {prazos.length === 0 && <p className="vazio">{t('Sem prazos. Indica no perfil o tipo de rendimento, se tens casa ou carro.', 'No deadlines. Add your type of income, and whether you own a home or a car, to your profile.')}</p>}
            <ol className="linha-tempo">
              {prazos.map(p => {
                const d = new Date(p.data + 'T00:00:00')
                return (
                  <li key={p.data + p.titulo} className={p.diasEmFalta <= 14 ? 'urgente' : ''}>
                    <div className="data-bloco">
                      <strong>{d.getDate()}</strong>
                      <span>{d.toLocaleDateString(localeDatas(), { month: 'short' }).replace('.', '')}</span>
                    </div>
                    <div className="prazo-corpo">
                      <div className="prazo-titulo">
                        <strong>{p.titulo}</strong>
                        <span className="etiqueta">{categoriasPrazo()[p.categoria]}</span>
                        {p.porConfirmar && <span className="etiqueta etiqueta-aviso" title={t('Confirmar na Agenda Fiscal', 'Check the official tax calendar')}>{t('a confirmar', 'to confirm')}</span>}
                      </div>
                      <p className="suave pequeno">{p.descricao}</p>
                      {p.link && <a className="pequeno" href={p.link} target="_blank" rel="noreferrer">{t('Fonte oficial', 'Official source')} <Icone nome="externo" tamanho={12} /></a>}
                    </div>
                    <div className="dias">
                      <strong>{p.diasEmFalta}</strong>
                      <span>{p.diasEmFalta === 1 ? t('dia', 'day') : t('dias', 'days')}</span>
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

      <p className="pequeno"><strong>{t('Como pedir:', 'How to apply:')}</strong> {r.apoio.comoPedir}</p>
      <p className="pequeno suave">
        <a href={r.apoio.fonteOficial} target="_blank" rel="noreferrer">{t('Fonte oficial', 'Official source')} <Icone nome="externo" tamanho={12} /></a>
        {' · '}{t('verificado a', 'checked on')} {dataCurta(r.apoio.verificadoEm)}
      </p>
    </article>
  )
}
