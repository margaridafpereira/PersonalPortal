import { useState, type FormEvent } from 'react'
import { api } from '../api'

export function Entrar({ aoEntrar }: { aoEntrar: () => void }) {
  const [modo, setModo] = useState<'entrar' | 'registar'>('entrar')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [aEnviar, setAEnviar] = useState(false)

  const submeter = async (e: FormEvent) => {
    e.preventDefault()
    setErro(null)
    setAEnviar(true)
    try {
      if (modo === 'registar') await api.registar(email, password)
      await api.entrar(email, password)
      aoEntrar()
    } catch (err) {
      setErro(modo === 'entrar' ? 'Email ou palavra-passe incorretos.' : (err as Error).message)
    } finally {
      setAEnviar(false)
    }
  }

  return (
    <main className="centro">
      <form className="cartao entrar" onSubmit={submeter}>
        <h1>Portal pessoal</h1>
        <p className="suave">Os teus apoios, prazos e a procura de casa num só sítio.</p>

        <label>Email
          <input type="email" autoComplete="email" required value={email} onChange={e => setEmail(e.target.value)} />
        </label>
        <label>Palavra-passe
          <input type="password" autoComplete={modo === 'entrar' ? 'current-password' : 'new-password'}
            required minLength={10} value={password} onChange={e => setPassword(e.target.value)} />
        </label>
        {modo === 'registar' && <p className="suave pequeno">Mínimo 10 caracteres, com maiúscula, minúscula, número e símbolo.</p>}
        {erro && <p className="erro" role="alert">{erro}</p>}

        <button type="submit" disabled={aEnviar}>{modo === 'entrar' ? 'Entrar' : 'Criar conta'}</button>
        <button type="button" className="ligacao" onClick={() => { setModo(modo === 'entrar' ? 'registar' : 'entrar'); setErro(null) }}>
          {modo === 'entrar' ? 'Ainda não tens conta? Regista-te' : 'Já tens conta? Entra'}
        </button>
      </form>
    </main>
  )
}
