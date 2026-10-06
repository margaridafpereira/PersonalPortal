import { useEffect, useState, type FormEvent } from 'react'
import {
  api, dataCurta, distritos, euros, inteiro, periodoIne,
  type EstadoFavorito, type Favorito, type Mediana, type NovaPesquisa, type NovoFavorito, type Perfil, type PesquisaComLinks,
} from '../api'
import { Icone } from '../componentes/Icone'
import { t } from '../i18n'

const numero = (v: string) => (v === '' ? null : Number(v))

const estados = (): Record<EstadoFavorito, string> => ({
  Ativo: t('A seguir', 'Following'),
  Visitado: t('Visitado', 'Visited'),
  Descartado: t('Descartado', 'Discarded'),
  SaiuDoPortal: t('Saiu do portal', 'Removed from portal'),
})

const negocios = () => ({ Comprar: t('Comprar', 'Buy'), Arrendar: t('Arrendar', 'Rent') })
const tipos = () => ({ Apartamento: t('Apartamento', 'Flat'), Moradia: t('Moradia', 'House'), Terreno: t('Terreno', 'Plot') })

export function AnunciosPagina() {
  const [separador, setSeparador] = useState<'favoritos' | 'pesquisas'>('favoritos')
  const [perfil, setPerfil] = useState<Perfil | null>(null)
  const [pesquisas, setPesquisas] = useState<PesquisaComLinks[] | null>(null)
  const [favoritos, setFavoritos] = useState<Favorito[] | null>(null)
  const [mercado, setMercado] = useState<Mediana | null>(null)

  useEffect(() => {
    api.perfil().then(p => {
      setPerfil(p)
      if (p.concelho) fetch(`/api/anuncios/mercado/${encodeURIComponent(p.concelho)}`).then(r => r.ok ? r.json() : null).then(setMercado)
    })
    api.pesquisas().then(setPesquisas)
    api.favoritos().then(setFavoritos)
  }, [])

  const substituir = (f: Favorito) => setFavoritos(fs => fs?.map(x => (x.id === f.id ? f : x)) ?? null)

  return (
    <>
      <header className="pagina-topo">
        <div>
          <h1>{t('Anúncios', 'Listings')}</h1>
          <p className="suave">{t('Guarda pesquisas, abre-as em todos os portais e acompanha os preços dos imóveis de que gostas.', 'Save searches, open them on every portal and track the prices of the homes you like.')}</p>
        </div>
        {mercado?.total && (
          <div className="mercado">
            <span className="suave pequeno">{t('Mediana de venda', 'Median sale price')} · {mercado.concelho}</span>
            <strong>{inteiro(mercado.total)} €/m²</strong>
            <span className="suave pequeno">INE, {periodoIne(mercado.periodo)}</span>
          </div>
        )}
      </header>

      <div className="separadores" role="tablist">
        <button role="tab" aria-selected={separador === 'favoritos'} onClick={() => setSeparador('favoritos')}>
          <Icone nome="coracao" tamanho={18} /> {t('Imóveis que sigo', 'Listings I follow')} <span className="contador">{favoritos?.length ?? 0}</span>
        </button>
        <button role="tab" aria-selected={separador === 'pesquisas'} onClick={() => setSeparador('pesquisas')}>
          <Icone nome="lupa" tamanho={18} /> {t('Pesquisas guardadas', 'Saved searches')} <span className="contador">{pesquisas?.length ?? 0}</span>
        </button>
      </div>

      {separador === 'pesquisas' && perfil && pesquisas && (
        <>
          <FormPesquisa perfil={perfil} aoCriar={p => setPesquisas([p, ...pesquisas])} />
          {pesquisas.length === 0 && <p className="vazio">{t('Ainda não tens pesquisas. Cria a primeira acima.', 'No searches yet. Create the first one above.')}</p>}
          <div className="lista">
            {pesquisas.map(p => (
              <article key={p.pesquisa.id} className="cartao pesquisa">
                <div className="pesquisa-topo">
                  <div>
                    <h3>{p.pesquisa.nome}</h3>
                    <p className="suave pequeno">
                      {p.pesquisa.distrito} · {p.pesquisa.concelho}
                      {p.pesquisa.quartosMinimo ? t(` · T${p.pesquisa.quartosMinimo} ou mais (confirma no portal)`, ` · ${p.pesquisa.quartosMinimo}+ bedrooms (check on the portal)`) : ''}
                    </p>
                  </div>
                  <button className="icone-botao" aria-label={t('Apagar pesquisa', 'Delete search')} title={t('Apagar', 'Delete')}
                    onClick={async () => { await api.apagarPesquisa(p.pesquisa.id); setPesquisas(pesquisas.filter(x => x.pesquisa.id !== p.pesquisa.id)) }}>
                    <Icone nome="lixo" tamanho={18} />
                  </button>
                </div>
                <div className="portais">
                  {p.links.map(l => (
                    <a key={l.portal} className={`portal portal-${l.portal.toLowerCase().replace(/\s/g, '')}`} href={l.url} target="_blank" rel="noreferrer"
                      title={l.verificado ? t('Abre a pesquisa com os filtros', 'Opens the search with the filters') : t('Abre a zona; confirma os filtros no portal', 'Opens the area; check the filters on the portal')}>
                      {l.portal} <Icone nome="externo" tamanho={14} />
                    </a>
                  ))}
                </div>
              </article>
            ))}
          </div>
        </>
      )}

      {separador === 'favoritos' && favoritos && (
        <>
          <FormFavorito perfil={perfil} aoCriar={f => setFavoritos([f, ...favoritos])} />
          {favoritos.length === 0 && (
            <p className="vazio">
              {t('Quando vires um anúncio de que gostas, cola aqui o link e o preço. A plataforma compara com a mediana do INE, verifica os apoios para jovens e guarda o histórico sempre que atualizares o preço.',
                'When you see a listing you like, paste the link and the price here. The platform compares it with the INE median, checks the schemes for young buyers and keeps the history each time you update the price.')}
            </p>
          )}
          <div className="grelha">
            {favoritos.map(f => (
              <CartaoFavorito key={f.id} f={f} aoAlterar={substituir}
                aoApagar={async () => { await api.apagarFavorito(f.id); setFavoritos(favoritos.filter(x => x.id !== f.id)) }} />
            ))}
          </div>
        </>
      )}
    </>
  )
}

