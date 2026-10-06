import { useEffect, useState } from 'react'
import { api, dataCurta, type EstadoAvisos, type Modulo, type Preferencias } from '../api'
import { Explicacao } from '../componentes/Explicacao'
import { t } from '../i18n'

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
    setTeste(t('A enviar…', 'Sending…'))
    try {
      setTeste((await api.emailTeste()).mensagem)
    } catch (err) {
      setTeste((err as Error).message)
    }
  }

  if (!modulos || !prefs) return <p>{t('A carregar…', 'Loading…')}</p>

  const guardar = async (novas: Preferencias) => {
    setPrefs(novas)
    try {
      setPrefs(await api.guardarPreferencias(novas))
      setEstado(t('Guardado.', 'Saved.'))
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
      <h2 className="titulo-seccao">{t('Preferências', 'Preferences')}</h2>

      <fieldset className="cartao">
        <legend>{t('Secções do painel', 'Dashboard sections')}</legend>
        <p className="suave pequeno">{t('Escolhe o que queres ver e por que ordem.', 'Choose what you want to see, and in what order.')}</p>
        <ol className="lista-seccoes">
          {ativas.map((id, i) => (
            <li key={id}>
              <span>{nome(id)}</span>
              <span className="botoes">
                <button className="icone" aria-label={`${t('Subir', 'Move up')} ${nome(id)}`} disabled={i === 0} onClick={() => mover(i, -1)}>↑</button>
                <button className="icone" aria-label={`${t('Descer', 'Move down')} ${nome(id)}`} disabled={i === ativas.length - 1} onClick={() => mover(i, 1)}>↓</button>
                <button className="ligacao" onClick={() => guardar({ ...prefs, seccoesAtivas: ativas.filter(s => s !== id) })}>{t('Ocultar', 'Hide')}</button>
              </span>
            </li>
          ))}
        </ol>
        {inativas.length > 0 && (
          <>
            <p className="pequeno">{t('Ocultas:', 'Hidden:')}</p>
            <ul className="lista-seccoes">
              {inativas.map(m => (
                <li key={m.id}>
                  <span>{m.nome} <span className="suave pequeno">{m.descricao}</span></span>
                  <button className="ligacao" onClick={() => guardar({ ...prefs, seccoesAtivas: [...ativas, m.id] })}>{t('Mostrar', 'Show')}</button>
                </li>
              ))}
            </ul>
          </>
        )}
      </fieldset>

      <fieldset className="cartao">
        <legend>{t('Alertas', 'Alerts')}</legend>
        <label className="linha">
          <input type="checkbox" checked={prefs.alertasEmail} onChange={e => guardar({ ...prefs, alertasEmail: e.target.checked })} />
          {t('Por email', 'By email')}{avisos?.email && <span className="suave pequeno">({avisos.email})</span>}
        </label>
        <label className="linha">
          <input type="checkbox" checked={prefs.alertasTelegram} onChange={e => guardar({ ...prefs, alertasTelegram: e.target.checked })} />
          {t('Por Telegram', 'By Telegram')} <span className="suave pequeno">{t('(em breve)', '(coming soon)')}</span>
        </label>
        <p className="pequeno">{t('Avisar antes de um prazo:', 'Remind me before a deadline:')}</p>
        <div className="linha">
          {[30, 14, 7, 3, 1].map(dia => (
            <label key={dia} className="linha">
              <input type="checkbox" checked={prefs.diasAntecedencia.includes(dia)} onChange={() => alternarDia(dia)} />
              {dia} {dia === 1 ? t('dia', 'day') : t('dias', 'days')}
            </label>
          ))}
        </div>

        {avisos && (
          <>
            <p className="pequeno">
              {t(`Um email por dia, a partir das ${avisos.horaEnvio}h, só quando há prazos a chegar. Cada aviso vai uma vez para cada antecedência escolhida.`, `One email a day, from ${avisos.horaEnvio}:00, only when deadlines are coming up. Each reminder goes once for each notice period you chose. Emails are in Portuguese.`)}
              {' '}<span className="suave">{t('Os emails são', 'Emails are')} {avisos.destino}.</span>
            </p>
            <div className="acoes">
              <button type="button" onClick={enviarTeste}>{t('Enviar email de teste', 'Send a test email')}</button>
              {teste && <span className="pequeno" role="status">{teste}</span>}
            </div>
            {avisos.proximos.length > 0 ? (
              <>
                <p className="pequeno"><strong>{t('Prazos dentro da antecedência escolhida:', 'Deadlines within the notice you chose:')}</strong></p>
                <ul className="pequeno">
                  {avisos.proximos.map(p => (
                    <li key={p.titulo + p.data}>{p.titulo} — {dataCurta(p.data)} ({p.diasEmFalta === 0 ? t('hoje', 'today') : t(`daqui a ${p.diasEmFalta} dias`, `in ${p.diasEmFalta} days`)}) <span className="suave">· {p.seccao}</span></li>
                  ))}
                </ul>
              </>
            ) : <p className="suave pequeno">{t('Nenhum prazo dentro da antecedência escolhida.', 'No deadlines within the notice you chose.')}</p>}
            <Explicacao titulo={t('Como ligar um servidor de email a sério?', 'How do I connect a real email server?')}>
              <p>
                {t('Sem servidor de email configurado, os avisos ficam gravados como ficheiros .eml na pasta da API (abrem-se no Outlook): serve para testar. '
                  + 'Para receberes os emails na tua caixa, configura "Email" no appsettings.json com Modo "Smtp" e os dados do servidor '
                  + '(por exemplo o Gmail, com uma palavra-passe de aplicação). Vê docs/avisos.md.',
                'With no email server configured, reminders are saved as .eml files in the API folder (they open in Outlook): good for testing. '
                  + 'To get them in your inbox, set "Email" in appsettings.json to Modo "Smtp" with your server details '
                  + '(for example Gmail, with an app password). See docs/avisos.md.')}
              </p>
            </Explicacao>
          </>
        )}
      </fieldset>

      {estado && <p role="status">{estado}</p>}
    </>
  )
}
