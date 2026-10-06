import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { api, type Perfil } from '../api'
import { PreferenciasPagina } from './PreferenciasPagina'
import { t } from '../i18n'

const numero = (v: string) => (v === '' ? null : Number(v))
const simNao = (v: string) => (v === '' ? null : v === 'sim')
const valorSimNao = (v: boolean | null) => (v === null ? '' : v ? 'sim' : 'nao')

export function PerfilPagina({ email }: { email: string }) {
  const [perfil, setPerfil] = useState<Perfil | null>(null)
  const [estado, setEstado] = useState<string | null>(null)

  useEffect(() => { api.perfil().then(setPerfil) }, [])

  if (!perfil) return <p>{t('A carregar…', 'Loading…')}</p>

  const mudar = <K extends keyof Perfil>(campo: K, valor: Perfil[K]) => {
    setPerfil({ ...perfil, [campo]: valor })
    setEstado(null)
  }

  const guardar = async (e: FormEvent) => {
    e.preventDefault()
    try {
      setPerfil(await api.guardarPerfil(perfil))
      setEstado(t('Guardado.', 'Saved.'))
    } catch (err) {
      setEstado((err as Error).message)
    }
  }

  return (
    <div className="formulario">
      <h1>{t('Perfil e preferências', 'Profile and preferences')}</h1>
      <p className="suave">{t('Preenches uma vez e todas as secções usam estes dados. Nada é partilhado.', 'Fill it in once and every section uses it. Nothing is shared.')}</p>

      <Conta email={email} />

    <form onSubmit={guardar}>
      <Grupo titulo={t('Sobre ti', 'About you')}>
        <label>{t('Nome', 'Name')}
          <input autoComplete="given-name" placeholder={t('Como te chamamos', 'What should we call you')} value={perfil.nome ?? ''} onChange={e => mudar('nome', e.target.value || null)} />
        </label>
        <label>{t('Data de nascimento', 'Date of birth')}
          <input type="date" value={perfil.dataNascimento ?? ''} onChange={e => mudar('dataNascimento', e.target.value || null)} />
        </label>
        <label>{t('Concelho', 'Municipality')}
          <input value={perfil.concelho ?? ''} onChange={e => mudar('concelho', e.target.value || null)} />
        </label>
        <label>{t('Freguesia', 'Parish')}
          <input value={perfil.freguesia ?? ''} onChange={e => mudar('freguesia', e.target.value || null)} />
        </label>
        <SimNao rotulo={t('Residente fiscal em Portugal', 'Tax resident in Portugal')} valor={perfil.residenteFiscal} aoMudar={v => mudar('residenteFiscal', v)} />
        <SimNao rotulo={t('És dependente no IRS de alguém', 'Are you a dependant on someone\'s IRS return')} valor={perfil.dependente} aoMudar={v => mudar('dependente', v)} />
      </Grupo>

      <Grupo titulo={t('Rendimentos e agregado', 'Income and household')}>
        <label>{t('Tipo de rendimento', 'Type of income')}
          <select value={perfil.categoriaRendimento ?? ''} onChange={e => mudar('categoriaRendimento', (e.target.value || null) as Perfil['categoriaRendimento'])}>
            <option value="">—</option>
            <option value="TrabalhoDependente">{t('Trabalho por conta de outrem (cat. A)', 'Employed (category A)')}</option>
            <option value="TrabalhoIndependente">{t('Trabalho independente (cat. B)', 'Self-employed (category B)')}</option>
            <option value="Ambos">{t('Ambos', 'Both')}</option>
            <option value="Nenhum">{t('Nenhum', 'None')}</option>
          </select>
        </label>
        <label>{t('Rendimento anual bruto do agregado (€)', 'Gross annual household income (€)')}
          <input type="number" min={0} step={100} value={perfil.rendimentoAnualAgregado ?? ''} onChange={e => mudar('rendimentoAnualAgregado', numero(e.target.value))} />
        </label>
        <label>{t('Adultos no agregado', 'Adults in the household')}
          <input type="number" min={1} max={10} value={perfil.numeroAdultos ?? ''} onChange={e => mudar('numeroAdultos', numero(e.target.value))} />
        </label>
        <label>{t('Idades dos filhos (separadas por vírgula)', 'Children\'s ages (comma-separated)')}
          <input value={perfil.idadesFilhos.join(', ')} placeholder={t('ex.: 3, 7', 'e.g. 3, 7')}
            onChange={e => mudar('idadesFilhos', e.target.value.split(',').map(s => s.trim()).filter(Boolean).map(Number).filter(n => Number.isInteger(n) && n >= 0))} />
        </label>
      </Grupo>

      <Grupo titulo={t('Habitação', 'Housing')}>
        <label>{t('Situação atual', 'Current situation')}
          <select value={perfil.situacaoHabitacao ?? ''} onChange={e => mudar('situacaoHabitacao', (e.target.value || null) as Perfil['situacaoHabitacao'])}>
            <option value="">—</option>
            <option value="Arrenda">{t('Arrendo casa', 'I rent')}</option>
            <option value="Proprietario">{t('Tenho casa própria', 'I own my home')}</option>
            <option value="ProcuraArrendar">{t('Procuro casa para arrendar', 'Looking to rent')}</option>
            <option value="ProcuraComprar">{t('Procuro casa para comprar', 'Looking to buy')}</option>
            <option value="CasaDeFamilia">{t('Vivo em casa de família', 'I live with family')}</option>
          </select>
        </label>
        {perfil.situacaoHabitacao === 'Arrenda' && (
          <>
            <label>{t('Renda mensal (€)', 'Monthly rent (€)')}
              <input type="number" min={0} value={perfil.rendaMensal ?? ''} onChange={e => mudar('rendaMensal', numero(e.target.value))} />
            </label>
            <label>{t('Data do contrato de arrendamento', 'Lease start date')}
              <input type="date" value={perfil.dataContratoArrendamento ?? ''} onChange={e => mudar('dataContratoArrendamento', e.target.value || null)} />
            </label>
          </>
        )}
        <SimNao rotulo={t('Queres comprar casa', 'Do you want to buy a home')} valor={perfil.procuraComprarCasa} aoMudar={v => mudar('procuraComprarCasa', v)} />
        {perfil.procuraComprarCasa && (
          <label>{t('Orçamento de compra (€)', 'Purchase budget (€)')}
            <input type="number" min={0} step={1000} value={perfil.orcamentoCompra ?? ''} onChange={e => mudar('orcamentoCompra', numero(e.target.value))} />
          </label>
        )}
      </Grupo>

      <Grupo titulo={t('Património (para prazos)', 'Property (for deadlines)')}>
        <SimNao rotulo={t('És proprietário de imóvel', 'Do you own property')} valor={perfil.proprietarioImovel} aoMudar={v => mudar('proprietarioImovel', v)} />
        {perfil.proprietarioImovel && (
          <label>{t('Valor anual do IMI (€)', 'Annual IMI property tax (€)')}
            <input type="number" min={0} value={perfil.valorImi ?? ''} onChange={e => mudar('valorImi', numero(e.target.value))} />
          </label>
        )}
        <p className="suave pequeno">{t('Os carros e os respetivos prazos (IUC, inspeção, seguro) estão na secção', 'Cars and their deadlines (road tax, inspection, insurance) are in the')} <a href="#/carro">{t('Carro', 'Car')}</a>{t('.', ' section.')}</p>
      </Grupo>

      <div className="acoes barra-guardar">
        <button type="submit">{t('Guardar perfil', 'Save profile')}</button>
        {estado && <span role="status">{estado}</span>}
      </div>
    </form>

      <PreferenciasPagina />
    </div>
  )
}

