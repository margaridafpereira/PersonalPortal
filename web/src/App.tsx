import { useCallback, useEffect, useState } from 'react'
import { api, NaoAutenticado } from './api'
import { Assistente } from './componentes/Assistente'
import { Icone, type NomeIcone } from './componentes/Icone'
import { SeletorLingua } from './componentes/SeletorLingua'
import { lingua, t, type Lingua } from './i18n'
import { AnunciosPagina } from './paginas/AnunciosPagina'
import { ApoiosPagina } from './paginas/ApoiosPagina'
import { CarroPagina } from './paginas/CarroPagina'
import { InvestimentosPagina } from './paginas/InvestimentosPagina'
import { Entrar } from './paginas/Entrar'
import { Painel } from './paginas/Painel'
import { PerfilPagina } from './paginas/PerfilPagina'

type Pagina = 'painel' | 'apoios' | 'anuncios' | 'carro' | 'investimentos' | 'perfil'

const paginas = (): { id: Pagina; nome: string; icone: NomeIcone; seccao?: boolean }[] => [
  { id: 'painel', nome: t('Início', 'Home'), icone: 'inicio' },
  { id: 'apoios', nome: t('Apoios e prazos', 'Benefits'), icone: 'escudo', seccao: true },
  { id: 'anuncios', nome: t('Anúncios', 'Listings'), icone: 'casa', seccao: true },
  { id: 'carro', nome: t('Carro', 'Car'), icone: 'carro', seccao: true },
  { id: 'investimentos', nome: t('Investimentos', 'Investments'), icone: 'grafico', seccao: true },
  { id: 'perfil', nome: t('Perfil', 'Profile'), icone: 'pessoa' },
]

function paginaDoEndereco(): Pagina {
  // As preferências passaram para a página do perfil; links antigos continuam a funcionar.
  const hash = window.location.hash.replace('#/', '').replace('preferencias', 'perfil')
  return paginas().some(p => p.id === hash) ? (hash as Pagina) : 'painel'
}

export default function App() {
  const [email, setEmail] = useState<string | null | undefined>(undefined)
  const [pagina, setPagina] = useState<Pagina>(paginaDoEndereco)
  const [ativas, setAtivas] = useState<string[] | null>(null)
  const [emDemo, setEmDemo] = useState(false)
  // Mudar de língua volta a montar as páginas, que pedem os dados outra vez já na língua nova.
  const [idioma, setIdioma] = useState<Lingua>(lingua)

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

  if (email === undefined) return <main className="centro">{t('A carregar…', 'Loading…')}</main>
  if (email === null) return <Entrar key={idioma} aoEntrar={verificarSessao} aoMudarLingua={setIdioma} />

  const sair = async () => { await api.sair(); setEmail(null) }
  const visiveis = paginas().filter(p => !p.seccao || !ativas || ativas.includes(p.id))

  return (
    <div className="app" key={idioma}>
      <header className="topo">
        <a href="#/painel" className="marca">
          <span className="logo" aria-hidden="true">P</span>
          {t('Portal pessoal', 'Personal portal')}
        </a>
        <nav aria-label={t('Principal', 'Main')}>
          {visiveis.map(p => (
            <a key={p.id} href={`#/${p.id}`} aria-current={pagina === p.id ? 'page' : undefined}>
              <Icone nome={p.icone} tamanho={18} />
              <span>{p.nome}</span>
            </a>
          ))}
        </nav>
        <div className="utilizador">
          <SeletorLingua aoMudar={setIdioma} />
          <a href="#/perfil" className="avatar" title={`${email} · ${t('editar perfil', 'edit profile')}`}>{email[0].toUpperCase()}</a>
          <button className="icone-botao" onClick={sair} title={t('Sair', 'Sign out')} aria-label={t('Sair', 'Sign out')}>
            <Icone nome="sair" tamanho={18} />
          </button>
        </div>
      </header>
      {emDemo && (
        <p className="faixa-demo" role="status">
          {t('Estás na conta de demonstração: os dados são fictícios e não podes alterar nada. Repostos todos os dias.', 'You are in the demo account: the data is fictitious and you cannot change anything. Reset every day.')}
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
