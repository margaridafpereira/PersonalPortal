/**
 * Língua da interface: português ou inglês. Os textos ficam lado a lado no código, t('Entrar', 'Sign in'),
 * sem ficheiros de chaves. A escolha fica no browser; sem escolha, segue a língua do browser.
 * Ao mudar, a aplicação volta a montar as páginas (key no App), que voltam a pedir os dados ao servidor
 * já na língua nova (cabeçalho X-Idioma em api.ts).
 */
export type Lingua = 'pt' | 'en'

const CHAVE = 'portal.idioma'

function inicial(): Lingua {
  try {
    const guardada = localStorage.getItem(CHAVE)
    if (guardada === 'pt' || guardada === 'en') return guardada
  } catch {
    // Sem acesso ao armazenamento (janela privada, bloqueado): segue a língua do browser.
  }
  return navigator.language?.toLowerCase().startsWith('pt') ? 'pt' : 'en'
}

let atual: Lingua = inicial()
aplicarAoDocumento()

function aplicarAoDocumento() {
  document.documentElement.lang = atual === 'pt' ? 'pt-PT' : 'en'
  document.title = atual === 'pt' ? 'Portal pessoal' : 'Personal portal'
}

export const lingua = () => atual

export function mudarLingua(nova: Lingua) {
  atual = nova
  aplicarAoDocumento()
  try {
    localStorage.setItem(CHAVE, nova)
  } catch {
    // Fica só nesta visita.
  }
}

/** O texto na língua escolhida. */
export const t = (pt: string, en: string) => (atual === 'en' ? en : pt)

/** Locale para datas por extenso (nomes dos meses). Os números e os euros ficam sempre no formato português. */
export const localeDatas = () => (atual === 'en' ? 'en-GB' : 'pt-PT')
