import { useCallback, useEffect, useState } from 'react'
import { api, NaoAutenticado } from './api'
import { Entrar } from './paginas/Entrar'
import { Painel } from './paginas/Painel'
import { PerfilPagina } from './paginas/PerfilPagina'
import { PreferenciasPagina } from './paginas/PreferenciasPagina'

type Pagina = 'painel' | 'perfil' | 'preferencias'

const paginas: { id: Pagina; nome: string }[] = [
  { id: 'painel', nome: 'Painel' },
  { id: 'perfil', nome: 'Perfil' },
  { id: 'preferencias', nome: 'Preferências' },
]

function paginaDoEndereco(): Pagina {
  const hash = window.location.hash.replace('#/', '')
  return paginas.some(p => p.id === hash) ? (hash as Pagina) : 'painel'
}

export default function App() {
  const [email, setEmail] = useState<string | null | undefined>(undefined)
  const [pagina, setPagina] = useState<Pagina>(paginaDoEndereco)

  const verificarSessao = useCallback(() => {
    api.quemSou()
      .then(info => setEmail(info.email))
      .catch(e => { if (e instanceof NaoAutenticado) setEmail(null); else throw e })
  }, [])

  useEffect(verificarSessao, [verificarSessao])

  useEffect(() => {
    const aoMudar = () => setPagina(paginaDoEndereco())
    window.addEventListener('hashchange', aoMudar)
    return () => window.removeEventListener('hashchange', aoMudar)
  }, [])

  if (email === undefined) return <main className="centro">A carregar…</main>
  if (email === null) return <Entrar aoEntrar={verificarSessao} />

  const sair = async () => { await api.sair(); setEmail(null) }

  return (
    <>
      <header className="topo">
        <strong className="marca">Portal pessoal</strong>
        <nav>
          {paginas.map(p => (
            <a key={p.id} href={`#/${p.id}`} aria-current={pagina === p.id ? 'page' : undefined}>{p.nome}</a>
          ))}
        </nav>
        <span className="utilizador">
          {email} <button className="ligacao" onClick={sair}>Sair</button>
        </span>
      </header>
      <main>
        {pagina === 'painel' && <Painel />}
        {pagina === 'perfil' && <PerfilPagina />}
        {pagina === 'preferencias' && <PreferenciasPagina />}
      </main>
    </>
  )
}
