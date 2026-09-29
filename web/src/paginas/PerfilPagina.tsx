import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { api, type Perfil } from '../api'
import { PreferenciasPagina } from './PreferenciasPagina'

const numero = (v: string) => (v === '' ? null : Number(v))
const simNao = (v: string) => (v === '' ? null : v === 'sim')
const valorSimNao = (v: boolean | null) => (v === null ? '' : v ? 'sim' : 'nao')

export function PerfilPagina({ email }: { email: string }) {
  const [perfil, setPerfil] = useState<Perfil | null>(null)
  const [estado, setEstado] = useState<string | null>(null)

  useEffect(() => { api.perfil().then(setPerfil) }, [])

  if (!perfil) return <p>A carregar…</p>

  const mudar = <K extends keyof Perfil>(campo: K, valor: Perfil[K]) => {
    setPerfil({ ...perfil, [campo]: valor })
    setEstado(null)
  }

  const guardar = async (e: FormEvent) => {
    e.preventDefault()
    try {
      setPerfil(await api.guardarPerfil(perfil))
      setEstado('Guardado.')
    } catch (err) {
      setEstado((err as Error).message)
    }
  }

  return (
    <div className="formulario">
      <h1>Perfil e preferências</h1>
      <p className="suave">Preenches uma vez e todas as secções usam estes dados. Nada é partilhado.</p>

      <Conta email={email} />

    <form onSubmit={guardar}>
      <Grupo titulo="Sobre ti">
        <label>Nome
          <input autoComplete="given-name" placeholder="Como te chamamos" value={perfil.nome ?? ''} onChange={e => mudar('nome', e.target.value || null)} />
        </label>
        <label>Data de nascimento
          <input type="date" value={perfil.dataNascimento ?? ''} onChange={e => mudar('dataNascimento', e.target.value || null)} />
        </label>
        <label>Concelho
          <input value={perfil.concelho ?? ''} onChange={e => mudar('concelho', e.target.value || null)} />
        </label>
        <label>Freguesia
          <input value={perfil.freguesia ?? ''} onChange={e => mudar('freguesia', e.target.value || null)} />
        </label>
        <SimNao rotulo="Residente fiscal em Portugal" valor={perfil.residenteFiscal} aoMudar={v => mudar('residenteFiscal', v)} />
        <SimNao rotulo="És dependente no IRS de alguém" valor={perfil.dependente} aoMudar={v => mudar('dependente', v)} />
      </Grupo>

      <Grupo titulo="Rendimentos e agregado">
        <label>Tipo de rendimento
          <select value={perfil.categoriaRendimento ?? ''} onChange={e => mudar('categoriaRendimento', (e.target.value || null) as Perfil['categoriaRendimento'])}>
            <option value="">—</option>
            <option value="TrabalhoDependente">Trabalho por conta de outrem (cat. A)</option>
            <option value="TrabalhoIndependente">Trabalho independente (cat. B)</option>
            <option value="Ambos">Ambos</option>
            <option value="Nenhum">Nenhum</option>
          </select>
        </label>
        <label>Rendimento anual bruto do agregado (€)
          <input type="number" min={0} step={100} value={perfil.rendimentoAnualAgregado ?? ''} onChange={e => mudar('rendimentoAnualAgregado', numero(e.target.value))} />
        </label>
        <label>Adultos no agregado
          <input type="number" min={1} max={10} value={perfil.numeroAdultos ?? ''} onChange={e => mudar('numeroAdultos', numero(e.target.value))} />
        </label>
        <label>Idades dos filhos (separadas por vírgula)
          <input value={perfil.idadesFilhos.join(', ')} placeholder="ex.: 3, 7"
            onChange={e => mudar('idadesFilhos', e.target.value.split(',').map(s => s.trim()).filter(Boolean).map(Number).filter(n => Number.isInteger(n) && n >= 0))} />
        </label>
      </Grupo>

      <Grupo titulo="Habitação">
        <label>Situação atual
          <select value={perfil.situacaoHabitacao ?? ''} onChange={e => mudar('situacaoHabitacao', (e.target.value || null) as Perfil['situacaoHabitacao'])}>
            <option value="">—</option>
            <option value="Arrenda">Arrendo casa</option>
            <option value="Proprietario">Tenho casa própria</option>
            <option value="ProcuraArrendar">Procuro casa para arrendar</option>
            <option value="ProcuraComprar">Procuro casa para comprar</option>
            <option value="CasaDeFamilia">Vivo em casa de família</option>
          </select>
        </label>
        {perfil.situacaoHabitacao === 'Arrenda' && (
          <>
            <label>Renda mensal (€)
              <input type="number" min={0} value={perfil.rendaMensal ?? ''} onChange={e => mudar('rendaMensal', numero(e.target.value))} />
            </label>
            <label>Data do contrato de arrendamento
              <input type="date" value={perfil.dataContratoArrendamento ?? ''} onChange={e => mudar('dataContratoArrendamento', e.target.value || null)} />
            </label>
          </>
        )}
        <SimNao rotulo="Queres comprar casa" valor={perfil.procuraComprarCasa} aoMudar={v => mudar('procuraComprarCasa', v)} />
        {perfil.procuraComprarCasa && (
          <label>Orçamento de compra (€)
            <input type="number" min={0} step={1000} value={perfil.orcamentoCompra ?? ''} onChange={e => mudar('orcamentoCompra', numero(e.target.value))} />
          </label>
        )}
      </Grupo>

      <Grupo titulo="Património (para prazos)">
        <SimNao rotulo="És proprietário de imóvel" valor={perfil.proprietarioImovel} aoMudar={v => mudar('proprietarioImovel', v)} />
        {perfil.proprietarioImovel && (
          <label>Valor anual do IMI (€)
            <input type="number" min={0} value={perfil.valorImi ?? ''} onChange={e => mudar('valorImi', numero(e.target.value))} />
          </label>
        )}
        <p className="suave pequeno">Os carros e os respetivos prazos (IUC, inspeção, seguro) estão na secção <a href="#/carro">Carro</a>.</p>
      </Grupo>

      <div className="acoes barra-guardar">
        <button type="submit">Guardar perfil</button>
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
      setEstado({ ok: true, texto: 'Palavra-passe alterada.' })
    } catch (err) {
      setEstado({ ok: false, texto: (err as Error).message })
    }
  }

  return (
    <fieldset className="cartao">
      <legend>Conta</legend>
      <div className="campos">
        <label>Email
          <input value={email} readOnly disabled />
        </label>
      </div>
      <form onSubmit={mudarPalavraPasse}>
        <p className="pequeno"><strong>Mudar a palavra-passe</strong></p>
        <div className="campos">
          <label>Palavra-passe atual
            <input type="password" autoComplete="current-password" required value={atual} onChange={e => setAtual(e.target.value)} />
          </label>
          <label>Nova palavra-passe
            <input type="password" autoComplete="new-password" required minLength={10} value={nova} onChange={e => setNova(e.target.value)} />
          </label>
        </div>
        <div className="acoes">
          <button type="submit">Mudar palavra-passe</button>
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
        <option value="sim">Sim</option>
        <option value="nao">Não</option>
      </select>
    </label>
  )
}
