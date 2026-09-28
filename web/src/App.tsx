import { useCallback, useEffect, useState } from 'react'
import { api, NaoAutenticado } from './api'
import { Icone, type NomeIcone } from './componentes/Icone'
import { AnunciosPagina } from './paginas/AnunciosPagina'
import { ApoiosPagina } from './paginas/ApoiosPagina'
import { Entrar } from './paginas/Entrar'
import { Painel } from './paginas/Painel'
import { PerfilPagina } from './paginas/PerfilPagina'
import { PreferenciasPagina } from './paginas/PreferenciasPagina'

type Pagina = 'painel' | 'apoios' | 'anuncios' | 'perfil' | 'preferencias'

const paginas: { id: Pagina; nome: string; icone: NomeIcone; seccao?: boolean }[] = [
  { id: 'painel', nome: 'Início', icone: 'inicio' },
  { id: 'apoios', nome: 'Apoios e prazos', icone: 'escudo', seccao: true },
  { id: 'anuncios', nome: 'Anúncios', icone: 'casa', seccao: true },
  { id: 'perfil', nome: 'Perfil', icone: 'pessoa' },
  { id: 'preferencias', nome: 'Preferências', icone: 'ajustes' },
]

function paginaDoEndereco(): Pagina {
  const hash = window.location.hash.replace('#/', '')
  return paginas.some(p => p.id === hash) ? (hash as Pagina) : 'painel'
}

export default function App() {
  const [email, setEmail] = useState<string | null | undefined>(undefined)
  const [pagina, setPagina] = useState<Pagina>(paginaDoEndereco)
  const [ativas, setAtivas] = useState<string[] | null>(null)

  const verificarSessao = useCallback(() => {
    api.quemSou()
      .then(info => setEmail(info.email))
      .catch(e => { if (e instanceof NaoAutenticado) setEmail(null); else throw e })
  }, [])

  useEffect(verificarSessao, [verificarSessao])

  // As secções ocultas nas preferências também saem do menu.
  useEffect(() => {
    if (email) api.preferencias().then(p => setAtivas(p.seccoesAtivas))
  }, [email, pagina])

  useEffect(() => {
    const aoMudar = () => { setPagina(paginaDoEndereco()); window.scrollTo(0, 0) }
    window.addEventListener('hashchange', aoMudar)
    return () => window.removeEventListener('hashchange', aoMudar)
  }, [])

  if (email === undefined) return <main className="centro">A carregar…</main>
  if (email === null) return <Entrar aoEntrar={verificarSessao} />

  const sair = async () => { await api.sair(); setEmail(null) }
  const visiveis = paginas.filter(p => !p.seccao || !ativas || ativas.includes(p.id))

  return (
    <div className="app">
      <header className="topo">
        <a href="#/painel" className="marca">
          <span className="logo" aria-hidden="true">P</span>
          Portal pessoal
        </a>
        <nav aria-label="Principal">
          {visiveis.map(p => (
            <a key={p.id} href={`#/${p.id}`} aria-current={pagina === p.id ? 'page' : undefined}>
              <Icone nome={p.icone} tamanho={18} />
              <span>{p.nome}</span>
            </a>
          ))}
        </nav>
        <div className="utilizador">
          <span className="avatar" title={email}>{email[0].toUpperCase()}</span>
          <button className="icone-botao" onClick={sair} title="Sair" aria-label="Sair">
            <Icone nome="sair" tamanho={18} />
          </button>
        </div>
      </header>
      <main>
        {pagina === 'painel' && <Painel email={email} />}
        {pagina === 'apoios' && <ApoiosPagina />}
        {pagina === 'anuncios' && <AnunciosPagina />}
        {pagina === 'perfil' && <PerfilPagina />}
        {pagina === 'preferencias' && <PreferenciasPagina />}
      </main>
    </div>
  )
}
