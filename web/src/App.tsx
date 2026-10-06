import { useCallback, useEffect, useState } from 'react'
import { api, NaoAutenticado } from './api'
import { Assistente } from './componentes/Assistente'
import { Icone, type NomeIcone } from './componentes/Icone'
import { AnunciosPagina } from './paginas/AnunciosPagina'
import { ApoiosPagina } from './paginas/ApoiosPagina'
import { CarroPagina } from './paginas/CarroPagina'
import { InvestimentosPagina } from './paginas/InvestimentosPagina'
import { Entrar } from './paginas/Entrar'
import { Painel } from './paginas/Painel'
import { PerfilPagina } from './paginas/PerfilPagina'

type Pagina = 'painel' | 'apoios' | 'anuncios' | 'carro' | 'investimentos' | 'perfil'

const paginas: { id: Pagina; nome: string; icone: NomeIcone; seccao?: boolean }[] = [
  { id: 'painel', nome: 'Início', icone: 'inicio' },
  { id: 'apoios', nome: 'Apoios e prazos', icone: 'escudo', seccao: true },
  { id: 'anuncios', nome: 'Anúncios', icone: 'casa', seccao: true },
  { id: 'carro', nome: 'Carro', icone: 'carro', seccao: true },
  { id: 'investimentos', nome: 'Investimentos', icone: 'grafico', seccao: true },
  { id: 'perfil', nome: 'Perfil', icone: 'pessoa' },
]

function paginaDoEndereco(): Pagina {
  // As preferências passaram para a página do perfil; links antigos continuam a funcionar.
  const hash = window.location.hash.replace('#/', '').replace('preferencias', 'perfil')
  return paginas.some(p => p.id === hash) ? (hash as Pagina) : 'painel'
}

export default function App() {
  const [email, setEmail] = useState<string | null | undefined>(undefined)
  const [pagina, setPagina] = useState<Pagina>(paginaDoEndereco)
  const [ativas, setAtivas] = useState<string[] | null>(null)
  const [emDemo, setEmDemo] = useState(false)

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
    if (email) api.demo().then(d => setEmDemo(d.emDemo)).catch(() => setEmDemo(false))
  }, [email])

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
          <a href="#/perfil" className="avatar" title={`${email} · editar perfil`}>{email[0].toUpperCase()}</a>
          <button className="icone-botao" onClick={sair} title="Sair" aria-label="Sair">
            <Icone nome="sair" tamanho={18} />
          </button>
        </div>
      </header>
      {emDemo && (
        <p className="faixa-demo" role="status">
          Estás na <strong>conta de demonstração</strong>: os dados são fictícios e não podes alterar nada. Repostos todos os dias.
        </p>
      )}
      <main>
        {pagina === 'painel' && <Painel email={email} />}
        {pagina === 'apoios' && <ApoiosPagina />}
        {pagina === 'anuncios' && <AnunciosPagina />}
        {pagina === 'carro' && <CarroPagina />}
        {pagina === 'investimentos' && <InvestimentosPagina />}
        {pagina === 'perfil' && <PerfilPagina email={email} />}
      </main>
      <Assistente seccao={pagina} />
    </div>
  )
}
