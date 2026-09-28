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
  itens: { texto: string; detalhe?: string | null; link?: string | null }[]
  camposPerfilEmFalta: string[]
  indicadores: { valor: string; rotulo: string; tom: 'positivo' | 'aviso' | 'neutro' }[]
}

// ---- Apoios ----

export type EstadoElegibilidade = 'Provavel' | 'FaltaInformacao' | 'NaoElegivel' | 'Encerrado'

export interface ResultadoApoio {
  apoio: {
    id: string
    nome: string
    descricao: string
    categoria: string
    comoPedir: string
    fonteOficial: string
    verificadoEm: string
    prazo: string | null
    aviso: string | null
  }
  estado: EstadoElegibilidade
  condicoes: { descricao: string; resultado: 'Cumpre' | 'NaoCumpre' | 'Desconhecido' }[]
  estimativa: string | null
}

export interface Prazo {
  data: string
  titulo: string
  descricao: string
  categoria: 'Impostos' | 'SegurancaSocial' | 'Apoios'
  link: string | null
  porConfirmar: boolean
  diasEmFalta: number
}

// ---- Anúncios ----

export type Negocio = 'Comprar' | 'Arrendar'
export type TipoImovel = 'Apartamento' | 'Moradia' | 'Terreno'
export type EstadoFavorito = 'Ativo' | 'Visitado' | 'Descartado' | 'SaiuDoPortal'

export interface Pesquisa {
  id: string
  nome: string
  negocio: Negocio
  tipo: TipoImovel
  distrito: string
  concelho: string
  precoMaximo: number | null
  quartosMinimo: number | null
}

export interface PesquisaComLinks {
  pesquisa: Pesquisa
  links: { portal: string; url: string; verificado: boolean }[]
}

export interface NovaPesquisa {
  nome: string | null
  negocio: Negocio
  tipo: TipoImovel
  distrito: string
  concelho: string
  precoMaximo: number | null
  quartosMinimo: number | null
}

export interface Mediana {
  concelho: string
  periodo: string
  total: number | null
  novos: number | null
  existentes: number | null
}

export interface Favorito {
  id: string
  url: string
  portal: string
  titulo: string
  tipo: TipoImovel
  tipologia: string | null
  areaM2: number | null
  concelho: string | null
  estado: EstadoFavorito
  notas: string | null
  criadoEm: string
  diasASeguir: number
  precoAtual: number | null
  precoInicial: number | null
  variacaoPercent: number | null
  eurM2: number | null
  mediana: Mediana | null
  diferencaMedianaPercent: number | null
  precos: { data: string; preco: number }[]
  eventos: { data: string; de: number; para: number; variacaoPercent: number }[]
  etiquetas: string[]
}

export interface NovoFavorito {
  url: string
  titulo: string | null
  tipo: TipoImovel
  tipologia: string | null
  areaM2: number | null
  concelho: string | null
  preco: number | null
  notas: string | null
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

  apoios: () => pedido<ResultadoApoio[]>('GET', '/api/apoios'),
  prazos: () => pedido<Prazo[]>('GET', '/api/apoios/prazos'),

  pesquisas: () => pedido<PesquisaComLinks[]>('GET', '/api/anuncios/pesquisas'),
  criarPesquisa: (p: NovaPesquisa) => pedido<PesquisaComLinks>('POST', '/api/anuncios/pesquisas', p),
  apagarPesquisa: (id: string) => pedido<void>('DELETE', `/api/anuncios/pesquisas/${id}`),
  favoritos: () => pedido<Favorito[]>('GET', '/api/anuncios/favoritos'),
  criarFavorito: (f: NovoFavorito) => pedido<Favorito>('POST', '/api/anuncios/favoritos', f),
  alterarFavorito: (f: Favorito) => pedido<Favorito>('PUT', `/api/anuncios/favoritos/${f.id}`,
    { titulo: f.titulo, tipologia: f.tipologia, areaM2: f.areaM2, concelho: f.concelho, estado: f.estado, notas: f.notas }),
  registarPreco: (id: string, preco: number) => pedido<Favorito>('POST', `/api/anuncios/favoritos/${id}/precos`, { preco }),
  apagarFavorito: (id: string) => pedido<void>('DELETE', `/api/anuncios/favoritos/${id}`),
}

export const distritos = [
  'Aveiro', 'Beja', 'Braga', 'Bragança', 'Castelo Branco', 'Coimbra', 'Évora', 'Faro', 'Guarda', 'Leiria',
  'Lisboa', 'Portalegre', 'Porto', 'Santarém', 'Setúbal', 'Viana do Castelo', 'Vila Real', 'Viseu', 'Açores', 'Madeira',
]

const eur = new Intl.NumberFormat('pt-PT', { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 })
const num = new Intl.NumberFormat('pt-PT', { maximumFractionDigits: 0 })
export const euros = (v: number) => eur.format(v)
export const inteiro = (v: number) => num.format(v)
export const dataCurta = (iso: string) =>
  new Date(iso + 'T00:00:00').toLocaleDateString('pt-PT', { day: 'numeric', month: 'short', year: 'numeric' })

/** "S5A20261" → "1.º trim. 2026" */
export const periodoIne = (codigo: string) => {
  const m = /^S5A(\d{4})(\d)$/.exec(codigo)
  return m ? `${m[2]}.º trim. ${m[1]}` : codigo
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
