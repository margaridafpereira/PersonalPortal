import type { ReactNode } from 'react'
import { t } from '../i18n'
import { Icone } from './Icone'

/** Explicação que abre ao clicar: o porquê de um valor, sem obrigar ninguém a lê-la. */
export function Explicacao({ titulo = t('Porquê?', 'Why?'), children }: { titulo?: string; children: ReactNode }) {
  return (
    <details className="explicacao">
      <summary><Icone nome="duvida" tamanho={14} /> {titulo}</summary>
      <div className="pequeno">{children}</div>
    </details>
  )
}
