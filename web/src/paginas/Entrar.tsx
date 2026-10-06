import { useEffect, useState, type FormEvent } from 'react'
import { api } from '../api'
import { SeletorLingua } from '../componentes/SeletorLingua'
import { t, type Lingua } from '../i18n'

export function Entrar({ aoEntrar, aoMudarLingua }: { aoEntrar: () => void; aoMudarLingua: (l: Lingua) => void }) {
  const [modo, setModo] = useState<'entrar' | 'registar'>('entrar')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [aEnviar, setAEnviar] = useState(false)
  const [demoAtiva, setDemoAtiva] = useState(false)

  useEffect(() => { api.demo().then(d => setDemoAtiva(d.ativa)).catch(() => setDemoAtiva(false)) }, [])

  const experimentar = async () => {
    setErro(null)
    setAEnviar(true)
    try {
      await api.entrarDemo()
      aoEntrar()
    } catch (err) {
      setErro((err as Error).message)
    } finally {
      setAEnviar(false)
    }
  }

  const submeter = async (e: FormEvent) => {
    e.preventDefault()
    setErro(null)
    setAEnviar(true)
    try {
      if (modo === 'registar') await api.registar(email, password)
      await api.entrar(email, password)
      aoEntrar()
    } catch (err) {
      setErro(modo === 'entrar' ? t('Email ou palavra-passe incorretos.', 'Wrong email or password.') : (err as Error).message)
    } finally {
      setAEnviar(false)
    }
  }

  return (
    <main className="centro">
      <form className="cartao entrar" onSubmit={submeter}>
        <div className="entrar-topo">
          <h1>{t('Portal pessoal', 'Personal portal')}</h1>
          <SeletorLingua aoMudar={aoMudarLingua} />
        </div>
        <p className="suave">{t('Os teus apoios, prazos e a procura de casa num só sítio.', 'Your benefits, deadlines and home search in one place.')}</p>

        <label>Email
          <input type="email" autoComplete="email" required value={email} onChange={e => setEmail(e.target.value)} />
        </label>
        <label>{t('Palavra-passe', 'Password')}
          <input type="password" autoComplete={modo === 'entrar' ? 'current-password' : 'new-password'}
            required minLength={10} value={password} onChange={e => setPassword(e.target.value)} />
        </label>
        {modo === 'registar' && <p className="suave pequeno">{t('Mínimo 10 caracteres, com maiúscula, minúscula, número e símbolo.', 'At least 10 characters, with upper and lower case, a number and a symbol.')}</p>}
        {erro && <p className="erro" role="alert">{erro}</p>}

        <button type="submit" disabled={aEnviar}>{modo === 'entrar' ? t('Entrar', 'Sign in') : t('Criar conta', 'Create account')}</button>
        <button type="button" className="ligacao" onClick={() => { setModo(modo === 'entrar' ? 'registar' : 'entrar'); setErro(null) }}>
          {modo === 'entrar' ? t('Ainda não tens conta? Regista-te', 'No account yet? Sign up') : t('Já tens conta? Entra', 'Already have an account? Sign in')}
        </button>

        {demoAtiva && (
          <div className="demo-entrada">
            <button type="button" className="secundario" onClick={experimentar} disabled={aEnviar}>{t('Experimentar com dados de exemplo', 'Try it with sample data')}</button>
            <p className="suave pequeno">{t('Uma conta de demonstração, só de leitura, com dados fictícios em todas as secções.', 'A read-only demo account with fictitious data in every section.')}</p>
          </div>
        )}
      </form>
    </main>
  )
}
