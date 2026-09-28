// Cliente da API do portal. Autenticação por cookie de sessão (mesma origem via proxy do Vite).

export type CategoriaRendimento = 'Nenhum' | 'TrabalhoDependente' | 'TrabalhoIndependente' | 'Ambos'
export type SituacaoHabitacao = 'Arrenda' | 'Proprietario' | 'ProcuraArrendar' | 'ProcuraComprar' | 'CasaDeFamilia'

export interface Perfil {
  dataNascimento: string | null
  concelho: string | null
  freguesia: string | null
  residenteFiscal: boolean | null
  dependente: boolean | null
  categoriaRendimento: CategoriaRendimento | null
  rendimentoAnualAgregado: number | null
  numeroAdultos: number | null
  idadesFilhos: number[]
  situacaoHabitacao: SituacaoHabitacao | null
  rendaMensal: number | null
  dataContratoArrendamento: string | null
  procuraComprarCasa: boolean | null
  orcamentoCompra: number | null
  temVeiculo: boolean | null
  mesMatricula: number | null
  proprietarioImovel: boolean | null
  valorImi: number | null
}

export interface Preferencias {
  seccoesAtivas: string[]
  zonasInteresse: string[]
  alertasEmail: boolean
  alertasTelegram: boolean
  diasAntecedencia: number[]
}

export interface Modulo {
  id: string
  nome: string
  descricao: string
  camposPerfil: string[]
}

export interface CartaoPainel {
  moduloId: string
  titulo: string
  resumo: string
  itens: { texto: string; detalhe?: string; link?: string }[]
  camposPerfilEmFalta: string[]
}

export class NaoAutenticado extends Error {}

async function pedido<T>(metodo: string, url: string, corpo?: unknown): Promise<T> {
  const resposta = await fetch(url, {
    method: metodo,
    credentials: 'same-origin',
    headers: corpo === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: corpo === undefined ? undefined : JSON.stringify(corpo),
  })
  if (resposta.status === 401) throw new NaoAutenticado()
  if (!resposta.ok) throw new Error(await mensagemErro(resposta))
  const texto = await resposta.text()
  return (texto ? JSON.parse(texto) : undefined) as T
}

async function mensagemErro(resposta: Response): Promise<string> {
  try {
    const problema = await resposta.json()
    const erros = problema.errors ? Object.values(problema.errors).flat() : []
    return erros.length > 0 ? erros.join(' ') : problema.title ?? `Erro ${resposta.status}`
  } catch {
    return `Erro ${resposta.status}`
  }
}

export const api = {
  entrar: (email: string, password: string) => pedido<void>('POST', '/api/auth/login?useCookies=true', { email, password }),
  registar: (email: string, password: string) => pedido<void>('POST', '/api/auth/register', { email, password }),
  sair: () => pedido<void>('POST', '/api/auth/logout'),
  quemSou: () => pedido<{ email: string }>('GET', '/api/auth/manage/info'),

  painel: () => pedido<CartaoPainel[]>('GET', '/api/painel'),
  modulos: () => pedido<Modulo[]>('GET', '/api/modulos'),
  perfil: () => pedido<Perfil>('GET', '/api/perfil'),
  guardarPerfil: (p: Perfil) => pedido<Perfil>('PUT', '/api/perfil', p),
  preferencias: () => pedido<Preferencias>('GET', '/api/preferencias'),
  guardarPreferencias: (p: Preferencias) => pedido<Preferencias>('PUT', '/api/preferencias', p),
}

/** Nomes legíveis dos campos do perfil, para o painel dizer o que falta preencher. */
export const nomesCampos: Record<string, string> = {
  DataNascimento: 'data de nascimento',
  Concelho: 'concelho',
  Freguesia: 'freguesia',
  ResidenteFiscal: 'residência fiscal',
  Dependente: 'dependente',
  CategoriaRendimento: 'tipo de rendimento',
  RendimentoAnualAgregado: 'rendimento do agregado',
  NumeroAdultos: 'adultos no agregado',
  SituacaoHabitacao: 'situação da habitação',
  RendaMensal: 'renda mensal',
  ProcuraComprarCasa: 'procura comprar casa',
  OrcamentoCompra: 'orçamento de compra',
}