function FormPesquisa({ perfil, aoCriar }: { perfil: Perfil; aoCriar: (p: PesquisaComLinks) => void }) {
  const [p, setP] = useState<NovaPesquisa>({
    nome: null, negocio: 'Comprar', tipo: 'Apartamento', distrito: '',
    concelho: perfil.concelho ?? '', precoMaximo: perfil.orcamentoCompra, quartosMinimo: null,
  })
  const [erro, setErro] = useState<string | null>(null)

  const submeter = async (e: FormEvent) => {
    e.preventDefault()
    try {
      aoCriar(await api.criarPesquisa(p))
      setErro(null)
    } catch (err) {
      setErro((err as Error).message)
    }
  }

  return (
    <form className="cartao formulario-linha" onSubmit={submeter}>
      <h2><Icone nome="mais" tamanho={18} /> {t('Nova pesquisa', 'New search')}</h2>
      <div className="alternador" role="group" aria-label={t('Negócio', 'Deal')}>
        {(['Comprar', 'Arrendar'] as const).map(n => (
          <button key={n} type="button" aria-pressed={p.negocio === n} onClick={() => setP({ ...p, negocio: n })}>{negocios()[n]}</button>
        ))}
      </div>
      <div className="alternador" role="group" aria-label={t('Tipo de imóvel', 'Property type')}>
        {(['Apartamento', 'Moradia', 'Terreno'] as const).map(tipo => (
          <button key={tipo} type="button" aria-pressed={p.tipo === tipo} onClick={() => setP({ ...p, tipo })}>{tipos()[tipo]}</button>
        ))}
      </div>
      <div className="campos">
        <label>{t('Distrito', 'District')}
          <select required value={p.distrito} onChange={e => setP({ ...p, distrito: e.target.value })}>
            <option value="">—</option>
            {distritos.map(d => <option key={d}>{d}</option>)}
          </select>
        </label>
        <label>{t('Concelho', 'Municipality')}
          <input required value={p.concelho} onChange={e => setP({ ...p, concelho: e.target.value })} placeholder={t('ex.: Valongo', 'e.g. Valongo')} />
        </label>
        <label>{t('Preço máximo (€)', 'Maximum price (€)')}
          <input type="number" min={0} step={1000} value={p.precoMaximo ?? ''} onChange={e => setP({ ...p, precoMaximo: numero(e.target.value) })} />
        </label>
        {p.tipo !== 'Terreno' && (
          <label>{t('Quartos (mínimo)', 'Bedrooms (minimum)')}
            <input type="number" min={0} max={10} value={p.quartosMinimo ?? ''} onChange={e => setP({ ...p, quartosMinimo: numero(e.target.value) })} />
          </label>
        )}
      </div>
      {erro && <p className="erro">{erro}</p>}
      <button type="submit">{t('Guardar pesquisa', 'Save search')}</button>
    </form>
  )
}

