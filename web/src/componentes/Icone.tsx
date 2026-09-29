// Ícones em linha (estilo "outline", 24×24), sem dependências externas.

const caminhos = {
  casa: 'M3 11.5 12 4l9 7.5M5 10v10h5v-6h4v6h5V10',
  escudo: 'M12 3 4 6v6c0 4.5 3.4 8.3 8 9 4.6-.7 8-4.5 8-9V6l-8-3Zm-3.5 9 2.5 2.5 4.5-5',
  calendario: 'M7 3v3M17 3v3M4 8h16M5 5h14a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Zm3 7h2m4 0h2m-8 4h2m4 0h2',
  pessoa: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm-7 8c0-3.3 3.1-6 7-6s7 2.7 7 6',
  ajustes: 'M4 6h10M18 6h2M4 12h4M12 12h8M4 18h12M20 18h0M14 4v4M8 10v4M16 16v4',
  inicio: 'M4 5h7v7H4zM13 5h7v4h-7zM13 11h7v8h-7zM4 14h7v5H4z',
  lupa: 'M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14Zm5-2 4 4',
  coracao: 'M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10Z',
  externo: 'M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5',
  mais: 'M12 5v14M5 12h14',
  lixo: 'M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3',
  seta: 'M9 6l6 6-6 6',
  descer: 'M12 5v14M6 13l6 6 6-6',
  subir: 'M12 19V5M6 11l6-6 6 6',
  alerta: 'M12 9v4M12 17h0M10.3 4.3 2.6 18a2 2 0 0 0 1.7 3h15.4a2 2 0 0 0 1.7-3L13.7 4.3a2 2 0 0 0-3.4 0Z',
  certo: 'M5 12.5 10 17l9-10',
  errado: 'M6 6l12 12M18 6 6 18',
  duvida: 'M9.5 9a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.6V14M12 18h0',
  sair: 'M15 4h4a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-4M10 16l-4-4 4-4M6 12h10',
  carro: 'M5 16h14M5 16v2h2v-2M17 16v2h2v-2M4 16v-4l2-5h12l2 5v4M4 12h16M7.5 14h0M16.5 14h0',
  grafico: 'M4 20h16M6 16l4-5 3 3 5-7M14 7h4v4',
  lapis: 'M4 20h4L19 9l-4-4L4 16v4Zm9-13 4 4',
  conversa: 'M4 6a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H9l-5 4V6Zm4 4h0m4 0h0m4 0h0',
  enviar: 'M4 12 20 4l-6 16-2.5-6.5L4 12Zm7.5 1.5L20 4',
} as const

export type NomeIcone = keyof typeof caminhos

export function Icone({ nome, tamanho = 20, className }: { nome: NomeIcone; tamanho?: number; className?: string }) {
  return (
    <svg width={tamanho} height={tamanho} viewBox="0 0 24 24" fill="none" stroke="currentColor"
      strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" className={className}>
      <path d={caminhos[nome]} />
    </svg>
  )
}
