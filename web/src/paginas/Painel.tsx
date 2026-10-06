import { useEffect, useState } from 'react'
import { api, nomesCampos, type CartaoPainel, type Perfil } from '../api'
import { Icone, type NomeIcone } from '../componentes/Icone'
import { t } from '../i18n'

const aparencia = (): Record<string, { icone: NomeIcone; cor: string; acao: string }> => ({
  apoios: { icone: 'escudo', cor: 'verde', acao: t('Ver apoios e prazos', 'See benefits and deadlines') },
  anuncios: { icone: 'casa', cor: 'azul', acao: t('Abrir anúncios', 'Open listings') },
  carro: { icone: 'carro', cor: 'laranja', acao: t('Ver carro e combustíveis', 'See car and fuel') },
  investimentos: { icone: 'grafico', cor: 'roxo', acao: t('Preparar o Anexo J', 'Prepare Annex J') },
})

// Campos que contam para a barra de "perfil completo".
const camposPerfil: (keyof Perfil)[] = [
  'nome', 'dataNascimento', 'concelho', 'residenteFiscal', 'dependente', 'categoriaRendimento',
  'rendimentoAnualAgregado', 'numeroAdultos', 'situacaoHabitacao', 'procuraComprarCasa',
]

function saudacao() {
  const h = new Date().getHours()
  return h < 12 ? t('Bom dia', 'Good morning') : h < 20 ? t('Boa tarde', 'Good afternoon') : t('Boa noite', 'Good evening')
}

export function Painel({ email }: { email: string }) {
  const [cartoes, setCartoes] = useState<CartaoPainel[] | null>(null)
  const [perfil, setPerfil] = useState<Perfil | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    Promise.all([api.painel(), api.perfil()])
      .then(([c, p]) => { setCartoes(c); setPerfil(p) })
      .catch(e => setErro((e as Error).message))
  }, [])

  if (erro) return <p className="erro" role="alert">{erro}</p>
  if (!cartoes || !perfil) return <Esqueleto />

  const preenchidos = camposPerfil.filter(c => perfil[c] !== null && perfil[c] !== '').length
  const percentagem = Math.round((preenchidos / camposPerfil.length) * 100)

  return (
    <>
      <section className="heroi">
        <div>
          <p className="suave">{saudacao()},</p>
          <h1>{perfil.nome ?? email.split('@')[0]}</h1>
          <p className="suave">{t('O que a plataforma sabe sobre ti e o que podes fazer hoje.', 'What the platform knows about you and what you can do today.')}</p>
        </div>
        <a href="#/perfil" className="progresso-perfil">
          <div className="progresso-topo">
            <span>{t('Perfil', 'Profile')}</span>
            <strong>{percentagem}%</strong>
          </div>
          <div className="barra" role="progressbar" aria-valuenow={percentagem} aria-valuemin={0} aria-valuemax={100} aria-label={t('Perfil preenchido', 'Profile completed')}>
            <div style={{ width: `${percentagem}%` }} />
          </div>
          <span className="pequeno suave">
            {percentagem < 100 ? t('Completa o perfil para resultados mais certos →', 'Complete your profile for more accurate results →') : t('Perfil completo ✓', 'Profile complete ✓')}
          </span>
        </a>
      </section>

      {cartoes.length === 0 && (
        <p>{t('Não tens secções ativas.', 'You have no active sections.')} <a href="#/perfil">{t('Escolhe-as nas preferências do perfil.', 'Choose them in your profile preferences.')}</a></p>
      )}

      <div className="grelha">
        {cartoes.map(c => {
          const a = aparencia()[c.moduloId] ?? { icone: 'inicio' as NomeIcone, cor: 'azul', acao: t('Abrir', 'Open') }
          return (
            <section key={c.moduloId} className={`cartao seccao seccao-${a.cor}`}>
              <header className="seccao-topo">
                <span className="seccao-icone"><Icone nome={a.icone} tamanho={22} /></span>
                <div>
                  <h2>{c.titulo}</h2>
                  <p className="suave pequeno">{c.resumo}</p>
                </div>
              </header>

              {c.indicadores.length > 0 && (
                <div className="indicadores">
                  {c.indicadores.map((i, n) => (
                    <div key={n} className={`indicador tom-${i.tom}`}>
                      <strong>{i.valor}</strong>
                      <span>{i.rotulo}</span>
                    </div>
                  ))}
                </div>
              )}

              {c.itens.length > 0 && (
                <ul className="itens">
                  {c.itens.map((i, n) => (
                    <li key={n}>
                      <span>{i.texto}</span>
                      {i.detalhe && <span className="suave pequeno">{i.detalhe}</span>}
                    </li>
                  ))}
                </ul>
              )}

              {c.camposPerfilEmFalta.length > 0 && (
                <p className="aviso pequeno">
                  {t('Falta no perfil:', 'Missing from your profile:')} {c.camposPerfilEmFalta.map(f => nomesCampos[f] ?? f).join(', ')}.
                </p>
              )}

              <a className="botao seccao-acao" href={`#/${c.moduloId}`}>
                {a.acao} <Icone nome="seta" tamanho={16} />
              </a>
            </section>
          )
        })}
      </div>
    </>
  )
}

function Esqueleto() {
  return (
    <div className="grelha" aria-busy="true">
      <div className="cartao esqueleto" />
      <div className="cartao esqueleto" />
    </div>
  )
}