function FormFavorito({ perfil, aoCriar }: { perfil: Perfil | null; aoCriar: (f: Favorito) => void }) {
  const vazio: NovoFavorito = { url: '', titulo: null, tipo: 'Apartamento', tipologia: null, areaM2: null, concelho: perfil?.concelho ?? null, preco: null, notas: null }
  const [aberto, setAberto] = useState(false)
  const [f, setF] = useState<NovoFavorito>(vazio)
  const [erro, setErro] = useState<string | null>(null)

  if (!aberto)
    return (
      <button className="botao-grande" onClick={() => setAberto(true)}>
        <Icone nome="mais" tamanho={20} /> {t('Seguir um anúncio', 'Follow a listing')}
      </button>
    )

  const submeter = async (e: FormEvent) => {
    e.preventDefault()
    try {
      aoCriar(await api.criarFavorito(f))
      setF(vazio)
      setAberto(false)
      setErro(null)
    } catch (err) {
      setErro((err as Error).message)
    }
  }

  return (
    <form className="cartao formulario-linha" onSubmit={submeter}>
      <h2><Icone nome="coracao" tamanho={18} /> {t('Seguir um anúncio', 'Follow a listing')}</h2>
      <div className="campos">
        <label className="largo">{t('Link do anúncio', 'Listing link')}
          <input type="url" required placeholder="https://www.idealista.pt/imovel/…" value={f.url} onChange={e => setF({ ...f, url: e.target.value })} />
        </label>
        <label className="largo">{t('Nome (para te lembrares)', 'Name (to help you remember)')}
          <input placeholder={t('ex.: T2 com varanda perto do metro', 'e.g. 2-bed with balcony near the metro')} value={f.titulo ?? ''} onChange={e => setF({ ...f, titulo: e.target.value || null })} />
        </label>
        <label>{t('Tipo', 'Type')}
          <select value={f.tipo} onChange={e => setF({ ...f, tipo: e.target.value as NovoFavorito['tipo'] })}>
            {(['Apartamento', 'Moradia', 'Terreno'] as const).map(tipo => <option key={tipo} value={tipo}>{tipos()[tipo]}</option>)}
          </select>
        </label>
        {f.tipo !== 'Terreno' && (
          <label>{t('Tipologia', 'Layout')}
            <input placeholder="T2" value={f.tipologia ?? ''} onChange={e => setF({ ...f, tipologia: e.target.value || null })} />
          </label>
        )}
        <label>{t('Preço (€)', 'Price (€)')}
          <input type="number" min={0} step={500} required value={f.preco ?? ''} onChange={e => setF({ ...f, preco: numero(e.target.value) })} />
        </label>
        <label>{t('Área (m²)', 'Area (m²)')}
          <input type="number" min={0} value={f.areaM2 ?? ''} onChange={e => setF({ ...f, areaM2: numero(e.target.value) })} />
        </label>
        <label>{t('Concelho', 'Municipality')}
          <input value={f.concelho ?? ''} onChange={e => setF({ ...f, concelho: e.target.value || null })} />
        </label>
      </div>
      {erro && <p className="erro">{erro}</p>}
      <div className="acoes">
        <button type="submit">{t('Seguir', 'Follow')}</button>
        <button type="button" className="ligacao" onClick={() => setAberto(false)}>{t('Cancelar', 'Cancel')}</button>
      </div>
    </form>
  )
}