function Conta({ email }: { email: string }) {
  const [atual, setAtual] = useState('')
  const [nova, setNova] = useState('')
  const [estado, setEstado] = useState<{ ok: boolean; texto: string } | null>(null)

  const mudarPalavraPasse = async (e: FormEvent) => {
    e.preventDefault()
    try {
      await api.alterarPalavraPasse(atual, nova)
      setAtual('')
      setNova('')
      setEstado({ ok: true, texto: t('Palavra-passe alterada.', 'Password changed.') })
    } catch (err) {
      setEstado({ ok: false, texto: (err as Error).message })
    }
  }

  return (
    <fieldset className="cartao">
      <legend>{t('Conta', 'Account')}</legend>
      <div className="campos">
        <label>Email
          <input value={email} readOnly disabled />
        </label>
      </div>
      <form onSubmit={mudarPalavraPasse}>
        <p className="pequeno"><strong>{t('Mudar a palavra-passe', 'Change password')}</strong></p>
        <div className="campos">
          <label>{t('Palavra-passe atual', 'Current password')}
            <input type="password" autoComplete="current-password" required value={atual} onChange={e => setAtual(e.target.value)} />
          </label>
          <label>{t('Nova palavra-passe', 'New password')}
            <input type="password" autoComplete="new-password" required minLength={10} value={nova} onChange={e => setNova(e.target.value)} />
          </label>
        </div>
        <div className="acoes">
          <button type="submit">{t('Mudar palavra-passe', 'Change password')}</button>
          {estado && <span role="status" className={estado.ok ? '' : 'erro'}>{estado.texto}</span>}
        </div>
      </form>
    </fieldset>
  )
}

function Grupo({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <fieldset className="cartao">
      <legend>{titulo}</legend>
      <div className="campos">{children}</div>
    </fieldset>
  )
}

function SimNao({ rotulo, valor, aoMudar }: { rotulo: string; valor: boolean | null; aoMudar: (v: boolean | null) => void }) {
  return (
    <label>{rotulo}
      <select value={valorSimNao(valor)} onChange={e => aoMudar(simNao(e.target.value))}>
        <option value="">—</option>
        <option value="sim">{t('Sim', 'Yes')}</option>
        <option value="nao">{t('Não', 'No')}</option>
      </select>
    </label>
  )
}
