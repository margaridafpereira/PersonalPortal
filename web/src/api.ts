// Cliente da API do portal. Autenticação por cookie de sessão (mesma origem via proxy do Vite).

export type CategoriaRendimento = 'Nenhum' | 'TrabalhoDependente' | 'TrabalhoIndependente' | 'Ambos'
export type SituacaoHabitacao = 'Arrenda' | 'Proprietario' | 'ProcuraArrendar' | 'ProcuraComprar' | 'CasaDeFamilia'

export interface Perfil {
  nome: string | null
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

// ---- Carro ----

export type CategoriaVeiculo = 'LigeiroPassageiros' | 'LigeiroMercadorias'
export type Combustivel = 'GasoleoSimples' | 'GasoleoEspecial' | 'Gasolina95' | 'Gasolina95Especial' | 'Gasolina98' | 'Gpl' | 'Eletrico'

export const nomesCombustivel: Record<Combustivel, string> = {
  GasoleoSimples: 'Gasóleo simples',
  GasoleoEspecial: 'Gasóleo especial',
  Gasolina95: 'Gasolina 95',
  Gasolina95Especial: 'Gasolina 95 especial',
  Gasolina98: 'Gasolina 98',
  Gpl: 'GPL',
  Eletrico: 'Elétrico',
}

export type NormaEmissoes = 'Nedc' | 'Wltp'

export interface DadosVeiculo {
  nome: string
  matricula: string | null
  categoria: CategoriaVeiculo
  combustivel: Combustivel
  dataPrimeiraMatricula: string
  cilindrada: number | null
  emissoesCo2: number | null
  normaCo2: NormaEmissoes | null
  renovacaoSeguro: string | null
  seguradora: string | null
  apoliceSeguro: string | null
  valorSeguroAnual: number | null
  proximaRevisao: string | null
  consumoLitros100Km: number | null
}

export interface Veiculo extends DadosVeiculo { id: string }

export interface EstimativaIuc {
  valor: number | null
  categoria: string
  detalhe: string[]
  emFalta: string[]
  aviso: string | null
}

export interface EstatisticasConsumo {
  abastecimentos: number
  consumoReal: number | null
  custoPorKm: number | null
  gastoMensal: number | null
  precoMedioLitro: number | null
  quilometrosMedidos: number | null
}

export interface Abastecimento {
  id: string
  data: string
  quilometros: number
  litros: number
  valorTotal: number
  depositoCheio: boolean
  posto: string | null
}

export type NovoAbastecimento = Omit<Abastecimento, 'id'>

export interface VeiculoCompleto {
  veiculo: Veiculo
  prazos: PrazoVeiculo[]
  iuc: EstimativaIuc
  consumo: EstatisticasConsumo
  abastecimentos: Abastecimento[]
}

export interface PostoCarregamento {
  id: string
  morada: string
  operador: string
  tipo: string
  potenciaKw: number
  custoOperador: number
  tarifas: string[]
}

export interface CarregamentoConcelho { concelho: string; numeroPostos: number; energia: number; maisBaratos: PostoCarregamento[] }

export interface PrazoVeiculo {
  veiculoId: string
  tipo: 'Inspecao' | 'Iuc' | 'Seguro' | 'Revisao' | 'CartaConducao'
  data: string
  titulo: string
  descricao: string
  link: string | null
  diasEmFalta: number
}

export interface PrecosConcelho {
  concelho: string
  combustivel: Combustivel
  minimo: number
  media: number
  numeroPostos: number
  maisBaratos: { nome: string; marca: string; morada: string; localidade: string; preco: number; atualizadoEm: string; latitude: number | null; longitude: number | null }[]
  /** Localidades dos postos do concelho (normalmente freguesias ou vilas), para filtrar. */
  localidades: string[] | null
  localidade: string | null
}

// ---- Investimentos ----

export type TipoOperacao = 'Compra' | 'Venda' | 'Dividendo'
export type TipoAtivo = 'Acao' | 'Etf' | 'Fundo'

export interface Mapeamento {
  data: number
  hora: number | null
  tipo: number | null
  ativo: number
  nome: number | null
  quantidade: number
  preco: number
  moeda: number | null
  moedaFixa: string | null
  comissoes: number | null
  retencao: number | null
  moedaRetencao: number | null
  idExterno: number | null
  valoresTipo: Record<string, TipoOperacao | null>
  vendaSeQuantidadeNegativa: boolean
}

export interface AnaliseFicheiro {
  /** Formato reconhecido pelo cabeçalho: os dois primeiros têm leitor próprio e não precisam de mapeamento. */
  formato: 'trading212' | 'modelo' | 'degiro' | 'revolut' | 'xtb' | 'etoro' | 'ibkr' | 'ibkr-dividendos' | 'universal'
  separador: string
  colunas: string[]
  amostra: string[][]
  sugestao: Mapeamento | null
  camposEmFalta: string[]
  /** Por coluna: valores distintos (até 50) e o tipo sugerido para cada um. */
  valoresPorColuna: Record<string, TipoOperacao | null>[]
}

export interface Operacao {
  id: string
  tipo: TipoOperacao
  momento: string
  ativo: string
  nome: string
  tipoAtivo: TipoAtivo
  quantidade: number
  precoUnitario: number
  moeda: string
  comissoes: number
  /** null = euros */
  moedaComissoes: string | null
  retencaoFonte: number
  moedaRetencao: string | null
  corretora: string | null
}

export type NovaOperacao = Omit<Operacao, 'id' | 'tipoAtivo' | 'moedaComissoes'> & { tipoAtivo: TipoAtivo | null }

/** Um ficheiro lido no browser: texto (CSV) ou, se for Excel, o conteúdo em base64. */
export type FicheiroImportado = { conteudo: string; xlsx?: undefined } | { conteudo?: undefined; xlsx: string }

export interface RelatorioIrs {
  ano: number
  maisValias: {
    ativo: string; nome: string; pais: string; codigoPais: string; codigo: string
    dataRealizacao: string; valorRealizacao: number; dataAquisicao: string; valorAquisicao: number
    despesas: number; resultado: number; quantidade: number; detidoMenosDe365Dias: boolean
  }[]
  dividendos: { pais: string; codigoPais: string; codigo: string; bruto: number; retencao: number; pagamentos: number; retencaoNaoCreditada: number }[]
  saldoMaisValias: number
  impostoMaisValias: number
  dividendosBrutos: number
  retencaoEstrangeiro: number
  impostoDividendos: number
  avisos: string[]
}

// ---- Avisos por email ----

export interface EstadoAvisos {
  ativos: boolean
  diasAntecedencia: number[]
  email: string | null
  /** Para onde vão os emails: servidor configurado ou pasta de teste. */
  destino: string
  horaEnvio: number
  proximos: { titulo: string; data: string; seccao: string; diasEmFalta: number }[]
}

// ---- Assistente ----

export interface EstadoAssistente {
  configurado: boolean
  fornecedor: string
  modelo: string
  planoGratuito: boolean
  termosDados: string | null
  /** O que é enviado ao fornecedor de IA; mostrada antes da primeira utilização e sempre acessível. */
  nota: string
  aceiteEm: string | null
}

export interface MensagemConversa { papel: 'utilizador' | 'assistente'; texto: string }

export interface RespostaAssistente {
  texto: string
  /** O que o assistente consultou para responder (ex.: "Relatório de IRS"). */
  consultas: string[]
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
    // "detail" tem a explicação do servidor em português; "title" é só o nome do código HTTP (ex.: "Bad Gateway").
    return erros.length > 0 ? erros.join(' ') : problema.detail ?? problema.title ?? `Erro ${resposta.status}`
  } catch {
    return `Erro ${resposta.status}`
  }
}

