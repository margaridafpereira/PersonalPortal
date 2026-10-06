import { lingua, mudarLingua, t, type Lingua } from '../i18n'

/** PT | EN. Quem o mostra recebe a nova língua para voltar a montar as páginas. */
export function SeletorLingua({ aoMudar }: { aoMudar: (l: Lingua) => void }) {
  const atual = lingua()
  const escolher = (l: Lingua) => {
    if (l === atual) return
    mudarLingua(l)
    aoMudar(l)
  }
  return (
    <div className="seletor-lingua" role="group" aria-label={t('Língua', 'Language')}>
      {(['pt', 'en'] as const).map(l => (
        <button key={l} type="button" aria-pressed={l === atual} onClick={() => escolher(l)} lang={l === 'pt' ? 'pt-PT' : 'en'}
          title={l === 'pt' ? 'Português' : 'English'}>
          {l.toUpperCase()}
        </button>
      ))}
    </div>
  )
}
