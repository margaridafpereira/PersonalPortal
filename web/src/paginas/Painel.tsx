import { useEffect, useState } from 'react'
import { api, nomesCampos, type CartaoPainel } from '../api'

export function Painel() {
  const [cartoes, setCartoes] = useState<CartaoPainel[] | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    api.painel().then(setCartoes).catch(e => setErro((e as Error).message))
  }, [])

  if (erro) return <p className="erro" role="alert">{erro}</p>
  if (!cartoes) return <p>A carregar…</p>

  if (cartoes.length === 0)
    return <p>Não tens secções ativas. <a href="#/preferencias">Escolhe-as nas preferências.</a></p>

  return (
    <>
      <h1>Painel</h1>
      <div className="grelha">
        {cartoes.map(c => (
          <section key={c.moduloId} className="cartao">
            <h2>{c.titulo}</h2>
            <p>{c.resumo}</p>
            {c.itens.length > 0 && (
              <ul className="itens">
                {c.itens.map((i, n) => (
                  <li key={n}>
                    {i.link ? <a href={i.link} target="_blank" rel="noreferrer">{i.texto}</a> : i.texto}
                    {i.detalhe && <span className="suave pequeno"> {i.detalhe}</span>}
                  </li>
                ))}
              </ul>
            )}
            {c.camposPerfilEmFalta.length > 0 && (
              <p className="aviso pequeno">
                Falta: {c.camposPerfilEmFalta.map(f => nomesCampos[f] ?? f).join(', ')}. <a href="#/perfil">Completar perfil</a>
              </p>
            )}
          </section>
        ))}
      </div>
    </>
  )
}