export const api = {
  entrar: (email: string, password: string) => pedido<void>('POST', '/api/auth/login?useCookies=true', { email, password }),
  registar: (email: string, password: string) => pedido<void>('POST', '/api/auth/register', { email, password }),
  sair: () => pedido<void>('POST', '/api/auth/logout'),
  demo: () => pedido<{ ativa: boolean; emDemo: boolean }>('GET', '/api/demo'),
  entrarDemo: () => pedido<void>('POST', '/api/demo/entrar'),
  quemSou: async () => {
    const resposta = await fetch('/api/auth/manage/info', { credentials: 'same-origin' })
    if (resposta.status === 401) throw new NaoAutenticado()
    // 404: o cookie é de uma conta que já não existe (ex.: base de dados recriada). Termina essa sessão.
    if (resposta.status === 404) {
      await fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' })
      throw new NaoAutenticado()
    }
    if (!resposta.ok) throw new Error(`Erro ${resposta.status}`)
    return (await resposta.json()) as { email: string }
  },

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

  veiculos: () => pedido<VeiculoCompleto[]>('GET', '/api/carro/veiculos'),
  novoAbastecimento: (veiculoId: string, a: NovoAbastecimento) => pedido<Abastecimento>('POST', `/api/carro/veiculos/${veiculoId}/abastecimentos`, a),
  apagarAbastecimento: (id: string) => pedido<void>('DELETE', `/api/carro/abastecimentos/${id}`),
  carta: () => pedido<{ validade: string | null; prazo: PrazoVeiculo | null }>('GET', '/api/carro/carta'),
  guardarCarta: (validade: string | null) => pedido<void>('PUT', '/api/carro/carta', { validade }),
  concelhos: () => pedido<string[]>('GET', '/api/carro/concelhos'),
  carregamento: async (concelho: string) => {
    const r = await fetch(`/api/carro/carregamento?concelho=${encodeURIComponent(concelho)}`, { credentials: 'same-origin' })
    return r.ok ? ((await r.json()) as CarregamentoConcelho) : null
  },
  avisos: () => pedido<EstadoAvisos>('GET', '/api/avisos'),
  emailTeste: () => pedido<{ mensagem: string }>('POST', '/api/avisos/teste'),
  criarVeiculo: (v: DadosVeiculo) => pedido<Veiculo>('POST', '/api/carro/veiculos', v),
  alterarVeiculo: (id: string, v: DadosVeiculo) => pedido<Veiculo>('PUT', `/api/carro/veiculos/${id}`, v),
  apagarVeiculo: (id: string) => pedido<void>('DELETE', `/api/carro/veiculos/${id}`),
  combustiveis: async (concelho: string, combustivel: Combustivel, localidade?: string | null) => {
    const filtro = localidade ? `&localidade=${encodeURIComponent(localidade)}` : ''
    const r = await fetch(`/api/carro/combustiveis?concelho=${encodeURIComponent(concelho)}&combustivel=${combustivel}${filtro}`, { credentials: 'same-origin' })
    return r.ok ? ((await r.json()) as PrecosConcelho) : null
  },

  operacoes: () => pedido<Operacao[]>('GET', '/api/investimentos/operacoes'),
  criarOperacao: (o: NovaOperacao) => pedido<Operacao>('POST', '/api/investimentos/operacoes', o),
  apagarOperacao: (id: string) => pedido<void>('DELETE', `/api/investimentos/operacoes/${id}`),
  apagarTodasOperacoes: () => pedido<{ apagadas: number }>('DELETE', '/api/investimentos/operacoes'),
  importar: (formato: AnaliseFicheiro['formato'], ficheiro: FicheiroImportado, mapeamento?: Mapeamento) =>
    pedido<{ importadas: number; avisos: string[] }>('POST', '/api/investimentos/importar', { formato, ...ficheiro, mapeamento }),
  analisar: (ficheiro: FicheiroImportado) => pedido<AnaliseFicheiro>('POST', '/api/investimentos/analisar', ficheiro),
  alterarIsin: (ativo: string, isin: string) => pedido<void>('PUT', `/api/investimentos/ativos/${encodeURIComponent(ativo)}/isin`, { isin }),
  alterarTipoAtivo: (ativo: string, tipo: TipoAtivo) => pedido<void>('PUT', `/api/investimentos/ativos/${encodeURIComponent(ativo)}/tipo`, { tipo }),
  alterarPalavraPasse: (oldPassword: string, newPassword: string) => pedido<void>('POST', '/api/auth/manage/info', { oldPassword, newPassword }),
  assistente: () => pedido<EstadoAssistente>('GET', '/api/assistente'),
  aceitarAssistente: () => pedido<void>('POST', '/api/assistente/aceitar'),
  perguntar: (conversa: MensagemConversa[], seccao: string) =>
    pedido<RespostaAssistente>('POST', '/api/assistente/mensagens', { conversa, seccao }),
  relatorio: (ano: number) => pedido<RelatorioIrs>('GET', `/api/investimentos/relatorio/${ano}`),
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
