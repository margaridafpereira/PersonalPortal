import { useEffect, useState } from 'react'
import { api, dataCurta, type EstadoAvisos, type Modulo, type Preferencias } from '../api'
import { Explicacao } from '../componentes/Explicacao'

export function PreferenciasPagina() {
  const [modulos, setModulos] = useState<Modulo[] | null>(null)
  const [prefs, setPrefs] = useState<Preferencias | null>(null)
  const [estado, setEstado] = useState<string | null>(null)
  const [avisos, setAvisos] = useState<EstadoAvisos | null>(null)
  const [teste, setTeste] = useState<string | null>(null)

  useEffect(() => {
    Promise.all([api.modulos(), api.preferencias()]).then(([m, p]) => { setModulos(m); setPrefs(p) })
  }, [])

  // Recarrega quando mudam as secções ou os dias: os próximos avisos dependem de ambos.
  useEffect(() => {
    api.avisos().then(setAvisos, () => setAvisos(null))
  }, [prefs?.seccoesAtivas, prefs?.diasAntecedencia, prefs?.alertasEmail])

  const enviarTeste = async () => {
    setTeste('A enviar…')
    try {
      setTeste((await api.emailTeste()).mensagem)
    } catch (err) {
      setTeste((err as Error).message)
    }
  }

  if (!modulos || !prefs) return <p>A carregar…</p>

  const guardar = async (novas: Preferencias) => {
    setPrefs(novas)
    try {
      setPrefs(await api.guardarPreferencias(novas))
      setEstado('Guardado.')
    } catch (err) {
      setEstado((err as Error).message)
    }
  }

  const ativas = prefs.seccoesAtivas
  const inativas = modulos.filter(m => !ativas.includes(m.id))
  const nome = (id: string) => modulos.find(m => m.id === id)?.nome ?? id

  const mover = (indice: number, delta: number) => {
    const nova = [...ativas]
    const [item] = nova.splice(indice, 1)
    nova.splice(indice + delta, 0, item)
    guardar({ ...prefs, seccoesAtivas: nova })
  }

  const alternarDia = (dia: number) => {
    const dias = prefs.diasAntecedencia.includes(dia)
      ? prefs.diasAntecedencia.filter(d => d !== dia)
      : [...prefs.diasAntecedencia, dia]
    guardar({ ...prefs, diasAntecedencia: dias })
  }

  return (
    <>
      <h2 className="titulo-seccao">Preferências</h2>

      <fieldset className="cartao">
        <legend>Secções do painel</legend>
        <p className="suave pequeno">Escolhe o que queres ver e por que ordem.</p>
        <ol className="lista-seccoes">
          {ativas.map((id, i) => (
            <li key={id}>
              <span>{nome(id)}</span>
              <span className="botoes">
                <button className="icone" aria-label={`Subir ${nome(id)}`} disabled={i === 0} onClick={() => mover(i, -1)}>↑</button>
                <button className="icone" aria-label={`Descer ${nome(id)}`} disabled={i === ativas.length - 1} onClick={() => mover(i, 1)}>↓</button>
                <button className="ligacao" onClick={() => guardar({ ...prefs, seccoesAtivas: ativas.filter(s => s !== id) })}>Ocultar</button>
              </span>
            </li>
          ))}
        </ol>
        {inativas.length > 0 && (
          <>
            <p className="pequeno">Ocultas:</p>
            <ul className="lista-seccoes">
              {inativas.map(m => (
                <li key={m.id}>
                  <span>{m.nome} <span className="suave pequeno">{m.descricao}</span></span>
                  <button className="ligacao" onClick={() => guardar({ ...prefs, seccoesAtivas: [...ativas, m.id] })}>Mostrar</button>
                </li>
              ))}
            </ul>
          </>
        )}
      </fieldset>

      <fieldset className="cartao">
        <legend>Alertas</legend>
        <label className="linha">
          <input type="checkbox" checked={prefs.alertasEmail} onChange={e => guardar({ ...prefs, alertasEmail: e.target.checked })} />
          Por email{avisos?.email && <span className="suave pequeno">({avisos.email})</span>}
        </label>
        <label className="linha">
          <input type="checkbox" checked={prefs.alertasTelegram} onChange={e => guardar({ ...prefs, alertasTelegram: e.target.checked })} />
          Por Telegram <span className="suave pequeno">(em breve)</span>
        </label>
        <p className="pequeno">Avisar antes de um prazo:</p>
        <div className="linha">
          {[30, 14, 7, 3, 1].map(dia => (
            <label key={dia} className="linha">
              <input type="checkbox" checked={prefs.diasAntecedencia.includes(dia)} onChange={() => alternarDia(dia)} />
              {dia} {dia === 1 ? 'dia' : 'dias'}
            </label>
          ))}
        </div>

        {avisos && (
          <>
            <p className="pequeno">
              Um email por dia, a partir das {avisos.horaEnvio}h, só quando há prazos a chegar. Cada aviso vai uma vez para cada antecedência escolhida.
              {' '}<span className="suave">Os emails são {avisos.destino}.</span>
            </p>
            <div className="acoes">
              <button type="button" onClick={enviarTeste}>Enviar email de teste</button>
              {teste && <span className="pequeno" role="status">{teste}</span>}
            </div>
            {avisos.proximos.length > 0 ? (
              <>
                <p className="pequeno"><strong>Prazos dentro da antecedência escolhida:</strong></p>
                <ul className="pequeno">
                  {avisos.proximos.map(p => (
                    <li key={p.titulo + p.data}>{p.titulo} — {dataCurta(p.data)} ({p.diasEmFalta === 0 ? 'hoje' : `daqui a ${p.diasEmFalta} dias`}) <span className="suave">· {p.seccao}</span></li>
                  ))}
                </ul>
              </>
            ) : <p className="suave pequeno">Nenhum prazo dentro da antecedência escolhida.</p>}
            <Explicacao titulo="Como ligar um servidor de email a sério?">
              <p>
                Sem servidor de email configurado, os avisos ficam gravados como ficheiros .eml na pasta da API (abrem-se no Outlook): serve para testar.
                Para receberes os emails na tua caixa, configura "Email" no appsettings.json com Modo "Smtp" e os dados do servidor
                (por exemplo o Gmail, com uma palavra-passe de aplicação). Vê docs/avisos.md.
              </p>
            </Explicacao>
          </>
        )}
      </fieldset>

      {estado && <p role="status">{estado}</p>}
    </>
  )
}