function CartaoFavorito({ f, aoAlterar, aoApagar }: { f: Favorito; aoAlterar: (f: Favorito) => void; aoApagar: () => void }) {
  const [novoPreco, setNovoPreco] = useState('')

  const registar = async (e: FormEvent) => {
    e.preventDefault()
    if (!novoPreco) return
    aoAlterar(await api.registarPreco(f.id, Number(novoPreco)))
    setNovoPreco('')
  }

  const mudarEstado = async (estado: EstadoFavorito) => aoAlterar(await api.alterarFavorito({ ...f, estado }))

  return (
    <article className={`cartao favorito ${f.estado === 'Descartado' || f.estado === 'SaiuDoPortal' ? 'apagado' : ''}`}>
      <div className="favorito-topo">
        <span className="etiqueta">{f.portal}</span>
        <select className="estado" value={f.estado} onChange={e => mudarEstado(e.target.value as EstadoFavorito)} aria-label={t('Estado', 'Status')}>
          {Object.entries(estados()).map(([v, nome]) => <option key={v} value={v}>{nome}</option>)}
        </select>
      </div>

      <h3><a href={f.url} target="_blank" rel="noreferrer">{f.titulo} <Icone nome="externo" tamanho={14} /></a></h3>
      <p className="suave pequeno">
        {[f.tipologia, f.areaM2 ? `${inteiro(f.areaM2)} m²` : null, f.concelho].filter(Boolean).join(' · ')}
      </p>

      <div className="preco-linha">
        <strong className="preco">{f.precoAtual ? euros(f.precoAtual) : '—'}</strong>
        {f.variacaoPercent !== null && (
          <span className={`variacao ${f.variacaoPercent < 0 ? 'desceu' : 'subiu'}`}>
            <Icone nome={f.variacaoPercent < 0 ? 'descer' : 'subir'} tamanho={14} />
            {Math.abs(f.variacaoPercent).toLocaleString('pt-PT')}%
          </span>
        )}
      </div>

      {f.precos.length >= 2 && <Evolucao precos={f.precos.map(p => p.preco)} />}

      {f.eurM2 && (
        <div className="comparacao">
          <span>{inteiro(f.eurM2)} €/m²</span>
          {f.mediana?.total && f.diferencaMedianaPercent !== null && (
            <span className={f.diferencaMedianaPercent > 0 ? 'acima' : 'abaixo'}>
              {f.diferencaMedianaPercent > 0 ? '+' : ''}{f.diferencaMedianaPercent.toLocaleString('pt-PT')}% {t('vs mediana de', 'vs median in')} {f.mediana.concelho} ({inteiro(f.mediana.total)} €/m²)
            </span>
          )}
        </div>
      )}

      {f.etiquetas.length > 0 && (
        <div className="etiquetas">
          {f.etiquetas.map(e => <span key={e} className="etiqueta etiqueta-positivo">{e}</span>)}
        </div>
      )}

      {f.eventos.length > 0 && (
        <ul className="eventos pequeno">
          {f.eventos.slice(-3).reverse().map(e => (
            <li key={e.data}>
              {dataCurta(e.data)}: {euros(e.de)} → {euros(e.para)} ({e.variacaoPercent > 0 ? '+' : ''}{e.variacaoPercent.toLocaleString('pt-PT')}%)
            </li>
          ))}
        </ul>
      )}

      <p className="suave pequeno">{t('A seguir há', 'Following for')} {f.diasASeguir} {f.diasASeguir === 1 ? t('dia', 'day') : t('dias', 'days')}</p>

      <div className="favorito-acoes">
        <form onSubmit={registar} className="atualizar-preco">
          <input type="number" min={0} step={500} placeholder={t('Preço de hoje', 'Today\'s price')} aria-label={t('Preço de hoje', 'Today\'s price')} value={novoPreco} onChange={e => setNovoPreco(e.target.value)} />
          <button type="submit" disabled={!novoPreco}>{t('Atualizar', 'Update')}</button>
        </form>
        <button className="icone-botao" aria-label={t('Deixar de seguir', 'Unfollow')} title={t('Deixar de seguir', 'Unfollow')} onClick={aoApagar}>
          <Icone nome="lixo" tamanho={18} />
        </button>
      </div>
    </article>
  )
}

/** Mini-gráfico da evolução do preço. */
function Evolucao({ precos }: { precos: number[] }) {
  const min = Math.min(...precos)
  const max = Math.max(...precos)
  const amplitude = max - min || 1
  const pontos = precos.map((p, i) => `${(i / (precos.length - 1)) * 100},${28 - ((p - min) / amplitude) * 24}`).join(' ')
  const desceu = precos[precos.length - 1] < precos[0]
  return (
    <svg className={`evolucao ${desceu ? 'desceu' : 'subiu'}`} viewBox="0 0 100 30" preserveAspectRatio="none" role="img"
      aria-label={t(`Evolução do preço: de ${euros(precos[0])} para ${euros(precos[precos.length - 1])}`, `Price trend: from ${euros(precos[0])} to ${euros(precos[precos.length - 1])}`)}>
      <polyline points={pontos} fill="none" strokeWidth={2} vectorEffect="non-scaling-stroke" />
    </svg>
  )
}
